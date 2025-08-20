using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace DevVorpian
{
    [System.Serializable]
    public class StateMachine<StateType>
    {
        public Action OnAutonomicTransition;
        [SerializeField] private string _stateName;

        [SerializeField] public StateType _currentStateType;
        private List<StateTransition<StateType>> _inputBasedTransitions;
        private List<StateTransition<StateType>> _autonomicTransitions;

        private IState _currentState;

        public StateMachine()
        {
            _inputBasedTransitions = new();
            _autonomicTransitions = new();
        }

        public void Update()
        {
            _currentState?.Update();

            checkTransition();
        }

        public void AddTransition(StateTransition<StateType> stateTransition)
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
            if (transitionData == null || (_currentState != null && _currentState.Equals(transitionData.To)))
            {
                return;
            }

            setState(transitionData);
            _currentStateType = newStateType;
        }

        private void setStateAutonomous(StateTransitionData transitionData)
        {
            if (transitionData != null)
            {
                setState(transitionData);
                OnAutonomicTransition?.Invoke();
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
            _currentState = transitionData.To;
            _currentState.Enter();
            _stateName = _currentState.State;
        }


        private StateTransitionData findInputBasedTransition(StateType to)
        {
            var transition =
                _inputBasedTransitions.FirstOrDefault(s =>
                    s.To.Equals(to) && s.From.Equals(_currentStateType))
                ?? _inputBasedTransitions.FirstOrDefault(s => s.To.Equals(to));

            if (transition == null) return null;

            return new StateTransitionData(transition.TargetState, transition.OnTransition);
        }

        private StateTransitionData findAutonomicTransition()
        {
            foreach (var transition in _autonomicTransitions)
            {
                if (_currentStateType.Equals(transition.To))
                    return null;

                if (transition.From.Equals(transition.From) && transition.Condition())
                {
                    return new StateTransitionData(transition.TargetState, transition.OnTransition);
                }

                if (transition.Condition())
                {
                    return new StateTransitionData(transition.TargetState, transition.OnTransition);
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

        public StateTransition(StateType from, StateType to, Func<bool> condition, IState state, Action onTransition = null)
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

    public class StateTransitionData
    {
        public IState To;
        public Action OnTransition;

        public StateTransitionData(IState to, Action onTransition)
        {
            To = to;
            OnTransition = onTransition;
        }
    }
}
