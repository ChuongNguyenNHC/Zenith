using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShowcaseSceneSetup
{
    private const string ScenePath = "Assets/ZenithUnity.unity";
    private const string PlayerAnimatorPath = "Assets/Animators/PlayerAnimator.controller";
    private const string EnemyAnimatorPath = "Assets/Animators/EnemyAnimator.controller";

    private const string FightIdleFbx = "Assets/Animators/Brawler Animations Sample/Animations/Fight_Idle.fbx";
    private const string WalkForwardFbx = "Assets/Animators/WebMocap/Walk_F.fbx";
    private const string WalkBackwardFbx = "Assets/Animators/Brawler Animations Sample/Animations/Walk_B.fbx";
    private const string QuickstepForwardFbx = "Assets/Animators/WebMocap/Quickstep_F.fbx";
    private const string QuickstepBackwardFbx = "Assets/Animators/WebMocap/Quickstep_B.fbx";
    private const string GuardIdleFbx = "Assets/Animators/Brawler Animations Sample/Animations/Guard_Idle.fbx";
    private const string GuardHitFbx = "Assets/Animators/Brawler Animations Sample/Animations/Guard_Hit.fbx";
    private const string HitFrontFbx = "Assets/Animators/WebMocap/Hit_F.fbx";
    private const string HitBackFbx = "Assets/Animators/WebMocap/Hit_B.fbx";
    private const string AtkPunch1Fbx = "Assets/Animators/Brawler Animations Sample/Animations/Atk_P_1.fbx";

    private const string RunForwardFbx = "Assets/Animators/WebMocap/Run_F.fbx";
    private const string StrafeLeftFbx = "Assets/Animators/WebMocap/Strafe_L.fbx";
    private const string StrafeRightFbx = "Assets/Animators/WebMocap/Strafe_R.fbx";
    private const string QuickstepLeftFbx = "Assets/Animators/WebMocap/Quickstep_L.fbx";
    private const string QuickstepRightFbx = "Assets/Animators/WebMocap/Quickstep_R.fbx";
    private const string AvoidLeftFbx = "Assets/Animators/WebMocap/Avoid_Left.fbx";
    private const string AvoidRightFbx = "Assets/Animators/WebMocap/Avoid_Right.fbx";
    private const string AvoidDuckFbx = "Assets/Animators/WebMocap/Avoid_Duck.fbx";
    private const string AvoidHopFbx = "Assets/Animators/WebMocap/Avoid_Hop.fbx";

    private const int WebMocapSpecVersion = 7;
    private const string WebMocapVersionPrefKey = "Zenith_WebMocapSetupVersion";

    private const string KneeStrikeAnim = "Assets/Animators/AttackVariants/FreeFighterAnimations/Animations/knee strike.anim";
    private const string SpinningElbowAnim = "Assets/Animators/AttackVariants/FreeFighterAnimations/Animations/spinning elbow.anim";
    private const string FrontSweepAnim = "Assets/Animators/AttackVariants/FreeFighterAnimations/Animations/front sweep.anim";

    private const string RiggedNinjaFbx = "Assets/Characters/RiggedNinja.fbx";
    private const string MaterialsDir = "Assets/Materials/Showcase";
    private const string ArenaRootName = "ShowcaseArena";

    [MenuItem("Zenith/Setup Showcase Scene")]
    public static void SetupShowcaseScene()
    {
        Scene scene = EnsureTargetSceneOpen();

        EnsureDirectory(MaterialsDir);

        Material wallMat = GetOrCreateMaterial("Showcase_Wall.mat", new Color(0.16f, 0.18f, 0.22f));
        Material pillarMat = GetOrCreateMaterial("Showcase_Pillar.mat", new Color(0.55f, 0.16f, 0.16f));
        Material matSurface = GetOrCreateMaterial("Showcase_DojoMat.mat", new Color(0.28f, 0.24f, 0.19f));

        GameObject existingArena = GameObject.Find(ArenaRootName);
        if (existingArena != null)
        {
            Object.DestroyImmediate(existingArena);
        }

        GameObject arenaRoot = new GameObject(ArenaRootName);

        // Center Dojo Mat (no collider so ground locomotion remains completely planar)
        CreateBox("DojoMat", arenaRoot.transform, new Vector3(0f, 0.01f, 0f), new Vector3(12f, 0.02f, 12f), matSurface, false, false);

        // Perimeter Walls (24m x 24m arena)
        float halfSize = 12f;
        float wallHeight = 3.5f;
        float wallThickness = 0.6f;

        CreateBox("Wall_North", arenaRoot.transform, new Vector3(0f, wallHeight * 0.5f, halfSize), new Vector3(halfSize * 2f + wallThickness, wallHeight, wallThickness), wallMat, true, true);
        CreateBox("Wall_South", arenaRoot.transform, new Vector3(0f, wallHeight * 0.5f, -halfSize), new Vector3(halfSize * 2f + wallThickness, wallHeight, wallThickness), wallMat, true, true);
        CreateBox("Wall_East", arenaRoot.transform, new Vector3(halfSize, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, halfSize * 2f), wallMat, true, true);
        CreateBox("Wall_West", arenaRoot.transform, new Vector3(-halfSize, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, halfSize * 2f), wallMat, true, true);

        // Corner Pillars
        float pillarOffset = halfSize - 0.8f;
        Vector3[] corners =
        {
            new Vector3(pillarOffset, 2.25f, pillarOffset),
            new Vector3(-pillarOffset, 2.25f, pillarOffset),
            new Vector3(pillarOffset, 2.25f, -pillarOffset),
            new Vector3(-pillarOffset, 2.25f, -pillarOffset)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            CreateBox($"Pillar_{i + 1}", arenaRoot.transform, corners[i], new Vector3(0.9f, 4.5f, 0.9f), pillarMat, true, true);
        }

        // Extra Training Dummies for 360 Soft-Lock showcase
        GameObject baseDummy = GameObject.Find("Dummy");
        if (baseDummy != null)
        {
            SpawnDummyClone(baseDummy, arenaRoot.transform, "Dummy_Left", new Vector3(-2.6f, 1f, 4.2f));
            SpawnDummyClone(baseDummy, arenaRoot.transform, "Dummy_Right", new Vector3(2.6f, 1f, 4.2f));
        }

        // Humanoid Sparring Opponent to test Parry / Guard / Dodge / Heavy Combos
        AnimatorController enemyController = EnsureEnemyAnimatorController();
        SpawnSparringOpponent(arenaRoot.transform, enemyController);

        // Attach ShowcaseHUD
        if (Object.FindAnyObjectByType<ShowcaseHUD>() == null)
        {
            GameObject hudObj = new GameObject("ShowcaseHUD");
            hudObj.transform.SetParent(arenaRoot.transform);
            hudObj.AddComponent<ShowcaseHUD>();
        }

        ApplyFeelTuningInternal(scene);

        Debug.Log("[Zenith] Showcase scene setup, Sifu animations & sparring opponent ready: Assets/ZenithUnity.unity");
    }

    public static void SetupShowcaseBatchmode()
    {
        SetupShowcaseScene();
        EditorApplication.Exit(0);
    }

    public static void SyncWebMocapBatchmode()
    {
        EnsurePlayerAnimatorStates();
        EnsureEnemyAnimatorController();
        EditorPrefs.SetInt(WebMocapVersionPrefKey, WebMocapSpecVersion);
        AssetDatabase.SaveAssets();
        Debug.Log("[Zenith] Batchmode WebMocap sync complete.");
        EditorApplication.Exit(0);
    }

    [MenuItem("Zenith/Apply Sifu Feel Tuning")]
    public static void ApplyFeelTuning()
    {
        Scene scene = EnsureTargetSceneOpen();
        ApplyFeelTuningInternal(scene);
        Debug.Log("[Zenith] Applied Sifu movement, animations & camera tuning.");
    }

    private static Scene EnsureTargetSceneOpen()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path == ScenePath)
        {
            return active;
        }
        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void ApplyFeelTuningInternal(Scene scene)
    {
        EnsurePlayerAnimatorStates();

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            CharacterController playerCc = player.GetComponent<CharacterController>();
            if (playerCc != null)
            {
                playerCc.minMoveDistance = 0f;
                EditorUtility.SetDirty(playerCc);
            }

            Animator anim = player.GetComponent<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                EditorUtility.SetDirty(anim);
            }

            SerializedObject soPlayer = new SerializedObject(player);
            SetFloat(soPlayer, "walkSpeed", 3.8f);
            SetFloat(soPlayer, "sprintSpeed", 7.0f);
            SetFloat(soPlayer, "guardWalkSpeed", 2.2f);
            SetFloat(soPlayer, "acceleration", 28.0f);
            SetFloat(soPlayer, "deceleration", 34.0f);
            SetFloat(soPlayer, "rotationSmoothTime", 0.06f);
            SetFloat(soPlayer, "sprintForwardLeanAngle", 6.0f);
            SetFloat(soPlayer, "turnBankAngle", 5.5f);
            SetFloat(soPlayer, "dodgeSpeed", 13.2f);
            SetFloat(soPlayer, "dodgeDuration", 0.25f);
            SetFloat(soPlayer, "dodgeInvulnDuration", 0.19f);
            SetFloat(soPlayer, "avoidDuration", 0.34f);
            SetFloat(soPlayer, "avoidActiveWindow", 0.30f);
            SetFloat(soPlayer, "avoidCooldown", 0.06f);
            SetFloat(soPlayer, "parryWindowDuration", 0.24f);
            SetFloat(soPlayer, "attackSpeedMultiplier", 1.35f);
            SetFloat(soPlayer, "softLockRotationSpeed", 1200.0f);
            SetFloat(soPlayer, "hitstopDuration", 0.065f);
            SetFloat(soPlayer, "dashSpeed", 10.5f);
            SetFloat(soPlayer, "attackRange", 5.0f);

            SerializedProperty stopDistances = soPlayer.FindProperty("stopDistancesPerHit");
            if (stopDistances != null)
            {
                float[] tunedStops = { 1.05f, 1.05f, 1.0f, 1.15f, 1.4f };
                stopDistances.arraySize = tunedStops.Length;
                for (int i = 0; i < tunedStops.Length; i++)
                {
                    stopDistances.GetArrayElementAtIndex(i).floatValue = tunedStops[i];
                }
            }

            soPlayer.ApplyModifiedProperties();
            EditorUtility.SetDirty(player);
        }

        ThirdPersonCamera cam = Object.FindAnyObjectByType<ThirdPersonCamera>();
        if (cam != null)
        {
            SerializedObject soCam = new SerializedObject(cam);
            SerializedProperty pivotProp = soCam.FindProperty("pivotOffset");
            if (pivotProp != null)
            {
                pivotProp.vector3Value = new Vector3(0.24f, 0.52f, 0.0f);
            }
            SerializedProperty lockPivotProp = soCam.FindProperty("lockOnPivotOffset");
            if (lockPivotProp != null)
            {
                lockPivotProp.vector3Value = new Vector3(0.32f, 0.58f, 0.0f);
            }
            SetFloat(soCam, "cameraDistance", 5.0f);
            SetFloat(soCam, "minZoomDistance", 3.2f);
            SetFloat(soCam, "maxZoomDistance", 7.5f);
            SetFloat(soCam, "zoomScrollStep", 0.35f);
            SetFloat(soCam, "followDamping", 22.0f);
            SetFloat(soCam, "mouseSensitivityX", 1.2f);
            SetFloat(soCam, "mouseSensitivityY", 1.0f);
            SetFloat(soCam, "baseFov", 62.0f);
            SetFloat(soCam, "sprintFovBoost", 4.0f);
            SetFloat(soCam, "lockOnPitchTarget", 14.0f);
            SetFloat(soCam, "lockOnDistanceOffset", 0.65f);
            SetFloat(soCam, "minDistance", 0.90f);
            soCam.ApplyModifiedProperties();
            EditorUtility.SetDirty(cam);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    [InitializeOnLoadMethod]
    private static void AutoSyncWebMocapIfNeeded()
    {
        if (Application.isBatchMode || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (EditorPrefs.GetInt(WebMocapVersionPrefKey, 0) >= WebMocapSpecVersion)
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorPrefs.SetInt(WebMocapVersionPrefKey, WebMocapSpecVersion);
            EnsurePlayerAnimatorStates();
            Debug.Log("[Zenith] Configured WebMocap Humanoid clips and synced PlayerAnimator.controller.");
        };
    }

    private static void ConfigureWebMocapImporters()
    {
        ConfigureWebMocapClip(WalkForwardFbx,      "Walk_F",      true,  false, true,  false, -1f, -1f, true,  true);
        ConfigureWebMocapClip(RunForwardFbx,       "Run_F",       true,  false, true,  false, -1f, -1f, true,  true);
        ConfigureWebMocapClip(StrafeLeftFbx,       "Strafe_L",    true,  false, true,  false, -1f, -1f, false, false);
        ConfigureWebMocapClip(StrafeRightFbx,      "Strafe_R",    true,  false, true,  false, -1f, -1f, false, false);
        ConfigureWebMocapClip(QuickstepForwardFbx, "Quickstep_F", false, false, true,  false,  8f, 26f);
        ConfigureWebMocapClip(QuickstepBackwardFbx,"Quickstep_B", false, false, true,  false,  5f, 22f);
        ConfigureWebMocapClip(QuickstepLeftFbx,    "Quickstep_L", false, false, true,  false,  1f, 19f);
        ConfigureWebMocapClip(QuickstepRightFbx,   "Quickstep_R", false, false, true,  false,  3f, 23f);
        ConfigureWebMocapClip(AvoidLeftFbx,        "Avoid_Left",  false, true,  true,  true,   9f, 27f);
        ConfigureWebMocapClip(AvoidRightFbx,       "Avoid_Right", false, false, true,  true,   9f, 27f);
        ConfigureWebMocapClip(AvoidDuckFbx,        "Avoid_Duck",  false, false, true,  false,  0f, 25f);
        ConfigureWebMocapClip(AvoidHopFbx,         "Avoid_Hop",   false, false, true,  false, 15f, 49f);
        ConfigureWebMocapClip(HitFrontFbx,         "Hit_F",       false, false, true,  false,  1f, 27f, false, false);
        ConfigureWebMocapClip(HitBackFbx,          "Hit_B",       false, false, true,  false, -1f, -1f, false, false);
    }

    private static bool HasHumanBone(HumanDescription desc, string boneName)
    {
        if (desc.human == null) return false;
        foreach (HumanBone hb in desc.human)
        {
            if (hb.boneName == boneName) return true;
        }
        return false;
    }

    private static void ConfigureWebMocapClip(
        string fbxPath,
        string clipName,
        bool loopTime,
        bool mirror,
        bool lockHeightY,
        bool lockPositionXZ,
        float trimStartOffset,
        float trimEndOffset,
        bool keepOriginalOrientation = true,
        bool keepOriginalPositionXZ = true)
    {
        AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport);
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[Zenith] WebMocap FBX missing at {fbxPath}");
            return;
        }

        ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
        bool hasMixamoBones = HasHumanBone(importer.humanDescription, "mixamorig:Hips");
        bool takeMismatch = defaults != null && defaults.Length > 0
            && importer.clipAnimations != null && importer.clipAnimations.Length > 0
            && importer.clipAnimations[0].takeName != defaults[0].takeName;

        if (importer.animationType != ModelImporterAnimationType.Human
            || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
            || !hasMixamoBones
            || takeMismatch)
        {
            SerializedObject so = new SerializedObject(importer);
            SerializedProperty humanProp = so.FindProperty("m_HumanDescription.m_Human");
            SerializedProperty skelProp = so.FindProperty("m_HumanDescription.m_Skeleton");
            if (humanProp != null) humanProp.ClearArray();
            if (skelProp != null) skelProp.ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();

            importer.clipAnimations = System.Array.Empty<ModelImporterClipAnimation>();
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            defaults = importer.defaultClipAnimations;
        }

        if (defaults == null || defaults.Length == 0)
        {
            Debug.LogWarning($"[Zenith] WebMocap FBX has no default clips at {fbxPath}");
            return;
        }

        ModelImporterClipAnimation def = defaults[0];
        float targetFirst = trimStartOffset >= 0f
            ? Mathf.Clamp(def.firstFrame + trimStartOffset, def.firstFrame, Mathf.Max(def.firstFrame, def.lastFrame - 1f))
            : def.firstFrame;
        float targetLast = trimEndOffset > trimStartOffset && trimStartOffset >= 0f
            ? Mathf.Clamp(def.firstFrame + trimEndOffset, Mathf.Min(def.lastFrame, targetFirst + 1f), def.lastFrame)
            : def.lastFrame;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        ModelImporterClipAnimation c = (clips != null && clips.Length > 0) ? clips[0] : def;

        bool needsClipSave = clips == null || clips.Length == 0
            || c.name != clipName
            || c.takeName != def.takeName
            || !Mathf.Approximately(c.firstFrame, targetFirst)
            || !Mathf.Approximately(c.lastFrame, targetLast)
            || c.loopTime != loopTime
            || c.loopPose != loopTime
            || c.mirror != mirror
            || !c.lockRootRotation
            || c.lockRootHeightY != lockHeightY
            || c.lockRootPositionXZ != lockPositionXZ
            || c.keepOriginalOrientation != keepOriginalOrientation
            || !c.keepOriginalPositionY
            || c.keepOriginalPositionXZ != keepOriginalPositionXZ
            || c.heightFromFeet;

        if (!needsClipSave)
        {
            return;
        }

        c.name = clipName;
        c.takeName = def.takeName;
        c.firstFrame = targetFirst;
        c.lastFrame = targetLast;
        c.loopTime = loopTime;
        c.loopPose = loopTime;
        c.mirror = mirror;
        c.lockRootRotation = true;
        c.lockRootHeightY = lockHeightY;
        c.lockRootPositionXZ = lockPositionXZ;
        c.keepOriginalOrientation = keepOriginalOrientation;
        c.keepOriginalPositionY = true;
        c.keepOriginalPositionXZ = keepOriginalPositionXZ;
        c.heightFromFeet = false;
        importer.clipAnimations = new[] { c };
        importer.SaveAndReimport();

        Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(fbxPath);
        AnimationClip importedClip = LoadFirstClipFromAsset(fbxPath);
        Debug.Log($"[Zenith] Imported {clipName} ({fbxPath}): take={c.takeName}, frames={targetFirst:F0}..{targetLast:F0} (def {def.firstFrame:F0}..{def.lastFrame:F0}), len={(importedClip != null ? importedClip.length : 0f):F2}s, mirror={mirror}, lockY={lockHeightY}, lockXZ={lockPositionXZ}, avatarValid={(avatar != null && avatar.isValid)}");
    }

    private static void EnsurePlayerAnimatorStates()
    {
        ConfigureWebMocapImporters();

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorPath);
        if (controller == null || controller.layers.Length == 0) return;

        EnsureFloatParameter(controller, "MoveX");
        EnsureFloatParameter(controller, "MoveZ");

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState locomotionState = FindState(sm, "Locomotion");
        if (locomotionState == null) return;

        AnimationClip idleClip = LoadFirstClipFromAsset(FightIdleFbx);
        AnimationClip walkClip = LoadFirstClipFromAsset(WalkForwardFbx);
        AnimationClip walkBackClip = LoadFirstClipFromAsset(WalkBackwardFbx);
        AnimationClip runClip = LoadFirstClipFromAsset(RunForwardFbx);
        AnimationClip strafeLeftClip = LoadFirstClipFromAsset(StrafeLeftFbx);
        AnimationClip strafeRightClip = LoadFirstClipFromAsset(StrafeRightFbx);

        // Upgrade Locomotion BlendTree to use real mocap Run_F clip at threshold 1.8 (1.0x cadence)
        if (locomotionState.motion is BlendTree blendTree && idleClip != null && walkClip != null)
        {
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "Speed";
            blendTree.useAutomaticThresholds = false;
            blendTree.minThreshold = 0f;
            blendTree.maxThreshold = 1.8f;

            ChildMotion[] children = new ChildMotion[3];
            children[0] = new ChildMotion { motion = idleClip, threshold = 0f, timeScale = 1f };
            children[1] = new ChildMotion { motion = walkClip, threshold = 1.0f, timeScale = 1f };
            children[2] = new ChildMotion { motion = runClip != null ? runClip : walkClip, threshold = 1.8f, timeScale = 1f };
            blendTree.children = children;
            EditorUtility.SetDirty(blendTree);
        }
        locomotionState.iKOnFeet = true;
        EditorUtility.SetDirty(locomotionState);

        // 2D LockOnLocomotion BlendTree for omnidirectional footwork while facing locked target
        AnimatorState lockOnState = FindState(sm, "LockOnLocomotion");
        if (lockOnState == null)
        {
            lockOnState = sm.AddState("LockOnLocomotion", new Vector3(140f, 120f, 0f));
        }
        lockOnState.iKOnFeet = true;
        EditorUtility.SetDirty(lockOnState);

        BlendTree lockTree = lockOnState.motion as BlendTree;
        if (lockTree == null)
        {
            lockTree = new BlendTree { name = "LockOnLocomotion" };
            AssetDatabase.AddObjectToAsset(lockTree, controller);
            lockOnState.motion = lockTree;
        }
        lockTree.blendType = BlendTreeType.FreeformDirectional2D;
        lockTree.blendParameter = "MoveX";
        lockTree.blendParameterY = "MoveZ";
        if (idleClip != null && walkClip != null && walkBackClip != null && strafeLeftClip != null && strafeRightClip != null)
        {
            lockTree.children = new[]
            {
                new ChildMotion { motion = idleClip, position = Vector2.zero, timeScale = 1f },
                new ChildMotion { motion = walkClip, position = new Vector2(0f, 1f), timeScale = 1f },
                new ChildMotion { motion = walkBackClip, position = new Vector2(0f, -1f), timeScale = 1f },
                new ChildMotion { motion = strafeLeftClip, position = new Vector2(-1f, 0f), timeScale = 1.15f },
                new ChildMotion { motion = strafeRightClip, position = new Vector2(1f, 0f), timeScale = 1.15f }
            };
        }
        EditorUtility.SetDirty(lockTree);

        // Directional Step-Dodges
        EnsureTransitionState(sm, locomotionState, "Quickstep_F", QuickstepForwardFbx, new Vector3(360f, 380f, 0f), 0.85f, 0.08f, true);
        EnsureTransitionState(sm, locomotionState, "Quickstep_B", QuickstepBackwardFbx, new Vector3(580f, 380f, 0f), 0.85f, 0.08f, true);
        EnsureTransitionState(sm, locomotionState, "Quickstep_L", QuickstepLeftFbx, new Vector3(360f, 310f, 0f), 0.85f, 0.08f, true);
        EnsureTransitionState(sm, locomotionState, "Quickstep_R", QuickstepRightFbx, new Vector3(580f, 310f, 0f), 0.85f, 0.08f, true);

        // Sifu Avoid / Weaves (Left/Right/Back-Duck/Low-Hop)
        EnsureTransitionState(sm, locomotionState, "Avoid_Left", AvoidLeftFbx, new Vector3(360f, 540f, 0f), 0.88f, 0.08f, true);
        EnsureTransitionState(sm, locomotionState, "Avoid_Right", AvoidRightFbx, new Vector3(580f, 540f, 0f), 0.88f, 0.08f, true);
        EnsureTransitionState(sm, locomotionState, "Avoid_Duck", AvoidDuckFbx, new Vector3(360f, 610f, 0f), 0.88f, 0.08f, true);
        EnsureTransitionState(sm, locomotionState, "Avoid_Hop", AvoidHopFbx, new Vector3(580f, 610f, 0f), 0.88f, 0.08f, true);

        // Guard & Deflect
        AnimatorState guardIdle = EnsureStateOnly(sm, "Guard_Idle", GuardIdleFbx, new Vector3(360f, 470f, 0f));
        if (guardIdle != null)
        {
            EnsureTransitionState(sm, guardIdle, "Guard_Hit", GuardHitFbx, new Vector3(580f, 470f, 0f), 0.85f, 0.08f, true);
        }

        // Hit reactions
        EnsureTransitionState(sm, locomotionState, "Hit_F", HitFrontFbx, new Vector3(140f, 380f, 0f), 0.85f, 0.10f, true);
        EnsureTransitionState(sm, locomotionState, "Hit_B", HitBackFbx, new Vector3(140f, 440f, 0f), 0.85f, 0.10f, true);

        // Heavy Attack Combo (RMB)
        EnsureTransitionState(sm, locomotionState, "knee strike", KneeStrikeAnim, new Vector3(120f, 880f, 0f), 0.78f, 0.12f, true);
        EnsureTransitionState(sm, locomotionState, "spinning elbow", SpinningElbowAnim, new Vector3(360f, 880f, 0f), 0.78f, 0.12f, true);
        EnsureTransitionState(sm, locomotionState, "front sweep", FrontSweepAnim, new Vector3(600f, 880f, 0f), 0.80f, 0.15f, true);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFloatParameter(AnimatorController controller, string paramName)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == paramName) return;
        }
        controller.AddParameter(paramName, AnimatorControllerParameterType.Float);
    }

    private static AnimatorController EnsureEnemyAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyAnimatorPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(EnemyAnimatorPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState locomotionState = FindState(sm, "Locomotion");
        AnimationClip idleClip = LoadFirstClipFromAsset(FightIdleFbx);
        AnimationClip walkClip = LoadFirstClipFromAsset(WalkForwardFbx);
        if (locomotionState == null)
        {
            locomotionState = sm.AddState("Locomotion", new Vector3(300f, 120f, 0f));
            sm.defaultState = locomotionState;

            BlendTree bt = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                minThreshold = 0f,
                maxThreshold = 1f
            };
            bt.children = new[]
            {
                new ChildMotion { motion = idleClip, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = walkClip, threshold = 1f, timeScale = 1f }
            };
            AssetDatabase.AddObjectToAsset(bt, controller);
            locomotionState.motion = bt;
        }
        else if (locomotionState.motion is BlendTree existingBt && idleClip != null && walkClip != null)
        {
            existingBt.children = new[]
            {
                new ChildMotion { motion = idleClip, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = walkClip, threshold = 1f, timeScale = 1f }
            };
            EditorUtility.SetDirty(existingBt);
        }
        if (locomotionState != null)
        {
            locomotionState.iKOnFeet = true;
            EditorUtility.SetDirty(locomotionState);
        }

        EnsureTransitionState(sm, locomotionState, "Atk_P_1", AtkPunch1Fbx, new Vector3(120f, 260f, 0f), 0.82f, 0.12f, true);
        EnsureTransitionState(sm, locomotionState, "front sweep", FrontSweepAnim, new Vector3(120f, 330f, 0f), 0.84f, 0.12f, true);
        EnsureTransitionState(sm, locomotionState, "Hit_F", HitFrontFbx, new Vector3(320f, 260f, 0f), 0.85f, 0.10f, true);
        EnsureTransitionState(sm, locomotionState, "Hit_B", HitBackFbx, new Vector3(520f, 260f, 0f), 0.85f, 0.10f, true);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimatorState EnsureStateOnly(AnimatorStateMachine sm, string stateName, string assetPath, Vector3 pos)
    {
        AnimatorState state = FindState(sm, stateName);
        if (state != null) return state;

        AnimationClip clip = LoadFirstClipFromAsset(assetPath);
        if (clip == null) return null;

        state = sm.AddState(stateName, pos);
        state.motion = clip;
        state.speed = 1.0f;
        return state;
    }

    private static void EnsureTransitionState(AnimatorStateMachine sm, AnimatorState returnTarget, string stateName, string assetPath, Vector3 pos, float exitTime, float duration, bool withReturnTransition)
    {
        AnimationClip clip = LoadFirstClipFromAsset(assetPath);
        AnimatorState state = FindState(sm, stateName);
        if (state == null)
        {
            if (clip == null)
            {
                Debug.LogWarning($"[Zenith] Missing animation clip at path: {assetPath}");
                return;
            }

            state = sm.AddState(stateName, pos);
            state.motion = clip;
            state.speed = 1.0f;
        }
        else if (clip != null && state.motion != clip)
        {
            state.motion = clip;
        }

        if (withReturnTransition && returnTarget != null && state.transitions.Length == 0)
        {
            AnimatorStateTransition exit = state.AddTransition(returnTarget);
            exit.hasExitTime = true;
            exit.exitTime = exitTime;
            exit.hasFixedDuration = true;
            exit.duration = duration;
        }
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string stateName)
    {
        foreach (ChildAnimatorState child in sm.states)
        {
            if (child.state != null && child.state.name == stateName)
            {
                return child.state;
            }
        }
        return null;
    }

    private static AnimationClip LoadFirstClipFromAsset(string assetPath)
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

    private static void SpawnSparringOpponent(Transform parent, AnimatorController enemyController)
    {
        GameObject ninjaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedNinjaFbx);
        if (ninjaPrefab == null)
        {
            Debug.LogWarning($"[Zenith] RiggedNinja prefab not found at {RiggedNinjaFbx}");
            return;
        }

        GameObject root = new GameObject("Ninja_Sparring");
        root.tag = "Enemy";
        root.transform.SetParent(parent);
        root.transform.position = new Vector3(0f, 0.05f, 7.8f);
        root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        CharacterController cc = root.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 0.95f, 0f);
        cc.height = 1.9f;
        cc.radius = 0.35f;
        cc.skinWidth = 0.04f;
        cc.minMoveDistance = 0f;

        GameObject visual = PrefabUtility.InstantiatePrefab(ninjaPrefab, root.transform) as GameObject;
        if (visual != null)
        {
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            Animator visualAnim = visual.GetComponent<Animator>();
            if (visualAnim == null) visualAnim = visual.AddComponent<Animator>();
            visualAnim.runtimeAnimatorController = enemyController;
            visualAnim.applyRootMotion = false;
        }

        root.AddComponent<SparringEnemy>();
    }

    private static void SetFloat(SerializedObject so, string propName, float value)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null)
        {
            prop.floatValue = value;
        }
    }

    private static void SpawnDummyClone(GameObject template, Transform parent, string name, Vector3 position)
    {
        GameObject clone = Object.Instantiate(template, position, Quaternion.Euler(0f, 180f, 0f), parent);
        clone.name = name;
        clone.tag = template.tag;
    }

    private static void CreateBox(string name, Transform parent, Vector3 position, Vector3 scale, Material mat, bool isStatic, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.isStatic = isStatic;

        if (!keepCollider)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null)
        {
            r.sharedMaterial = mat;
        }
    }

    private static Material GetOrCreateMaterial(string fileName, Color color)
    {
        string path = $"{MaterialsDir}/{fileName}";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static void EnsureDirectory(string assetPath)
    {
        if (!AssetDatabase.IsValidFolder(assetPath))
        {
            Directory.CreateDirectory(assetPath);
            AssetDatabase.Refresh();
        }
    }
}
