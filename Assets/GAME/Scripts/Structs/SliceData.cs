using UnityEngine;

[System.Serializable]
public readonly struct SliceData
{
    private const float MinNormalSqrMagnitude = 0.0001f;

    public Vector3 Point { get; }
    public Vector3 PlaneNormal { get; }
    public bool IsValid => PlaneNormal.sqrMagnitude > MinNormalSqrMagnitude;

    public SliceData(Vector3 point, Vector3 planeNormal)
    {
        Point = point;
        PlaneNormal = planeNormal.sqrMagnitude > MinNormalSqrMagnitude
            ? planeNormal.normalized
            : Vector3.zero;
    }
}
