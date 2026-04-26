using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Combat/Attack Database")]
public class AttackDatabase : ScriptableObject
{
    [SerializeField] private List<AttackDefinition> _attacks;

    private Dictionary<string, AttackDefinition> _lookup;

    private void OnEnable()
    {
        _lookup = new Dictionary<string, AttackDefinition>();

        foreach (var attack in _attacks)
        {
            if (attack == null || string.IsNullOrWhiteSpace(attack.Key))
                continue;

            _lookup[attack.Key] = attack;
        }
    }

    public bool TryGet(string key, out AttackDefinition attack)
    {
        if (_lookup == null)
            OnEnable();

        return _lookup.TryGetValue(key, out attack);
    }
}