public struct CombatTransition
{
    public CombatType From;  // Previous Combat state
    public CombatType To;    // New Combat state

    // Optionally, add timing or extra data if needed:
    // public float Time;        // Time of transition
    // public CombatTransition Snapshot; // The full snapshot at transition (optional)
}
