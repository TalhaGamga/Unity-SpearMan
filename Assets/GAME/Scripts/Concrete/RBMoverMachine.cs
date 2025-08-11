using R3;
using UnityEngine;

public class RBMoverMachine : IMover
{
    public MovementType CurrentState => throw new System.NotImplementedException();

    public void End()
    {
        throw new System.NotImplementedException();
    }

    public void HandleAction(MovementAction action)
    {
        throw new System.NotImplementedException();
    }

    public void HandleRootMotion(Vector3 delta)
    {
        throw new System.NotImplementedException();
    }

    public void Init(IMovementManager movementManager, BehaviorSubject<MovementSnapshot> SnapshotStream, Subject<MovementTransition> TransitionStream)
    {
        throw new System.NotImplementedException();
    }

    public void UpdateMover(float deltaTime)
    {
        throw new System.NotImplementedException();
    }
}