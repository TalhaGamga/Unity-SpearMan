using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

public interface IReactiveCapabilityProvider
{
    Observable<(bool Allowed, string Reason)> ObserveCapability(Capability capability);
}

public interface IMovementInputReceiver
{
    void HandleInput(MovementAction action);
    void SetMoveInput(Vector2 move);
}

public interface IMovementStateProvider
{
    bool IsGrounded { get; }
    bool CurrentSpeed { get; }
}

public interface IMovementManager
{
    public Transform CharacterModelTransform { get; }
    public Observable<MovementSnapshot> Stream { get; }
    
    void SetSpeedModifier(float newModifier);
    void SetJumpModifier(float newModifier);
    bool IsGrounded();
    void SetMover(IMover newMover);
}

public interface ICombatManager
{
    public Observable<CombatSnapshot> Stream { get; }
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
    void Init(IHumanoidCombatPromptReceiver promptReceiver, CombatManager combatManager);
    void Enable();
    void Disable();
}

public interface ICombat<T> : IReactiveCapabilityProvider, ICombatBase where T : IWeapon
{
    public T Weapon { get; }
}

public interface IFirearmCombat<T> : ICombat<T> where T : IFirearm
{
    void AttemptFire();
    void StopFiring();
    void Reload();
    void TakeAim(Vector2 aimInput);
}

public interface IRifleCombat : IFirearmCombat<IRifle> { }

public interface IWeapon : IEquipable
{
    void SetCombat(ICombatManager combatManager);
    public Transform WeaponTransform { get; }
}

public interface IFirearm : IWeapon
{
    public FirearmCursorDataSO FirearmCursorData { get; }
    public Transform FirePoint { get; }
    public IFireTriggerSystem FireSystem { get; }
    public IRecoilSystem RecoilSystem { get; }
    public IAimSystem AimSystem { get; }
    public IAmmoSystem AmmoSystem { get; }
    public IProjectileSystem ProjectileSystem { get; }
    public IBulletTrail BulletTrail { get; }
}

public interface IRifle : IFirearm
{
}

public interface ISpear : IWeapon
{
}

public interface IMover
{
    void Init(IMovementManager movementManager);
    void End();
    public void HandleInput(MovementAction action);
    void SetMoveInput(Vector2 move);
    public void UpdateMover(float deltaTime);
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

public interface IFireTriggerSystem : ITickable
{
    public event Action OnFireAttempt;
    public void Init();
    public bool CanFire { get; }
    public float FireRate { get; }

    void AttemptFire();
    void StopFire();
    //public Action OnMissed { get; set; }
}

public interface IAmmoSystem
{
    event Action<float> OnReloadStarted;
    event Action OnAmmoConsumed;
    void Init();
    void TryConsumeAmmo();
    void Reload();
    void FinishReload();
}

public interface IRecoilSystem
{
    void Init();
    void KickBack();
    event Action<float, float> OnKickback;
}

public interface IKickbackReceiver
{
    void ApplyKickback(float strength, float recoveryDelay);
}

public interface IAimSystem : ITickable
{
    public event Action<Vector2> OnAiming;
    void Init(Camera camera);
    void TakeAimInput(Vector2 aimTarget, Transform weapon);
}

public interface IProjectileSystem
{
    public event Action<ProjectileHitInfo> OnProjectileGatheredInfo;
    public void Init();
    IProjectile CreateProjectile();
}
public interface IHitscanProjectileSystem : IProjectileSystem
{
}

public interface IProjectile
{
    //event Action OnHit;
    event Action<ProjectileHitInfo> OnProjectileFiredAndHit;
    void Fire(Vector3 origin, Vector3 direction, float speed);
}

public struct ProjectileHitInfo
{
    public Vector3 FirePoint;
    public Vector3 EndPoint;
    public Vector3 Direction;
    public GameObject HitObject;
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
    void DealDamage(IDamageable target, float damage, Vector3 hitPoint, float critRate, float impulse);
}

public interface ICameraTargeting
{
    Transform GetFollowTarget();
    Transform GetLookAtTarget(); // Can be null
}

public interface ICameraEffect<T>
{
    void Tick(float deltaTime);
    bool IsFinished { get; }
    void Apply(T t);
}

public interface ICameraEffectSystem<T>
{
    public List<ICameraEffect<T>> ActiveEffects { get; }
    void Inject(ICameraEffect<T> effect);
    void Tick(T t);
}

public interface IDamageable
{
    void ReceiveDamage(float amount);
}
public interface IKnockbackable
{
    void ApplyForce(Vector3 vector3);
}

public interface IDestructible
{
    void Break();
}

public interface IReactiveEvent
{
    void Consume(TargetContext ctx);
}

public interface IDamageEventSource
{
    Observable<IReactiveEvent> Stream(GameObject target);
}