using System.Collections.Generic;
using System;
using System.Linq;

namespace DevVorpian
{
    [System.Serializable]
    public class StateMachine
    {
        List<StateTransition> _stateTransitions;

        public IState _currentState;
        private IState _emptyState;

        public string _currentStateType;
        private bool _isDropped = false;

        public StateMachine()
        {
            _stateTransitions = new List<StateTransition>();

            _emptyState = new EmptyState();
        }

        public void Update()
        {
            _currentState?.Update();
        }

        public void SetState(IState newState)
        {
            var transitionData = findTransition(newState, _currentState);
            setState(transitionData);
        }

        private void setState(StateTransitionData transitionData)
        {
            _currentState?.Exit();
            transitionData?.onTransition();
            _currentState = transitionData.to;
            _currentState.Enter();
            _currentStateType = _currentState.State;
        }

        public void AddNormalTransition(StateTransition stateTransition)
        {
            _stateTransitions.Add(stateTransition);
        }

        public void AddAnyTransition(StateTransition anyTransition)
        {
            _stateTransitions.Add(anyTransition);
        }

        public void AddAnyTransitionTrigger(ref Action action, StateTransition transition)
        {
            action += () =>
            {
                if (transition.Condition() && !_currentState.Equals(transition.To))
                {
                    SetState(transition.To);
                    transition.OnTransition?.Invoke();
                }
            };
        }

        public void AddNormalTransitionTrigger(ref Action action, StateTransition transition)
        {
            action += () =>
            {
                if (transition.Condition() && _currentState.Equals(transition.From) && !_currentState.Equals(transition.To))
                {
                    SetState(transition.To);
                    transition.OnTransition?.Invoke();
                }
            };
        }

        public void DropMachine(bool isDropped)
        {
            if (isDropped)
            {
                SetState(_emptyState);
            }

            _isDropped = isDropped;
        }

        private StateTransitionData checkTransitions() // Fix here. if the state doesn't contain from, but not condition met, it's overriding the bottom.
        {
            foreach (var transition in _stateTransitions.OrderByDescending(t => t.Priority))
            {
                if (transition.From != null)
                {
                    if (transition.From.Equals(_currentState))
                    {
                        if (transition.Condition.Invoke())
                        {
                            return new StateTransitionData(transition.To, transition.OnTransition);
                        }

                        return null;
                    }
                }

                else
                {
                    if (transition.Condition.Invoke())
                    {
                        return new StateTransitionData(transition.To, transition.OnTransition);
                    }
                }
            }

            return null;
        }

        private StateTransitionData findTransition(IState to, IState from = null)
        {
            var transition = _stateTransitions.FirstOrDefault(
                t => t.To.Equals(to) /*&& object.Equals(t.From, from)*/
            );

            return transition != null
                ? new StateTransitionData(to, transition.OnTransition)
                : null;
        }
    }

    public class StateTransition
    {
        private IState _from = null;
        private IState _to;
        private Func<bool> _condition;
        private Action onTransition;
        private int _priority { get; set; }

        public StateTransition(IState from, IState to, Func<bool> condition, int priority)
        {
            _from = from;
            _to = to;
            _condition = condition;
            _priority = priority;
        }

        public IState From
        {
            get { return _from; }
        }

        public IState To
        {
            get { return _to; }
        }

        public Func<bool> Condition
        {
            get { return _condition; }
        }

        public Action OnTransition
        {
            get
            {
                return onTransition;
            }
        }
        public int Priority
        {
            get
            {
                return _priority;
            }
        }
        public StateTransition(IState from, IState to, Func<bool> condition)
        {
            _from = from;
            _to = to;
            _condition = condition;
        }

        public StateTransition(IState to, Func<bool> condition, int priority)
        {
            _to = to;
            _condition = condition;
            _priority = priority;
        }

        public StateTransition(IState to, Func<bool> condition)
        {
            _to = to;
            _condition = condition;
        }

        public StateTransition(IState from, IState to)
        {
            _from = from;
            _to = to;
        }

        public StateTransition(IState to)
        {
            _to = to;
        }

        public void SetOnTransition(Action transitionAction)
        {
            onTransition = transitionAction;
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
