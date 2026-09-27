using System.Collections.Generic;
using UnityEngine;

namespace CombatEditor
{
    /// <summary>
    /// How a colour variant turns the source slash's colours into its own.
    /// </summary>
    public enum SlashRecolorMode
    {
        /// <summary>
        /// The source's two colour families - its violet body and its jade
        /// under-stroke - each go to a colour of their own; the blues that
        /// run between them in the source run between the new two.
        /// </summary>
        TwoTone = 0,

        /// <summary>Every hue turns by the same angle round the wheel.</summary>
        HueShift = 1
    }

    /// <summary>
    /// One colour variant of the crescent slash: the recipe, not the result.
    ///
    /// A variant is the source weapon pack's effects with nothing changed but
    /// their colours, so this holds only what makes it different - which pack
    /// it recolours, and to what - and the generated assets are rebuilt from
    /// the source every time. That is what keeps "everything else the same"
    /// true over time: when the slash itself is retuned or rebuilt, every
    /// variant is re-derived from the new version instead of being left as a
    /// copy of the old one. CrescentSlashBuilder does that at the end of
    /// every build.
    ///
    /// Only hue and saturation are ever changed. Each colour keeps its own
    /// brightness - its strongest channel, the one URP's bloom threshold
    /// reads - so what glows, what stays under the bloom, the dark streaks
    /// and the whole value structure of the readability pass carry over
    /// exactly.
    ///
    /// Editor-only: nothing at runtime references it. Made and edited in
    /// Tools > VFX > Crescent Slash > Colour Variants.
    /// </summary>
    public sealed class SlashColorVariant : ScriptableObject
    {
        [Tooltip("The name the variant's folder and assets are given.")]
        public string VariantName;

        [Tooltip("The weapon pack whose effects are recoloured. Every slash " +
            "profile and prefab its cues use is copied with new colours, and " +
            "the pack itself is copied pointing at the copies.")]
        public WeaponVisualPack SourcePack;

        public SlashRecolorMode Mode = SlashRecolorMode.TwoTone;

        [Tooltip("Two-tone: what the source's main colour - the violet body - " +
            "becomes. Only its hue and saturation are used; brightness always " +
            "comes from the source.")]
        [ColorUsage(false, false)] public Color Primary = new(0.73f, 0.29f, 1f, 1f);

        [Tooltip("Two-tone: what the source's second colour - the jade " +
            "under-stroke - becomes. Only its hue and saturation are used.")]
        [ColorUsage(false, false)] public Color Secondary = new(0f, 1f, 0.87f, 1f);

        [Tooltip("Hue shift: degrees every hue turns round the colour wheel.")]
        [Range(-180f, 180f)] public float HueShift;

        [Tooltip("Multiplies every colour's saturation after the mapping. 1 " +
            "keeps it; lower washes the variant towards white and grey.")]
        [Range(0f, 2f)] public float Saturation = 1f;

        [Header("Generated")]
        [Tooltip("The recoloured copy of the source pack. Give this to a weapon " +
            "(its Sword's Visualizer > Pack) to use the variant.")]
        public WeaponVisualPack OutputPack;

        [Tooltip("Every asset the last generation wrote, for reference.")]
        public List<Object> Outputs = new();
    }
}
