using UnityEngine;

public struct MovementTransition
{
    public MovementType From;  // Previous movement state
    public MovementType To;    // New movement state

    public MovementTransition(MovementType from, MovementType to)
    {
        From = from;
        To = to;
    }

    // Optionally, add timing or extra data if needed:
    // public float Time;        // Time of transition
    // public MovementSnapshot Snapshot; // The full snapshot at transition (optional)
}
