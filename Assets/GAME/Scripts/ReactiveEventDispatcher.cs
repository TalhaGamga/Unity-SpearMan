using R3;
using System.Collections.Generic;
using UnityEngine;

public sealed class ReactiveEventDispatcher : MonoBehaviour
{
    public void Apply(IReactiveEventSource source, GameObject target)
    {
        if (source == null || target == null)
            return;

        var execution = new ReactiveEventExecution(target);
        source.Stream().Subscribe(execution.Enqueue);
    }

    private sealed class ReactiveEventExecution
    {
        private readonly Queue<IReactiveEvent> _events = new();
        private IReadOnlyList<TargetContext> _targets;
        private bool _isExecuting;

        public ReactiveEventExecution(GameObject target)
        {
            _targets = new[] { new TargetContext(target) };
        }

        public void Enqueue(IReactiveEvent reactiveEvent)
        {
            if (reactiveEvent == null)
                return;

            _events.Enqueue(reactiveEvent);
            ExecuteNext();
        }

        private void ExecuteNext()
        {
            if (_isExecuting || _events.Count == 0)
                return;

            _isExecuting = true;
            IReactiveEvent reactiveEvent = _events.Dequeue();
            reactiveEvent.Consume(_targets, OnEventCompleted);
        }

        private void OnEventCompleted(IReadOnlyList<TargetContext> targets)
        {
            _targets = targets ?? System.Array.Empty<TargetContext>();
            _isExecuting = false;
            ExecuteNext();
        }
    }
}