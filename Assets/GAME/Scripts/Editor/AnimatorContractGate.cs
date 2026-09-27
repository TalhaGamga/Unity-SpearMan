using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AnimationEditor
{
    /// <summary>
    /// Runs <see cref="AnimatorContractValidator"/> where it will actually be
    /// seen: the moment a controller or an enum is saved, and again before a
    /// build is allowed out.
    /// </summary>
    /// <remarks>
    /// A validator nobody runs is a validator that does not exist. The import
    /// hook is the one that matters day to day - it puts the error in front of
    /// whoever just dragged the transition, while they still remember dragging
    /// it, instead of two weeks later when it surfaces as a jump that plays the
    /// fall clip.
    ///
    /// The enum trigger is there because the failure this guards against does
    /// not start in the controller. It starts with somebody adding a member to
    /// MovementType.
    /// </remarks>
    public sealed class AnimatorContractGate :
        AssetPostprocessor, IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private const string EnumFolder = "Assets/GAME/Scripts/Enums/";

        private static void OnPostprocessAllAssets(
            string[] imported,
            string[] deleted,
            string[] moved,
            string[] movedFrom)
        {
            bool touched = imported
                .Concat(moved)
                .Any(path =>
                    path.EndsWith(".controller") ||
                    (path.StartsWith(EnumFolder) && path.EndsWith(".cs")));

            if (!touched)
                return;

            Validate(logClean: false);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (Validate(logClean: false) > 0)
            {
                throw new BuildFailedException(
                    "Animator contract validation failed. A parameter mismatch " +
                    "does not crash - it plays the wrong animation - so it is " +
                    "caught here rather than in the build.");
            }
        }

        [MenuItem("Tools/Animation/Validate Animator Contract", priority = 0)]
        public static void ValidateFromMenu()
        {
            int errors = Validate(logClean: true);

            if (errors == 0)
                Debug.Log("Animator contract: clean.");
        }

        /// <summary>Returns how many errors were found.</summary>
        public static int Validate(bool logClean)
        {
            int errors = 0;

            foreach (string path in AnimatorContractValidator.ContractControllers)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

                if (controller == null)
                {
                    Debug.LogError(
                        $"Animator contract: no controller at '{path}'. " +
                        "Update AnimatorContractValidator.ContractControllers.");
                    errors++;
                    continue;
                }

                List<AnimatorContractValidator.Issue> issues =
                    AnimatorContractValidator.Validate(controller);

                AnimatorContractValidator.Log(controller, issues);

                errors += issues.Count(
                    i => i.Severity == AnimatorContractValidator.Severity.Error);

                if (logClean)
                {
                    Debug.Log(
                        $"Animator contract: {controller.name} checked - " +
                        $"{controller.parameters.Length} parameters, " +
                        $"{issues.Count} issue(s).", controller);
                }
            }

            reportStrays(logClean);
            return errors;
        }

        /// <summary>
        /// Names controllers outside the contract that still compare against
        /// numbers. Warnings, not errors: they are nobody's dependency today,
        /// and failing a build over an asset the game never loads would teach
        /// people to switch this off.
        /// </summary>
        private static void reportStrays(bool verbose)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:AnimatorController", new[] { "Assets/GAME" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (AnimatorContractValidator.ContractControllers.Contains(path))
                    continue;

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                List<string> strays = AnimatorContractValidator.ReportStrays(controller);

                if (strays.Count == 0)
                    continue;

                Debug.LogWarning(
                    $"Animator contract: '{path}' is not on the contract and still " +
                    $"uses ordinal conditions ({strays.Count}).\n" +
                    string.Join("\n", strays), controller);
            }
        }

        [MenuItem("Tools/Animation/Dump Animator Transitions", priority = 1)]
        public static void DumpTransitions()
        {
            foreach (string path in AnimatorContractValidator.ContractControllers)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

                if (controller == null)
                    continue;

                var text = new System.Text.StringBuilder();
                text.AppendLine(controller.name);

                foreach (AnimatorControllerLayer layer in controller.layers)
                {
                    text.AppendLine($"  layer: {layer.name}");
                    dump(layer.stateMachine, text, "    ");
                }

                Debug.Log(text.ToString(), controller);
            }
        }

        private static void dump(
            AnimatorStateMachine machine,
            System.Text.StringBuilder text,
            string indent)
        {
            if (machine == null)
                return;

            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
                line(transition, "AnyState", text, indent);

            foreach (ChildAnimatorState child in machine.states)
                foreach (AnimatorStateTransition transition in child.state.transitions)
                    line(transition, child.state.name, text, indent);

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                text.AppendLine($"{indent}[{child.stateMachine.name}]");
                dump(child.stateMachine, text, indent + "  ");
            }
        }

        private static void line(
            AnimatorStateTransition transition,
            string from,
            System.Text.StringBuilder text,
            string indent)
        {
            string to = transition.destinationState != null
                ? transition.destinationState.name
                : transition.destinationStateMachine != null
                    ? transition.destinationStateMachine.name
                    : "Exit";

            string conditions = transition.conditions.Length == 0
                ? transition.hasExitTime ? "on exit time" : "unconditional"
                : string.Join(" and ", transition.conditions.Select(describe));

            text.AppendLine($"{indent}{from} -> {to}: {conditions}");
        }

        private static string describe(AnimatorCondition condition)
        {
            return condition.mode switch
            {
                AnimatorConditionMode.If => condition.parameter,
                AnimatorConditionMode.IfNot => $"not {condition.parameter}",
                _ => $"{condition.parameter} {condition.mode} {condition.threshold}"
            };
        }
    }
}
