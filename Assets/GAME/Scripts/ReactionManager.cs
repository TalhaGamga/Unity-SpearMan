using R3;
using UnityEngine;

public class ReactionManager : MonoBehaviour, IReactionManager, IHitReactable
{
    public Subject<ReactionSnapshot> SnapshotStream { get; } = new();
    public Subject<ReactionTransition> TransitionStream { get; } = new();

    [SerializeField] private ReactionStateMachine _reactionMachine;

    private IReactor _currentReactor;
    private readonly CompositeDisposable _disposables = new();

    private void Awake()
    {
        SetReactor(_reactionMachine);
    }

    private void Update()
    {
        _currentReactor?.UpdateReactor(Time.deltaTime);
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
        _currentReactor?.End();
    }

    public void SetReactor(IReactor newReactor)
    {
        _currentReactor?.End();
        _currentReactor = newReactor;
        _currentReactor?.Init(this, SnapshotStream, TransitionStream);
    }

    public void HandleReaction(HitReaction reaction)
    {
        Debug.Log($"{name} received reaction: {reaction.Type}");

        _currentReactor?.HandleAction(reaction);
    }
}