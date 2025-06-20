using R3;

public class ReactionManager
{
    public Subject<ReactionSnapshot> SnapshotStream { get; } = new();
}