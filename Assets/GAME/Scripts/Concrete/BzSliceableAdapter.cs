using BzKovSoft.ObjectSlicer;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("GAME/Reactive Capabilities/Bz Sliceable")]
[DisallowMultipleComponent]
public sealed class BzSliceableAdapter : MonoBehaviour, ISliceable
{
    private IBzSliceable _sliceable;
    private bool _slicePending;

    private void Awake()
    {
        _sliceable = GetComponent<IBzSliceable>();
        _slicePending = false;
    }

    public void Slice(
        SliceData data,
        Action<StructuralResult> completed)
    {
        if (_sliceable == null || !data.IsValid || _slicePending)
        {
            completed?.Invoke(default);
            return;
        }

        _slicePending = true;
        Vector3 localPoint = transform.InverseTransformPoint(data.Point);
        Vector3 localNormal = transform.InverseTransformDirection(data.PlaneNormal);
        StartCoroutine(SliceAfterPhysics(localPoint, localNormal, completed));
    }

    private IEnumerator SliceAfterPhysics(
        Vector3 localPoint,
        Vector3 localNormal,
        Action<StructuralResult> completed)
    {
        yield return new WaitForFixedUpdate();

        Vector3 point = transform.TransformPoint(localPoint);
        Vector3 normal = transform.TransformDirection(localNormal).normalized;
        var plane = new Plane(normal, point);
        _sliceable.Slice(
            plane,
            result => HandleSliceCompleted(result, completed)
        );
    }

    private void HandleSliceCompleted(
        BzSliceTryResult result,
        Action<StructuralResult> completed)
    {
        if (result == null || !result.sliced)
        {
            _slicePending = false;
            completed?.Invoke(default);
            return;
        }

        StartCoroutine(CompleteAfterComponents(result, completed));
    }

    private IEnumerator CompleteAfterComponents(
        BzSliceTryResult result,
        Action<StructuralResult> completed)
    {
        // Bz finishes replacing generated components on following frames.
        yield return null;
        yield return new WaitForFixedUpdate();

        var pieces = new List<GameObject>(2);
        if (result.outObjectNeg != null)
            pieces.Add(result.outObjectNeg);
        if (result.outObjectPos != null)
            pieces.Add(result.outObjectPos);

        _slicePending = false;
        completed?.Invoke(new StructuralResult(pieces.Count > 0, pieces));
    }
}