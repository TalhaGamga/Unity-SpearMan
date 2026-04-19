public struct ReactionTransition
{
    public ReactionType From;
    public ReactionType To;

    public ReactionTransition(ReactionType from, ReactionType to)
    {
        From = from;
        To = to;
    }
}