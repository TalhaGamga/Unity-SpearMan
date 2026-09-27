using Combat;
using UnityEditor;

namespace CombatEditor
{
    /// <summary>
    /// Draws an impact's tuning with the curve only where it is read, and says
    /// what the numbers come to: how long the impact now lasts, which is not
    /// written anywhere else once five lifetimes have all been stretched.
    /// </summary>
    [CustomEditor(typeof(ImpactTuning))]
    [CanEditMultipleObjects]
    public sealed class ImpactTuningEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty shape = serializedObject.FindProperty(nameof(ImpactTuning.FadeShape));
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

                // The named shapes are curves of their own; the drawn one is
                // only read once the shape is Custom.
                if (property.name == nameof(ImpactTuning.FadeCurve) &&
                    (shape.hasMultipleDifferentValues || shape.enumValueIndex != (int)ImpactFadeShape.Custom))
                {
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (targets.Length != 1)
                return;

            var tuning = (ImpactTuning)target;
            float authored = tuning.AuthoredSeconds();

            EditorGUILayout.HelpBox(
                $"Lasts about {authored * tuning.Duration:0.00} s (authored {authored:0.00} s). " +
                "The hit cue's lifetime is stretched to fit, so nothing is cut off.\n" +
                "Shows in play mode, from the next hit on.",
                MessageType.None);
        }
    }
}
