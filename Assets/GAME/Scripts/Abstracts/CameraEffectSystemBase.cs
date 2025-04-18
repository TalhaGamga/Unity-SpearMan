using System.Collections.Generic;
using UnityEngine;

public abstract class CameraEffectSystemBase<T> : ICameraEffectSystem<T>
{
    public List<ICameraEffect<T>> ActiveEffects => _activeEffects;

    private List<ICameraEffect<T>> _activeEffects = new();

    public void Inject(ICameraEffect<T> effect)
    {
        _activeEffects?.Add(effect);
    }

    public void Tick(T t)
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = _activeEffects[i];
            effect.Tick(Time.deltaTime);
            effect.Apply(t);

            if (effect.IsFinished)
            {
                _activeEffects.RemoveAt(i);
            }
        }
    }
}