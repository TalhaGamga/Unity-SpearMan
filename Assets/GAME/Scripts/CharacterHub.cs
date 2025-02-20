using UnityEngine;

public class CharacterHub : MonoBehaviour
{
    IMovementManager movementController;
    ICombatManager combatManager;

    private void Start()
    {
        movementController = GetComponent<MovementManager>();
        combatManager = GetComponent<CombatManager>();
    }
}