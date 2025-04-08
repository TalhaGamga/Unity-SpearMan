using System;
using System.Collections.Generic;
using UnityEngine;

public class ServiceManager
{
    public IEnumerable<object> RegisteredServices => _services.Values;

    public bool TryGet<T>(out T service) where T : class
    {
        Type type = typeof(T);
        if (_services.TryGetValue(type, out object obj))
        {
            service = obj as T;
            return true;
        }

        service = null;
        return false;
    }

    private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

    public T Get<T>() where T : class
    {
        Type type = typeof(T);
        if (_services.TryGetValue(type, out object obj))
        {
            return obj as T;
        }

        throw new ArgumentException($"ServiceManager.Get: Service of type {type.FullName} not registered");
    }

    public ServiceManager Register<T>(T service)
    {
        Type type = typeof(T);
        if (!_services.TryAdd(type, service))
        {
            Debug.LogError($"Servicemanager.Register: Service of type {type.FullName} already registered");
        }

        return this;
    }

    public ServiceManager Register(Type type, object service)
    {
        if (!type.IsInstanceOfType(service))
        {
            throw new ArgumentException("Type of service does not match type of service interface", nameof(service));
        }

        if (!_services.TryAdd(type, service))
        {
            Debug.LogError($"ServiceManager.Register: service of type {type.FullName} already registered");
        }

        return this;
    }
}