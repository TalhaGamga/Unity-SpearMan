using UnityEditor;

/// <summary>
/// Draws a slash profile with the settings the current choices make inert
/// hidden.
///
/// Several of the profile's numbers are only read under one setting: the five
/// highlight numbers are replaced wholesale by the named shapes, the accent
/// numbers are dead with no accents, and a reach is either taken from the
/// weapon or from the profile but never both. Showing them regardless invites
/// changes that do nothing and leaves no way to tell which those are.
/// </summary>
[CustomEditor(typeof(SlashProfile))]
public sealed class SlashProfileEditor : Editor
{
    private static readonly System.Collections.Generic.HashSet<string> RibbonOnly = new()
    {
        "StartAlongBlade", "TaperSharpness", "MinStep", "Smoothing",
        "HighlightShape", "HighlightWidth", "HighlightStart", "HighlightLength",
        "HighlightBias", "HighlightSharpness", "HighlightOffset", "HighlightOpacity",
        "AccentCount", "AccentWidth", "AccentGap", "AccentSpacing",
        "AccentStart", "AccentLength", "AccentOpacity",
        "VolumeShells", "VolumeSeparation", "VolumeSpread", "VolumeOpacity",
        "Material", "CoreColor", "EdgeColor", "GlowColor",
        "Brightness", "CoreSharpness", "GlowIntensity",
        "EdgeFalloff", "HighlightFalloff",
        "DepthOffset", "LayerSeparation",
    };

    /// <summary>
    /// Dials the ring reads and the band has no use for.
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> CircleOnly = new()
    {
        "CircleMaterial", "CircleSpanDegrees", "CircleRadiusSource", "CircleFollowSweep",
        "CircleHeadFalloff", "CircleTailOpacity", "CircleSquash", "CircleWobble", "CircleWobbleLobes",
        "CircleTaper", "CircleSegments", "CircleDepthOffset",
        "CircleTextureFrames", "CircleTextureFrame", "CircleTextureFlip",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(property);

                continue;
            }

            if (!applies(property.name))
                continue;

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private bool applies(string field)
    {
        bool circle = count("ShapeMode") == (int)SlashShapeMode.Circle;

        // Nothing about the generated ribbon matters when it is switched off -
        // except the edge colour, which is also what tints the ring, and so is
        // the one dial that has to outlive the ribbon on a circle profile.
        bool ribbonDial = RibbonOnly.Contains(field)
            && !(circle && field == "EdgeColor");

        if (ribbonDial && !flag("DrawRibbon"))
            return false;

        // Nothing about the ring matters when the cut is swept as a band.
        if (CircleOnly.Contains(field) && !circle)
            return false;

        switch (field)
        {
            // A ring with no wander is a plain circle, so it has no lobes to
            // count and no frame to pick out of a sheet that is one picture.
            case "CircleWobbleLobes":
                return number("CircleWobble") > 0f;

            case "CircleTextureFrame":
                return count("CircleTextureFrames") > 1;

            // The ring reads these as its inner and outer radius, so they are
            // live whether or not a particle prefab is assigned - the band is
            // the one that only exists when there is one.
            case "ShapeInnerAlongBlade":
            case "ShapeOuterAlongBlade":
            case "ShapeFadeSeconds":
                return circle || reference("Particles");

            // The named shapes write these six, so they only mean anything
            // once the shape is Custom.
            case "HighlightWidth":
            case "HighlightStart":
            case "HighlightLength":
            case "HighlightBias":
            case "HighlightSharpness":
            case "HighlightOffset":
                return count("HighlightShape") == (int)SlashHighlightShape.Custom;

            case "AccentWidth":
            case "AccentGap":
            case "AccentSpacing":
            case "AccentLength":
            case "AccentStart":
            case "AccentOpacity":
                return count("AccentCount") > 0;

            case "VolumeSeparation":
            case "VolumeSpread":
            case "VolumeOpacity":
                return count("VolumeShells") > 0;

            case "ShapeBurstCount":
            case "ShapeArcLength":
            case "AlignParticlesToSweep":
            case "ParticleRotationOffset":
            case "ParticlesPerUnit":
            case "ParticleSpeedScale":
            case "ParticleSpread":
            case "ParticleSizeScale":
            case "ParticleOpacity":
            case "ParticleLifetime":
                return reference("Particles");

            default:
                return true;
        }
    }

    /// <summary>True when an object reference is assigned.</summary>
    private bool reference(string name)
    {
        SerializedProperty property = serializedObject.FindProperty(name);
        return property != null && property.objectReferenceValue != null;
    }

    private float number(string name)
    {
        SerializedProperty property = serializedObject.FindProperty(name);
        return property != null ? property.floatValue : 0f;
    }

    private bool flag(string name)
    {
        SerializedProperty property = serializedObject.FindProperty(name);
        return property != null && property.boolValue;
    }

    /// <summary>Reads an int or an enum, which serialize the same way.</summary>
    private int count(string name)
    {
        SerializedProperty property = serializedObject.FindProperty(name);

        if (property == null)
            return 0;

        return property.propertyType == SerializedPropertyType.Enum
            ? property.enumValueIndex
            : property.intValue;
    }
}
