using Movement;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterImpactable : MonoBehaviour, IImpactable
{
    [SerializeField] private MovementManager _movementManager;

    private void Awake()
    {
        if (_movementManager == null)
            _movementManager = GetComponent<MovementManager>();

        if (_movementManager == null)
        {
            Debug.LogError(
                $"{name}: CharacterImpactable requires a MovementManager.",
                this
            );
        }
    }

    private void Reset()
    {
        _movementManager = GetComponent<MovementManager>();
    }

    public void ApplyImpact(ImpactData impact)
    {
        _movementManager?.HandleImpact(impact);
    }
}
