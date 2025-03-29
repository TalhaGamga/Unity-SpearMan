using UnityEngine;

public class AnimatorSystem : MonoBehaviour, IAnimatorSystem
{
    private IAnimationHandler AnimationHandler => _animationHandler;
    private IAnimationHandler _animationHandler;

    public void SetAnimationHandler(IAnimationHandler AnimationHandler)
    {
        _animationHandler = AnimationHandler;
    }

    public void InvokeAnimationEvent(string EventName)
    {
        if (_animationHandler.AnimationEvents.ContainsKey(EventName))
        {
            _animationHandler.AnimationEvents[EventName]?.Invoke();
        }
    }
}