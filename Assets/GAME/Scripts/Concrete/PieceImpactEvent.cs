using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PieceImpactEvent : IReactiveEvent
{
    private const float MinVisualMeasure = 0.0001f;

    private readonly ImpactData _data;

    public PieceImpactEvent(ImpactData data)
    {
        _data = data;
    }

    public void Consume(
        IReadOnlyList<TargetContext> contexts,
        Action<IReadOnlyList<TargetContext>> completed)
    {
        var targets = new List<PieceTarget>();
        float totalInverseSize = 0f;

        foreach (TargetContext context in contexts)
        {
            if (context == null || !context.IsStructuralResult ||
                !context.TryGet<IImpactable>(out var impactable))
            {
                continue;
            }

            GetVisualData(
                context.Target,
                out Vector3 center,
                out float inverseSize
            );
            targets.Add(new PieceTarget(impactable, center, inverseSize));
            totalInverseSize += inverseSize;
        }

        if (totalInverseSize > 0f)
        {
            foreach (PieceTarget target in targets)
            {
                float share = target.InverseSize / totalInverseSize;
                Vector3 direction = target.Center - _data.Point;
                target.Impactable.ApplyImpact(
                    _data.ForTarget(direction, share)
                );
            }
        }

        completed?.Invoke(contexts);
    }

    private void GetVisualData(
        GameObject target,
        out Vector3 center,
        out float inverseSize)
    {
        center = target.transform.position;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds bounds = default;

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null || !targetRenderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        if (!hasBounds)
        {
            inverseSize = 1f;
            return;
        }

        center = bounds.center;
        float fakeSize = PhysicsAxesUtility.SizeMeasure(
            bounds.size,
            _data.TranslationAxes
        );
        inverseSize = fakeSize > MinVisualMeasure
            ? 1f / fakeSize
            : 1f;
    }

    private readonly struct PieceTarget
    {
        public IImpactable Impactable { get; }
        public Vector3 Center { get; }
        public float InverseSize { get; }

        public PieceTarget(
            IImpactable impactable,
            Vector3 center,
            float inverseSize)
        {
            Impactable = impactable;
            Center = center;
            InverseSize = inverseSize;
        }
    }
}
