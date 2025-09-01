using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Movement.State;

namespace DevVorpian
{
    [System.Serializable]
    public class StateMachine<StateType> where StateType : Enum
    {
        public UnityEvent OnTransitionedAutonomously = new();

        [SerializeField] private StateType _currentStateType;
        private List<StateTransition<StateType>> _intentBasedTransitions;
        private List<StateTransition<StateType>> _autonomicTransitions;

        private IState _currentState;
        [HideInInspector] private string _stateName;

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
            if (transitionData == null || (_currentState != null && _currentState.Equals(transitionData.TargetState)))
            {
                return;
            }

            setState(transitionData);
        }

        private void setStateAutonomous(StateTransitionData<StateType> transitionData)
        {
            if (transitionData != null)
            {
                setState(transitionData);
                OnTransitionedAutonomously?.Invoke();
            }
        }

        private void checkTransition()
        {
            StateTransitionData<StateType> transitionData = findAutonomicTransition();
            setStateAutonomous(transitionData);
        }

        private void setState(StateTransitionData<StateType> transitionData)
        {
            _currentState?.Exit();
            transitionData?.OnTransition();
            _currentState = transitionData.TargetState;
            _currentState.Enter();
            _currentStateType = transitionData.TargetType;
            _stateName = _currentState.State;
        }

        private StateTransitionData<StateType> findInputBasedTransition(StateType to)
        {
            var transition =
                _intentBasedTransitions.FirstOrDefault(t =>
                    t.To.Equals(to) && t.From.Equals(_currentStateType) && !_currentState.Equals(t.TargetState))
                ?? _intentBasedTransitions.FirstOrDefault(t => t.To.Equals(to) && !_currentState.Equals(t.TargetState));

            if (transition == null) return null;

            return new StateTransitionData<StateType>(transition.To, transition.TargetState, transition.OnTransition);
        }

        private StateTransitionData<StateType> findAutonomicTransition()
        {
            foreach (var transition in _autonomicTransitions)
            {
                if (_currentState.Equals(transition.TargetState))
                    continue;

                if (transition.From.Equals(_currentStateType) && transition.Condition())
                {
                    return new StateTransitionData<StateType>(transition.To, transition.TargetState, transition.OnTransition);
                }

                if (transition.From.Equals(default(StateType)) && transition.Condition())
                {
                    return new StateTransitionData<StateType>(transition.To, transition.TargetState, transition.OnTransition);
                }
            }

            return null;
        }
    }

    public class StateTransition<StateType>
    {
        private StateType _from;
        private StateType _to;
        private IState _state;

        private Func<bool> _condition;
        private Action _onTransition;
        private int _priority { get; set; }

        public StateTransition(StateType from, StateType to, IState state, Action onTransition = null)
        {
            _from = from;
            _to = to;
            _state = state;
            _onTransition = onTransition;
        }

        public StateTransition(StateType from, StateType to, IState state, Func<bool> condition, Action onTransition = null)
        {
            _from = from;
            _to = to;
            _state = state;
            _condition = condition;
            _onTransition = onTransition;
        }

        public StateType From => _from;
        public StateType To => _to;
        public IState TargetState => _state;
        public Func<bool> Condition => _condition;
        public Action OnTransition => _onTransition;
        public int Priority => _priority;
    }

    public class StateTransitionData<StateType>
    {
        public IState TargetState;
        public Action OnTransition;
        public StateType TargetType;
        public StateTransitionData(StateType stateType, IState state, Action onTransition)
        {
            TargetState = state;
            OnTransition = onTransition;
            TargetType = stateType;
        }
    }
}
