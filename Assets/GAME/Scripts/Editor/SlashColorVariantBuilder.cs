using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CombatEditor
{
    /// <summary>
    /// A colour variant's mapping, as pure colour maths.
    /// </summary>
    /// <remarks>
    /// Only hue and saturation move. A colour's brightness is its strongest
    /// channel - the number URP's bloom threshold is compared with - and it is
    /// put back exactly, HDR intensity included, so a variant glows where the
    /// source glows and nowhere else, and every dark streak stays as dark.
    ///
    /// Hue and saturation are read in display (gamma) space, where a colour
    /// picker shows them, from the colour normalised to its strongest channel;
    /// the result is returned to the colour's own storage space and scaled
    /// back to its original strength.
    ///
    /// Two-tone maps the arc of the colour wheel between the source's two
    /// colours onto the arc between the new two, so a colour a third of the
    /// way from violet to jade lands a third of the way between the new
    /// colours. Colours outside that arc keep their offset from the nearer end.
    /// Near-greys and whites have no hue to speak of and keep theirs.
    /// </remarks>
    public readonly struct SlashRecolor
    {
        /// <summary>Below this saturation a colour's hue is noise, not a colour.</summary>
        private const float MinSaturation = 0.02f;

        /// <summary>A source colour this washed out still counts as this saturated, so ratios stay sane.</summary>
        private const float SaturationFloor = 0.05f;

        private const float MaxSaturationRatio = 4f;

        private readonly SlashRecolorMode _mode;
        private readonly float _sourcePrimaryHue;
        private readonly float _sourceSecondaryHue;
        private readonly float _targetPrimaryHue;
        private readonly float _targetSecondaryHue;
        private readonly float _primaryRatio;
        private readonly float _secondaryRatio;
        private readonly float _shift;
        private readonly float _saturation;

        private SlashRecolor(
            SlashRecolorMode mode,
            float sourcePrimaryHue, float sourceSecondaryHue,
            float targetPrimaryHue, float targetSecondaryHue,
            float primaryRatio, float secondaryRatio,
            float shift, float saturation)
        {
            _mode = mode;
            _sourcePrimaryHue = sourcePrimaryHue;
            _sourceSecondaryHue = sourceSecondaryHue;
            _targetPrimaryHue = targetPrimaryHue;
            _targetSecondaryHue = targetSecondaryHue;
            _primaryRatio = primaryRatio;
            _secondaryRatio = secondaryRatio;
            _shift = shift;
            _saturation = saturation;
        }

        /// <summary>
        /// The mapping a recipe describes, given the source palette in display
        /// space (see <see cref="SlashColorVariantBuilder.TryDetectPalette"/>).
        /// </summary>
        public static SlashRecolor For(SlashColorVariant variant, Color sourcePrimary, Color sourceSecondary)
        {
            float saturation = Mathf.Max(0f, variant.Saturation);

            if (variant.Mode == SlashRecolorMode.HueShift)
                return new SlashRecolor(SlashRecolorMode.HueShift, 0f, 0f, 0f, 0f, 1f, 1f, variant.HueShift, saturation);

            Color.RGBToHSV(sourcePrimary, out float sph, out float sps, out _);
            Color.RGBToHSV(sourceSecondary, out float ssh, out float sss, out _);
            Color.RGBToHSV(variant.Primary, out float tph, out float tps, out _);
            Color.RGBToHSV(variant.Secondary, out float tsh, out float tss, out _);

            return new SlashRecolor(
                SlashRecolorMode.TwoTone,
                sph * 360f, ssh * 360f,
                tph * 360f, tsh * 360f,
                Mathf.Min(tps / Mathf.Max(sps, SaturationFloor), MaxSaturationRatio),
                Mathf.Min(tss / Mathf.Max(sss, SaturationFloor), MaxSaturationRatio),
                0f, saturation);
        }

        /// <summary>
        /// A colour as it is stored: linear for an HDR material colour in a
        /// linear project (the value the shader receives), display space for
        /// everything else a colour picker writes.
        /// </summary>
        public Color MapStored(Color colour, bool linear) =>
            linear ? MapLinear(colour) : MapGamma(colour);

        /// <summary>A linear colour, HDR allowed.</summary>
        public Color MapLinear(Color colour)
        {
            float strength = maxChannel(colour);
            if (strength <= 1e-6f)
                return colour;

            var display = new Color(
                Mathf.LinearToGammaSpace(colour.r / strength),
                Mathf.LinearToGammaSpace(colour.g / strength),
                Mathf.LinearToGammaSpace(colour.b / strength),
                1f);

            Color mapped = mapNormalised(display);
            var linear = new Color(
                Mathf.GammaToLinearSpace(mapped.r),
                Mathf.GammaToLinearSpace(mapped.g),
                Mathf.GammaToLinearSpace(mapped.b),
                1f);

            return rescale(linear, strength, colour.a);
        }

        /// <summary>A display-space colour, as particle colours and plain material colours are.</summary>
        public Color MapGamma(Color colour)
        {
            float strength = maxChannel(colour);
            if (strength <= 1e-6f)
                return colour;

            var display = new Color(colour.r / strength, colour.g / strength, colour.b / strength, 1f);
            return rescale(mapNormalised(display), strength, colour.a);
        }

        public Gradient Map(Gradient gradient)
        {
            if (gradient == null)
                return null;

            bool linear = gradient.colorSpace == ColorSpace.Linear;
            GradientColorKey[] keys = gradient.colorKeys;

            for (int i = 0; i < keys.Length; i++)
                keys[i].color = linear ? MapLinear(keys[i].color) : MapGamma(keys[i].color);

            var mapped = new Gradient { mode = gradient.mode, colorSpace = gradient.colorSpace };
            mapped.SetKeys(keys, gradient.alphaKeys);
            return mapped;
        }

        public ParticleSystem.MinMaxGradient Map(ParticleSystem.MinMaxGradient gradient)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(MapGamma(gradient.color));

                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(MapGamma(gradient.colorMin), MapGamma(gradient.colorMax));

                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Map(gradient.gradient));

                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Map(gradient.gradientMin), Map(gradient.gradientMax));

                case ParticleSystemGradientMode.RandomColor:
                    var random = new ParticleSystem.MinMaxGradient(Map(gradient.gradient));
                    random.mode = ParticleSystemGradientMode.RandomColor;
                    return random;

                default:
                    return gradient;
            }
        }

        /// <summary>A colour already normalised to its strongest channel, in display space.</summary>
        private Color mapNormalised(Color display)
        {
            Color.RGBToHSV(display, out float h, out float s, out float v);

            if (s < MinSaturation)
                return display;

            mapHueSaturation(h * 360f, s, out float hue, out float saturation);
            return Color.HSVToRGB(hue / 360f, saturation, v, false);
        }

        private void mapHueSaturation(float hue, float saturation, out float mappedHue, out float mappedSaturation)
        {
            if (_mode == SlashRecolorMode.HueShift)
            {
                mappedHue = Mathf.Repeat(hue + _shift, 360f);
                mappedSaturation = Mathf.Clamp01(saturation * _saturation);
                return;
            }

            float sourceArc = delta(_sourcePrimaryHue, _sourceSecondaryHue);
            float targetArc = delta(_targetPrimaryHue, _targetSecondaryHue);
            float fromPrimary = delta(_sourcePrimaryHue, hue);
            float ratio;

            bool onArc = Mathf.Abs(sourceArc) > 1e-3f &&
                Mathf.Sign(fromPrimary) == Mathf.Sign(sourceArc) &&
                Mathf.Abs(fromPrimary) <= Mathf.Abs(sourceArc);

            if (onArc)
            {
                float t = fromPrimary / sourceArc;
                mappedHue = Mathf.Repeat(_targetPrimaryHue + t * targetArc, 360f);
                ratio = Mathf.Lerp(_primaryRatio, _secondaryRatio, t);
            }
            else
            {
                // Off the arc, a colour keeps its offset from the nearer end -
                // mirrored when the new arc runs the other way round, so a
                // colour beyond the primary stays beyond it rather than
                // swinging round towards the secondary.
                bool flipped = Mathf.Abs(sourceArc) > 1e-3f && Mathf.Abs(targetArc) > 1e-3f &&
                    Mathf.Sign(sourceArc) != Mathf.Sign(targetArc);
                float orientation = flipped ? -1f : 1f;
                float fromSecondary = delta(_sourceSecondaryHue, hue);

                if (Mathf.Abs(fromPrimary) <= Mathf.Abs(fromSecondary))
                {
                    mappedHue = Mathf.Repeat(_targetPrimaryHue + fromPrimary * orientation, 360f);
                    ratio = _primaryRatio;
                }
                else
                {
                    mappedHue = Mathf.Repeat(_targetSecondaryHue + fromSecondary * orientation, 360f);
                    ratio = _secondaryRatio;
                }
            }

            mappedSaturation = Mathf.Clamp01(saturation * ratio * _saturation);
        }

        /// <summary>Signed shortest turn from one hue to another, in degrees, [-180, 180).</summary>
        private static float delta(float from, float to) => Mathf.Repeat(to - from + 180f, 360f) - 180f;

        private static float maxChannel(Color colour) => Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));

        private static Color rescale(Color colour, float strength, float alpha)
        {
            float peak = maxChannel(colour);
            if (peak <= 1e-6f)
                return new Color(0f, 0f, 0f, alpha);

            float scale = strength / peak;
            return new Color(colour.r * scale, colour.g * scale, colour.b * scale, alpha);
        }
    }

    /// <summary>
    /// Writes a colour variant of a weapon pack's effects: copies of every slash
    /// profile, effect prefab and material the pack uses, identical to the
    /// source in everything but colour, and a copy of the pack pointing at them.
    /// </summary>
    /// <remarks>
    /// Every generation starts again from the source. Prefabs are the source
    /// prefab's contents recoloured and saved; materials are the source
    /// material's properties copied and recoloured; profiles and the pack are
    /// serialized copies with their references swapped. Nothing is patched
    /// onto an older copy, so a variant can never drift from the effect it
    /// was made from - regenerate it and it matches the source as it stands.
    /// Meshes are not copied: every variant draws on the source's own
    /// crescent and emitter meshes, so geometry is shared, not duplicated.
    ///
    /// Assets are written over their own paths, so their GUIDs - and whatever
    /// references them, a weapon holding the variant pack - survive every
    /// regeneration.
    /// </remarks>
    public static class SlashColorVariantBuilder
    {
        public const string VariantsRoot = "Assets/GAME/Data/Combat/Visuals/Variants";

        private const string RecipePrefix = "ColorVariant_";
        private const string LayeredShaderName = "Game/Slash Layered";
        private const string MidColorProperty = "_MidColor";
        private const string CuesProperty = "_cues";

        [MenuItem("Tools/VFX/Crescent Slash/Refresh Colour Variants", priority = 21)]
        public static void RefreshAllMenu()
        {
            int count = RefreshAll();
            Debug.Log($"Colour variants: {count} refreshed.");
        }

        /// <summary>
        /// Regenerates every colour variant in the project from its source as
        /// it stands now. Returns how many were regenerated.
        /// </summary>
        public static int RefreshAll()
        {
            int count = 0;

            foreach (SlashColorVariant variant in FindAll())
            {
                if (Generate(variant, out string message))
                    count++;
                else
                    Debug.LogWarning($"Colour variant '{variant.name}' was not refreshed: {message}", variant);
            }

            return count;
        }

        public static List<SlashColorVariant> FindAll()
        {
            var found = new List<SlashColorVariant>();

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(SlashColorVariant)}"))
            {
                var variant = AssetDatabase.LoadAssetAtPath<SlashColorVariant>(AssetDatabase.GUIDToAssetPath(guid));
                if (variant != null)
                    found.Add(variant);
            }

            found.Sort((a, b) => string.Compare(a.VariantName, b.VariantName, StringComparison.OrdinalIgnoreCase));
            return found;
        }

        public static string FolderFor(string variantName) => $"{VariantsRoot}/{Sanitize(variantName)}";

        public static string RecipePathFor(string variantName) =>
            $"{FolderFor(variantName)}/{RecipePrefix}{Sanitize(variantName)}.asset";

        /// <summary>
        /// The recipe asset for a variant name, created in its folder if there
        /// is none yet, with <paramref name="apply"/> run on it and saved.
        /// </summary>
        public static SlashColorVariant SaveRecipe(string variantName, Action<SlashColorVariant> apply)
        {
            string name = Sanitize(variantName);
            string path = RecipePathFor(name);

            ensureFolder(FolderFor(name));

            var recipe = AssetDatabase.LoadAssetAtPath<SlashColorVariant>(path);

            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<SlashColorVariant>();
                apply?.Invoke(recipe);
                recipe.VariantName = name;
                AssetDatabase.CreateAsset(recipe, path);
            }
            else
            {
                apply?.Invoke(recipe);
                recipe.VariantName = name;
                EditorUtility.SetDirty(recipe);
            }

            return recipe;
        }

        /// <summary>
        /// A name safe for folders and asset files: letters, digits, spaces,
        /// hyphens and underscores, trimmed.
        /// </summary>
        public static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var clean = new StringBuilder(name.Length);

            foreach (char c in name.Trim())
                clean.Append(char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' ? c : '_');

            return clean.ToString().Trim();
        }

        /// <summary>
        /// The pack that uses the crescent slash, for a new variant to start
        /// from: the first pack with a slash cue whose profile draws a prefab
        /// built on the layered slash shader.
        /// </summary>
        public static WeaponVisualPack FindDefaultPack()
        {
            WeaponVisualPack first = null;

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(WeaponVisualPack)}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // A variant's own pack is never a source.
                if (path.StartsWith(VariantsRoot + "/", StringComparison.Ordinal))
                    continue;

                var pack = AssetDatabase.LoadAssetAtPath<WeaponVisualPack>(path);
                if (pack == null)
                    continue;

                first ??= pack;

                if (TryDetectPalette(pack, out _, out _))
                    return pack;
            }

            return first;
        }

        /// <summary>
        /// The source's two colours in display space, full strength: the main
        /// colour is the last layered sheet's mid colour (the body, drawn on
        /// top), the second is the sheet before it (the under-stroke).
        /// </summary>
        public static bool TryDetectPalette(WeaponVisualPack pack, out Color primary, out Color secondary)
        {
            primary = Color.white;
            secondary = Color.white;

            if (pack == null)
                return false;

            CollectSources(pack, out List<SlashProfile> profiles, out _);

            foreach (SlashProfile profile in profiles)
            {
                if (profile == null || profile.Particles == null)
                    continue;

                var layered = new List<Material>();

                foreach (Renderer drawn in profile.Particles.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in drawn.sharedMaterials)
                    {
                        if (material != null && material.shader != null &&
                            material.shader.name == LayeredShaderName &&
                            material.HasProperty(MidColorProperty) &&
                            !layered.Contains(material))
                        {
                            layered.Add(material);
                        }
                    }
                }

                if (layered.Count == 0)
                    continue;

                primary = DisplayOf(layered[layered.Count - 1].GetColor(MidColorProperty));
                secondary = DisplayOf(layered[Mathf.Max(0, layered.Count - 2)].GetColor(MidColorProperty));
                return true;
            }

            return false;
        }

        /// <summary>A linear HDR colour as a display-space colour at full strength, for swatches and hue.</summary>
        public static Color DisplayOf(Color linear)
        {
            float strength = Mathf.Max(linear.r, Mathf.Max(linear.g, linear.b));
            if (strength <= 1e-6f)
                return Color.black;

            return new Color(
                Mathf.LinearToGammaSpace(linear.r / strength),
                Mathf.LinearToGammaSpace(linear.g / strength),
                Mathf.LinearToGammaSpace(linear.b / strength),
                1f);
        }

        /// <summary>
        /// Every slash profile the pack's cues use, and every prefab they use -
        /// the cues' own prefabs (impacts, accents) and the profiles' particle
        /// prefabs.
        /// </summary>
        public static void CollectSources(WeaponVisualPack pack, out List<SlashProfile> profiles, out List<GameObject> prefabs)
        {
            profiles = new List<SlashProfile>();
            prefabs = new List<GameObject>();

            if (pack == null)
                return;

            var serialized = new SerializedObject(pack);
            SerializedProperty cues = serialized.FindProperty(CuesProperty);

            if (cues == null)
                return;

            for (int i = 0; i < cues.arraySize; i++)
            {
                SerializedProperty cue = cues.GetArrayElementAtIndex(i);

                if (cue.FindPropertyRelative("SlashProfile")?.objectReferenceValue is SlashProfile profile &&
                    !profiles.Contains(profile))
                {
                    profiles.Add(profile);
                }

                if (cue.FindPropertyRelative("Prefab")?.objectReferenceValue is GameObject prefab &&
                    !prefabs.Contains(prefab))
                {
                    prefabs.Add(prefab);
                }
            }

            foreach (SlashProfile profile in profiles)
            {
                if (profile.Particles != null && !prefabs.Contains(profile.Particles))
                    prefabs.Add(profile.Particles);
            }
        }

        /// <summary>
        /// Every material the pack's effects draw with, in the order the
        /// preview lists them.
        /// </summary>
        public static List<Material> CollectMaterials(WeaponVisualPack pack)
        {
            var materials = new List<Material>();
            CollectSources(pack, out List<SlashProfile> profiles, out List<GameObject> prefabs);

            foreach (GameObject prefab in prefabs)
            {
                foreach (Renderer drawn in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in drawn.sharedMaterials)
                    {
                        if (material != null && !materials.Contains(material))
                            materials.Add(material);
                    }

                    if (drawn is ParticleSystemRenderer particles &&
                        particles.trailMaterial != null &&
                        !materials.Contains(particles.trailMaterial))
                    {
                        materials.Add(particles.trailMaterial);
                    }
                }
            }

            foreach (SlashProfile profile in profiles)
            {
                if (profile.Material != null && !materials.Contains(profile.Material))
                    materials.Add(profile.Material);
                if (profile.CircleMaterial != null && !materials.Contains(profile.CircleMaterial))
                    materials.Add(profile.CircleMaterial);
            }

            return materials;
        }

        /// <summary>
        /// Whether the material's colour property is stored linear - an HDR
        /// colour in a linear project, which Unity hands the shader as it is -
        /// or in display space, as a plain colour is.
        /// </summary>
        public static bool IsLinearColour(Shader shader, int propertyIndex) =>
            (shader.GetPropertyFlags(propertyIndex) & ShaderPropertyFlags.HDR) != 0 &&
            QualitySettings.activeColorSpace == ColorSpace.Linear;

        /// <summary>The mapping a recipe describes against its source as it stands.</summary>
        public static bool TryGetRecolor(SlashColorVariant variant, out SlashRecolor recolor, out string problem)
        {
            recolor = default;
            problem = null;

            if (variant.Mode == SlashRecolorMode.HueShift)
            {
                recolor = SlashRecolor.For(variant, Color.white, Color.white);
                return true;
            }

            if (!TryDetectPalette(variant.SourcePack, out Color primary, out Color secondary))
            {
                problem = "the source pack has no slash drawn with the layered slash shader to read its colours from; use Hue Shift instead.";
                return false;
            }

            recolor = SlashRecolor.For(variant, primary, secondary);
            return true;
        }

        /// <summary>
        /// Writes (or rewrites) every asset of one variant from its source.
        /// </summary>
        public static bool Generate(SlashColorVariant variant, out string message)
        {
            message = null;

            if (variant == null)
            {
                message = "no variant.";
                return false;
            }

            string name = Sanitize(variant.VariantName);

            if (string.IsNullOrEmpty(name))
            {
                message = "the variant has no name.";
                return false;
            }

            if (variant.SourcePack == null)
            {
                message = "the variant has no source pack.";
                return false;
            }

            string packPath = AssetDatabase.GetAssetPath(variant.SourcePack);
            if (packPath.StartsWith(VariantsRoot + "/", StringComparison.Ordinal))
            {
                message = "the source pack is itself a variant's pack; recolour the original instead.";
                return false;
            }

            if (!TryGetRecolor(variant, out SlashRecolor recolor, out string problem))
            {
                message = problem;
                return false;
            }

            string folder = FolderFor(name);
            ensureFolder(folder);

            CollectSources(variant.SourcePack, out List<SlashProfile> profiles, out List<GameObject> prefabs);

            if (profiles.Count == 0 && prefabs.Count == 0)
            {
                message = "the source pack uses no profile and no prefab, so there is nothing to recolour.";
                return false;
            }

            // No StartAssetEditing batch around this: a prefab saved inside one
            // would reference materials that are not imported assets yet.
            // Order matters for the same reason - materials are written as the
            // prefabs need them, the prefabs before the profiles that draw
            // them, and the pack last, once everything it points at exists.
            var session = new Session(recolor, folder, name);

            foreach (GameObject prefab in prefabs)
                session.Prefab(prefab);

            foreach (SlashProfile profile in profiles)
                session.Profile(profile, profiles.Count == 1);

            WeaponVisualPack pack = session.Pack(variant.SourcePack);

            variant.VariantName = name;
            variant.OutputPack = pack;
            variant.Outputs = session.Outputs;
            EditorUtility.SetDirty(variant);
            AssetDatabase.SaveAssets();

            message = $"'{name}': {session.MaterialCount} materials, {session.PrefabCount} prefabs, " +
                $"{session.ProfileCount} profiles and the pack '{AssetDatabase.GetAssetPath(pack)}'.";
            return true;
        }

        private static void ensureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);

            ensureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        /// <summary>
        /// One generation's bookkeeping: which source asset became which copy,
        /// so a material shared by several prefabs is copied once and every
        /// reference to it lands on the same copy.
        /// </summary>
        private sealed class Session
        {
            private readonly SlashRecolor _recolor;
            private readonly string _folder;
            private readonly string _name;

            private readonly Dictionary<Material, Material> _materials = new();
            private readonly Dictionary<GameObject, GameObject> _prefabs = new();
            private readonly Dictionary<SlashProfile, SlashProfile> _profiles = new();

            public readonly List<Object> Outputs = new();

            public int MaterialCount => _materials.Count;
            public int PrefabCount => _prefabs.Count;
            public int ProfileCount => _profiles.Count;

            public Session(SlashRecolor recolor, string folder, string name)
            {
                _recolor = recolor;
                _folder = folder;
                _name = name;
            }

            public Material Material(Material source)
            {
                if (source == null)
                    return null;

                if (_materials.TryGetValue(source, out Material done))
                    return done;

                string path = $"{_folder}/{source.name}_{_name}.mat";
                var copy = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (copy == null)
                {
                    copy = new Material(source) { name = $"{source.name}_{_name}" };
                    AssetDatabase.CreateAsset(copy, path);
                }
                else
                {
                    copy.shader = source.shader;
                    copy.CopyPropertiesFromMaterial(source);
                }

                // Stated rather than trusted to the copy: the draw order
                // between the layers is part of the look, and so is anything
                // else the property copy may not carry.
                copy.renderQueue = source.renderQueue;
                copy.shaderKeywords = source.shaderKeywords;
                copy.enableInstancing = source.enableInstancing;
                copy.doubleSidedGI = source.doubleSidedGI;
                copy.globalIlluminationFlags = source.globalIlluminationFlags;

                Shader shader = source.shader;

                for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    if (shader.GetPropertyType(i) != ShaderPropertyType.Color)
                        continue;

                    string property = shader.GetPropertyName(i);
                    copy.SetColor(property, _recolor.MapStored(source.GetColor(property), IsLinearColour(shader, i)));
                }

                EditorUtility.SetDirty(copy);
                _materials[source] = copy;
                Outputs.Add(copy);
                return copy;
            }

            public GameObject Prefab(GameObject source)
            {
                if (source == null)
                    return null;

                if (_prefabs.TryGetValue(source, out GameObject done))
                    return done;

                string sourcePath = AssetDatabase.GetAssetPath(source);
                string path = $"{_folder}/{source.name}_{_name}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);

                try
                {
                    recolour(root);

                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    if (!success || saved == null)
                        throw new InvalidOperationException($"could not save '{path}'.");

                    _prefabs[source] = saved;
                    Outputs.Add(saved);
                    return saved;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            /// <summary>
            /// Every colour on the prefab's renderers and particle systems.
            /// Modules are recoloured whether or not they are on, so turning
            /// one on later in the source and regenerating gives the same
            /// variant as if it had always been on.
            /// </summary>
            private void recolour(GameObject root)
            {
                foreach (Renderer drawn in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = drawn.sharedMaterials;

                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = Material(materials[i]);

                    drawn.sharedMaterials = materials;

                    if (drawn is ParticleSystemRenderer particles && particles.trailMaterial != null)
                        particles.trailMaterial = Material(particles.trailMaterial);
                }

                foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = system.main;
                    main.startColor = _recolor.Map(main.startColor);

                    ParticleSystem.ColorOverLifetimeModule overLife = system.colorOverLifetime;
                    overLife.color = _recolor.Map(overLife.color);

                    ParticleSystem.ColorBySpeedModule bySpeed = system.colorBySpeed;
                    bySpeed.color = _recolor.Map(bySpeed.color);

                    ParticleSystem.TrailModule trails = system.trails;
                    trails.colorOverLifetime = _recolor.Map(trails.colorOverLifetime);
                    trails.colorOverTrail = _recolor.Map(trails.colorOverTrail);

                    ParticleSystem.CustomDataModule custom = system.customData;
                    recolour(custom, ParticleSystemCustomData.Custom1);
                    recolour(custom, ParticleSystemCustomData.Custom2);
                }
            }

            private void recolour(ParticleSystem.CustomDataModule custom, ParticleSystemCustomData stream)
            {
                if (custom.GetMode(stream) == ParticleSystemCustomDataMode.Color)
                    custom.SetColor(stream, _recolor.Map(custom.GetColor(stream)));
            }

            public SlashProfile Profile(SlashProfile source, bool only)
            {
                if (source == null)
                    return null;

                if (_profiles.TryGetValue(source, out SlashProfile done))
                    return done;

                // A pack with one slash gets a clean name; several keep their
                // own names so they can be told apart.
                string file = only ? $"Slash_{_name}" : $"{source.name}_{_name}";
                string path = $"{_folder}/{file}.asset";

                var copy = AssetDatabase.LoadAssetAtPath<SlashProfile>(path);
                bool created = copy == null;

                if (created)
                    copy = ScriptableObject.CreateInstance<SlashProfile>();

                EditorUtility.CopySerialized(source, copy);
                copy.name = file;

                copy.Particles = source.Particles != null ? Prefab(source.Particles) : null;
                copy.Material = Material(source.Material);
                copy.CircleMaterial = Material(source.CircleMaterial);

                // The ribbon's colours, and the edge colour that also tints
                // the ring. HDR, used as the shader receives them.
                copy.CoreColor = _recolor.MapLinear(source.CoreColor);
                copy.EdgeColor = _recolor.MapLinear(source.EdgeColor);
                copy.GlowColor = _recolor.MapLinear(source.GlowColor);

                if (created)
                    AssetDatabase.CreateAsset(copy, path);
                else
                    EditorUtility.SetDirty(copy);

                _profiles[source] = copy;
                Outputs.Add(copy);
                return copy;
            }

            public WeaponVisualPack Pack(WeaponVisualPack source)
            {
                string file = $"{source.name}_{_name}";
                string path = $"{_folder}/{file}.asset";

                var copy = AssetDatabase.LoadAssetAtPath<WeaponVisualPack>(path);
                bool created = copy == null;

                if (created)
                    copy = ScriptableObject.CreateInstance<WeaponVisualPack>();

                EditorUtility.CopySerialized(source, copy);
                copy.name = file;

                if (created)
                    AssetDatabase.CreateAsset(copy, path);

                // Every cue kept exactly - key, kind, anchors, timings, scale -
                // with only the effect it names swapped for its recoloured copy.
                var serialized = new SerializedObject(copy);
                SerializedProperty cues = serialized.FindProperty(CuesProperty);

                for (int i = 0; cues != null && i < cues.arraySize; i++)
                {
                    SerializedProperty cue = cues.GetArrayElementAtIndex(i);
                    SerializedProperty profile = cue.FindPropertyRelative("SlashProfile");
                    SerializedProperty prefab = cue.FindPropertyRelative("Prefab");

                    if (profile != null && profile.objectReferenceValue is SlashProfile p &&
                        _profiles.TryGetValue(p, out SlashProfile recoloured))
                    {
                        profile.objectReferenceValue = recoloured;
                    }

                    if (prefab != null && prefab.objectReferenceValue is GameObject g &&
                        _prefabs.TryGetValue(g, out GameObject recolouredPrefab))
                    {
                        prefab.objectReferenceValue = recolouredPrefab;
                    }
                }

                // Applying runs the pack's OnValidate, which rebuilds its cue
                // lookup from the swapped entries - without that the copy
                // would keep answering with the source's effects until the
                // next domain reload.
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(copy);

                Outputs.Add(copy);
                return copy;
            }
        }
    }
}
