using R3;
using UnityEngine;

public class CharacterHub : MonoBehaviour
{
    [SerializeField] AnimatorSystem _animatorSystem;
    [SerializeField] MovementManager _movementManager;
    [SerializeField] CombatManager _combatManager;

    private void Start()
    {
        _animatorSystem.RegisterStreams(_movementManager.Stream.AsSystemObservable(),_combatManager.Stream.AsSystemObservable());
    }
}