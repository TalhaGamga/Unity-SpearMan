using System.Collections.Generic;
using UnityEngine;

public class CharacterHub : MonoBehaviour
{
    [SerializeField] AnimatorSystem _animatorSystem;
    [SerializeField] MovementManager _movementManager;
    [SerializeField] CombatManager _combatManager;

    private List<Component> _modules;

    private void Awake()
    {
        _modules = new List<Component>()
        {
            _animatorSystem,
            _movementManager,
            _combatManager
        };
    }

    private void Start()
    {
        foreach (var module in _modules)
        {
            if (module is IInitializable<CharacterHub> initializable)
            {
                initializable.Initialize(this);
            }
        }
    }

    public T GetModule<T>() where T : class
    {
        foreach (var m in _modules)
        {
            if (m is T t) return t;
        }

        return null;
    }
}