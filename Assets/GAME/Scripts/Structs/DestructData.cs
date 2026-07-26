using UnityEngine;

[System.Serializable]
public readonly struct DestructData
{
    public Vector3 Direction { get; }
    public Vector3 Point { get; }

    public DestructData(Vector3 direction, Vector3 point)
    {
        Direction = direction.sqrMagnitude > Mathf.Epsilon
            ? direction.normalized
            : Vector3.zero;
        Point = point;
    }
}