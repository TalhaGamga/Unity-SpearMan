using UnityEngine;

public class RifleCombat : IRifleCombat
{
    private CombatManager _combatManager;

    public IRifle Weapon { get; set; }

    public RifleCombat(IRifle rifle)
    {
        Weapon = rifle;
    }

    public void Init(CombatManager combatManager)
    {
        _combatManager = combatManager;
    }

    public void Tick()
    {
        Debug.Log("Rifle Combat Tick");
        Weapon.Tick();
    }

    public void FixedTick()
    {
        Debug.Log("Rifle Combat FixedTick");
        Weapon.FixedTick();
    }


    public void End()
    {
    }
}