using UnityEngine;

public class NoCombat 
{
    public void Init(CombatManager combatManager)
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