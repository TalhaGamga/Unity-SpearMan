public class EmptyState : IState
{
    public string State => throw new System.NotImplementedException();

    public void Enter()
    {
    }

    public void Exit()
    {
    }

    public void Tick()
    {
    }

    public void Update()
    {
    }
}