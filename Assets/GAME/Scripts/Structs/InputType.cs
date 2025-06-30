using UnityEngine;

public struct InputType
{
    public PlayerAction Action;
    public bool IsHeld;
    public bool WasPresseedThisFrame;
    public bool WasReleasedThisFrame;
    public float Value;
    public Vector2 Direction;
}