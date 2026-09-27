using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    /// <summary>
    /// A colour variant's recipe in the inspector: its settings, and the two
    /// things to do with them - retune it in the variant window, or rebuild it
    /// from the source as it stands.
    ///
    /// The name is shown but not editable: it is the folder the variant's
    /// assets live in, so renaming it here would quietly start a second
    /// variant next to the first. Make a new one in the window instead.
    /// </summary>
    [CustomEditor(typeof(SlashColorVariant))]
    public sealed class SlashColorVariantEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                bool locked = property.propertyPath == "m_Script" ||
                    property.name == nameof(SlashColorVariant.VariantName) ||
                    property.name == nameof(SlashColorVariant.OutputPack) ||
                    property.name == nameof(SlashColorVariant.Outputs);

                using (new EditorGUI.DisabledScope(locked))
                    EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            var variant = (SlashColorVariant)target;
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open In Colour Variants"))
                    SlashColorVariantWindow.Open(variant);

                if (GUILayout.Button("Regenerate"))
                {
                    if (SlashColorVariantBuilder.Generate(variant, out string message))
                        Debug.Log($"Colour variant generated {message}", variant);
                    else
                        Debug.LogWarning($"Colour variant not generated: {message}", variant);
                }
            }
        }
    }
}
