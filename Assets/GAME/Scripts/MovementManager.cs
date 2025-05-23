using R3;
using UnityEngine;

public class MovementManager : MonoBehaviour, IMovementManager
{
    public Transform CharacterModelTransform => throw new System.NotImplementedException();

    public Observable<MovementSnapshot> Stream => throw new System.NotImplementedException();

    public bool IsGrounded()
    {
        throw new System.NotImplementedException();
    }

    public void SetJumpModifier(float newModifier)
    {
        throw new System.NotImplementedException();
    }

    public void SetMover(IMover newMover)
    {
        throw new System.NotImplementedException();
    }

    public void SetSpeedModifier(float newModifier)
    {
        throw new System.NotImplementedException();
    }
}