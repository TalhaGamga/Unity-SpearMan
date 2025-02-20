using UnityEngine;

public class SpearCombat : ICombat
{
    private CombatManager combatManager;
    private ISpear _spear;

    public SpearCombat(ISpear spear)
    {
        _spear = spear;
    }

    public void Init(ICombatManager combatManager)
    {
        Debug.Log("Spear combat has been initialized");
    }

    public void Tick()
    {
        Debug.Log("Spear Combat");
        _spear?.Tick();
    }

    public void FixedTick()
    {
    }

    public void End()
    {
        Debug.Log("Spear combat has ended");
    }
}