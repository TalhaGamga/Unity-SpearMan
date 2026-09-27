using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a cue entry showing only the fields the chosen kind of effect
/// actually reads.
///
/// A cue entry carries the settings for two quite different effects, and most
/// of them belong to one or the other. Left to the default inspector it offers
/// an alignment, a scale and a lifetime on a slash, none of which the slash
/// looks at, so a reasonable change silently does nothing. Hiding them is not
/// tidying: the field being there is the claim that it works.
/// </summary>
[CustomPropertyDrawer(typeof(VisualCueEntry))]
public sealed class VisualCueEntryDrawer : PropertyDrawer
{
    private static readonly List<string> Fields = new(20);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;

        if (!property.isExpanded)
            return height;

        collect(property, Fields);

        for (int i = 0; i < Fields.Count; i++)
        {
            SerializedProperty field = property.FindPropertyRelative(Fields[i]);
            if (field == null)
                continue;

            height += EditorGUI.GetPropertyHeight(field, true)
                + EditorGUIUtility.standardVerticalSpacing;
        }

        return height + EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // The cue key is what identifies an entry, so it titles the row rather
        // than the array index, which says nothing.
        SerializedProperty key = property.FindPropertyRelative("CueKey");
        string title = string.IsNullOrWhiteSpace(key.stringValue)
            ? label.text
            : key.stringValue;

        var row = new Rect(
            position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, title, true);

        if (property.isExpanded)
        {
            collect(property, Fields);

            EditorGUI.indentLevel++;
            row.y += row.height + EditorGUIUtility.standardVerticalSpacing;

            for (int i = 0; i < Fields.Count; i++)
            {
                SerializedProperty field = property.FindPropertyRelative(Fields[i]);
                if (field == null)
                    continue;

                row.height = EditorGUI.GetPropertyHeight(field, true);
                EditorGUI.PropertyField(row, field, true);
                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static void collect(SerializedProperty property, List<string> into)
    {
        into.Clear();
        into.Add("CueKey");
        into.Add("Kind");

        var kind = (VisualCueKind)property.FindPropertyRelative("Kind").enumValueIndex;

        if (kind == VisualCueKind.Slash)
        {
            into.Add("SlashProfile");
            into.Add("Anchor");
            into.Add("ArcTip");

            // A slash may still spawn a prefab alongside, as an impact accent.
            into.Add("Prefab");
            return;
        }

        into.Add("Prefab");
        into.Add("Anchor");
        into.Add("Alignment");
        into.Add("PositionOffset");
        into.Add("RotationOffset");
        into.Add("Scale");
        into.Add("FollowAnchor");
        into.Add("MirrorOnFacing");

        var alignment =
            (VisualAlignment)property.FindPropertyRelative("Alignment").enumValueIndex;

        if (alignment == VisualAlignment.BladeTrailArc)
        {
            into.Add("ArcTip");
            into.Add("ArcRadius");
            into.Add("ArcLeadAngle");
            into.Add("ArcWinding");
        }

        into.Add("ScaleBy");
        into.Add("RateBy");

        bool driven =
            property.FindPropertyRelative("ScaleBy").enumValueIndex ==
                (int)VisualScalar.SwingSpeed ||
            property.FindPropertyRelative("RateBy").enumValueIndex ==
                (int)VisualScalar.SwingSpeed;

        if (driven)
        {
            into.Add("SwingSpeedRange");
            into.Add("SwingStrengthRange");
        }

        into.Add("Lifetime");
    }
}
