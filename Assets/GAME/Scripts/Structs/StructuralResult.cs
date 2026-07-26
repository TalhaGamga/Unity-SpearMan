using System.Collections.Generic;
using UnityEngine;

public readonly struct StructuralResult
{
    public bool Succeeded { get; }
    public IReadOnlyList<GameObject> Targets { get; }

    public StructuralResult(
        bool succeeded,
        IReadOnlyList<GameObject> targets)
    {
        Succeeded = succeeded;
        Targets = targets;
    }
}
