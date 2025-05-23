using BzKovSoft.ObjectSlicer.Samples;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class KnifeSlicerTest : MonoBehaviour
{
    private BzKnife _knife;

    private void Awake()
    {
        _knife = GetComponent<BzKnife>();
        if (_knife == null)
        {
            Debug.LogError("KnifeSlicerTest requires a BzKnife component on the same GameObject.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Find sliceable component in the object or its parents
        var sliceable = other.GetComponentInParent<IBzSliceableNoRepeat>();
        if (sliceable == null)
            return;

        // Start slice coroutine
        StartCoroutine(PerformSlice(sliceable, other.transform));
    }

    private IEnumerator PerformSlice(IBzSliceableNoRepeat sliceable, Transform targetTransform)
    {
        // Wait a frame to ensure collision positions are stable
        yield return null;

        // Calculate slicing plane
        Vector3 point = GetCollisionPoint(targetTransform.position);
        Vector3 normal = Vector3.Cross(_knife.MoveDirection, _knife.BladeDirection);
        Plane slicePlane = new Plane(normal, point);

        // Perform slice
        sliceable.Slice(slicePlane, _knife.SliceID, null);
    }

    private Vector3 GetCollisionPoint(Vector3 targetPosition)
    {
        Vector3 distToObject = targetPosition - _knife.Origin;
        Vector3 projected = Vector3.Project(distToObject, _knife.BladeDirection);
        return _knife.Origin + projected;
    }
}