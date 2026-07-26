using DinoFracture;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("GAME/Reactive Capabilities/Dino Destructible")]
[DisallowMultipleComponent]
public sealed class DinoDestructibleAdapter : MonoBehaviour, IDestructible
{
    [SerializeField] private PreFracturedGeometry _preFractured;
    [SerializeField] private RuntimeFracturedGeometry _runtimeFractured;

    private void Awake()
    {
        ResolveFractureComponent();
    }

    private void Reset()
    {
        ResolveFractureComponent();
    }

    public void Destruct(
        DestructData data,
        Action<StructuralResult> completed)
    {
        FractureGeometry geometry = _preFractured != null
            ? (FractureGeometry)_preFractured
            : _runtimeFractured;

        if (geometry == null)
        {
            Debug.LogWarning(
                $"{nameof(DinoDestructibleAdapter)} on {name} needs a Dino fracture component.",
                this
            );
            completed?.Invoke(default);
            return;
        }

        Vector3 localPoint = transform.InverseTransformPoint(data.Point);
        AsyncFractureResult fracture = geometry.Fracture(localPoint);
        if (fracture == null)
        {
            completed?.Invoke(default);
            return;
        }

        if (fracture.IsComplete)
        {
            Complete(fracture, completed);
            return;
        }

        Observable.EveryUpdate()
            .Where(_ => fracture.IsComplete)
            .Take(1)
            .Subscribe(_ => Complete(fracture, completed));
    }

    private static void Complete(
        AsyncFractureResult fracture,
        Action<StructuralResult> completed)
    {
        var targets = new List<GameObject>();
        if (fracture.PiecesRoot != null)
        {
            var uniqueTargets = new HashSet<GameObject>();
            foreach (Rigidbody body in fracture.PiecesRoot.GetComponentsInChildren<Rigidbody>())
            {
                if (body != null && uniqueTargets.Add(body.gameObject))
                    targets.Add(body.gameObject);
            }

            if (targets.Count == 0)
                targets.Add(fracture.PiecesRoot);
        }

        completed?.Invoke(new StructuralResult(targets.Count > 0, targets));
    }

    private void ResolveFractureComponent()
    {
        if (_preFractured == null)
            _preFractured = GetComponent<PreFracturedGeometry>();

        if (_runtimeFractured == null)
            _runtimeFractured = GetComponent<RuntimeFracturedGeometry>();
    }
}