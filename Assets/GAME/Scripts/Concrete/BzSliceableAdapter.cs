using BzKovSoft.ObjectSlicer;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BzSliceableAdapter : MonoBehaviour, ISliceable
{
    private IBzSliceable _sliceable;

    private void Awake()
    {
        _sliceable = GetComponent<IBzSliceable>();
    }

    public void Slice(SliceData data)
    {
        if (_sliceable == null || !data.IsValid)
            return;

        var plane = new Plane(data.PlaneNormal, data.Point);
        _sliceable.Slice(plane, null);
    }
}
