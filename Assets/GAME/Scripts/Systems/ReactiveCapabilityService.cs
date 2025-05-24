using System;
using System.Collections.Generic;
using System.Linq;
using R3;

public class ReactiveCapabilityService
{
    private readonly List<IReactiveCapabilityProvider> _providers = new();

    public void RegisterProvider(IReactiveCapabilityProvider provider)
    {
        if (!_providers.Contains(provider))
        {
            _providers.Add(provider);
        }
    }

    public void UnregisterProvider(IReactiveCapabilityProvider provider)
    {
        _providers.Remove(provider);
    }

    public Observable<(bool Allowed, List<string> Reasons)> ObserveCapability(Capability capability)
    {
        var streams = _providers.Select(p => p.ObserveCapability(capability)).ToList();

        if (streams.Count == 0)
            return Observable.Return((true, new List<string>()));

        return Observable
            .CombineLatest(streams)
            .Select(values =>
            {
                bool allowed = values.All(v => v.Allowed);
                var reasons = values.Where(v => !v.Allowed).Select(v => v.Reason).ToList();
                return (allowed, reasons);
            });
    }
}