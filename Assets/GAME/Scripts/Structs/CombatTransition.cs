public struct CombatTransition
{
    public CombatType From;  // Previous Combat state
    public CombatType To;    // New Combat state

    public CombatTransition(CombatType from, CombatType to)
    {
        From = from;
        To = to;
    }
    // Optionally, add timing or extra data if needed:
    // public float Time;        // Time of transition
    // public CombatTransition Snapshot; // The full snapshot at transition (optional)
}
