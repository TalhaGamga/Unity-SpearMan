using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StateMachine
{
    public StateType currentState;

    private List<StateTransition> transitions;

    public StateMachine(StateType initialState)
    {
        currentState = initialState;

        transitions = new List<StateTransition>();
    }

    public void Update()
    {
        StateTransitionData transitionData = checkTransitions();
        if (transitionData != null)
        {
            setState(transitionData);
        }
    }

    public void AddTransition(StateTransition stateTransition)
    {
        transitions.Add(stateTransition);
    }

    private void setState(StateType newState)
    {
        currentState = newState;
    }

    private void setState(StateTransitionData transitionData)
    {
        if (transitionData.To.Equals(currentState))
        {
            return;
        }

        transitionData.OnTransition?.Invoke();
        currentState = transitionData.To;
    }

    private StateTransitionData checkTransitions()
    {
        foreach (var transition in transitions.OrderByDescending(t => t.Priority))
        {
            if (transition.From != StateType.Any)
            {
                if (transition.From.Equals(currentState))
                {
                    if (transition.Condition.Invoke())
                    {
                        return new StateTransitionData(transition.To, transition.OnTransition);
                    }
                }
            }

            if (transition.From == StateType.Any)
            {
                if (transition.Condition.Invoke())
                {
                    Debug.Log(transition.From);

                    return new StateTransitionData(transition.To, transition.OnTransition);
                }
            }
        }

        return null;
    }
}