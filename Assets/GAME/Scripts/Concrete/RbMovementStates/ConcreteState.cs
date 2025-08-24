using UnityEngine.Events;

namespace Movement
{
    namespace State
    {
        public class ConcreteState : IState
        {
            public string State => "MoverState";

            public UnityEvent OnEnter { get; set; } = new UnityEvent();
            public UnityEvent OnExit { get; set; } = new UnityEvent();
            public UnityEvent OnUpdate { get; set; } = new UnityEvent();

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