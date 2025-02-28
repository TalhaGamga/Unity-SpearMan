using System;
using System.Collections.Generic;
using UnityEngine;

public interface IMovementManager
{
    void SetSpeedModifier(float newModifier);
    void SetJumpModifier(float newModifier);
    bool IsGrounded();
    void SetMover(IMover newMover);
}

public interface ICombatManager
{
    void SetCombat(ICombatBase newCombat);
    void UnsettleCombat(ICombatBase combat);
    public float CombatSpeedModifier { get; set; }
    public float DamageModifier { get; set; }
    public float RangedAttackModifier { get; set; }
    public float AccuracyModifier { get; set; }
    public float CritModifier { get; set; }
}

public interface ICombatBase : ITickable, IFixedTickable
{
    void Init(CombatManager combatManager);
    void End();
}
public interface ICombat<T> : ICombatBase where T : IWeapon
{
    public T Weapon { get; set; }
}

public interface IFirearmCombat<T> : ICombat<T> where T : IFirearm { }

public interface IRifleCombat : IFirearmCombat<IRifle> { }

public interface IWeapon : IEquipable, ITickable, IFixedTickable
{
    void SetCombat(ICombatManager CombatManager);
}

public interface IFirearm : IWeapon
{
    public IFireSystem FireSystem { get; }
    public IRecoilSystem RecoilSystem { get; }
    public IAimSystem AimSystem { get; }
    public IAmmoSystem AmmoSystem { get; }
}

public interface IRifle : IFirearm
{

}

public interface ISpear : IWeapon
{
}

public interface IMover : ITickable, IFixedTickable
{
    void Init(MovementManager movementManager);
    void TriggerJump();
    void CancelJump();
    void End();
}

public enum StateType
{
    Any,
    Idle,
    Move,
    Jump,
    Fall
}

public interface IHumanoidMovementPromptReceiver
{
    Action<Vector2> OnMoveInput { get; set; }
    Action OnJumpInput { get; set; }
    Action OnJumpCancel { get; set; }
}

public interface IHumanoidCombatPromptReceiver { }

public interface ICharacterPromptReceiver : IHumanoidMovementPromptReceiver, IHumanoidCombatPromptReceiver { }

public interface IEquipmentManager
{
    public Transform Hand { get; }
    void Equip(IEquipable equipable);
    void Unequip(IEquipable equipable);
    void Pickup();
}

public interface IEquipable
{
    void Equip(IEquipmentManager equipmentManager);
    void Unequip(IEquipmentManager equipmentManager);
}

public interface ITickable
{
    void Tick();
}

public interface IFixedTickable
{
    void FixedTick();
}

public interface IInventoryItem
{
    string ItemName { get; }
    Sprite ItemIcon { get; }
}

public interface IStackableItem : IInventoryItem
{
    int MaxStackSize { get; set; }
    bool CanStackWith(IStackableItem otherItem);
}

public interface IEquipableInventoryItem : IInventoryItem
{
    GameObject WeaponPrefab { get; }
    void Use(IEquipmentManager EquipmentManager);
}

public interface ICombatInventoryItem : IEquipableInventoryItem
{
    void Use(ICombatManager combatManager);
}


public interface IInventory
{
    int Capacity { get; }
    IReadOnlyList<IInventorySlot> Slots { get; }

    bool AddItem(IInventoryItem item, int amount = 1);
    bool RemoveItem(IInventoryItem item, int amount = 1);
    bool HasItem(IInventoryItem item);
    IInventoryItem GetItem(string itemName);
    void ClearInventory();
}

public interface IInventorySlot
{
    IInventoryItem Item { get; }
    public bool IsEmpty { get; }
    public int StackCount { get; }
    public int MaxStackSize { get; }
    public Action OnSlotUpdated { get; set; }

    void SetItem(IInventoryItem item, int count = 1);
    void AddToStack(int cout = 1);
    void RemoveFromStack(int cout = 1);
    void ClearSlot();
    void UseItem();
}

public interface IInventoryUI
{
    void Initialize(IInventory inventory);
    void RefreshUI();
}

public interface IInventorySlotUI
{
    void SetSlot(IInventorySlot InventorySlot);
    void UpdateUI();
    public void OnSlotClicked();
    void ClearSlot();
}

public interface IInventoryManager
{
    public IInventory Inventory { get; }
    public Action OnInventoryLoaded { get; set; }
}

public interface IFireSystem
{
    public IProjectileSystem ProjectileSystem { get; }
    public Action OnFired { get; set; }
}

public interface IProjectileSystem
{
    IProjectile CreateProjectile();
    public Action OnProjectileCreated { get; set; }
}

public interface IAmmoSystem
{
    public int MagazinwAmmo { get; set; }
    public int MagazineCount { get; set; }
    void ResetMagazine();

    public Action OnMagazineConsumed { get; set; }
}

public interface IRecoilSystem
{
    public int KickbackAmount { get; set; }
    public Action<float> OnKickback { get; set; }
}

public interface IAimSystem
{
    public void SetAccuracyModifier();
}

public interface IProjectile
{
    void Fire(Vector2 origin, Vector2 direction, float speed, float damage);
}

public interface IPhysicalProjectile : IProjectile
{

}

public interface IHitscanProjectile : IProjectile
{

}