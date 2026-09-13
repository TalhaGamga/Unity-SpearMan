using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A weapon's visual dictionary: cue key in, effect out.
///
/// Cue keys are authored on the weapon's own animation clips, so the key space
/// is per-weapon and needs no global enum. The same <c>SlashArc</c> is a wide
/// arc on a greatsword, a thrust on a spear, and simply absent on a weapon
/// whose pack does not list it - a missing key is a silent no-op by design,
/// not an error, because a shared clip may cue effects only some weapons want.
/// </summary>
[CreateAssetMenu(
    menuName = "ScriptableObjects/Combat/Weapon Visual Pack",
    fileName = "WeaponVisualPack")]
public class WeaponVisualPack : ScriptableObject
{
    [SerializeField] private VisualCueEntry[] _cues = new VisualCueEntry[0];

    private Dictionary<string, VisualCueEntry> _lookup;

    public bool TryGetCue(string cueKey, out VisualCueEntry entry)
    {
        entry = default;

        if (string.IsNullOrWhiteSpace(cueKey))
            return false;

        if (_lookup == null)
            buildLookup();

        return _lookup.TryGetValue(cueKey, out entry);
    }

    private void OnEnable() => buildLookup();

    private void buildLookup()
    {
        // Ordinal-ignore-case: keys are hand-typed into clip event strings and
        // into this asset, in two different windows, often months apart.
        _lookup = new Dictionary<string, VisualCueEntry>(
            _cues.Length,
            System.StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _cues.Length; i++)
        {
            VisualCueEntry cue = _cues[i];

            if (string.IsNullOrWhiteSpace(cue.CueKey))
                continue;

            string key = cue.CueKey.Trim();

            if (_lookup.ContainsKey(key))
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[WeaponVisualPack] '{name}' lists cue '{key}' more than " +
                    "once; keeping the first entry.",
                    this);
#endif
                continue;
            }

            _lookup[key] = cue;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        for (int i = 0; i < _cues.Length; i++)
        {
            // A zero scale is the single easiest way to author an effect that
            // spawns, costs a pool slot, and is invisible.
            if (_cues[i].Scale == Vector3.zero)
                _cues[i].Scale = Vector3.one;

            if (_cues[i].SwingSpeedRange == Vector2.zero)
                _cues[i].SwingSpeedRange = new Vector2(2f, 14f);

            if (_cues[i].SwingStrengthRange == Vector2.zero)
                _cues[i].SwingStrengthRange = new Vector2(0.75f, 1.25f);
        }

        buildLookup();
    }
#endif
}
