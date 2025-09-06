using UnityEngine.Events;

namespace Movement
{
    namespace State
    {
        public class ConcreteState : IState
        {
            public string StateName => _stateName;

            public UnityEvent OnEnter { get; set; } = new UnityEvent();
            public UnityEvent OnExit { get; set; } = new UnityEvent();
            public UnityEvent OnUpdate { get; set; } = new UnityEvent();


            private string _stateName = "Concrete State";
            public ConcreteState(string stateName="")
            {
                if (!string.IsNullOrEmpty(stateName))
                {
                    _stateName = stateName;
                }
            }

            public void Enter()
            {
                OnEnter?.Invoke();
            }

            public void Exit()
            {
                OnExit?.Invoke();
            }

            public void Update()
            {
                OnUpdate?.Invoke();
            }
        }
    }
}