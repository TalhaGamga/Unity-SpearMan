using UnityEngine;

public class SpearCombat : ICombat<ISpear>
{
    private CombatManager _combatManager;

    public ISpear Weapon { get; set; }

    public SpearCombat(ISpear spear)
    {
        Weapon = spear;
    }

    public void Init(CombatManager combatManager)
    {
        _combatManager = combatManager;
        Debug.Log("Spear combat has been initialized");
    }

    public void Tick()
    {
        Debug.Log("Spear Combat");
        Weapon?.Tick();
    }

    public void FixedTick()
    {
    }

    public void End()
    {
        Debug.Log("Spear combat has ended");
    }
}