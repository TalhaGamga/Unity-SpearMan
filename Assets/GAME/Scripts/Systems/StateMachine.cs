using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Movement.State;
using Unity.VisualScripting;

namespace DevVorpian
{
    [System.Serializable]
    public class StateMachine<StateType>
    {
        public UnityEvent OnTransitionedAutonomously = new();
        [SerializeField] private string _stateName;

        private List<StateTransition<StateType>> _intentBasedTransitions;
        private List<StateTransition<StateType>> _autonomicTransitions;

        private IState _currentState;

        public StateMachine()
        {
            _intentBasedTransitions = new();
            _autonomicTransitions = new();
            _currentState = new ConcreteState();
        }

        public void Update()
        {
            _currentState?.Update();
            checkTransition();
        }

        public void AddIntentBasedTransition(StateTransition<StateType> stateTransition)
        {
            _intentBasedTransitions.Add(stateTransition);
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
            _stateName = _currentState.State;
        }

        private StateTransitionData findInputBasedTransition(StateType targetStateType)
        {
            foreach (var t in _intentBasedTransitions)
            {
                if (!t.TargetStateType.Equals(targetStateType)) continue;
                if (t.From != null && t.From.Equals(_currentState) && t.Condition())
                    return new StateTransitionData(t.To, t.OnTransition);
            }

            foreach (var t in _intentBasedTransitions)
            {
                if (!t.TargetStateType.Equals(targetStateType)) continue;
                if (!_currentState.Equals(t.To))
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
