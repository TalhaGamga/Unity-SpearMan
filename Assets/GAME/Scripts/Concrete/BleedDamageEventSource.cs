using R3;
using System;
using UnityEngine;

public sealed class BleedDamageEventSource : IDamageEventSource
{
    private readonly float _perTick;
    private readonly float _interval;
    private readonly int _ticks;
    private readonly string _fx;

    public BleedDamageEventSource(float perTick, float interval, int ticks, string fx)
    {
        _perTick = perTick;
        _interval = interval;
        _ticks = ticks;
        _fx = fx;
    }

    public Observable<IDamageEvent> Stream(GameObject target)
    {
        return Observable.Interval(TimeSpan.FromSeconds(_interval))
            .Take(_ticks)
            .Select(_ => (IDamageEvent)new DamageEvent(_perTick));
    }
}