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
    void Init(CombatManager combatManager, IHumanoidCombatPromptReceiver promptReceiver);
    void End();
}

public interface ICombat<T> : ICombatBase where T : IWeapon
{
    public T Weapon { get; }
}

public interface IFirearmCombat<T> : ICombat<T> where T : IFirearm
{
    void Fire();
    void StopFiring();
    void Reload();
    void Aim(Vector2 aimInput);
}

public interface IRifleCombat : IFirearmCombat<IRifle> { }

public interface IWeapon : IEquipable, ITickable, IFixedTickable
{
    public Transform WeaponTransform { get; }
    void SetCombat(ICombatManager combatManager);
}

public interface IFirearm : IWeapon
{
    public Transform FirePoint { get; }
    public IFireSystem FireSystem { get; }
    public IRecoilSystem RecoilSystem { get; }
    public IAimSystem AimSystem { get; }
    public IAmmoSystem AmmoSystem { get; }
    public IProjectileSystem ProjectileSystem { get; }
    public IBulletDamageDealerSystem DamageDealerSystem { get; }
    public IBulletTrail BulletTrail { get; }
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
    event Action<Vector2> OnMoveInput;
    event Action OnJumpInput;
    event Action OnJumpCancel;
    public void InvokeMoveInput(Vector2 input);
    public void InvokeJumpInput();
    public void InvokeJumpCancel();
}

public interface IHumanoidCombatPromptReceiver
{
    event Action OnPrimaryCombatInput;
    event Action OnSecondaryCombatInput;
    event Action OnPrimaryCombatCancel;
    event Action OnSecondaryCombatCancel;
    event Action OnReloadInput;
    event Action<Vector2> OnAimInput;

    public void InvokePrimaryCombatInput();
    public void InvokeSecondaryCombatInput();
    public void InvokePrimaryCombatCancel();
    public void InvokeSecondaryCombatCancel();
    public void InvokeOnReloadInput();
    public void InvokeOnAimInput(Vector2 aim);
}

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
    void Use(IEquipmentManager equipmentManager);
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
    void SetSlot(IInventorySlot inventorySlot);
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
    public event Action OnFired;
    public void Init();
    public bool CanFire { get; }
    public float FireRate { get; }

    void Fire(IProjectile projectile, Vector3 origin, Vector3 direction);
    //public Action OnMissed { get; set; }
}

public interface IDamagable
{
    void TakeDamage(float damage);
}

public interface IAmmoSystem
{
    event Action<bool> OnReloadStateChanged;
    event Action<float> OnReloadStarted;
    event Action OnAmmoConsumed;
    public bool IsReloading { get; }
    public bool HasAmmo { get; }
    void ConsumeAmmo();
    void Reload();
    void FinishReload();
}

public interface IRecoilSystem
{
    void Init(Transform firearmTransform, List<IKickbackReceiver> kickbackReceivers);
    void KickBack();
    void RecoveryCurrentRecoil();
    public Action<float, float> OnKickback { get; set; }
}

public interface IKickbackReceiver
{
    void ApplyKickback(float strength, float recoveryDelay);
}

public interface IAimSystem : IKickbackReceiver
{
    void Init(Transform weaponTransform, Camera camera);
    void UpdateAim(Vector2 aimTarget);
    void RecoveryKickback();
    Quaternion GetAimRotation();
}

public interface IProjectileSystem
{
    public event Action<ProjectileGatheredInfo> OnProjectileGatheredInfo;
    public void Init();
    IProjectile CreateProjectile();
}
public interface IHitscanProjectileSystem : IProjectileSystem
{
}

public interface IProjectile
{
    //event Action OnHit;
    event Action<ProjectileGatheredInfo> OnProjectileFiredAndHit;
    void Fire(Vector3 origin, Vector3 direction, float speed);
}

public interface IPhysicalProjectile : IProjectile
{
    void ApplyPhysics();
}

public interface IHitscanProjectile : IProjectile
{
    public event Action<Vector3, Vector3> OnHitscanFired;
}

public interface IInputHandler
{
    void BindInputs();
    void UnbindInputs();
}

public interface IAnimatorSystem
{
    public void SetAnimationHandler(IAnimationHandler animationHandler);
    public void InvokeAnimationEvent(string eventName);
}

public interface IAnimationHandler
{
    public Dictionary<string, Action> AnimationEvents { get; }
}

public interface IRifleCombatAnimationHandler : IAnimationHandler
{
    public event Action OnReloaded;

    public void InvokeOnReloaded();
}

public interface IBulletTrail
{
    void VisualizeFire(Vector3 start, Vector3 end);
}

public interface IBulletDamageDealerSystem
{
    public void DealDamage(IDamagable target, float damage, Vector3 hitPoint, float critRate, float impulse);
}

public interface ICursor
{
    public Sprite Sprite { get; }
    public Vector2 HotSpot { get; }
}