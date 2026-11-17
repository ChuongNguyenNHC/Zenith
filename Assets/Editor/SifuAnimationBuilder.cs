using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SifuAnimationBuilder
{
    public const string CustomAnimDir = "Assets/Animators/Custom";
    private const int AnimSpecVersion = 5;
    private const string EditorVersionPrefKey = "Zenith.SifuAnim.Version";

    private const string FightIdleFbx = "Assets/Animators/Brawler Animations Sample/Animations/Fight_Idle.fbx";
    private const string GuardIdleFbx = "Assets/Animators/Brawler Animations Sample/Animations/Guard_Idle.fbx";
    private const string WalkForwardFbx = "Assets/Animators/Brawler Animations Sample/Animations/Walk_F.fbx";
    private const string RiggedNinjaFbx = "Assets/Characters/RiggedNinja.fbx";

    private sealed class PoseSampler : System.IDisposable
    {
        public readonly GameObject PreviewGo;
        public readonly Animator Anim;
        public readonly HumanPoseHandler Handler;
        public readonly Dictionary<string, int> MuscleIndex = new Dictionary<string, int>();
        public readonly Transform Hips;
        public readonly Transform Head;
        public readonly Transform LeftFoot;
        public readonly Transform RightFoot;
        public readonly Transform LeftHand;
        public readonly Transform RightHand;

        public PoseSampler()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedNinjaFbx);
            PreviewGo = (GameObject)PrefabUtility.InstantiatePrefab(model);
            Anim = PreviewGo.GetComponent<Animator>();
            Handler = new HumanPoseHandler(Anim.avatar, PreviewGo.transform);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                MuscleIndex[HumanTrait.MuscleName[i]] = i;
            }
            Hips = Anim.GetBoneTransform(HumanBodyBones.Hips);
            Head = Anim.GetBoneTransform(HumanBodyBones.Head);
            LeftFoot = Anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            RightFoot = Anim.GetBoneTransform(HumanBodyBones.RightFoot);
            LeftHand = Anim.GetBoneTransform(HumanBodyBones.LeftHand);
            RightHand = Anim.GetBoneTransform(HumanBodyBones.RightHand);
        }

        public void ApplyPose(Dictionary<string, float> pose)
        {
            HumanPose hp = new HumanPose
            {
                bodyPosition = new Vector3(
                    pose.TryGetValue("RootT.x", out float rx) ? rx : 0f,
                    pose.TryGetValue("RootT.y", out float ry) ? ry : 0.92f,
                    pose.TryGetValue("RootT.z", out float rz) ? rz : 0f),
                bodyRotation = new Quaternion(
                    pose.TryGetValue("RootQ.x", out float qx) ? qx : 0f,
                    pose.TryGetValue("RootQ.y", out float qy) ? qy : 0f,
                    pose.TryGetValue("RootQ.z", out float qz) ? qz : 0f,
                    pose.TryGetValue("RootQ.w", out float qw) ? qw : 1f),
                muscles = new float[HumanTrait.MuscleCount]
            };

            foreach (KeyValuePair<string, float> kv in pose)
            {
                if (MuscleIndex.TryGetValue(kv.Key, out int idx))
                {
                    hp.muscles[idx] = kv.Value;
                }
            }

            Handler.SetHumanPose(ref hp);
        }

        public void GroundFeet(Dictionary<string, float> pose, float targetMinFootY)
        {
            ApplyPose(pose);
            float lfY = PreviewGo.transform.InverseTransformPoint(LeftFoot.position).y;
            float rfY = PreviewGo.transform.InverseTransformPoint(RightFoot.position).y;
            float minFootY = Mathf.Min(lfY, rfY);
            float delta = targetMinFootY - minFootY;
            if (pose.TryGetValue("RootT.y", out float ry))
            {
                pose["RootT.y"] = ry + delta;
            }
        }

        public float MeasureWalkPhaseAngle(AnimationClip walkClip)
        {
            const int N = 24;
            float sumSin = 0f;
            float sumCos = 0f;
            float sumVal = 0f;
            float[] samples = new float[N];

            for (int i = 0; i < N; i++)
            {
                float p = (float)i / N;
                Dictionary<string, float> pose = SamplePoseDictionary(walkClip, p * walkClip.length);
                // Negative Upper Leg Front-Back means thigh swung forward (+Z).
                float v = -(pose.TryGetValue("Left Upper Leg Front-Back", out float lf) ? lf : 0f);
                samples[i] = v;
                sumVal += v;
            }

            float mean = sumVal / N;
            for (int i = 0; i < N; i++)
            {
                float p = (float)i / N;
                float ang = p * Mathf.PI * 2f;
                float centered = samples[i] - mean;
                sumCos += centered * Mathf.Cos(ang);
                sumSin += centered * Mathf.Sin(ang);
            }

            if (sumCos * sumCos + sumSin * sumSin < 0.0001f)
            {
                return 0f;
            }
            return Mathf.Atan2(sumSin, sumCos);
        }

        public void Dispose()
        {
            Handler?.Dispose();
            if (PreviewGo != null)
            {
                Object.DestroyImmediate(PreviewGo);
            }
        }
    }

    public static void GenerateAllBatchmode()
    {
        GenerateAllCustomClips(true);
        EditorApplication.Exit(0);
    }

    [MenuItem("Zenith/Generate Custom Sifu Animations")]
    public static void GenerateAllCustomClipsMenu()
    {
        GenerateAllCustomClips(true);
        EditorPrefs.SetInt(EditorVersionPrefKey, AnimSpecVersion);
    }

    public static void GenerateAllCustomClips(bool logKinematics = false)
    {
        if (!AssetDatabase.IsValidFolder(CustomAnimDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Animators"))
            {
                AssetDatabase.CreateFolder("Assets", "Animators");
            }
            AssetDatabase.CreateFolder("Assets/Animators", "Custom");
        }

        AnimationClip fightIdle = LoadFirstClip(FightIdleFbx);
        AnimationClip guardIdle = LoadFirstClip(GuardIdleFbx);
        AnimationClip walkForward = LoadFirstClip(WalkForwardFbx);
        if (fightIdle == null || guardIdle == null || walkForward == null)
        {
            Debug.LogError("[SifuAnim] Missing Fight_Idle, Guard_Idle, or Walk_F reference clip.");
            return;
        }

        Dictionary<string, float> fightSample = SamplePoseDictionary(fightIdle, 0f);
        Dictionary<string, float> guardSample = SamplePoseDictionary(guardIdle, 0f);
        Dictionary<string, float> athleticGuardTemplate = BuildAthleticGuardTemplate(fightSample, guardSample);

        using (PoseSampler sampler = new PoseSampler())
        {
            float walkLeftPeakPhase = sampler.MeasureWalkPhaseAngle(walkForward);

            List<AnimationClip> generated = new List<AnimationClip>
            {
                CreateOrUpdateClip("Run_F", BuildRunClip(fightSample, walkForward, walkLeftPeakPhase, sampler)),
                CreateOrUpdateClip("Strafe_L", BuildStrafeClip(athleticGuardTemplate, -1f, sampler)),
                CreateOrUpdateClip("Strafe_R", BuildStrafeClip(athleticGuardTemplate, +1f, sampler)),
                CreateOrUpdateClip("Quickstep_L", BuildQuickstepSideClip(athleticGuardTemplate, -1f, sampler)),
                CreateOrUpdateClip("Quickstep_R", BuildQuickstepSideClip(athleticGuardTemplate, +1f, sampler)),
                CreateOrUpdateClip("Avoid_Left", BuildAvoidSideClip(athleticGuardTemplate, -1f, sampler)),
                CreateOrUpdateClip("Avoid_Right", BuildAvoidSideClip(athleticGuardTemplate, +1f, sampler)),
                CreateOrUpdateClip("Avoid_Duck", BuildAvoidDuckClip(athleticGuardTemplate, sampler)),
                CreateOrUpdateClip("Avoid_Hop", BuildAvoidHopClip(athleticGuardTemplate, sampler))
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorPrefs.SetInt(EditorVersionPrefKey, AnimSpecVersion);

            if (logKinematics)
            {
                VerifyGeneratedKinematics(generated, sampler);
            }

            Debug.Log($"[SifuAnim] Generated/updated {generated.Count} v{AnimSpecVersion} Humanoid AnimationClips in {CustomAnimDir}.");
        }
    }

    private static Dictionary<string, float> CreateCleanChannelTemplate(Dictionary<string, float> keyDonor)
    {
        Dictionary<string, float> pose = new Dictionary<string, float>(keyDonor.Count);
        foreach (string key in keyDonor.Keys)
        {
            // Preserve finger fist curls from donor; reset all body/head/limb rotations cleanly to neutral forward.
            if (key.Contains("Hand."))
            {
                pose[key] = keyDonor[key];
            }
            else
            {
                pose[key] = 0f;
            }
        }

        pose["RootT.x"] = 0f;
        pose["RootT.y"] = 0.92f;
        pose["RootT.z"] = 0f;
        SetRootQ(pose, Quaternion.identity);
        return pose;
    }

    private static Dictionary<string, float> BuildAthleticGuardTemplate(Dictionary<string, float> fightSample, Dictionary<string, float> guardSample)
    {
        Dictionary<string, float> pose = CreateCleanChannelTemplate(fightSample);

        // Borrow authentic mocap fighting guard arms & hands ONLY (never Fight_Idle's twisted neck or bladed RootQ.y!).
        foreach (KeyValuePair<string, float> kv in guardSample)
        {
            if (kv.Key.StartsWith("Left Shoulder") || kv.Key.StartsWith("Right Shoulder") ||
                kv.Key.StartsWith("Left Arm") || kv.Key.StartsWith("Right Arm") ||
                kv.Key.StartsWith("Left Forearm") || kv.Key.StartsWith("Right Forearm") ||
                kv.Key.StartsWith("Left Hand") || kv.Key.StartsWith("Right Hand") ||
                kv.Key.Contains("Hand."))
            {
                pose[kv.Key] = kv.Value;
            }
        }

        // Neutralize asymmetrical boxing shoulder yaw into balanced forward Sifu fighting guard.
        pose["Spine Front-Back"] = -0.10f;
        pose["Chest Front-Back"] = -0.06f;
        pose["UpperChest Front-Back"] = 0f;
        pose["Neck Nod Down-Up"] = 0.10f;
        pose["Head Nod Down-Up"] = 0.06f;
        pose["Neck Turn Left-Right"] = 0f;
        pose["Head Turn Left-Right"] = 0f;

        // Athletic shoulder-width stance with light knee bend.
        pose["Left Upper Leg Front-Back"] = -0.14f;
        pose["Right Upper Leg Front-Back"] = -0.10f;
        pose["Left Upper Leg In-Out"] = 0.14f;
        pose["Right Upper Leg In-Out"] = 0.14f;
        pose["Left Lower Leg Stretch"] = 0.22f;
        pose["Right Lower Leg Stretch"] = 0.24f;
        pose["Left Foot Up-Down"] = 0.04f;
        pose["Right Foot Up-Down"] = 0.04f;

        return pose;
    }

    private static AnimationClip BuildRunClip(Dictionary<string, float> fightSample, AnimationClip walkForward, float walkLeftPeakPhase, PoseSampler sampler)
    {
        const float len = 0.56f;
        const int samples = 24;
        AnimationClip clip = NewHumanoidClip("Run_F", len, true);
        Dictionary<string, float> baseTemplate = CreateCleanChannelTemplate(fightSample);
        Dictionary<string, List<Keyframe>> tracks = InitTracks(baseTemplate, out List<string> curveNames);

        for (int s = 0; s <= samples; s++)
        {
            float p = (float)s / samples;
            float t = p * len;

            // Phase-locked to Walk_F so 1D BlendTree Walk->Run blends feet smoothly with zero crossover pop.
            float thetaL = p * Mathf.PI * 2f - walkLeftPeakPhase;
            float thetaR = thetaL + Mathf.PI;

            // Grab mocap torso twist subtle stabilization from Walk_F while keeping eyes dead ahead.
            Dictionary<string, float> walkPose = SamplePoseDictionary(walkForward, p * walkForward.length);
            Dictionary<string, float> pose = new Dictionary<string, float>(baseTemplate);

            Quaternion sprintPitch = Quaternion.AngleAxis(4f, Vector3.right);
            // At thetaL=0 (left leg full forward, right arm forward), left hip swings +Z (+yaw around +Y) while chest counter-twists opposite!
            Quaternion hipCounterYaw = Quaternion.AngleAxis(+4.5f * Mathf.Cos(thetaL), Vector3.up);
            SetRootQ(pose, sprintPitch * hipCounterYaw);

            pose["RootT.x"] = -0.02f * Mathf.Cos(thetaL);
            pose["RootT.y"] = 0.92f;
            pose["RootT.z"] = 0f;

            pose["Spine Front-Back"] = -0.14f - 0.03f * Mathf.Cos(2f * thetaL);
            pose["Spine Left-Right"] = 0.03f * Mathf.Cos(thetaL);
            pose["Spine Twist Left-Right"] = -0.11f * Mathf.Cos(thetaL);

            pose["Chest Front-Back"] = -0.08f;
            pose["Chest Left-Right"] = -0.02f * Mathf.Cos(thetaL);
            pose["Chest Twist Left-Right"] = -0.15f * Mathf.Cos(thetaL);

            pose["UpperChest Front-Back"] = walkPose.TryGetValue("UpperChest Front-Back", out float ucf)
                ? Mathf.Clamp(ucf, -0.08f, 0.04f)
                : -0.03f;

            // Real Humanoid Neck/Head stabilization channels so runner looks straight forward!
            pose["Neck Nod Down-Up"] = 0.14f;
            pose["Head Nod Down-Up"] = 0.08f;
            pose["Neck Turn Left-Right"] = 0f;
            pose["Head Turn Left-Right"] = 0f;
            pose["Neck Tilt Left-Right"] = 0f;
            pose["Head Tilt Left-Right"] = 0f;

            ApplyAthleticRunLeg(pose, "Left", thetaL);
            ApplyAthleticRunLeg(pose, "Right", thetaR);

            // Opposite arm drives forward when knee drives forward (Cos(thetaL) == +1 -> Right arm forward, Left arm back).
            ApplyAthleticRunArm(pose, "Left", -Mathf.Cos(thetaL));
            ApplyAthleticRunArm(pose, "Right", -Mathf.Cos(thetaR));

            // Real bipedal flight lift: highest just after toe-off when split is near widest, lowest in mid-stance absorption.
            float flightLift = Mathf.Pow(Mathf.Abs(Mathf.Cos(thetaL - 0.35f)), 1.6f);
            sampler.GroundFeet(pose, 0.11f + 0.06f * flightLift);
            AddFrameToTracks(tracks, curveNames, pose, t);
        }

        ApplyTracksToClip(clip, tracks, curveNames);
        return clip;
    }

    private static void ApplyAthleticRunLeg(Dictionary<string, float> pose, string side, float theta)
    {
        float c = Mathf.Cos(theta);
        float s = Mathf.Sin(theta);
        float c2 = Mathf.Cos(2f * theta);

        // Continuous C-infinity thigh drive: theta=0 -> -0.58 (high knee forward), pi -> +0.62 (strong push extension).
        float hipFlex = 0.02f - 0.60f * c + 0.06f * s;

        // Continuous C-infinity knee flexion:
        // 0: +0.32 (footstrike reach), pi/2: -0.10 (stance shock absorb), pi: +0.60 (push-off straighten), 3pi/2: -0.82 (heel whip up to glutes!)
        float kneeStretch = -0.14f * c + 0.36f * s + 0.46f * c2;

        float footUpDown = 0.04f + 0.14f * c - 0.28f * Mathf.Clamp01(-c);
        float toesUpDown = -0.22f * Mathf.Clamp01(-c);

        pose[$"{side} Upper Leg Front-Back"] = hipFlex;
        pose[$"{side} Upper Leg In-Out"] = 0.03f;
        pose[$"{side} Upper Leg Twist In-Out"] = 0.02f;
        pose[$"{side} Lower Leg Stretch"] = kneeStretch;
        pose[$"{side} Lower Leg Twist In-Out"] = -0.02f;
        pose[$"{side} Foot Up-Down"] = footUpDown;
        pose[$"{side} Foot Twist In-Out"] = 0f;
        pose[$"{side} Toes Up-Down"] = toesUpDown;
    }

    private static void ApplyAthleticRunArm(Dictionary<string, float> pose, string side, float armForwardCos)
    {
        pose[$"{side} Shoulder Down-Up"] = -0.06f;
        pose[$"{side} Shoulder Front-Back"] = -0.12f * armForwardCos;

        // Hang upper arms closely alongside ribcage (-0.84) with ~95 deg bent forearm (-0.60..-0.72) so elbows pump front-back near waist!
        pose[$"{side} Arm Down-Up"] = -0.84f + 0.05f * Mathf.Abs(armForwardCos);
        pose[$"{side} Arm Front-Back"] = -0.06f - 0.54f * armForwardCos;
        pose[$"{side} Arm Twist In-Out"] = 0.18f;

        pose[$"{side} Forearm Stretch"] = -0.60f - 0.12f * Mathf.Clamp01(armForwardCos);
        pose[$"{side} Forearm Twist In-Out"] = 0.10f;
    }

    private static AnimationClip BuildStrafeClip(Dictionary<string, float> guardTemplate, float sideSign, PoseSampler sampler)
    {
        const float len = 0.56f;
        const int samples = 24;
        string name = sideSign < 0f ? "Strafe_L" : "Strafe_R";
        AnimationClip clip = NewHumanoidClip(name, len, true);
        Dictionary<string, List<Keyframe>> tracks = InitTracks(guardTemplate, out List<string> curveNames);

        for (int s = 0; s <= samples; s++)
        {
            float p = (float)s / samples;
            float t = p * len;
            float phase = p * Mathf.PI * 2f;
            float stepSin = Mathf.Sin(phase);
            float bounce = Mathf.Abs(Mathf.Cos(phase));

            Dictionary<string, float> pose = new Dictionary<string, float>(guardTemplate);
            pose["RootT.x"] = sideSign * 0.03f * stepSin;
            pose["RootT.z"] = 0f;
            SetRootQ(pose, Quaternion.identity);

            pose["Spine Front-Back"] = -0.14f;
            pose["Chest Front-Back"] = -0.08f;
            pose["Spine Left-Right"] = -sideSign * 0.06f;

            float leadSwing = Mathf.Max(0f, stepSin);
            float trailSwing = Mathf.Max(0f, -stepSin);
            float liftL = sideSign < 0f ? leadSwing : trailSwing;
            float liftR = sideSign > 0f ? leadSwing : trailSwing;

            pose["Left Upper Leg In-Out"] = 0.16f + 0.24f * stepSin * (sideSign < 0f ? 1f : -1f);
            pose["Right Upper Leg In-Out"] = 0.16f - 0.24f * stepSin * (sideSign < 0f ? 1f : -1f);
            pose["Left Lower Leg Stretch"] = 0.24f - 0.36f * liftL;
            pose["Right Lower Leg Stretch"] = 0.24f - 0.36f * liftR;
            pose["Left Upper Leg Front-Back"] = -0.14f - 0.18f * liftL;
            pose["Right Upper Leg Front-Back"] = -0.14f - 0.18f * liftR;

            sampler.GroundFeet(pose, 0.11f + 0.025f * bounce);
            AddFrameToTracks(tracks, curveNames, pose, t);
        }

        ApplyTracksToClip(clip, tracks, curveNames);
        return clip;
    }

    private static AnimationClip BuildQuickstepSideClip(Dictionary<string, float> guardTemplate, float sideSign, PoseSampler sampler)
    {
        // Explosive athletic lateral lunge-step without rigid-body RootQ board roll.
        const float len = 0.36f;
        const int samples = 18;
        string name = sideSign < 0f ? "Quickstep_L" : "Quickstep_R";
        AnimationClip clip = NewHumanoidClip(name, len, false);
        Dictionary<string, List<Keyframe>> tracks = InitTracks(guardTemplate, out List<string> curveNames);

        for (int s = 0; s <= samples; s++)
        {
            float p = (float)s / samples;
            float t = p * len;

            float pushEnv = p < 0.25f
                ? Mathf.Sin((p / 0.25f) * Mathf.PI * 0.5f)
                : Mathf.Cos(Mathf.Clamp01((p - 0.25f) / 0.75f) * Mathf.PI * 0.5f);

            // Lead leg lunges out quickly (p=0.10..0.55) while trail pushes then pulls smoothly underneath.
            float leadStepEnv = Mathf.Sin(Mathf.Clamp01(p / 0.85f) * Mathf.PI);
            float trailPushEnv = p < 0.45f ? Mathf.Sin((p / 0.45f) * Mathf.PI) : 0f;

            Dictionary<string, float> pose = new Dictionary<string, float>(guardTemplate);
            pose["RootT.x"] = sideSign * 0.06f * pushEnv;
            pose["RootT.z"] = 0f;
            SetRootQ(pose, Quaternion.AngleAxis(sideSign * 5f * pushEnv, Vector3.up));

            pose["Spine Front-Back"] = -0.14f - 0.12f * pushEnv;
            pose["Chest Front-Back"] = -0.08f - 0.10f * pushEnv;
            pose["Spine Left-Right"] = -sideSign * 0.18f * pushEnv;
            pose["Chest Left-Right"] = -sideSign * 0.14f * pushEnv;
            pose["Spine Twist Left-Right"] = -sideSign * 0.16f * pushEnv;
            pose["Head Tilt Left-Right"] = sideSign * 0.14f * pushEnv;
            pose["Neck Nod Down-Up"] = 0.14f + 0.08f * pushEnv;
            pose["Head Nod Down-Up"] = 0.08f + 0.06f * pushEnv;

            string leadSide = sideSign < 0f ? "Left" : "Right";
            string trailSide = sideSign < 0f ? "Right" : "Left";

            pose[$"{leadSide} Upper Leg In-Out"] = 0.18f + 0.46f * leadStepEnv;
            pose[$"{leadSide} Upper Leg Front-Back"] = -0.16f - 0.22f * leadStepEnv;
            pose[$"{leadSide} Lower Leg Stretch"] = 0.20f - 0.52f * leadStepEnv;
            pose[$"{leadSide} Foot Up-Down"] = 0.12f * leadStepEnv;

            pose[$"{trailSide} Upper Leg In-Out"] = 0.14f + 0.28f * trailPushEnv;
            pose[$"{trailSide} Upper Leg Front-Back"] = -0.10f + 0.18f * trailPushEnv;
            pose[$"{trailSide} Lower Leg Stretch"] = 0.24f + 0.36f * trailPushEnv - 0.25f * pushEnv;
            pose[$"{trailSide} Foot Up-Down"] = -0.25f * trailPushEnv;

            // Airborne pushoff apex briefly (+0.05m) during explosive step-cut, crisp plant landing.
            float hopLift = 0.05f * Mathf.Sin(Mathf.Clamp01((p - 0.10f) / 0.50f) * Mathf.PI);
            if (p < 0.10f || p > 0.60f) hopLift = 0f;

            sampler.GroundFeet(pose, 0.11f + hopLift);
            AddFrameToTracks(tracks, curveNames, pose, t);
        }

        ApplyTracksToClip(clip, tracks, curveNames);
        return clip;
    }

    private static AnimationClip BuildAvoidSideClip(Dictionary<string, float> guardTemplate, float sideSign, PoseSampler sampler)
    {
        // Authentic Sifu spot slip: BOTH feet stay grounded wide, hips sink smoothly, upper torso & head whip left/right.
        const float len = 0.36f;
        const int samples = 18;
        string name = sideSign < 0f ? "Avoid_Left" : "Avoid_Right";
        AnimationClip clip = NewHumanoidClip(name, len, false);
        Dictionary<string, List<Keyframe>> tracks = InitTracks(guardTemplate, out List<string> curveNames);

        for (int s = 0; s <= samples; s++)
        {
            float p = (float)s / samples;
            float t = p * len;
            float env = WeaveEnvelope(p, 0.24f, 0.58f);

            Dictionary<string, float> pose = new Dictionary<string, float>(guardTemplate);
            pose["RootT.z"] = 0f;
            pose["RootT.x"] = sideSign * 0.08f * env;
            // Subtle torso pivot WITHOUT rolling RootQ sideways so feet stay planted firmly on mat!
            SetRootQ(pose, Quaternion.AngleAxis(-sideSign * 6f * env, Vector3.up));

            pose["Spine Front-Back"] = -0.12f - 0.14f * env;
            pose["Chest Front-Back"] = -0.06f - 0.12f * env;
            pose["Spine Left-Right"] = sideSign * 0.44f * env;
            pose["Chest Left-Right"] = sideSign * 0.54f * env;
            pose["UpperChest Left-Right"] = sideSign * 0.36f * env;
            pose["Chest Twist Left-Right"] = -sideSign * 0.24f * env;

            pose["Neck Tilt Left-Right"] = -sideSign * 0.22f * env;
            pose["Head Tilt Left-Right"] = -sideSign * 0.28f * env;
            pose["Neck Nod Down-Up"] = 0.14f + 0.10f * env;
            pose["Head Nod Down-Up"] = 0.08f + 0.08f * env;

            // Keep wide boxing base while absorbing weight into slip-side knee.
            string loadSide = sideSign < 0f ? "Left" : "Right";
            string braceSide = sideSign < 0f ? "Right" : "Left";

            pose[$"{loadSide} Upper Leg In-Out"] = 0.24f;
            pose[$"{braceSide} Upper Leg In-Out"] = 0.24f + 0.08f * env;
            pose[$"{loadSide} Upper Leg Front-Back"] = -0.18f - 0.22f * env;
            pose[$"{braceSide} Upper Leg Front-Back"] = -0.10f - 0.08f * env;
            pose[$"{loadSide} Lower Leg Stretch"] = 0.20f - 0.45f * env;
            pose[$"{braceSide} Lower Leg Stretch"] = 0.24f + 0.16f * env;

            // Raise hands tighter to protect chin during weave.
            pose["Left Arm Front-Back"] = (guardTemplate.TryGetValue("Left Arm Front-Back", out float laf) ? laf : -0.3f) - 0.15f * env;
            pose["Right Arm Front-Back"] = (guardTemplate.TryGetValue("Right Arm Front-Back", out float raf) ? raf : -0.3f) - 0.15f * env;

            sampler.GroundFeet(pose, 0.11f);
            AddFrameToTracks(tracks, curveNames, pose, t);
        }

        ApplyTracksToClip(clip, tracks, curveNames);
        return clip;
    }

    private static AnimationClip BuildAvoidDuckClip(Dictionary<string, float> guardTemplate, PoseSampler sampler)
    {
        // Athletic Sifu/boxing bob-and-weave level change: knees bend deeply while chest stays braced and eyes up!
        const float len = 0.36f;
        const int samples = 18;
        AnimationClip clip = NewHumanoidClip("Avoid_Duck", len, false);
        Dictionary<string, List<Keyframe>> tracks = InitTracks(guardTemplate, out List<string> curveNames);

        for (int s = 0; s <= samples; s++)
        {
            float p = (float)s / samples;
            float t = p * len;
            float env = WeaveEnvelope(p, 0.24f, 0.58f);

            Dictionary<string, float> pose = new Dictionary<string, float>(guardTemplate);
            pose["RootT.x"] = 0f;
            pose["RootT.z"] = 0f;
            SetRootQ(pose, Quaternion.AngleAxis(5f * env, Vector3.right));

            // Compact boxer crunch + upward neck/head tilt so eyes remain locked on opponent!
            pose["Spine Front-Back"] = -0.12f - 0.20f * env;
            pose["Chest Front-Back"] = -0.08f - 0.16f * env;
            pose["Neck Nod Down-Up"] = 0.12f + 0.26f * env;
            pose["Head Nod Down-Up"] = 0.08f + 0.20f * env;

            // Deep grounded knee/hip squat with knees tracking outward cleanly.
            pose["Left Upper Leg Front-Back"] = -0.16f - 0.56f * env;
            pose["Right Upper Leg Front-Back"] = -0.12f - 0.56f * env;
            pose["Left Upper Leg In-Out"] = 0.20f + 0.18f * env;
            pose["Right Upper Leg In-Out"] = 0.20f + 0.18f * env;
            pose["Left Lower Leg Stretch"] = 0.20f - 0.92f * env;
            pose["Right Lower Leg Stretch"] = 0.22f - 0.92f * env;
            pose["Left Foot Up-Down"] = 0.24f * env;
            pose["Right Foot Up-Down"] = 0.24f * env;

            sampler.GroundFeet(pose, 0.11f);
            AddFrameToTracks(tracks, curveNames, pose, t);
        }

        ApplyTracksToClip(clip, tracks, curveNames);
        return clip;
    }

    private static AnimationClip BuildAvoidHopClip(Dictionary<string, float> guardTemplate, PoseSampler sampler)
    {
        // Crisp Sifu snap-hop over low sweeps: asymmetric crane knee tuck, upright guard, soft landing.
        const float len = 0.38f;
        const int samples = 19;
        AnimationClip clip = NewHumanoidClip("Avoid_Hop", len, false);
        Dictionary<string, List<Keyframe>> tracks = InitTracks(guardTemplate, out List<string> curveNames);

        for (int s = 0; s <= samples; s++)
        {
            float p = (float)s / samples;
            float t = p * len;

            float airEnv = 0f;
            float loadBend = 0f;
            float targetFootClearanceY = 0.11f;

            if (p < 0.14f)
            {
                loadBend = Mathf.Sin((p / 0.14f) * Mathf.PI);
            }
            else if (p <= 0.78f)
            {
                float hp = (p - 0.14f) / 0.64f;
                airEnv = Mathf.Sin(hp * Mathf.PI);
                targetFootClearanceY = 0.11f + 0.44f * Mathf.Pow(airEnv, 0.78f);
            }
            else
            {
                float lp = (p - 0.78f) / 0.22f;
                loadBend = Mathf.Sin(lp * Mathf.PI) * 0.85f;
            }

            Dictionary<string, float> pose = new Dictionary<string, float>(guardTemplate);
            pose["RootT.x"] = 0f;
            pose["RootT.z"] = 0f;
            SetRootQ(pose, Quaternion.AngleAxis(3f * airEnv, Vector3.right));

            pose["Spine Front-Back"] = -0.10f - 0.12f * airEnv - 0.08f * loadBend;
            pose["Chest Front-Back"] = -0.06f - 0.10f * airEnv;
            pose["Neck Nod Down-Up"] = 0.12f + 0.10f * airEnv;
            pose["Head Nod Down-Up"] = 0.06f + 0.08f * airEnv;

            // Asymmetric crane hop: lead knee tucks high, rear shin folds compactly underneath.
            pose["Left Upper Leg Front-Back"] = -0.14f - 0.78f * airEnv - 0.18f * loadBend;
            pose["Right Upper Leg Front-Back"] = -0.10f - 0.36f * airEnv - 0.18f * loadBend;
            pose["Left Lower Leg Stretch"] = 0.22f - 0.96f * airEnv - 0.40f * loadBend;
            pose["Right Lower Leg Stretch"] = 0.24f - 0.94f * airEnv - 0.40f * loadBend;
            pose["Left Foot Up-Down"] = -0.24f * airEnv + 0.14f * loadBend;
            pose["Right Foot Up-Down"] = -0.28f * airEnv + 0.14f * loadBend;

            sampler.GroundFeet(pose, targetFootClearanceY);
            AddFrameToTracks(tracks, curveNames, pose, t);
        }

        ApplyTracksToClip(clip, tracks, curveNames);
        return clip;
    }

    private static void SetRootQ(Dictionary<string, float> pose, Quaternion q)
    {
        pose["RootQ.x"] = q.x;
        pose["RootQ.y"] = q.y;
        pose["RootQ.z"] = q.z;
        pose["RootQ.w"] = q.w;
    }

    private static float WeaveEnvelope(float p, float peakStart, float peakEnd)
    {
        if (p <= peakStart)
        {
            return Mathf.Sin((p / peakStart) * Mathf.PI * 0.5f);
        }
        if (p <= peakEnd)
        {
            return 1.0f;
        }
        float recoverT = Mathf.Clamp01((p - peakEnd) / (1f - peakEnd));
        return Mathf.Cos(recoverT * Mathf.PI * 0.5f);
    }

    private static AnimationClip NewHumanoidClip(string name, float length, bool loop)
    {
        AnimationClip clip = new AnimationClip
        {
            name = name,
            frameRate = 30f
        };
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true;
        settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        settings.stopTime = length;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static AnimationClip CreateOrUpdateClip(string fileBaseName, AnimationClip generatedClip)
    {
        string assetPath = $"{CustomAnimDir}/{fileBaseName}.anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(generatedClip, existing);
            existing.name = fileBaseName;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(generatedClip);
            return existing;
        }

        AssetDatabase.CreateAsset(generatedClip, assetPath);
        return generatedClip;
    }

    private static Dictionary<string, float> SamplePoseDictionary(AnimationClip clip, float time)
    {
        Dictionary<string, float> dict = new Dictionary<string, float>();
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (EditorCurveBinding b in bindings)
        {
            if (b.type != typeof(Animator) || !string.IsNullOrEmpty(b.path)) continue;
            if (b.propertyName.Contains("FootT.") ||
                b.propertyName.Contains("FootQ.") ||
                b.propertyName.Contains("HandT.") ||
                b.propertyName.Contains("HandQ.") ||
                b.propertyName.Contains("TDOF."))
            {
                continue;
            }
            AnimationCurve c = AnimationUtility.GetEditorCurve(clip, b);
            if (c != null)
            {
                dict[b.propertyName] = c.Evaluate(time);
            }
        }
        return dict;
    }

    private static Dictionary<string, List<Keyframe>> InitTracks(Dictionary<string, float> template, out List<string> keys)
    {
        keys = new List<string>(template.Keys);
        Dictionary<string, List<Keyframe>> tracks = new Dictionary<string, List<Keyframe>>(keys.Count);
        foreach (string key in keys)
        {
            tracks[key] = new List<Keyframe>();
        }
        return tracks;
    }

    private static void AddFrameToTracks(Dictionary<string, List<Keyframe>> tracks, List<string> keys, Dictionary<string, float> pose, float time)
    {
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            float val = pose.TryGetValue(key, out float v) ? v : 0f;
            tracks[key].Add(new Keyframe(time, val));
        }
    }

    private static void ApplyTracksToClip(AnimationClip clip, Dictionary<string, List<Keyframe>> tracks, List<string> keys)
    {
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            AnimationCurve curve = new AnimationCurve(tracks[key].ToArray());
            for (int k = 0; k < curve.length; k++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Auto);
            }
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), key);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }

    private static void VerifyGeneratedKinematics(List<AnimationClip> clips, PoseSampler sampler)
    {
        foreach (AnimationClip clip in clips)
        {
            for (int i = 0; i < 4; i++)
            {
                float p = i * 0.25f;
                sampler.PreviewGo.transform.position = Vector3.zero;
                sampler.PreviewGo.transform.rotation = Quaternion.identity;
                clip.SampleAnimation(sampler.PreviewGo, clip.length * p);

                Vector3 hp = sampler.PreviewGo.transform.InverseTransformPoint(sampler.Hips.position);
                Vector3 hdp = sampler.PreviewGo.transform.InverseTransformPoint(sampler.Head.position);
                Vector3 lfp = sampler.PreviewGo.transform.InverseTransformPoint(sampler.LeftFoot.position);
                Vector3 rfp = sampler.PreviewGo.transform.InverseTransformPoint(sampler.RightFoot.position);
                Vector3 lhp = sampler.PreviewGo.transform.InverseTransformPoint(sampler.LeftHand.position);
                Vector3 rhp = sampler.PreviewGo.transform.InverseTransformPoint(sampler.RightHand.position);
                Debug.Log($"[SifuAnimVerify] {clip.name} p={p:F2}: hips={hp:F2} head={hdp:F2} lF={lfp:F2} rF={rfp:F2} lH={lhp:F2} rH={rhp:F2}");
            }
        }
    }

    private static AnimationClip LoadFirstClip(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }
}
