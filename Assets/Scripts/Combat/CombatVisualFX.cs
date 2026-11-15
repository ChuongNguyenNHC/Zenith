using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Drives URP-compatible emissive pulsing red / status flashes across a character's materials.
/// </summary>
public sealed class CombatFlashController
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private struct MaterialSlot
    {
        public Material material;
        public int colorPropId;
        public Color originalColor;
        public bool supportsEmission;
        public bool hadEmissionKeyword;
        public Color originalEmissionColor;
    }

    private MaterialSlot[] slots;
    private Color activeColor;
    private float activeDuration;
    private float remainingTimer;
    private float elapsed;
    private float activePulseHz;
    private float activeBaseBlend;
    private float activeEmissionIntensity;

    public bool IsFlashing => remainingTimer > 0f;
    public float NormalizedRemaining => activeDuration > 0.0001f ? Mathf.Clamp01(remainingTimer / activeDuration) : 0f;

    public void Initialize(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
        {
            slots = null;
            return;
        }

        List<MaterialSlot> collected = new List<MaterialSlot>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            Material[] mats = r.materials;
            for (int j = 0; j < mats.Length; j++)
            {
                Material mat = mats[j];
                if (mat == null) continue;

                int propId = mat.HasProperty(BaseColorId) ? BaseColorId
                    : (mat.HasProperty(ColorId) ? ColorId : 0);
                if (propId == 0) continue;

                bool hasEmission = mat.HasProperty(EmissionColorId);
                collected.Add(new MaterialSlot
                {
                    material = mat,
                    colorPropId = propId,
                    originalColor = mat.GetColor(propId),
                    supportsEmission = hasEmission,
                    hadEmissionKeyword = hasEmission && mat.IsKeywordEnabled("_EMISSION"),
                    originalEmissionColor = hasEmission ? mat.GetColor(EmissionColorId) : Color.black
                });
            }
        }

        slots = collected.ToArray();
    }

    public void TriggerFlash(
        Color color,
        float duration,
        float pulseHz = 0f,
        float baseBlend = 0.62f,
        float emissionIntensity = 2.4f)
    {
        if (slots == null || slots.Length == 0 || duration <= 0f) return;

        activeColor = color;
        activeDuration = duration;
        remainingTimer = duration;
        elapsed = 0f;
        activePulseHz = pulseHz;
        activeBaseBlend = baseBlend;
        activeEmissionIntensity = emissionIntensity;

        ApplyCurrentStrength();
    }

    public void Update(float deltaTime)
    {
        if (remainingTimer <= 0f || slots == null) return;

        elapsed += deltaTime;
        remainingTimer -= deltaTime;
        if (remainingTimer <= 0f)
        {
            Clear();
            return;
        }

        ApplyCurrentStrength();
    }

    public void Clear()
    {
        remainingTimer = 0f;
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            MaterialSlot s = slots[i];
            if (s.material == null) continue;

            s.material.SetColor(s.colorPropId, s.originalColor);
            if (s.supportsEmission)
            {
                s.material.SetColor(EmissionColorId, s.originalEmissionColor);
                if (!s.hadEmissionKeyword)
                {
                    s.material.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    private void ApplyCurrentStrength()
    {
        // Cosine starts at 1.0 on frame 0 so the very first hit/windup frame flashes vividly.
        float wave = activePulseHz > 0.01f
            ? 0.18f + 0.82f * (0.5f + 0.5f * Mathf.Cos(elapsed * activePulseHz * Mathf.PI * 2f))
            : 1.0f;

        float fadeDuration = Mathf.Min(0.10f, activeDuration * 0.30f);
        float tailFade = fadeDuration > 0.001f ? Mathf.Clamp01(remainingTimer / fadeDuration) : 1f;
        float strength = wave * tailFade;

        for (int i = 0; i < slots.Length; i++)
        {
            MaterialSlot s = slots[i];
            if (s.material == null) continue;

            Color blendedBase = Color.Lerp(s.originalColor, activeColor, activeBaseBlend * strength);
            s.material.SetColor(s.colorPropId, blendedBase);

            if (s.supportsEmission)
            {
                s.material.EnableKeyword("_EMISSION");
                Color targetEmission = Color.Lerp(s.originalEmissionColor, activeColor * activeEmissionIntensity, strength);
                s.material.SetColor(EmissionColorId, targetEmission);
            }
        }
    }
}

/// <summary>
/// World-space 3D gameplay attack telegraph rendered on an enemy during Windup.
/// Uses crisp procedural unlit URP meshes (no Gizmos, no colliders) visible in all camera modes.
/// </summary>
public sealed class AttackWarningSignal
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly GameObject root;
    private readonly Transform overheadRoot;
    private readonly Transform glintStar;
    private readonly Transform strikeRing;
    private readonly Transform strikeCore;

    private readonly Material outerMaterial;
    private readonly Material coreMaterial;
    private readonly Mesh starMesh;
    private readonly Mesh barMesh;
    private readonly Mesh dotMesh;
    private readonly Mesh ringMesh;

    public AttackWarningSignal(Transform owner)
    {
        root = new GameObject("AttackWarningSignal_VFX");
        root.transform.SetParent(owner, false);
        root.SetActive(false);

        outerMaterial = CreateUnlitMaterial(new Color(1.0f, 0.16f, 0.08f, 1f));
        coreMaterial = CreateUnlitMaterial(new Color(1.0f, 0.94f, 0.72f, 1f));

        starMesh = BuildFourPointStarMesh(0.28f, 0.055f);
        barMesh = BuildQuadMesh(0.052f, 0.18f, new Vector3(0f, 0.05f, 0f));
        dotMesh = BuildQuadMesh(0.056f, 0.056f, new Vector3(0f, -0.095f, 0f));
        ringMesh = BuildAnnulusMesh(0.32f, 0.42f, 28);

        overheadRoot = new GameObject("OverheadWarning").transform;
        overheadRoot.SetParent(root.transform, false);
        overheadRoot.localPosition = new Vector3(0f, 2.22f, 0f);

        GameObject starObj = CreateMeshChild("StarFlare", overheadRoot, starMesh, outerMaterial, Vector3.zero);
        glintStar = starObj.transform;
        CreateMeshChild("ExclamationBar", overheadRoot, barMesh, coreMaterial, new Vector3(0f, 0.02f, -0.01f));
        CreateMeshChild("ExclamationDot", overheadRoot, dotMesh, coreMaterial, new Vector3(0f, 0.02f, -0.01f));

        GameObject ringObj = CreateMeshChild("StrikeRing", root.transform, ringMesh, outerMaterial, new Vector3(0f, 1.38f, 0.48f));
        strikeRing = ringObj.transform;

        GameObject coreObj = CreateMeshChild("StrikeCore", strikeRing, starMesh, coreMaterial, new Vector3(0f, 0f, -0.01f));
        coreObj.transform.localScale = Vector3.one * 0.65f;
        strikeCore = coreObj.transform;
    }

    public void UpdateVisual(bool active, float normalizedProgress, bool isLowSweep, Camera cam)
    {
        if (!active)
        {
            if (root.activeSelf) root.SetActive(false);
            return;
        }

        if (!root.activeSelf) root.SetActive(true);

        float p = Mathf.Clamp01(normalizedProgress);
        float pulse = 0.5f + 0.5f * Mathf.Sin(p * Mathf.PI * 5f);

        Color outerColor = isLowSweep
            ? Color.Lerp(new Color(1.0f, 0.10f, 0.36f), new Color(1.0f, 0.38f, 0.60f), pulse)
            : Color.Lerp(new Color(1.0f, 0.14f, 0.06f), new Color(1.0f, 0.50f, 0.12f), pulse);
        Color innerColor = Color.Lerp(new Color(1.0f, 0.85f, 0.45f), Color.white, pulse);

        SetMaterialColor(outerMaterial, outerColor * 1.45f);
        SetMaterialColor(coreMaterial, innerColor * 1.35f);

        Quaternion billboard = cam != null
            ? cam.transform.rotation
            : Quaternion.LookRotation(root.transform.parent != null ? -root.transform.parent.forward : Vector3.back, Vector3.up);

        // 1. Overhead danger "!" + pulsing 4-point star
        overheadRoot.rotation = billboard;
        float popScale = Mathf.Lerp(1.30f, 0.95f, p) + 0.22f * pulse;
        overheadRoot.localScale = Vector3.one * popScale;
        glintStar.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(p * Mathf.PI * 2f) * 14f);

        // 2. Converging parry-timing strike ring at strike height (chest vs ankle for Low Sweep)
        Vector3 strikeAnchor = isLowSweep
            ? new Vector3(0f, 0.30f, 0.56f)
            : new Vector3(0f, 1.36f, 0.48f);
        strikeRing.localPosition = strikeAnchor;
        strikeRing.rotation = billboard;
        float ringScale = Mathf.Lerp(1.65f, 0.48f, p);
        strikeRing.localScale = Vector3.one * ringScale;
        strikeCore.localRotation = Quaternion.Euler(0f, 0f, p * 135f);
    }

    public void Dispose()
    {
        if (root != null) Object.Destroy(root);
        if (outerMaterial != null) Object.Destroy(outerMaterial);
        if (coreMaterial != null) Object.Destroy(coreMaterial);
        if (starMesh != null) Object.Destroy(starMesh);
        if (barMesh != null) Object.Destroy(barMesh);
        if (dotMesh != null) Object.Destroy(dotMesh);
        if (ringMesh != null) Object.Destroy(ringMesh);
    }

    private static GameObject CreateMeshChild(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return obj;
    }

    private static Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material mat = new Material(shader);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        SetMaterialColor(mat, color);
        return mat;
    }

    private static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
        else if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
    }

    private static Mesh BuildFourPointStarMesh(float tipRadius, float innerRadius)
    {
        Mesh m = new Mesh { name = "WarningStarMesh" };
        float diag = innerRadius * 0.7071f;
        m.vertices = new[]
        {
            Vector3.zero,
            new Vector3(0f, tipRadius, 0f),
            new Vector3(diag, diag, 0f),
            new Vector3(tipRadius, 0f, 0f),
            new Vector3(diag, -diag, 0f),
            new Vector3(0f, -tipRadius, 0f),
            new Vector3(-diag, -diag, 0f),
            new Vector3(-tipRadius, 0f, 0f),
            new Vector3(-diag, diag, 0f)
        };
        m.triangles = new[]
        {
            0, 1, 2,  0, 2, 3,
            0, 3, 4,  0, 4, 5,
            0, 5, 6,  0, 6, 7,
            0, 7, 8,  0, 8, 1
        };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static Mesh BuildQuadMesh(float width, float height, Vector3 offset)
    {
        Mesh m = new Mesh { name = "WarningQuadMesh" };
        float hw = width * 0.5f;
        float hh = height * 0.5f;
        m.vertices = new[]
        {
            new Vector3(-hw, -hh, 0f) + offset,
            new Vector3( hw, -hh, 0f) + offset,
            new Vector3( hw,  hh, 0f) + offset,
            new Vector3(-hw,  hh, 0f) + offset
        };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static Mesh BuildAnnulusMesh(float innerR, float outerR, int segments)
    {
        Mesh m = new Mesh { name = "WarningRingMesh" };
        Vector3[] verts = new Vector3[segments * 2];
        int[] tris = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            float cos = Mathf.Cos(a);
            float sin = Mathf.Sin(a);
            verts[i * 2] = new Vector3(cos * innerR, sin * innerR, 0f);
            verts[i * 2 + 1] = new Vector3(cos * outerR, sin * outerR, 0f);

            int next = (i + 1) % segments;
            int ti = i * 6;
            tris[ti + 0] = i * 2;
            tris[ti + 1] = next * 2 + 1;
            tris[ti + 2] = i * 2 + 1;
            tris[ti + 3] = i * 2;
            tris[ti + 4] = next * 2;
            tris[ti + 5] = next * 2 + 1;
        }
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
