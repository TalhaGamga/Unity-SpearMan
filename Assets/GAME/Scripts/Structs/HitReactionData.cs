using UnityEngine;

public struct HitReactionData
{
    public HitReactionType Type;
    public Vector3 Direction;
    public float Force;
    public float Lift;
    public float HitStop;
    public bool CausesUngrounded;
    public bool CanChainFromAir;
}