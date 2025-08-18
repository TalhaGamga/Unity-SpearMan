using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace DevVorpian
{
    [System.Serializable]
    public class StateMachine<StateType>
    {
        [SerializeField] private string _stateName;

        [SerializeField] public StateType _currentStateType;
        private List<StateTransition<StateType>> _stateTransitions;
        private IState _currentState;

        public StateMachine()
        {
            _stateTransitions = new List<StateTransition<StateType>>();
        }

        public void Update()
        {
            _currentState?.Update();
        }

        public void SetState(StateType newStateType)
        {
            var transitionData = findTransition(newStateType);
            setState(transitionData);
            _currentStateType = newStateType;
        }

        private void setState(StateTransitionData transitionData)
        {
            _currentState?.Exit();
            transitionData?.onTransition();
            _currentState = transitionData.to;
            _currentState.Enter();
            _stateName = _currentState.State;
        }

        public void AddTransition(StateTransition<StateType> stateTransition)
        {
            _stateTransitions.Add(stateTransition);
        }

        private StateTransitionData findTransition(StateType to)
        {
            var transition =
                _stateTransitions.FirstOrDefault(s =>
                    s.To.Equals(to) && s.From.Equals(_currentStateType))
                ?? _stateTransitions.FirstOrDefault(s => s.To.Equals(to));

            if (transition == null) return null;

            return new StateTransitionData(transition.State, transition.OnTransition);
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

        public StateTransition(StateType from, StateType to, IState state)
        {
            _from = from;
            _to = to;
            _state = state;
        }

        public StateType From => _from;
        public StateType To => _to;
        public IState State => _state;
        public Func<bool> Condition => _condition;
        public Action OnTransition => _onTransition;
        public int Priority => _priority;


        public void SetOnTransition(Action transitionAction)
        {
            _onTransition = transitionAction;
        }
    }

    public class StateTransitionData
    {
        public IState to;
        public Action onTransition;

        public StateTransitionData(IState to, Action onTransition)
        {
            this.to = to;
            this.onTransition += onTransition;
        }
    }
}
