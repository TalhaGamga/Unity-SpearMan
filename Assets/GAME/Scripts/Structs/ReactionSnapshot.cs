public readonly struct ReactionSnapshot
{
    public readonly ReactionType State; public readonly float Impact;
    public ReactionSnapshot(ReactionType state, float impact) { State = state; Impact = impact; }
    public static ReactionSnapshot Default => new ReactionSnapshot(ReactionType.None, 0);
}
