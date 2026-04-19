using Movement;
using UnityEngine;

public class CharacterReactionReceiver : MonoBehaviour, IKnockbackable, IHitReactable
{
    [SerializeField] private CharacterHub _hub;
    [SerializeField] private MovementManager _movementManager;
    [SerializeField] private CombatManager _combatManager;

    public void ApplyForce(Vector3 force)
    {

    }

    public void React(HitReactionData reaction)
    {

    }
}