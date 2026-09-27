using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Writes a <see cref="StageProfile"/> into a scene's lighting, its stage
/// objects and the stage's own material and volume assets.
///
/// Nothing here runs at play time and nothing here is created at play time.
/// Applying a profile is an authoring step: it edits real assets and real
/// scene objects, they are saved, and what ships is an ordinary lit scene
/// with no component keeping it alive. A stage that had to be assembled on
/// load would be a stage that cannot be inspected before it runs, cannot be
/// diffed, and quietly costs something in every build that includes it.
///
/// So the profile is the place the numbers are authored and this is the thing
/// that pushes them out. Once pushed, the profile can be deleted from the
/// scene entirely and the lighting stands on its own.
///
/// The objects and assets it writes into are references rather than lookups,
/// so a stage can be rearranged by hand afterwards - a light moved, a
/// different ground material dropped in - without this finding its way back
/// over the change on the next apply.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class StageRig : MonoBehaviour
{
    private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
    private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
    private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
    private static readonly int HorizonHeightId = Shader.PropertyToID("_HorizonHeight");
    private static readonly int HorizonSoftnessId = Shader.PropertyToID("_HorizonSoftness");
    private static readonly int TopFalloffId = Shader.PropertyToID("_TopFalloff");
    private static readonly int GroundFalloffId = Shader.PropertyToID("_GroundFalloff");
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

    /// <summary>
    /// Unity's built-in plane is ten metres across, so a plane scaled by one
    /// is ten wide. Every ground size in the profile is divided by this.
    /// </summary>
    private const float PrimitivePlaneSize = 10f;

    [Tooltip("The stage this rig writes out. Swap it and apply to relight " +
        "the whole scene.")]
    [SerializeField] private StageProfile _profile;

    [Header("Owned By The Stage")]
    [Tooltip("Sky material asset, on the Game/Stage Sky shader. The rig writes " +
        "the profile's gradient into it and hands it to the scene's lighting.")]
    [SerializeField] private Material _skyMaterial;

    [Tooltip("Ground material asset. Written from the profile, shared by " +
        "anything else that wants the same floor.")]
    [SerializeField] private Material _groundMaterial;

    [Tooltip("Volume profile asset holding the stage's bloom, vignette and " +
        "grading. Written from the profile.")]
    [SerializeField] private VolumeProfile _postProfile;

    [Header("Scene Objects")]
    [SerializeField] private Light _key;
    [SerializeField] private Light _fill;
    [SerializeField] private Light _rim;
    [SerializeField] private Transform _ground;
    [SerializeField] private Volume _volume;

    [Header("Authoring")]
    [Tooltip("Re-apply whenever the profile asset is edited, so the stage can " +
        "be lit by dragging sliders. Editor only - this never runs in a build.")]
    [SerializeField] private bool _liveUpdate = true;

    public StageProfile Profile
    {
        get => _profile;
        set => _profile = value;
    }

#if UNITY_EDITOR

    private int _appliedRevision = -1;

    private void OnValidate()
    {
        if (_liveUpdate && !Application.isPlaying)
            EditorApplication.delayCall += applyIfStillHere;
    }

    /// <summary>
    /// Re-lights the stage when the profile asset is edited.
    /// </summary>
    /// <remarks>
    /// The profile is an asset, so editing it never notifies the scene and
    /// there is nothing to subscribe to. Polling the editor's own
    /// modification counter is the cheap way round that, and the counter
    /// matters rather than the poll: applying unconditionally would rewrite
    /// the scene's lighting settings every editor frame, which leaves the
    /// scene permanently dirty and asks to save a file nothing changed.
    /// </remarks>
    private void Update()
    {
        if (!_liveUpdate || Application.isPlaying || _profile == null)
            return;

        int revision = EditorUtility.GetDirtyCount(_profile);

        if (revision == _appliedRevision)
            return;

        _appliedRevision = revision;
        Apply();
    }

    /// <summary>
    /// OnValidate is called during serialization, where touching other assets
    /// is not allowed. Deferring a frame puts the work somewhere it is.
    /// </summary>
    private void applyIfStillHere()
    {
        if (this == null)
            return;

        Apply();
    }

    /// <summary>Writes the profile into everything the stage owns.</summary>
    [ContextMenu("Apply Profile")]
    public void Apply()
    {
        if (_profile == null || Application.isPlaying)
            return;

        applySky();
        applyFog();
        applyAmbient();
        applyLights();
        applyGround();
        applyPost();

        // Lighting settings live in the scene rather than on this object, so
        // the scene is what has to be saved for any of this to survive.
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    #region Sky

    private void applySky()
    {
        if (_skyMaterial == null)
        {
            warnMissing("sky material", "the backdrop keeps whatever it had");
            return;
        }

        _skyMaterial.SetColor(TopColorId, _profile.SkyZenith);
        _skyMaterial.SetColor(HorizonColorId, _profile.SkyHorizon);
        _skyMaterial.SetColor(GroundColorId, _profile.SkyNadir);
        _skyMaterial.SetFloat(HorizonHeightId, _profile.HorizonHeight);
        _skyMaterial.SetFloat(HorizonSoftnessId, _profile.HorizonSoftness);
        _skyMaterial.SetFloat(TopFalloffId, _profile.ZenithFalloff);
        _skyMaterial.SetFloat(GroundFalloffId, _profile.NadirFalloff);
        _skyMaterial.SetFloat(ExposureId, _profile.SkyExposure);

        EditorUtility.SetDirty(_skyMaterial);

        if (RenderSettings.skybox != _skyMaterial)
            RenderSettings.skybox = _skyMaterial;
    }

    #endregion

    #region Environment

    private void applyFog()
    {
        RenderSettings.fog = _profile.FogEnabled;

        if (!_profile.FogEnabled)
            return;

        // Exponential squared rather than linear: the ground has to thin out
        // gradually over its whole length, and linear fog puts a visible band
        // where it starts.
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = _profile.ResolvedFogColor;
        RenderSettings.fogDensity = _profile.FogDensity;
    }

    private void applyAmbient()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = _profile.AmbientSky * _profile.AmbientIntensity;
        RenderSettings.ambientEquatorColor = _profile.AmbientEquator * _profile.AmbientIntensity;
        RenderSettings.ambientGroundColor = _profile.AmbientGround * _profile.AmbientIntensity;

        // The sky is a flat gradient with nothing in it to reflect, so a
        // reflection bounce only ever lifts the blacks.
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = null;
        RenderSettings.reflectionIntensity = 0f;
    }

    private void applyGround()
    {
        if (_ground != null)
            _ground.gameObject.SetActive(_profile.GroundEnabled);

        if (!_profile.GroundEnabled || _ground == null)
            return;

        Undo.RecordObject(_ground, "Apply Stage Profile");

        _ground.localPosition = new Vector3(0f, _profile.GroundHeight, 0f);

        float scale = Mathf.Max(_profile.GroundSize, 1f) / PrimitivePlaneSize;
        _ground.localScale = new Vector3(scale, 1f, scale);

        EditorUtility.SetDirty(_ground);

        if (_groundMaterial == null)
        {
            warnMissing("ground material", "the floor keeps whatever it had");
            return;
        }

        _groundMaterial.SetColor(BaseColorId, _profile.GroundColor);
        _groundMaterial.SetFloat(SmoothnessId, _profile.GroundSmoothness);
        _groundMaterial.SetFloat(MetallicId, _profile.GroundMetallic);

        EditorUtility.SetDirty(_groundMaterial);

        if (_ground.TryGetComponent(out MeshRenderer renderer) &&
            renderer.sharedMaterial != _groundMaterial)
        {
            renderer.sharedMaterial = _groundMaterial;
            EditorUtility.SetDirty(renderer);
        }
    }

    #endregion

    #region Lights

    private void applyLights()
    {
        configureLight(
            _key,
            _profile.KeyColor,
            _profile.KeyIntensity,
            _profile.KeyPitch,
            _profile.KeyYaw,
            _profile.KeyCastsShadows,
            _profile.KeyShadowStrength);

        configureLight(
            _fill,
            _profile.FillColor,
            _profile.FillIntensity,
            _profile.FillPitch,
            _profile.FillYaw,
            false,
            0f);

        configureLight(
            _rim,
            _profile.RimColor,
            _profile.RimIntensity,
            _profile.RimPitch,
            _profile.RimYaw,
            false,
            0f);
    }

    private static void configureLight(
        Light light,
        Color colour,
        float intensity,
        float pitch,
        float yaw,
        bool shadows,
        float shadowStrength)
    {
        if (light == null)
            return;

        Undo.RecordObject(light, "Apply Stage Profile");
        Undo.RecordObject(light.transform, "Apply Stage Profile");

        light.type = LightType.Directional;
        light.color = colour;
        light.intensity = intensity;
        light.transform.localRotation = StageProfile.Orientation(pitch, yaw);

        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = shadowStrength;

        // Only the key is allowed to be the sun; two lights claiming it fight
        // over the pipeline's main-light slot and the winner changes with
        // load order, which looks like the stage relighting itself at random.
        light.renderMode = shadows
            ? LightRenderMode.ForcePixel
            : LightRenderMode.Auto;

        // Everything on this stage is direct light. Saying so keeps the
        // lighting window from reporting a Mixed light with no bake behind it,
        // which is a warning about a problem this stage does not have.
        light.lightmapBakeType = LightmapBakeType.Realtime;

        EditorUtility.SetDirty(light);
        EditorUtility.SetDirty(light.transform);
    }

    #endregion

    #region Post

    private void applyPost()
    {
        if (_volume != null)
        {
            Undo.RecordObject(_volume, "Apply Stage Profile");

            _volume.enabled = _profile.PostProcessing;
            _volume.isGlobal = true;

            // Above the level's own grading, so a stage dropped into a dressed
            // scene wins without anybody having to find the other volume.
            _volume.priority = 100f;

            if (_volume.sharedProfile != _postProfile && _postProfile != null)
                _volume.sharedProfile = _postProfile;

            EditorUtility.SetDirty(_volume);
        }

        if (!_profile.PostProcessing || _postProfile == null)
            return;

        applyBloom();
        applyVignette();
        applyGrading();

        EditorUtility.SetDirty(_postProfile);
    }

    private void applyBloom()
    {
        if (!_postProfile.TryGet(out Bloom bloom))
            return;

        bloom.active = _profile.BloomIntensity > 0f;
        setFloat(bloom.intensity, _profile.BloomIntensity);
        setFloat(bloom.threshold, _profile.BloomThreshold);
        setFloat(bloom.scatter, _profile.BloomScatter);
    }

    private void applyVignette()
    {
        if (!_postProfile.TryGet(out Vignette vignette))
            return;

        vignette.active = _profile.VignetteIntensity > 0f;
        setFloat(vignette.intensity, _profile.VignetteIntensity);
        setFloat(vignette.smoothness, _profile.VignetteSmoothness);
    }

    private void applyGrading()
    {
        if (_postProfile.TryGet(out Tonemapping tonemapping))
        {
            tonemapping.active = true;
            tonemapping.mode.overrideState = true;

            // Neutral rather than ACES: the effects are already authored in
            // the colours they are meant to be, and ACES would pull the
            // saturated ones towards white exactly where they are brightest.
            tonemapping.mode.value = TonemappingMode.Neutral;
        }

        if (!_postProfile.TryGet(out ColorAdjustments grading))
            return;

        grading.active = true;
        setFloat(grading.postExposure, _profile.PostExposure);
        setFloat(grading.contrast, _profile.Contrast);
        setFloat(grading.saturation, _profile.Saturation);
    }

    private static void setFloat(VolumeParameter<float> parameter, float value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    #endregion

    private void warnMissing(string what, string consequence)
    {
        Debug.LogWarning(
            $"Stage rig on '{name}' has no {what} assigned, so {consequence}.",
            this);
    }

#endif
}
