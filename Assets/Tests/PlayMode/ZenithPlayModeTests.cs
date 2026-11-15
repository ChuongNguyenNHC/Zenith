using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ZenithPlayModeTests
{
    private PlayerController player;
    private ThirdPersonCamera thirdPersonCamera;
    private DummyEnemy dummy;
    private SparringEnemy sparringEnemy;
    private Animator playerAnimator;

    private IEnumerator LoadShowcaseScene()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(0, LoadSceneMode.Single);
        Assert.IsNotNull(op, "Failed to start loading primary scene (index 0: Assets/ZenithUnity.unity).");
        while (!op.isDone)
        {
            yield return null;
        }

        // Wait 2 frames for Awake/Start initialization
        yield return null;
        yield return null;

        player = Object.FindAnyObjectByType<PlayerController>();
        thirdPersonCamera = Object.FindAnyObjectByType<ThirdPersonCamera>();
        dummy = GameObject.Find("Dummy")?.GetComponent<DummyEnemy>();
        sparringEnemy = Object.FindAnyObjectByType<SparringEnemy>();
        playerAnimator = player != null ? player.GetComponentInChildren<Animator>() : null;

        Assert.IsNotNull(player, "PlayerController not found in ZenithUnity scene.");
        Assert.IsNotNull(thirdPersonCamera, "ThirdPersonCamera not found in ZenithUnity scene.");
        Assert.IsNotNull(playerAnimator, "Player Animator not found.");
    }

    [UnityTest]
    public IEnumerator SceneSetup_AllObjectsAndSifuAnimationsPresent()
    {
        yield return LoadShowcaseScene();

        Assert.IsFalse(playerAnimator.applyRootMotion, "Player Animator.applyRootMotion must be disabled so CharacterController owns movement cleanly.");
        Assert.IsNotNull(GameObject.Find("ShowcaseArena"), "ShowcaseArena object missing from scene.");
        Assert.IsNotNull(Object.FindAnyObjectByType<ShowcaseHUD>(), "ShowcaseHUD missing from scene.");
        Assert.IsNotNull(dummy, "Base Dummy not found.");
        Assert.IsNotNull(GameObject.Find("Dummy_Left"), "Dummy_Left not found.");
        Assert.IsNotNull(GameObject.Find("Dummy_Right"), "Dummy_Right not found.");
        Assert.IsNotNull(sparringEnemy, "Ninja_Sparring (SparringEnemy) not found.");

        string[] requiredStates =
        {
            "Locomotion",
            "LockOnLocomotion",
            "Quickstep_F",
            "Quickstep_B",
            "Quickstep_L",
            "Quickstep_R",
            "Avoid_Left",
            "Avoid_Right",
            "Avoid_Duck",
            "Avoid_Hop",
            "Guard_Idle",
            "Guard_Hit",
            "Hit_F",
            "Atk_P_1",
            "Atk_P_2",
            "elbow chop",
            "back fist",
            "spinning axe kick",
            "knee strike",
            "spinning elbow",
            "front sweep"
        };

        foreach (string stateName in requiredStates)
        {
            Assert.IsTrue(
                playerAnimator.HasState(0, Animator.StringToHash(stateName)),
                $"PlayerAnimator is missing required state '{stateName}'."
            );
        }

        bool hasRunClipAsset = false;
        foreach (AnimationClip clip in playerAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == "Run_F")
            {
                hasRunClipAsset = true;
                break;
            }
        }
        Assert.IsTrue(hasRunClipAsset, "PlayerAnimator must reference dedicated custom Run_F AnimationClip.");

        Animator enemyAnim = sparringEnemy.GetComponentInChildren<Animator>();
        Assert.IsNotNull(enemyAnim, "SparringEnemy Animator missing.");
        Assert.IsFalse(enemyAnim.applyRootMotion, "SparringEnemy Animator.applyRootMotion must be false.");
        Assert.IsTrue(enemyAnim.HasState(0, Animator.StringToHash("Locomotion")), "EnemyAnimator missing Locomotion.");
        Assert.IsTrue(enemyAnim.HasState(0, Animator.StringToHash("Atk_P_1")), "EnemyAnimator missing Atk_P_1.");
        Assert.IsTrue(enemyAnim.HasState(0, Animator.StringToHash("front sweep")), "EnemyAnimator missing front sweep.");
        Assert.IsTrue(enemyAnim.HasState(0, Animator.StringToHash("Hit_F")), "EnemyAnimator missing Hit_F.");
    }

    [UnityTest]
    public IEnumerator Movement_WalkSprintTurnAndStopInertiaFeelResponsive()
    {
        yield return LoadShowcaseScene();

        // Walk along open -Z lane (away from sparring opponent) for 0.45s
        player.SetTestInputs(new Vector2(0f, -1f), sprint: false, guard: false);
        float elapsed = 0f;
        while (elapsed < 0.45f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        float walkSpeedObserved = player.CurrentPlanarSpeed;
        Assert.Greater(walkSpeedObserved, 3.2f, $"Walk speed should converge cleanly above 3.2 m/s, got {walkSpeedObserved:F2}");
        Assert.Less(walkSpeedObserved, 4.5f, $"Walk speed overshot unexpectedly: {walkSpeedObserved:F2}");
        Assert.IsFalse(player.IsSprinting, "Player should not report sprinting while walking.");

        // Sprint along open -Z lane for 0.55s
        player.SetTestInputs(new Vector2(0f, -1f), sprint: true, guard: false);
        elapsed = 0f;
        while (elapsed < 0.55f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        float sprintSpeedObserved = player.CurrentPlanarSpeed;
        float sprintAnimSpeed = playerAnimator.speed;
        float sprintLean = player.CurrentForwardLean;
        float sprintFov = thirdPersonCamera.CurrentFov;

        Debug.Log($"[Telemetry][Sprint] speed={sprintSpeedObserved:F2} animSpeed={sprintAnimSpeed:F2} lean={sprintLean:F1}deg fov={sprintFov:F1}");

        Assert.Greater(sprintSpeedObserved, 6.0f, $"Sprint speed should reach >6.0 m/s, got {sprintSpeedObserved:F2}");
        Assert.IsTrue(player.IsSprinting, "Player.IsSprinting should be true.");
        Assert.AreEqual(1.0f, sprintAnimSpeed, 0.02f, "Sprint uses dedicated Run_F AnimationClip at natural 1.0x speed instead of accelerating Walk_F.");
        Assert.Greater(sprintLean, 4.0f, "Upper body should lean slightly forward during sprint.");
        Assert.Greater(sprintFov, 60.5f, "Dynamic FOV should boost slightly during sprint.");

        // Release keys -> verify crisp grounded deceleration back to stop (not lingering slide)
        Vector3 posAtRelease = player.transform.position;
        player.SetTestInputs(Vector2.zero, sprint: false, guard: false);
        yield return null;
        float oneFrameAfterReleaseSpeed = player.CurrentPlanarSpeed;
        Assert.Greater(oneFrameAfterReleaseSpeed, 0.5f, "Movement should decelerate smoothly over ~0.1-0.2s instead of snapping dead on frame 1.");

        elapsed = 0f;
        while (elapsed < 0.45f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        float settledSpeed = player.CurrentPlanarSpeed;
        float slideDist = Vector3.Distance(posAtRelease, player.transform.position);
        Debug.Log($"[Telemetry][Stop] settledSpeed={settledSpeed:F3} stopSlideDist={slideDist:F2}m");

        Assert.Less(settledSpeed, 0.1f, $"Player failed to come to a crisp stop within 0.45s (speed={settledSpeed:F2}).");
        Assert.Less(slideDist, 1.8f, $"Stop inertia slid too far ({slideDist:F2}m) — feels icy.");
        player.ClearTestInputOverride();
    }

    [UnityTest]
    public IEnumerator Dodge_CancelsAttackGrantsIFramesAndEvadesEnemyBlow()
    {
        yield return LoadShowcaseScene();

        Vector3 startPos = player.transform.position;
        player.SetTestInputs(new Vector2(1f, 0f), sprint: false, guard: false);
        player.TriggerLightAttack();
        yield return null;

        Assert.IsTrue(player.IsAttacking, "Attack did not start.");

        // Dodge immediately mid-swing to verify Sifu attack-cancel
        player.TriggerDodge();
        yield return null;

        Assert.IsFalse(player.IsAttacking, "Dodge should cancel active attack immediately.");
        Assert.IsTrue(player.IsDodging, "Player should enter dodging state.");
        Assert.IsTrue(player.IsInvulnerable, "Dodge should grant evade i-frames.");

        // Simulated incoming enemy blow during dodge i-frames
        player.ReceiveEnemyAttack(sparringEnemy.transform, 5.5f, sparringEnemy);
        Assert.AreEqual(1, player.EvadesLanded, "Incoming hit during dodge i-frames should count as EVADE!");
        Assert.AreEqual("EVADE!", player.LastDefenseEvent);

        float elapsed = 0f;
        while (elapsed < 0.30f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        float dodgeDisplacement = Vector3.Distance(startPos, player.transform.position);
        Debug.Log($"[Telemetry][Dodge] displacement={dodgeDisplacement:F2}m");
        Assert.Greater(dodgeDisplacement, 1.4f, $"Quickstep dodge displacement too short ({dodgeDisplacement:F2}m).");
        Assert.IsFalse(player.IsDodging, "Dodge should complete cleanly after dodgeDuration.");

        // Wait past dodge cooldown, then test neutral backstep (no directional input -> Quickstep_B backward)
        yield return new WaitForSeconds(0.18f);
        player.SetTestInputs(Vector2.zero, sprint: false, guard: false);
        Vector3 backStartPos = player.transform.position;
        Vector3 forwardBeforeBackstep = player.transform.forward;
        player.TriggerDodge();
        yield return null;
        Assert.IsTrue(player.IsDodging, "Neutral dodge should trigger Quickstep_B.");
        yield return new WaitForSeconds(0.30f);

        float backDot = Vector3.Dot(player.transform.position - backStartPos, -forwardBeforeBackstep);
        Debug.Log($"[Telemetry][Backstep] backwardDisplacement={backDot:F2}m");
        Assert.Greater(backDot, 1.0f, $"Neutral backstep should move cleanly backward ({backDot:F2}m).");
        player.ClearTestInputOverride();
    }

    [UnityTest]
    public IEnumerator GuardAndParry_DeflectsSparringEnemyAttack()
    {
        yield return LoadShowcaseScene();

        player.SetTestInputs(Vector2.zero, sprint: false, guard: true);
        yield return null;

        Assert.IsTrue(player.IsGuarding, "Player should be in Guard stance.");
        Assert.IsTrue(player.IsParryWindowActive, "Pressing Guard should open timed Deflect/Parry window.");

        player.ReceiveEnemyAttack(sparringEnemy.transform, 6.0f, sparringEnemy);

        Assert.AreEqual(1, player.ParriesLanded, "Attack during parry window should trigger PARRY.");
        Assert.AreEqual("PARRY!", player.LastDefenseEvent);
        Assert.AreEqual(SparringEnemy.AIState.Parried, sparringEnemy.CurrentState, "SparringEnemy should enter Parried stagger state.");
        Assert.AreEqual(1, sparringEnemy.ParryStaggerCount);

        player.SetTestInputs(Vector2.zero, sprint: false, guard: false);
        player.ClearTestInputOverride();
        yield return null;
        Assert.IsFalse(player.IsGuarding, "Releasing guard should return cleanly out of Guard stance.");
    }

    [UnityTest]
    public IEnumerator Combos_LightAndHeavyAttacksAcquireSoftLockRespectStopDistanceAndHitDummy()
    {
        yield return LoadShowcaseScene();

        // Isolate training dummy soft-lock combo test from the roaming sparring opponent
        if (sparringEnemy != null) sparringEnemy.enabled = false;

        // Position player within 3.2m of Dummy_Left so soft-lock engages cleanly
        GameObject leftDummyObj = GameObject.Find("Dummy_Left");
        Assert.IsNotNull(leftDummyObj);
        DummyEnemy leftDummy = leftDummyObj.GetComponent<DummyEnemy>();
        int initialHits = leftDummy.HitCount;

        // Place player ~2.8m in front of Dummy_Left facing it (preserve player's grounded Y)
        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = new Vector3(leftDummyObj.transform.position.x, player.transform.position.y, leftDummyObj.transform.position.z - 2.8f);
        player.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, 1f));
        cc.enabled = true;
        yield return null;

        // Chain Light 1 -> Light 2 -> Heavy 1 -> Heavy 2 -> Heavy 3 (front sweep), buffering right after each hit connects
        float minDistanceObserved = float.MaxValue;
        bool[] heavySequence = { false, true, true, true };

        player.TriggerLightAttack();
        yield return null;
        Transform acquiredTarget = player.TargetEnemy;

        for (int nextIdx = 0; nextIdx < heavySequence.Length; nextIdx++)
        {
            float waitElapsed = 0f;
            while (player.IsAttacking && !player.HasDealtDamage && waitElapsed < 1.2f)
            {
                float d = Vector3.Distance(
                    new Vector3(player.transform.position.x, 0f, player.transform.position.z),
                    new Vector3(leftDummyObj.transform.position.x, 0f, leftDummyObj.transform.position.z));
                if (d < minDistanceObserved) minDistanceObserved = d;
                waitElapsed += Time.deltaTime;
                yield return null;
            }

            if (heavySequence[nextIdx]) player.TriggerHeavyAttack();
            else player.TriggerLightAttack();

            // Wait for current step to transition into next buffered step
            string currentMove = player.CurrentMoveName;
            waitElapsed = 0f;
            while (player.IsAttacking && player.CurrentMoveName == currentMove && waitElapsed < 1.0f)
            {
                float d = Vector3.Distance(
                    new Vector3(player.transform.position.x, 0f, player.transform.position.z),
                    new Vector3(leftDummyObj.transform.position.x, 0f, leftDummyObj.transform.position.z));
                if (d < minDistanceObserved) minDistanceObserved = d;
                waitElapsed += Time.deltaTime;
                yield return null;
            }
        }

        // Wait for final finisher Heavy 3 to connect and wrap up
        float elapsed = 0f;
        while (player.IsAttacking && elapsed < 1.2f)
        {
            float dist = Vector3.Distance(
                new Vector3(player.transform.position.x, 0f, player.transform.position.z),
                new Vector3(leftDummyObj.transform.position.x, 0f, leftDummyObj.transform.position.z));
            if (dist < minDistanceObserved) minDistanceObserved = dist;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[Telemetry][Combo] target={acquiredTarget?.name} hits={leftDummy.HitCount - initialHits} minDist={minDistanceObserved:F2}m");

        Assert.AreEqual(leftDummyObj.transform, acquiredTarget, "Soft-lock should acquire closest Dummy_Left.");
        Assert.GreaterOrEqual(leftDummy.HitCount - initialHits, 4, "Chained light + heavy sweep combo should land at least 4 hits on Dummy_Left.");
        Assert.Greater(minDistanceObserved, 0.70f, "Soft-lock approach clamped improperly — clipped inside Dummy!");
    }

    [UnityTest]
    public IEnumerator Movement_180DegreePivotSnapsWithoutSluggishness()
    {
        yield return LoadShowcaseScene();

        player.SetTestInputs(new Vector2(0f, 1f), sprint: true, guard: false);
        yield return new WaitForSeconds(0.35f);
        Vector3 forwardBeforeTurn = player.transform.forward;

        // Slam opposite direction (180-degree reversal)
        player.SetTestInputs(new Vector2(0f, -1f), sprint: true, guard: false);
        yield return new WaitForSeconds(0.28f);

        float dot = Vector3.Dot(forwardBeforeTurn, player.transform.forward);
        Debug.Log($"[Telemetry][Pivot180] dotAfter0.28s={dot:F2}");
        Assert.Less(dot, -0.5f, $"180-degree reversal should turn cleanly past 120 deg within 0.28s (dot={dot:F2}).");
        player.ClearTestInputOverride();
    }

    [UnityTest]
    public IEnumerator SparringEnemy_ApproachesStrikesPlayerAndRespondsToCounterHit()
    {
        yield return LoadShowcaseScene();

        // Place player 2.8m in front of Ninja_Sparring preserving player's grounded Y
        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = new Vector3(sparringEnemy.transform.position.x, player.transform.position.y, sparringEnemy.transform.position.z - 2.8f);
        player.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, 1f));
        cc.enabled = true;

        player.SetTestInputs(Vector2.zero, sprint: false, guard: true);
        float elapsed = 0f;
        while (elapsed < 3.5f && (player.BlocksLanded + player.ParriesLanded) == 0)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 p = player.transform.position;
        Vector3 e = sparringEnemy.transform.position;
        float planar = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(e.x, e.z));
        Debug.Log($"[Telemetry][SparringAI] elapsed={elapsed:F2}s blocks={player.BlocksLanded} parries={player.ParriesLanded} enemyState={sparringEnemy.CurrentState} planar={planar:F2}m p={p} e={e}");
        Assert.Greater(player.BlocksLanded + player.ParriesLanded, 0, $"SparringEnemy failed to approach and strike Player within 3.5s (state={sparringEnemy.CurrentState}, planar={planar:F2}m).");

        // Release guard and counter with Heavy Strike!
        int hitsBefore = sparringEnemy.HitCount;
        player.SetTestInputs(Vector2.zero, sprint: false, guard: false);
        yield return null;
        player.TriggerHeavyAttack();
        yield return new WaitForSeconds(0.65f);

        Assert.Greater(sparringEnemy.HitCount, hitsBefore, "Counter-hit should connect cleanly on SparringEnemy using planar reach!");
        player.ClearTestInputOverride();
    }

    [UnityTest]
    public IEnumerator Camera_FollowsSmoothlyWithoutClippingOrViolentZoomOnNearbyEnemies()
    {
        yield return LoadShowcaseScene();

        // Move player right next to Dummies and sprint forward
        player.SetTestInputs(new Vector2(0f, 1f), sprint: true, guard: false);
        float elapsed = 0f;
        float minCamDist = float.MaxValue;
        while (elapsed < 0.8f)
        {
            if (thirdPersonCamera.CurrentDistance < minCamDist)
            {
                minCamDist = thirdPersonCamera.CurrentDistance;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[Telemetry][Camera] minDist={minCamDist:F2}m currentDist={thirdPersonCamera.CurrentDistance:F2}m");
        Assert.Greater(minCamDist, 4.0f, "Camera SphereCast should ignore Player/Enemy colliders and keep spacious ~5.0m follow distance near dummies.");
        player.ClearTestInputOverride();
    }

    [UnityTest]
    public IEnumerator Attack_RecoveryMoveCancelReturnsToLocomotionBeforeClipEnd()
    {
        yield return LoadShowcaseScene();

        player.SetTestInputs(Vector2.zero, sprint: false, guard: false);
        player.TriggerLightAttack();
        yield return null;

        Assert.IsTrue(player.IsAttacking, "Light attack should enter attacking state.");

        // Wait past active hit frame before supplying directional move input to trigger recovery cancel
        while (player.IsAttacking && !player.HasDealtDamage)
        {
            yield return null;
        }

        Assert.IsTrue(player.IsAttacking, "Attack should still be playing right after hit frame.");
        player.SetTestInputs(new Vector2(1f, 0f), sprint: false, guard: false);

        float maxProgressWhileAttacking = player.AttackProgress;
        float watchElapsed = 0f;
        while (player.IsAttacking && watchElapsed < 1.2f)
        {
            if (player.AttackProgress > maxProgressWhileAttacking)
            {
                maxProgressWhileAttacking = player.AttackProgress;
            }
            watchElapsed += Time.deltaTime;
            yield return null;
        }

        float speedAtExit = player.CurrentPlanarSpeed;
        yield return new WaitForSeconds(0.08f);
        float speedAfterRamp = player.CurrentPlanarSpeed;

        Debug.Log($"[Telemetry][RecoveryCancel] maxProgressObserved={maxProgressWhileAttacking:F2} speedAtExit={speedAtExit:F3} speedAfter0.08s={speedAfterRamp:F2}");
        Assert.IsFalse(player.IsAttacking, "Recovery cancel should exit attacking state.");
        Assert.GreaterOrEqual(maxProgressWhileAttacking, 0.70f, "Cancel should wait until recoveryCancelRatio (~0.72) rather than canceling mid-hit.");
        Assert.Less(maxProgressWhileAttacking, 0.96f, $"Directional input should cancel attack recovery early instead of waiting until clip end ({maxProgressWhileAttacking:F2}).");
        Assert.Greater(speedAtExit, 0.0f, "HandleFreeMovement should begin accelerating immediately on the cancel frame.");
        Assert.Greater(speedAfterRamp, 1.5f, $"Player should accelerate crisply after recovery cancel (speedAfter0.08s={speedAfterRamp:F2}).");
        player.ClearTestInputOverride();
    }

    [UnityTest]
    public IEnumerator Sprint_PlaysDedicatedRunClipWithNaturalCadenceAndArmSwing()
    {
        yield return LoadShowcaseScene();

        player.SetTestInputs(new Vector2(0f, -1f), sprint: true, guard: false);
        yield return new WaitForSeconds(0.45f);

        Assert.AreEqual(1.0f, playerAnimator.speed, 0.02f, "Sprint should play actual Run_F clip at 1.0x speed instead of speeding up Walk_F.");

        AnimatorClipInfo[] clips = playerAnimator.GetCurrentAnimatorClipInfo(0);
        float runWeight = 0f;
        foreach (AnimatorClipInfo info in clips)
        {
            if (info.clip != null && info.clip.name == "Run_F")
            {
                runWeight += info.weight;
            }
        }
        Assert.Greater(runWeight, 0.75f, $"Sprinting BlendTree should strongly weight Run_F clip (observed weight={runWeight:F2}).");

        Transform lHand = playerAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform lFoot = playerAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
        float minHandZ = float.MaxValue, maxHandZ = float.MinValue;
        float minFootZ = float.MaxValue, maxFootZ = float.MinValue;

        float elapsed = 0f;
        while (elapsed < 0.65f)
        {
            float hz = player.transform.InverseTransformPoint(lHand.position).z;
            float fz = player.transform.InverseTransformPoint(lFoot.position).z;
            if (hz < minHandZ) minHandZ = hz;
            if (hz > maxHandZ) maxHandZ = hz;
            if (fz < minFootZ) minFootZ = fz;
            if (fz > maxFootZ) maxFootZ = fz;
            elapsed += Time.deltaTime;
            yield return null;
        }

        float handSwing = maxHandZ - minHandZ;
        float footStride = maxFootZ - minFootZ;
        Debug.Log($"[Telemetry][RunClip] runWeight={runWeight:F2} animSpeed={playerAnimator.speed:F2} handSwing={handSwing:F2}m footStride={footStride:F2}m");
        Assert.Greater(handSwing, 0.12f, $"Run_F should visibly pump arms forward/back (handSwing={handSwing:F2}m).");
        Assert.Greater(footStride, 0.35f, $"Run_F should drive full front-to-back running stride (footStride={footStride:F2}m).");
        player.ClearTestInputOverride();
    }

    [UnityTest]
    public IEnumerator SifuAvoid_HighVsLowStaysInPlaceRestoresStructureAndOpensPunish()
    {
        yield return LoadShowcaseScene();

        // Keep sparring AI paused during deterministic high/low height-matching checks
        sparringEnemy.enabled = false;

        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = new Vector3(sparringEnemy.transform.position.x, player.transform.position.y, sparringEnemy.transform.position.z - 1.55f);
        player.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, 1f));
        cc.enabled = true;

        // 1. Hold late Guard to absorb one blocked hit and build Player Structure
        player.SetTestInputs(Vector2.zero, sprint: false, guard: true);
        yield return new WaitForSeconds(0.30f); // Past 0.24s parry window -> pure Block
        player.ReceiveEnemyAttack(sparringEnemy.transform, 5.5f, sparringEnemy, AttackHeight.HighMid);
        float structureAfterBlock = player.Structure;
        Assert.Greater(structureAfterBlock, 15f, "Blocking should build Player Structure meter.");

        yield return new WaitForSeconds(0.08f);
        Vector3 pocketPosBeforeAvoid = player.transform.position;

        // 2. Sifu Avoid (Duck) against High/Mid strike
        player.TriggerAvoid(AvoidDirection.Down);
        yield return null;
        Assert.IsTrue(player.IsAvoiding, "TriggerAvoid should enter Avoiding state.");
        Assert.AreEqual(AvoidDirection.Down, player.CurrentAvoidDirection);

        player.ReceiveEnemyAttack(sparringEnemy.transform, 5.5f, sparringEnemy, AttackHeight.HighMid);
        Assert.AreEqual(1, player.AvoidsLanded, "Ducking Avoid should cleanly evade High/Mid strike.");
        Assert.AreEqual("AVOID!", player.LastDefenseEvent);
        Assert.IsTrue(player.HasAvoidCounterWindow, "Successful Avoid must open Counter-Punish window.");
        Assert.Less(player.Structure, structureAfterBlock, "Sifu Avoid must restore Player Structure.");

        while (player.IsAvoiding)
        {
            yield return null;
        }
        yield return new WaitForSeconds(0.08f);

        float pocketDrift = Vector2.Distance(
            new Vector2(pocketPosBeforeAvoid.x, pocketPosBeforeAvoid.z),
            new Vector2(player.transform.position.x, player.transform.position.z));
        Debug.Log($"[Telemetry][Avoid] drift={pocketDrift:F3}m structAfterBlock={structureAfterBlock:F1} structAfterAvoid={player.Structure:F1}");
        Assert.Less(pocketDrift, 0.20f, $"Sifu Avoid must stay firmly in pocket range rather than sliding out (drift={pocketDrift:F2}m).");

        // 3. Hop Avoid cleanly clears Low Sweep
        player.TriggerAvoid(AvoidDirection.Hop);
        yield return null;
        Assert.IsTrue(player.IsAvoiding, "Hop Avoid should enter Avoiding state cleanly.");
        player.ReceiveEnemyAttack(sparringEnemy.transform, 5.5f, sparringEnemy, AttackHeight.Low);
        Assert.AreEqual(2, player.AvoidsLanded, "Hop Avoid should cleanly evade Low Sweep.");
        Assert.AreEqual("AVOID!", player.LastDefenseEvent);

        while (player.IsAvoiding)
        {
            yield return null;
        }
        yield return new WaitForSeconds(0.08f);

        // 4. Wrong height check: Duck Avoid must NOT evade a Low Sweep
        int avoidsBeforeWrong = player.AvoidsLanded;
        player.TriggerAvoid(AvoidDirection.Down);
        yield return null;
        player.ReceiveEnemyAttack(sparringEnemy.transform, 5.5f, sparringEnemy, AttackHeight.Low);
        Assert.AreEqual(avoidsBeforeWrong, player.AvoidsLanded, "Ducking into a Low Sweep must get caught cleanly (height match enforced).");

        // 5. Also verify Hop Avoid does NOT evade a High/Mid strike
        while (player.IsAvoiding)
        {
            yield return null;
        }
        yield return new WaitForSeconds(0.45f); // clear hitReactionTimer cleanly
        player.TriggerAvoid(AvoidDirection.Hop);
        yield return null;
        player.ReceiveEnemyAttack(sparringEnemy.transform, 5.5f, sparringEnemy, AttackHeight.HighMid);
        Assert.AreEqual(avoidsBeforeWrong, player.AvoidsLanded, "Hopping Avoid should not evade a High/Mid strike.");

        player.ClearTestInputOverride();
    }

    [UnityTest]
    public IEnumerator StepDodge_LockedOnSidestepsLaterallyWithCrispStopBrake()
    {
        yield return LoadShowcaseScene();

        // Lock onto SparringEnemy cleanly
        player.SetLockOnTarget(sparringEnemy.transform);
        yield return new WaitForSeconds(0.10f);
        Assert.IsTrue(player.IsLockedOn);

        Vector3 startPos = player.transform.position;
        player.SetTestInputs(new Vector2(1f, 0f), sprint: false, guard: false);
        yield return null;
        player.TriggerDodge();
        yield return null;

        Assert.IsTrue(player.IsDodging, "TriggerDodge should start Step-Dodge.");
        Assert.IsTrue(player.IsInvulnerable, "Step-Dodge startup should grant i-frames.");

        while (player.IsDodging)
        {
            yield return null;
        }

        // Release move input and confirm hard foot-plant stop without generic sliding tail
        player.SetTestInputs(Vector2.zero, sprint: false, guard: false);
        yield return null;
        float speedRightAfterDodge = player.CurrentPlanarSpeed;
        float displacement = Vector2.Distance(
            new Vector2(startPos.x, startPos.z),
            new Vector2(player.transform.position.x, player.transform.position.z));

        Vector3 toLocked = (sparringEnemy.transform.position - player.transform.position);
        toLocked.y = 0f;
        float facingDot = Vector3.Dot(player.transform.forward, toLocked.normalized);

        Debug.Log($"[Telemetry][StepDodgeLock] disp={displacement:F2}m endSpeed={speedRightAfterDodge:F3} facingDot={facingDot:F2}");
        Assert.Greater(displacement, 1.25f, "Locked-on Step-Dodge should still cover crisp evasive space.");
        Assert.Less(speedRightAfterDodge, 0.05f, $"Step-Dodge should brake cleanly to zero without lingering slide (endSpeed={speedRightAfterDodge:F2}).");
        Assert.Greater(facingDot, 0.70f, $"Locked-on sidestep should keep facing LockedTarget rather than spinning away (dot={facingDot:F2}).");
        player.ClearTestInputOverride();
    }

    [UnityTest]
    public IEnumerator LockOn_TogglesStrafesBreaksForSprintCyclesTargetsAndUnlocks()
    {
        yield return LoadShowcaseScene();

        // 1. Toggle Lock-On
        player.ToggleLockOn();
        yield return new WaitForSeconds(0.25f);
        Assert.IsTrue(player.IsLockedOn, "ToggleLockOn should lock onto nearest/aligned enemy.");
        Assert.IsTrue(thirdPersonCamera.IsLockedOn, "ThirdPersonCamera should mirror player Lock-On state.");
        Transform firstLocked = player.LockedTarget;

        // 2. Strafe sideways while locked -> uses LockOnLocomotion & keeps facing target
        player.SetTestInputs(new Vector2(1f, 0f), sprint: false, guard: false);
        yield return new WaitForSeconds(0.30f);
        Assert.IsTrue(player.IsActivelyLockOnStrafing, "Walking sideways while locked should use lock-on strafing.");
        Assert.IsTrue(
            playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("LockOnLocomotion"),
            "Animator should switch to LockOnLocomotion 2D BlendTree while strafing locked target.");

        Vector3 toFirst = firstLocked.position - player.transform.position;
        toFirst.y = 0f;
        float strafeFacingDot = Vector3.Dot(player.transform.forward, toFirst.normalized);
        Assert.Greater(strafeFacingDot, 0.80f, $"Lock-on strafe should face locked target (dot={strafeFacingDot:F2}).");

        // 3. Holding Sprint breaks cleanly into forward Run_F locomotion
        player.SetTestInputs(new Vector2(0f, -1f), sprint: true, guard: false);
        yield return new WaitForSeconds(0.25f);
        Assert.IsFalse(player.IsActivelyLockOnStrafing, "Sprinting should break free from slow lock-on strafe.");
        Assert.IsTrue(playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "Sprint while locked should switch back to forward Locomotion (Run_F).");

        // 4. Cycle target switches cleanly to another enemy
        player.SetTestInputs(Vector2.zero, sprint: false, guard: false);
        yield return null;
        player.CycleLockOnTarget(1);
        yield return new WaitForSeconds(0.10f);
        Assert.IsTrue(player.IsLockedOn);
        Assert.AreNotEqual(firstLocked, player.LockedTarget, "CycleLockOnTarget should switch to next available arena enemy.");

        // 5. Toggle Lock-On off releases camera & player
        player.ToggleLockOn();
        yield return new WaitForSeconds(0.10f);
        Assert.IsFalse(player.IsLockedOn, "Second ToggleLockOn call should disengage lock-on.");
        Assert.IsFalse(thirdPersonCamera.IsLockedOn, "ThirdPersonCamera should release lock-on when disengaged.");
        player.ClearTestInputOverride();
    }
}
