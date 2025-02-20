using System;

public class StateTransition
{
    public StateType From { get; }
    public StateType To { get; }
    public Func<bool> Condition { get; }
    public int Priority { get; set; }
    public Action OnTransition { get; set; } = null;

    public StateTransition(StateType from, StateType to, Func<bool> condition, int priority, Action onTransition)
    {
        From = from;
        To = to;
        Condition = condition ?? (() => true);
        Priority = priority;
        OnTransition = onTransition;
    }

    public static StateTransition NormalTransition(StateType from, StateType to, Func<bool> condition, int priority, Action onTransition)
    {
        return new StateTransition(from, to, condition, priority, onTransition);
    }

    public static StateTransition AnyTransition(StateType to, Func<bool> condition, int priority, Action onTransition)
    {
        return new StateTransition(default, to, condition, priority, onTransition);
    }
}

public class StateTransitionData
{
    public StateType To { get; set; }
    public Action OnTransition { get; set; }

    public StateTransitionData(StateType to, Action onTransition)
    {
        To = to;
        OnTransition = onTransition;
    }
}
