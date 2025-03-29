using System;
using System.Collections.Generic;

public class RifleCombatAnimationHandler : IRifleCombatAnimationHandler
{
    public Dictionary<string, Action> AnimationEvents { get; }

    public event Action OnReloaded;

    public void InvokeOnReloaded()
    {
        OnReloaded?.Invoke();
    }
}