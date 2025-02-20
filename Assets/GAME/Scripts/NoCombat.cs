using UnityEngine;

public class NoCombat : ICombat
{
    public void Init(ICombatManager combatManager)
    {
    }
    public void End()
    {
    }
    public void Tick()
    {
        Debug.Log("No Combat");
    }

    public void FixedTick()
    {
    }
}