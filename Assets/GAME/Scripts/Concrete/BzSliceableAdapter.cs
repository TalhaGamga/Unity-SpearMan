using BzKovSoft.ObjectSlicer;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BzSliceableAdapter : MonoBehaviour, ISliceable
{
    private const float MinVisualVolume = 0.0001f;

    private IBzSliceable _sliceable;
    private bool _slicePending;

    private void Awake()
    {
        _sliceable = GetComponent<IBzSliceable>();
        _slicePending = false;
    }

    public void Slice(SliceData data)
    {
        if (_sliceable == null || !data.IsValid || _slicePending)
            return;

        _slicePending = true;
        StartCoroutine(SliceAfterPhysics(data));
    }

    private IEnumerator SliceAfterPhysics(SliceData data)
    {
        yield return new WaitForFixedUpdate();

        var plane = new Plane(data.PlaneNormal, data.Point);
        _sliceable.Slice(
            plane,
            result => HandleSliceCompleted(result, data)
        );
    }

    private void HandleSliceCompleted(BzSliceTryResult result, SliceData data)
    {
        if (result == null || !result.sliced || data.Force <= 0f)
        {
            _slicePending = false;
            return;
        }

        StartCoroutine(ApplyPieceForces(result, data));
    }

    private IEnumerator ApplyPieceForces(BzSliceTryResult result, SliceData data)
    {
        // Bz finishes replacing the generated components next frame.
        yield return null;
        yield return new WaitForFixedUpdate();

        bool hasNegative = TryGetPieceData(
            result.outObjectNeg,
            out Rigidbody negativeBody,
            out Vector3 negativeCenter,
            out float negativeInverseSize
        );
        bool hasPositive = TryGetPieceData(
            result.outObjectPos,
            out Rigidbody positiveBody,
            out Vector3 positiveCenter,
            out float positiveInverseSize
        );

        float totalInverseSize = negativeInverseSize + positiveInverseSize;

        if (totalInverseSize > 0f)
        {
            if (hasNegative)
            {
                ApplyPieceForce(
                    negativeBody,
                    negativeCenter,
                    data,
                    negativeInverseSize / totalInverseSize,
                    -data.PlaneNormal
                );
            }

            if (hasPositive)
            {
                ApplyPieceForce(
                    positiveBody,
                    positiveCenter,
                    data,
                    positiveInverseSize / totalInverseSize,
                    data.PlaneNormal
                );
            }
        }

        _slicePending = false;
    }

    private static bool TryGetPieceData(
        GameObject piece,
        out Rigidbody body,
        out Vector3 center,
        out float inverseSize)
    {
        body = piece != null ? piece.GetComponent<Rigidbody>() : null;
        center = piece != null ? piece.transform.position : Vector3.zero;
        inverseSize = 0f;

        if (body == null || body.isKinematic)
            return false;

        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds visualBounds = default;

        foreach (Renderer pieceRenderer in renderers)
        {
            if (pieceRenderer == null || !pieceRenderer.enabled)
                continue;

            if (!hasBounds)
            {
                visualBounds = pieceRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                visualBounds.Encapsulate(pieceRenderer.bounds);
            }
        }

        if (!hasBounds)
        {
            inverseSize = 1f;
            return true;
        }

        center = visualBounds.center;
        Vector3 size = visualBounds.size;
        float fakeVolume = Mathf.Max(
            size.x * size.y * size.z,
            MinVisualVolume
        );
        inverseSize = 1f / fakeVolume;
        return true;
    }

    private static void ApplyPieceForce(
        Rigidbody body,
        Vector3 center,
        SliceData data,
        float forceShare,
        Vector3 fallbackDirection)
    {
        if (body == null || forceShare <= 0f)
            return;

        Vector3 direction = center - data.Point;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
            direction = fallbackDirection;

        body.AddForce(
            direction.normalized * data.Force * forceShare,
            ForceMode.VelocityChange
        );
    }
}
