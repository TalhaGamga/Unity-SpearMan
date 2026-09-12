using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Events;
using Movement.State;

namespace DevVorpian
{
    [System.Serializable]
    public class StateMachine<StateType>
    {
        [HideInInspector] public UnityEvent OnTransitionedAutonomously = new();
        public string CurrentStateName => _currentState.StateName;

        private List<StateTransition<StateType>> _inputBasedTransitions;
        private List<StateTransition<StateType>> _autonomicTransitions;

        private IState _currentState;

        public StateMachine()
        {
            _inputBasedTransitions = new();
            _autonomicTransitions = new();
            _currentState = new ConcreteState();
        }

        public void Update(bool checkTransitions = true)
        {
            _currentState?.Update();

            if (checkTransitions)
                checkTransition();
        }

        /// <summary>
        /// Transitions are evaluated BEFORE the state ticks, deliberately.
        ///
        /// Evaluating them afterwards costs a whole physics step: the frame
        /// that detects a landing has already run the airborne state's physics,
        /// so the grounded state does not get to move the body until the next
        /// step. Conditions read the same Rigidbody data either way - they were
        /// already looking at the previous step's result - so moving the check
        /// to the front changes nothing about when a transition is detected and
        /// everything about when the new state gets to act on it.
        /// </summary>
        public void PhysicsUpdate(bool checkTransitions = false)
        {
            if (checkTransitions)
                checkTransition();

            _currentState?.PhysicsUpdate();
        }

        public void AddIntentBasedTransition(StateTransition<StateType> stateTransition)
        {
            _inputBasedTransitions.Add(stateTransition);
        }

        public void AddAutonomicTransition(StateTransition<StateType> stateTransition)
        {
            _autonomicTransitions.Add(stateTransition);
        }

        public void SetState(StateType newStateType)
        {
            var transitionData = findInputBasedTransition(newStateType);
            if (transitionData == null)
            {
                return;
            }

            setState(transitionData);
        }

        private void setStateAutonomous(StateTransitionData transitionData)
        {
            if (transitionData != null)
            {
                setState(transitionData);
                OnTransitionedAutonomously?.Invoke();
            }
        }

        private void checkTransition()
        {
            StateTransitionData transitionData = findAutonomicTransition();
            setStateAutonomous(transitionData);
        }

        private void setState(StateTransitionData transitionData)
        {
            _currentState?.Exit();
            transitionData?.OnTransition();
            _currentState = transitionData.TargetState;
            _currentState.Enter();
        }

        private StateTransitionData findInputBasedTransition(StateType targetStateType)
        {
            foreach (var t in _inputBasedTransitions)
            {
                if (!t.TargetStateType.Equals(targetStateType)) continue;
                if (t.From != null && t.From.Equals(_currentState) && t.Condition())
                    return new StateTransitionData(t.To, t.OnTransition);
            }

            foreach (var t in _inputBasedTransitions)
            {
                if (!t.TargetStateType.Equals(targetStateType)) continue;
                if (t.From == null && !_currentState.Equals(t.To) && t.Condition())
                    return new StateTransitionData(t.To, t.OnTransition);
            }

            return null;
        }

        private StateTransitionData findAutonomicTransition()
        {
            var current = _currentState;
            StateTransition<StateType> fallback = null;

            foreach (var transition in _autonomicTransitions)
            {
                if (current.Equals(transition.To))
                    continue;

                if (!transition.Condition())
                    continue;

                if (transition.From != null && transition.From.Equals(current))
                    return new StateTransitionData(transition.To, transition.OnTransition);

                if (transition.From == null && fallback == null)
                    fallback = transition;
            }

            return fallback != null
                ? new StateTransitionData(fallback.To, fallback.OnTransition)
                : null;
        }
    }

    public class StateTransition<StateType>
    {
        private IState _from;
        private IState _to;
        private StateType _targetStateType;
        private Func<bool> _condition;
        private Action _onTransition;
        private int _priority { get; set; }

        public StateTransition(
            IState from,
            IState to,
            StateType targetStateType,
            Func<bool> condition = null,
            Action onTransition = null)
        {
            _from = from;
            _to = to;
            _targetStateType = targetStateType;
            _condition = condition ?? (() => true);
            _onTransition = onTransition ?? (() => Debug.Log("Transitioning to " + targetStateType));
        }

        public IState From => _from;
        public IState To => _to;
        public StateType TargetStateType => _targetStateType;
        public Func<bool> Condition => _condition;
        public Action OnTransition => _onTransition;
        public int Priority => _priority;
    }

    public class StateTransitionData
    {
        public IState TargetState;
        public Action OnTransition;
        public StateTransitionData(IState state, Action onTransition)
        {
            TargetState = state;
            OnTransition = onTransition;
        }
    }
}
