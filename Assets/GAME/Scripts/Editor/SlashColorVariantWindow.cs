using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CombatEditor
{
    /// <summary>
    /// Makes colour variants of the crescent slash.
    ///
    /// Pick the weapon pack to recolour, give the variant a name and choose
    /// its two colours - or a preset, or a plain hue shift - and it writes a
    /// copy of the pack whose slash, impact and every material are the
    /// source's with only the colours changed. Brightness, glow, timing,
    /// shapes, particle counts, cues: all of it is the source's. Give the
    /// copied pack to a weapon and the weapon slashes in the new colours.
    ///
    /// Variants are remembered, so they can be reopened, retuned and
    /// regenerated, and the crescent builder regenerates all of them whenever
    /// it rebuilds the slash, so they never fall behind it.
    /// </summary>
    public sealed class SlashColorVariantWindow : EditorWindow
    {
        private struct Preset
        {
            public string Name;
            public Color Primary;
            public Color Secondary;
        }

        /// <summary>
        /// Starting points, in display space. Only hue and saturation are
        /// read from them; brightness always comes from the source.
        /// </summary>
        private static readonly Preset[] Presets =
        {
            new() { Name = "Inferno", Primary = new Color(1f, 0.36f, 0.08f), Secondary = new Color(1f, 0.78f, 0.15f) },
            new() { Name = "Frost", Primary = new Color(0.25f, 0.6f, 1f), Secondary = new Color(0.62f, 0.95f, 1f) },
            new() { Name = "Venom", Primary = new Color(0.35f, 0.95f, 0.2f), Secondary = new Color(0.8f, 1f, 0.25f) },
            new() { Name = "Blood", Primary = new Color(0.9f, 0.06f, 0.18f), Secondary = new Color(1f, 0.45f, 0.12f) },
            new() { Name = "Holy", Primary = new Color(1f, 0.82f, 0.35f), Secondary = new Color(1f, 0.97f, 0.75f) },
            new() { Name = "Void", Primary = new Color(0.55f, 0.12f, 1f), Secondary = new Color(0.95f, 0.1f, 0.4f) },
        };

        [SerializeField] private WeaponVisualPack _pack;
        [SerializeField] private string _name = "Inferno";
        [SerializeField] private SlashRecolorMode _mode = SlashRecolorMode.TwoTone;
        [SerializeField] private Color _primary = Presets[0].Primary;
        [SerializeField] private Color _secondary = Presets[0].Secondary;
        [SerializeField] private float _hueShift;
        [SerializeField] private float _saturation = 1f;
        [SerializeField] private bool _showPreview = true;

        private SlashColorVariant _scratch;
        private Vector2 _scroll;
        private string _status;
        private MessageType _statusType;

        [MenuItem("Tools/VFX/Crescent Slash/Colour Variants...", priority = 20)]
        public static void Open()
        {
            var window = GetWindow<SlashColorVariantWindow>("Slash Colour Variants");
            window.minSize = new Vector2(440f, 520f);
            window.Show();
        }

        /// <summary>Opens the window on an existing variant, ready to retune.</summary>
        public static void Open(SlashColorVariant variant)
        {
            Open();
            GetWindow<SlashColorVariantWindow>().load(variant);
        }

        private void OnEnable()
        {
            _scratch = CreateInstance<SlashColorVariant>();
            _scratch.hideFlags = HideFlags.HideAndDontSave;

            if (_pack == null)
                _pack = SlashColorVariantBuilder.FindDefaultPack();
        }

        private void OnDisable()
        {
            if (_scratch != null)
                DestroyImmediate(_scratch);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            drawSource();
            EditorGUILayout.Space(8f);
            drawColours();
            EditorGUILayout.Space(8f);
            drawActions();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _statusType);

            EditorGUILayout.Space(8f);
            drawPreview();
            EditorGUILayout.Space(8f);
            drawExisting();

            EditorGUILayout.EndScrollView();
        }

        private void drawSource()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            _pack = (WeaponVisualPack)EditorGUILayout.ObjectField(
                new GUIContent("Weapon Pack", "The pack whose slash and impact are recoloured."),
                _pack, typeof(WeaponVisualPack), false);

            if (_pack == null)
            {
                EditorGUILayout.HelpBox("Pick the weapon pack that uses the slash (e.g. Sword_VisualPack).", MessageType.Info);
                return;
            }

            if (SlashColorVariantBuilder.TryDetectPalette(_pack, out Color primary, out Color secondary))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Source Colours");
                    swatch(primary, false);
                    swatch(secondary, false);

                    if (GUILayout.Button(new GUIContent("Use", "Start from the source's own colours."), GUILayout.Width(48f)))
                    {
                        _mode = SlashRecolorMode.TwoTone;
                        _primary = primary;
                        _secondary = secondary;
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This pack's slash does not use the layered slash shader, so its two colours cannot be " +
                    "read. Hue Shift still works.", MessageType.Warning);
            }
        }

        private void drawColours()
        {
            EditorGUILayout.LabelField("Variant", EditorStyles.boldLabel);
            _name = EditorGUILayout.TextField(new GUIContent("Name", "Folder and asset names use it."), _name);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Preset");

                foreach (Preset preset in Presets)
                {
                    if (GUILayout.Button(preset.Name, EditorStyles.miniButton))
                    {
                        _mode = SlashRecolorMode.TwoTone;
                        _primary = preset.Primary;
                        _secondary = preset.Secondary;

                        if (string.IsNullOrWhiteSpace(_name) || isPresetName(_name))
                            _name = preset.Name;
                    }
                }
            }

            _mode = (SlashRecolorMode)EditorGUILayout.EnumPopup(new GUIContent("Mode",
                "Two Tone: the source's main and second colours each become one of yours. " +
                "Hue Shift: every colour turns round the wheel by the same angle."), _mode);

            if (_mode == SlashRecolorMode.TwoTone)
            {
                _primary = EditorGUILayout.ColorField(new GUIContent("Primary",
                    "What the source's main colour (the violet body, rim tint, sparks, impact) becomes."),
                    _primary, true, false, false);
                _secondary = EditorGUILayout.ColorField(new GUIContent("Secondary",
                    "What the source's second colour (the jade under-stroke, wisps, impact chips) becomes."),
                    _secondary, true, false, false);
            }
            else
            {
                _hueShift = EditorGUILayout.Slider(new GUIContent("Hue Shift", "Degrees round the colour wheel."),
                    _hueShift, -180f, 180f);
            }

            _saturation = EditorGUILayout.Slider(new GUIContent("Saturation",
                "Multiplies every colour's saturation after the mapping."), _saturation, 0f, 2f);

            EditorGUILayout.HelpBox(
                "Only hue and saturation change. Each colour keeps the source's brightness - so what " +
                "glows and what stays under the bloom is unchanged - and timing, shapes, particles, " +
                "profiles and cues are copied as they are.", MessageType.None);
        }

        private void drawActions()
        {
            string name = SlashColorVariantBuilder.Sanitize(_name);
            bool exists = !string.IsNullOrEmpty(name) &&
                AssetDatabase.LoadAssetAtPath<SlashColorVariant>(SlashColorVariantBuilder.RecipePathFor(name)) != null;

            using (new EditorGUI.DisabledScope(_pack == null || string.IsNullOrEmpty(name)))
            {
                if (GUILayout.Button(exists ? $"Update Variant '{name}'" : $"Create Variant '{name}'", GUILayout.Height(28f)))
                    createOrUpdate(name);
            }

            SlashColorVariant current = string.IsNullOrEmpty(name)
                ? null
                : AssetDatabase.LoadAssetAtPath<SlashColorVariant>(SlashColorVariantBuilder.RecipePathFor(name));

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(current == null || current.OutputPack == null))
                {
                    if (GUILayout.Button(new GUIContent("Use On Selected Weapon",
                        "Gives the selected weapon(s) this variant's pack - every pack reference on the selection " +
                        "and its children is swapped.")))
                    {
                        assignToSelection(current.OutputPack);
                    }

                    if (GUILayout.Button("Select Pack"))
                    {
                        Selection.activeObject = current.OutputPack;
                        EditorGUIUtility.PingObject(current.OutputPack);
                    }
                }

                if (GUILayout.Button(new GUIContent("Refresh All", "Regenerates every variant from its source as it stands now.")))
                {
                    int count = SlashColorVariantBuilder.RefreshAll();
                    setStatus($"{count} variant(s) regenerated.", MessageType.Info);
                }
            }
        }

        private void drawPreview()
        {
            _showPreview = EditorGUILayout.Foldout(_showPreview, "Preview (before → after)", true);

            if (!_showPreview || _pack == null)
                return;

            applyTo(_scratch);
            _scratch.SourcePack = _pack;

            if (!SlashColorVariantBuilder.TryGetRecolor(_scratch, out SlashRecolor recolor, out string problem))
            {
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
                return;
            }

            foreach (Material material in SlashColorVariantBuilder.CollectMaterials(_pack))
            {
                Shader shader = material.shader;
                var colours = new List<(Color before, Color after, bool hdr)>();

                for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    if (shader.GetPropertyType(i) != ShaderPropertyType.Color)
                        continue;

                    // The swatches show only the colours the shader is set up to
                    // treat as colour; a hidden or runtime-only one would just be
                    // noise here.
                    if ((shader.GetPropertyFlags(i) & ShaderPropertyFlags.HideInInspector) != 0)
                        continue;

                    Color before = material.GetColor(shader.GetPropertyName(i));
                    bool linear = SlashColorVariantBuilder.IsLinearColour(shader, i);
                    bool hdr = (shader.GetPropertyFlags(i) & ShaderPropertyFlags.HDR) != 0;
                    colours.Add((before, recolor.MapStored(before, linear), hdr));
                }

                if (colours.Count == 0)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(material.name, GUILayout.Width(150f));

                    using (new EditorGUILayout.VerticalScope())
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            foreach ((Color before, Color _, bool hdr) in colours)
                                swatch(before, hdr);
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            foreach ((Color _, Color after, bool hdr) in colours)
                                swatch(after, hdr);
                        }
                    }
                }

                EditorGUILayout.Space(2f);
            }
        }

        private void drawExisting()
        {
            List<SlashColorVariant> variants = SlashColorVariantBuilder.FindAll();

            EditorGUILayout.LabelField($"Existing Variants ({variants.Count})", EditorStyles.boldLabel);

            if (variants.Count == 0)
            {
                EditorGUILayout.LabelField("None yet.", EditorStyles.miniLabel);
                return;
            }

            foreach (SlashColorVariant variant in variants)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(variant.VariantName, GUILayout.Width(150f));

                    if (variant.Mode == SlashRecolorMode.TwoTone)
                    {
                        swatch(variant.Primary, false);
                        swatch(variant.Secondary, false);
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"{variant.HueShift:+0;-0}°", GUILayout.Width(84f));
                    }

                    if (GUILayout.Button("Load", EditorStyles.miniButtonLeft))
                        load(variant);

                    if (GUILayout.Button("Regenerate", EditorStyles.miniButtonMid))
                        report(SlashColorVariantBuilder.Generate(variant, out string message), message);

                    if (GUILayout.Button("Select", EditorStyles.miniButtonRight))
                    {
                        Selection.activeObject = variant;
                        EditorGUIUtility.PingObject(variant);
                    }
                }
            }
        }

        private void createOrUpdate(string name)
        {
            SlashColorVariant recipe = SlashColorVariantBuilder.SaveRecipe(name, v =>
            {
                applyTo(v);
                v.SourcePack = _pack;
            });

            bool ok = SlashColorVariantBuilder.Generate(recipe, out string message);
            report(ok, message);

            if (ok && recipe.OutputPack != null)
                EditorGUIUtility.PingObject(recipe.OutputPack);
        }

        private void applyTo(SlashColorVariant variant)
        {
            variant.VariantName = SlashColorVariantBuilder.Sanitize(_name);
            variant.Mode = _mode;
            variant.Primary = _primary;
            variant.Secondary = _secondary;
            variant.HueShift = _hueShift;
            variant.Saturation = _saturation;
        }

        private void load(SlashColorVariant variant)
        {
            if (variant == null)
                return;

            _pack = variant.SourcePack;
            _name = variant.VariantName;
            _mode = variant.Mode;
            _primary = variant.Primary;
            _secondary = variant.Secondary;
            _hueShift = variant.HueShift;
            _saturation = variant.Saturation;
            setStatus($"Loaded '{variant.VariantName}'.", MessageType.Info);
            Repaint();
        }

        /// <summary>
        /// Swaps every weapon-pack reference on the selected objects and their
        /// children for the variant's pack - the Sword's own visualizer pack
        /// among them - with undo.
        /// </summary>
        private void assignToSelection(WeaponVisualPack pack)
        {
            int changed = 0;

            foreach (GameObject selected in Selection.gameObjects)
            {
                foreach (Component component in selected.GetComponentsInChildren<Component>(true))
                {
                    if (component == null)
                        continue;

                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    bool touched = false;

                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference)
                            continue;

                        bool isPack = property.objectReferenceValue is WeaponVisualPack ||
                            property.type == $"PPtr<${nameof(WeaponVisualPack)}>";

                        if (!isPack || property.objectReferenceValue == pack)
                            continue;

                        property.objectReferenceValue = pack;
                        touched = true;
                    }

                    if (touched)
                    {
                        serialized.ApplyModifiedProperties();
                        changed++;
                    }
                }
            }

            setStatus(changed > 0
                ? $"'{pack.name}' assigned on {changed} component(s)."
                : "No weapon pack reference found on the selection.",
                changed > 0 ? MessageType.Info : MessageType.Warning);
        }

        private static bool isPresetName(string name)
        {
            foreach (Preset preset in Presets)
            {
                if (string.Equals(preset.Name, name.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void swatch(Color colour, bool hdr)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ColorField(GUIContent.none, colour, false, false, hdr, GUILayout.Width(40f));
        }

        private void report(bool ok, string message)
        {
            setStatus(ok ? $"Generated {message}" : $"Not generated: {message}", ok ? MessageType.Info : MessageType.Error);

            if (ok)
                Debug.Log($"Colour variant generated {message}");
            else
                Debug.LogWarning($"Colour variant not generated: {message}");
        }

        private void setStatus(string message, MessageType type)
        {
            _status = message;
            _statusType = type;
        }
    }
}
