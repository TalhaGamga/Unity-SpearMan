using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ServiceLocator : MonoBehaviour
{
    static ServiceLocator _global;
    static Dictionary<Scene, ServiceLocator> _sceneContainers;
    static List<GameObject> _tmpSceneGameObjects;

    private readonly ServiceManager _services = new ServiceManager();

    private const string k_globalServiceLocatorNamae = "ServiceLocator [Global]";
    private const string k_sceneServiecLocatorName = "ServiceLocator [Scene]";

    internal void ConfigureAsGlobal(bool dontDestroyOnLoad)
    {
        if (_global == this)
        {
            Debug.LogWarning("ServiceLocator.ConfigureAsGlobal: Already configured as global", this);
        }
        else if (_global != null)
        {
            Debug.LogError("ServiceLocator.ConfigureAsglobal: Another ServiceLocator is already configured as global", this);
        }
        else
        {
            _global = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        }
    }

    internal void ConfigureForScene()
    {
        Scene scene = gameObject.scene;

        if (_sceneContainers.ContainsKey(scene))
        {
            Debug.LogError($"ServiceLocator.ConfigureForScene: Another ServiceLocator is already configured for this scene", this);
            return;
        }

        _sceneContainers.Add(scene, this);
    }

    public static ServiceLocator Global
    {
        get
        {
            if (_global != null) return _global;

            if (FindFirstObjectByType<ServiceLocatorGlobal>() is { } found)
            {
                found.BootstrapOnDemand();
                return _global;
            }

            var container = new GameObject(k_globalServiceLocatorNamae, typeof(ServiceLocatorGlobal));
            container.AddComponent<ServiceLocator>();

            return _global;
        }
    }

    public static ServiceLocator ForSceneOf(MonoBehaviour mb)
    {
        Scene scene = mb.gameObject.scene;

        if (_sceneContainers.TryGetValue(scene, out ServiceLocator container) && container != mb)
        {
            return container;
        }

        _tmpSceneGameObjects.Clear();

        scene.GetRootGameObjects(_tmpSceneGameObjects);

        foreach (GameObject go in _tmpSceneGameObjects.Where(go => go.GetComponent<ServiceLocatorScene>() != null))
        {
            if (go.TryGetComponent(out ServiceLocatorScene bootstrapper) && bootstrapper.Container != mb)
            {
                bootstrapper.BootstrapOnDemand();
                return bootstrapper.Container;
            }
        }

        return _global;
    }

    public static ServiceLocator For(MonoBehaviour mb)
    {
        return mb.GetComponentInParent<ServiceLocator>().OrNull() ?? ForSceneOf(mb) ?? Global;
    }

    public ServiceLocator Register<T>(T service)
    {
        _services.Register(service);
        Debug.Log("Registered Service : " + service);
        return this;
    }

    public ServiceLocator Register(Type type, object service)
    {
        _services.Register(type, service);
        return this;
    }

    public ServiceLocator Get<T>(out T service) where T : class
    {
        if (tryGetService(out service)) return this;

        if (tryGetNextInHierarchy(out ServiceLocator container))
        {
            container.Get(out service);
            return this;
        }

        throw new ArgumentException($"ServiceLocator.Get: Service of type {typeof(T).FullName} not registered");
    }

    public bool tryGetNextInHierarchy(out ServiceLocator container)
    {
        if (this == _global)
        {
            container = null;
            return false;
        }

        container = transform.parent.OrNull()?.GetComponentInParent<ServiceLocator>().OrNull() ?? ForSceneOf(this);
        return container != null;
    }

    public bool tryGetService<T>(out T service) where T : class
    {
        return _services.TryGet(out service);
    }

    public bool tryGetService<T>(Type type, out T service) where T : class
    {
        return _services.TryGet(out service);
    }

    private void OnDestroy()
    {
        if (this == _global)
        {
            _global = null;
        }
        else if (_sceneContainers.ContainsValue(this))
        {
            _sceneContainers.Remove(gameObject.scene);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void resetStatics()
    {
        _global = null;
        _sceneContainers = new Dictionary<Scene, ServiceLocator>();
        _tmpSceneGameObjects = new List<GameObject>();
    }

#if UNITY_EDITOR
    [MenuItem("GameObject/ServiceLocator/Add Global")]
    static void AddGlobal()
    {
        var go = new GameObject(k_globalServiceLocatorNamae, typeof(ServiceLocatorGlobal));
    }

    [MenuItem("GameObject/ServiceLocator/Add Scene")]
    static void AddScene()
    {
        var go = new GameObject(k_sceneServiecLocatorName, typeof(ServiceLocatorScene));
    }
#endif
}