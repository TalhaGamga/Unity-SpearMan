using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;

namespace AnimationEditor
{
    /// <summary>
    /// Checks that a controller and <see cref="AnimatorParams"/> still agree.
    /// </summary>
    /// <remarks>
    /// The animator boundary has no compiler. A parameter renamed on one side
    /// is not a build error, it is a transition that stops firing; a parameter
    /// the graph stopped reading is not a warning, it is a mechanic quietly
    /// wired to nothing. This is the compiler for that boundary.
    ///
    /// It matters more since the parameter writes moved to hashes. The string
    /// setters at least logged "Parameter does not exist" at run time; the hash
    /// overloads silently do nothing. That loud failure did not disappear, it
    /// moved here - which is the trade only worth making if this actually runs,
    /// so <see cref="AnimatorContractGate"/> runs it on every import and again
    /// before every build.
    /// </remarks>
    public static class AnimatorContractValidator
    {
        /// <summary>
        /// Controllers held to the contract. Anything else is legacy and is
        /// reported rather than checked - see <see cref="ReportStrays"/>.
        /// </summary>
        public static readonly string[] ContractControllers =
        {
            "Assets/GAME/Graphics/Anim/CharacterSwordCombat.controller"
        };

        public enum Severity
        {
            Warning,
            Error
        }

        public readonly struct Issue
        {
            public readonly string Rule;
            public readonly Severity Severity;
            public readonly string Message;

            public Issue(string rule, Severity severity, string message)
            {
                Rule = rule;
                Severity = severity;
                Message = message;
            }

            public override string ToString() => $"[{Rule}] {Message}";
        }

        public static List<Issue> Validate(AnimatorController controller, bool strict = true)
        {
            var issues = new List<Issue>();

            if (controller == null)
                return issues;

            Dictionary<string, AnimatorControllerParameter> declared =
                controller.parameters.ToDictionary(p => p.name, p => p);

            var transitions = new List<AnimatorStateTransition>();

            foreach (AnimatorControllerLayer layer in controller.layers)
                collectTransitions(layer.stateMachine, transitions);

            checkContractDeclared(declared, issues);
            checkNothingUndeclaredInGraph(controller, declared, issues);
            checkAllConditionsBoolean(transitions, issues, strict);
            checkEveryWrittenParameterIsRead(transitions, controller, issues);
            checkJumpFamilyNotMixed(transitions, issues);
            checkUnwrittenParameters(transitions, issues);

            return issues;
        }

        /// <summary>
        /// Walks sub-state machines too. The stab states live inside
        /// DashAttackMachine, so a flat walk over layer.stateMachine.states
        /// silently validates a graph with a hole in it.
        /// </summary>
        private static void collectTransitions(
            AnimatorStateMachine machine,
            List<AnimatorStateTransition> into)
        {
            if (machine == null)
                return;

            into.AddRange(machine.anyStateTransitions);

            foreach (ChildAnimatorState child in machine.states)
                into.AddRange(child.state.transitions);

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                collectTransitions(child.stateMachine, into);
        }

        /// <summary>V1 - the graph declares everything the game writes.</summary>
        private static void checkContractDeclared(
            Dictionary<string, AnimatorControllerParameter> declared,
            List<Issue> issues)
        {
            foreach (AnimatorParams.Entry entry in AnimatorParams.Contract)
            {
                if (!declared.TryGetValue(entry.Name, out AnimatorControllerParameter parameter))
                {
                    issues.Add(new Issue("V1", Severity.Error,
                        $"'{entry.Name}' is in the contract but the controller does " +
                        "not declare it. Animator.SetBool on a hash the controller " +
                        "has never heard of does nothing at all, and says nothing."));
                    continue;
                }

                AnimatorControllerParameterType expected = typeOf(entry.Kind);

                if (parameter.type != expected)
                {
                    issues.Add(new Issue("V1", Severity.Error,
                        $"'{entry.Name}' is declared as {parameter.type} but the " +
                        $"contract says {expected}."));
                }
            }
        }

        /// <summary>
        /// V2 - the game writes everything the graph declares. The inverse of
        /// V1, and the one that catches a parameter left behind: naming alone
        /// never finds those, because a dead parameter's name is still fine.
        /// </summary>
        private static void checkNothingUndeclaredInGraph(
            AnimatorController controller,
            Dictionary<string, AnimatorControllerParameter> declared,
            List<Issue> issues)
        {
            var contract = new HashSet<string>(AnimatorParams.Contract.Select(e => e.Name));

            foreach (string name in declared.Keys)
            {
                if (contract.Contains(name))
                    continue;

                issues.Add(new Issue("V2", Severity.Error,
                    $"'{name}' is declared in {controller.name} but is not in " +
                    "AnimatorParams.Contract, so nothing in the game writes it."));
            }
        }

        /// <summary>
        /// V3 - no condition compares against a number.
        /// </summary>
        /// <remarks>
        /// The whole ordinal problem reduced to one assertion. A threshold can
        /// only mean something by reference to a C# enum's declaration order,
        /// and an asset cannot see that order - so if no threshold exists, no
        /// threshold can silently re-point when somebody inserts an enum member.
        /// </remarks>
        private static void checkAllConditionsBoolean(
            List<AnimatorStateTransition> transitions,
            List<Issue> issues,
            bool strict)
        {
            foreach (AnimatorStateTransition transition in transitions)
            {
                foreach (AnimatorCondition condition in transition.conditions)
                {
                    bool boolean = condition.mode == AnimatorConditionMode.If ||
                        condition.mode == AnimatorConditionMode.IfNot;

                    if (boolean)
                        continue;

                    issues.Add(new Issue("V3", strict ? Severity.Error : Severity.Warning,
                        $"'{transition.name}' compares {condition.parameter} " +
                        $"{condition.mode} {condition.threshold}. A threshold only " +
                        "means anything by reference to an enum's declaration order, " +
                        "which this asset cannot see."));
                }
            }
        }

        /// <summary>V4 - a parameter the game writes that the graph ignores.</summary>
        private static void checkEveryWrittenParameterIsRead(
            List<AnimatorStateTransition> transitions,
            AnimatorController controller,
            List<Issue> issues)
        {
            var read = new HashSet<string>();

            foreach (AnimatorStateTransition transition in transitions)
                foreach (AnimatorCondition condition in transition.conditions)
                    read.Add(condition.parameter);

            foreach (AnimatorParams.Entry entry in AnimatorParams.Contract)
            {
                if (!entry.WrittenByMapper || read.Contains(entry.Name))
                    continue;

                // A Float is normally a blend tree's input rather than a
                // condition's, so it is expected not to appear above.
                if (entry.Kind == AnimatorParams.Kind.Float)
                    continue;

                issues.Add(new Issue("V4", Severity.Warning,
                    $"The game writes '{entry.Name}' every frame and no transition " +
                    $"in {controller.name} reads it - a mechanic wired to nothing."));
            }
        }

        /// <summary>
        /// V5 - the one documented non-exclusive group is not mixed.
        /// </summary>
        /// <remarks>
        /// Jump is the superset of GroundJump and AirJump. Listing Jump beside
        /// either of them asks the graph to settle by transition priority what
        /// the mechanic already decided, which shows up as a double jump that
        /// sometimes plays the wrong clip and never reproduces on demand.
        /// </remarks>
        private static void checkJumpFamilyNotMixed(
            List<AnimatorStateTransition> transitions,
            List<Issue> issues)
        {
            foreach (AnimatorStateTransition transition in transitions)
            {
                var used = new HashSet<string>();

                foreach (AnimatorCondition condition in transition.conditions)
                {
                    if (AnimatorParams.JumpFamily.Contains(condition.parameter))
                        used.Add(condition.parameter);
                }

                if (used.Contains(AnimatorParams.Jump) && used.Count > 1)
                {
                    issues.Add(new Issue("V5", Severity.Error,
                        $"'{transition.name}' lists {AnimatorParams.Jump} together " +
                        $"with {string.Join(", ", used.Where(u => u != AnimatorParams.Jump))}. " +
                        "Jump is the superset of both; pick the specific one."));
                }
            }
        }

        /// <summary>
        /// V6 - a parameter the graph reads that no code writes. Reported
        /// every run on purpose: allowlisting it forever is the outcome to
        /// resist, because it is a mechanic that exists in the graph and not
        /// in the game.
        /// </summary>
        private static void checkUnwrittenParameters(
            List<AnimatorStateTransition> transitions,
            List<Issue> issues)
        {
            foreach (AnimatorParams.Entry entry in AnimatorParams.Contract)
            {
                if (entry.WrittenByMapper)
                    continue;

                List<string> gated = transitions
                    .Where(t => t.conditions.Any(c => c.parameter == entry.Name))
                    .Select(t => t.name)
                    .ToList();

                if (gated.Count == 0)
                    continue;

                issues.Add(new Issue("V6", Severity.Warning,
                    $"'{entry.Name}' gates {string.Join(", ", gated)} and no code " +
                    "in the project ever writes it."));
            }
        }

        private static AnimatorControllerParameterType typeOf(AnimatorParams.Kind kind)
        {
            return kind switch
            {
                AnimatorParams.Kind.Float => AnimatorControllerParameterType.Float,
                AnimatorParams.Kind.Bool => AnimatorControllerParameterType.Bool,
                _ => AnimatorControllerParameterType.Trigger
            };
        }

        /// <summary>
        /// Names controllers outside the contract that still use the old
        /// ordinal vocabulary, so they cannot rot unnoticed.
        /// </summary>
        public static List<string> ReportStrays(AnimatorController controller)
        {
            var strays = new List<string>();

            if (controller == null)
                return strays;

            var transitions = new List<AnimatorStateTransition>();

            foreach (AnimatorControllerLayer layer in controller.layers)
                collectTransitions(layer.stateMachine, transitions);

            foreach (AnimatorStateTransition transition in transitions)
            {
                foreach (AnimatorCondition condition in transition.conditions)
                {
                    if (condition.mode == AnimatorConditionMode.If ||
                        condition.mode == AnimatorConditionMode.IfNot)
                    {
                        continue;
                    }

                    strays.Add(
                        $"{controller.name}: '{transition.name}' still compares " +
                        $"{condition.parameter} {condition.mode} {condition.threshold}");
                }
            }

            return strays;
        }

        public static void Log(AnimatorController controller, IEnumerable<Issue> issues)
        {
            foreach (Issue issue in issues)
            {
                if (issue.Severity == Severity.Error)
                    Debug.LogError($"Animator contract - {issue}", controller);
                else
                    Debug.LogWarning($"Animator contract - {issue}", controller);
            }
        }
    }
}
