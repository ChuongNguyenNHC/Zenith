using UnityEngine;

public class ShowcaseHUD : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private bool showHUD = true;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle comboStyle;
    private GUIStyle bannerStyle;
    private GUIStyle reticleStyle;
    private GUIStyle miniBarStyle;
    private GUIStyle reticleHintStyle;
    private Texture2D bgTex;
    private Texture2D whiteTex;
    private SparringEnemy[] sparringEnemies;

    private void Awake()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }
        sparringEnemies = FindObjectsByType<SparringEnemy>(FindObjectsSortMode.None);
    }

    private void OnDestroy()
    {
        if (bgTex != null) Destroy(bgTex);
        if (whiteTex != null) Destroy(whiteTex);
    }

    private void InitStyles()
    {
        if (panelStyle != null) return;

        bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0.06f, 0.07f, 0.09f, 0.78f));
        bgTex.Apply();

        whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = bgTex },
            padding = new RectOffset(14, 14, 12, 12)
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.98f, 0.78f, 0.25f) }
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            richText = true,
            normal = { textColor = new Color(0.92f, 0.94f, 0.96f) }
        };

        comboStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            richText = true,
            normal = { textColor = Color.white }
        };

        bannerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            normal = { textColor = new Color(1.0f, 0.85f, 0.28f) }
        };

        reticleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            normal = { textColor = Color.white }
        };

        miniBarStyle = new GUIStyle(bodyStyle)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };

        reticleHintStyle = new GUIStyle(bodyStyle)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter
        };
    }

    private void OnGUI()
    {
        if (!showHUD) return;
        InitStyles();

        // Top-Left: Controls Guide
        GUILayout.BeginArea(new Rect(18, 18, 355, 255), panelStyle);
        GUILayout.Label("ZENITH — SIFU COMBAT SHOWCASE", titleStyle);
        GUILayout.Space(4);
        GUILayout.Label(
            "<b>WASD</b>         Move / Lock-On Strafe\n" +
            "<b>Shift</b>        Sprint (Run Animation)\n" +
            "<b>MMB / Tab</b>    Lock-On Toggle  (<b>1/2 / Scroll</b> Switch)\n" +
            "<b>Hold Q + A/D/S</b> Sifu Avoid High/Mid (In-Place Slip/Duck)\n" +
            "<b>Hold Q + W</b>   Sifu Hop Avoid (Clears Low Sweep)\n" +
            "<b>Tap Q / Ctrl</b> Deflect Parry  |  <b>Hold Q</b> Guard\n" +
            "<b>Space / C</b>    Directional Step-Dodge\n" +
            "<b>LMB / RMB</b>    Light 5-Hit / Heavy 3-Hit Combo\n" +
            "<b>Mouse / Esc</b>  Orbit Camera / Unlock Cursor",
            bodyStyle);
        GUILayout.EndArea();

        if (player == null) return;

        // Top-Right: Live Combat State & Target Info
        float rightWidth = 335f;
        GUILayout.BeginArea(new Rect(Screen.width - rightWidth - 18, 18, rightWidth, 165), panelStyle);

        string stateText;
        if (player.IsStructureBroken)
        {
            stateText = "<color=#FF4D4D>GUARD BROKEN!</color>";
        }
        else if (player.IsAvoiding)
        {
            stateText = $"<color=#38BDF8>SIFU AVOID ({player.CurrentAvoidDirection.ToString().ToUpper()})</color>";
        }
        else if (player.IsDodging)
        {
            stateText = "<color=#56D4FF>STEP-DODGE</color>";
        }
        else if (player.IsParryWindowActive)
        {
            stateText = "<color=#FFD740>DEFLECT WINDOW</color>";
        }
        else if (player.IsGuarding)
        {
            stateText = "<color=#60A5FA>GUARD STANCE</color>";
        }
        else if (player.ComboStep > 0)
        {
            string kind = player.IsHeavyAttack ? "HEAVY" : "LIGHT";
            string color = player.IsHeavyAttack ? "#FF7043" : "#FFC840";
            stateText = $"<color={color}>{kind} {player.ComboStep}/{player.MaxComboSteps}</color> — {player.CurrentMoveName.ToUpper()}";
        }
        else if (player.IsSprinting)
        {
            stateText = "<color=#A3E635>RUNNING</color>";
        }
        else if (player.IsActivelyLockOnStrafing && player.CurrentPlanarSpeed > 0.25f)
        {
            stateText = "<color=#93C5FD>LOCK-ON FOOTWORK</color>";
        }
        else
        {
            stateText = "<color=#9AA5B1>IDLE</color>";
        }

        GUILayout.Label(stateText, comboStyle);
        GUILayout.Space(4);

        string lockStatus = player.IsLockedOn
            ? $"<color=#FFD740>LOCKED:</color> <color=#5CE67E>{player.LockedTarget.name}</color>"
            : $"Soft-Lock: {(player.TargetEnemy != null ? $"<color=#5CE67E>{player.TargetEnemy.name}</color>" : "<color=#88929E>None</color>")}";
        string counterTag = player.HasAvoidCounterWindow ? "<color=#38BDF8><b>AVOID COUNTER READY!</b></color>" : (player.HasBufferedAttack ? "<color=#FFC840>QUEUED</color>" : "-");
        GUILayout.Label($"{lockStatus}\nPunish / Buffer: {counterTag}\nAvoids: {player.AvoidsLanded}  |  Parries: {player.ParriesLanded}", bodyStyle);
        GUILayout.EndArea();

        // Bottom-Center: Player Structure Meter (and Enemy Structure if SparringEnemy is locked/active)
        DrawStructureMeters();

        // World-Projected Lock-On Reticle & Unlocked Enemy Attack Telegraphs
        DrawLockOnReticle();
        DrawUnlockedAttackWarnings();

        // Screen-edge crimson damage pulse when hit
        DrawPlayerHitVignette();

        // Center-Top Action Feedback Banner (AVOID! / PARRY! / EVADE! / BLOCKED / GUARD BROKEN!)
        if (!string.IsNullOrEmpty(player.LastDefenseEvent))
        {
            GUI.Label(new Rect(Screen.width * 0.5f - 180f, 80f, 360f, 45f), player.LastDefenseEvent, bannerStyle);
        }
    }

    private void DrawStructureMeters()
    {
        float barWidth = 260f;
        float barHeight = 10f;
        float x = Screen.width * 0.5f - barWidth * 0.5f;
        float y = Screen.height - 46f;

        Color playerBarColor = Color.Lerp(new Color(0.98f, 0.78f, 0.22f), new Color(0.95f, 0.22f, 0.22f), player.StructureNormalized);
        DrawBar(new Rect(x, y, barWidth, barHeight), player.StructureNormalized, playerBarColor, "PLAYER STRUCTURE");

        SparringEnemy activeEnemy = null;
        if (player.LockedTarget != null) activeEnemy = player.LockedTarget.GetComponent<SparringEnemy>();
        if (activeEnemy == null && player.TargetEnemy != null) activeEnemy = player.TargetEnemy.GetComponent<SparringEnemy>();

        if (activeEnemy != null)
        {
            Color enemyColor = Color.Lerp(new Color(0.95f, 0.55f, 0.2f), new Color(1.0f, 0.2f, 0.45f), activeEnemy.StructureNormalized);
            DrawBar(new Rect(x, 42f, barWidth, barHeight), activeEnemy.StructureNormalized, enemyColor, $"{activeEnemy.name.ToUpper()} STRUCTURE");
        }
    }

    private void DrawBar(Rect rect, float normalized, Color fillColor, string label)
    {
        GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
        GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), whiteTex);

        GUI.color = new Color(0.22f, 0.24f, 0.28f, 0.9f);
        GUI.DrawTexture(rect, whiteTex);

        float fillW = Mathf.Clamp01(normalized) * rect.width;
        if (fillW > 0.5f)
        {
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, fillW, rect.height), whiteTex);
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x, rect.y - 16f, rect.width, 16f), label, miniBarStyle);
    }

    private void DrawLockOnReticle()
    {
        if (!player.IsLockedOn) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 anchorWorld = player.LockedTarget.position + Vector3.up * 1.15f;
        Vector3 screenPos = cam.WorldToScreenPoint(anchorWorld);
        if (screenPos.z <= 0.1f) return;

        float guiY = Screen.height - screenPos.y;
        string glyph = "<color=#FFD740>◆</color>";
        string hint = string.Empty;

        SparringEnemy enemy = player.LockedTarget.GetComponent<SparringEnemy>();
        if (enemy != null)
        {
            if (enemy.CurrentState == SparringEnemy.AIState.Windup)
            {
                if (enemy.IsLowSweep)
                {
                    glyph = "<color=#FF3377>◈</color>";
                    hint = "<color=#FF4D88><b>LOW SWEEP [Q+W]</b></color>";
                }
                else
                {
                    glyph = "<color=#FF8C22>◈</color>";
                    hint = "<color=#FFA640><b>HIGH [Q+A/D/S]</b></color>";
                }
            }
            else if (enemy.CurrentState == SparringEnemy.AIState.Parried)
            {
                glyph = "<color=#38BDF8>◈</color>";
                hint = "<color=#38BDF8><b>OPEN!</b></color>";
            }
        }

        GUI.Label(new Rect(screenPos.x - 24f, guiY - 24f, 48f, 48f), glyph, reticleStyle);
        if (!string.IsNullOrEmpty(hint))
        {
            GUI.Label(new Rect(screenPos.x - 75f, guiY - 42f, 150f, 18f), hint, reticleHintStyle);
        }
    }

    private void DrawUnlockedAttackWarnings()
    {
        if (sparringEnemies == null || sparringEnemies.Length == 0) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        for (int i = 0; i < sparringEnemies.Length; i++)
        {
            SparringEnemy enemy = sparringEnemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            if (enemy.CurrentState != SparringEnemy.AIState.Windup) continue;
            if (player.IsLockedOn && player.LockedTarget == enemy.transform) continue;

            Vector3 screenPos = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * 2.25f);
            string hint = enemy.IsLowSweep
                ? "<color=#FF3366><b>! LOW SWEEP [Q+W]</b></color>"
                : "<color=#FF4D2E><b>! INCOMING [Q / SPACE]</b></color>";

            if (screenPos.z > 0.1f)
            {
                float gx = Mathf.Clamp(screenPos.x, 90f, Screen.width - 90f);
                float gy = Mathf.Clamp(Screen.height - screenPos.y, 28f, Screen.height - 60f);
                GUI.Label(new Rect(gx - 90f, gy - 20f, 180f, 22f), hint, reticleHintStyle);
            }
            else
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 100f, 52f, 200f, 22f), hint, reticleHintStyle);
            }
        }
    }

    private void DrawPlayerHitVignette()
    {
        if (player == null || whiteTex == null) return;
        float flash = player.HitFlashNormalized;
        if (flash <= 0.01f) return;

        float alpha = Mathf.Clamp01(flash * 0.36f);
        float border = 16f;
        GUI.color = new Color(0.95f, 0.08f, 0.08f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, border), whiteTex);
        GUI.DrawTexture(new Rect(0f, Screen.height - border, Screen.width, border), whiteTex);
        GUI.DrawTexture(new Rect(0f, border, border, Screen.height - border * 2f), whiteTex);
        GUI.DrawTexture(new Rect(Screen.width - border, border, border, Screen.height - border * 2f), whiteTex);
        GUI.color = Color.white;
    }
}
