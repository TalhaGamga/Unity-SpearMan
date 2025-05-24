using R3;
using UnityEngine;

public class SpearCombat : ICombat<ISpear>
{
    private CombatManager _combatManager;

    public ISpear Weapon { get; set; }

    public SpearCombat(ISpear spear)
    {
        Weapon = spear;
    }

    public void Init(CombatManager combatManager, IHumanoidCombatPromptReceiver promptReceiver)
    {
        _combatManager = combatManager;
        Debug.Log("Spear combat has been initialized");
    }

    public void Tick()
    {
        Debug.Log("Spear Combat");
    }

    public void FixedTick()
    {
    }

    public void End()
    {
        Debug.Log("Spear combat has ended");
    }

    public void Init()
    {
    }

    public void Init(IHumanoidCombatPromptReceiver promptReceiver)
    {
    }

    public void Enable()
    {
        throw new System.NotImplementedException();
    } 

    public void Disable()
    {
        throw new System.NotImplementedException();
    }

    public void Init(IHumanoidCombatPromptReceiver promptReceiver, CombatManager combatManager)
    {
        throw new System.NotImplementedException();
    }

    public Observable<(bool Allowed, string Reason)> ObserveCapability(Capability capability)
    {
        throw new System.NotImplementedException();
    }
}