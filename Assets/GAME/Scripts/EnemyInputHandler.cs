using R3;
using UnityEngine;

public class EnemyInputHandler : MonoBehaviour, IInputHandler
{
    public BehaviorSubject<InputSnapshot> InputSnapshotStream { get; }
        = new BehaviorSubject<InputSnapshot>(InputSnapshot.Empty);
}