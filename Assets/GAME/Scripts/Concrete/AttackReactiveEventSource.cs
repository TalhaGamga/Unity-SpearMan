using R3;
using UnityEngine;

public sealed class AttackReactiveEventSource : IReactiveEventSource
{
    private readonly AttackDefinition _attack;
    private readonly HitContext _hit;

    public AttackReactiveEventSource(
        AttackDefinition attack,
        HitContext hit)
    {
        _attack = attack;
        _hit = hit;
    }

    public Observable<IReactiveEvent> Stream()
    {
        var stream = Observable.Return<IReactiveEvent>(
            new DamageEvent(_attack.Damage)
        );

        ImpactData impact = default;
        if (_attack.HasImpact)
        {
            impact = BuildImpact();
            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(new ImpactEvent(impact))
            );
        }

        if (_attack.IsDestructive)
        {
            var destruct = new DestructData(_hit.Direction, _hit.Point);
            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(new DestructEvent(destruct))
            );
        }

        if (_attack.CanSlice && _hit.SlicePlaneNormal != Vector3.zero)
        {
            var slice = new SliceData(_hit.Point, _hit.SlicePlaneNormal);
            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(new SliceEvent(slice))
            );
        }

        if (_attack.HasImpact &&
            (_attack.IsDestructive || _attack.CanSlice))
        {
            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(new PieceImpactEvent(impact))
            );
        }

        // Reaction ordering is intentionally provisional.
        if (_attack.HasReaction)
        {
            HitReactionSettings settings = _attack.Reaction;
            var reaction = new HitReaction
            {
                Type = settings.Type,
                Duration = settings.Duration,
                LocksMovementInput = settings.LocksMovementInput,
                LocksCombatInput = settings.LocksCombatInput,
                AllowsAirDrift = settings.AllowsAirDrift
            };

            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(new HitReactionEvent(reaction))
            );
        }

        return stream;
    }

    private ImpactData BuildImpact()
    {
        PhysicsResponseSettings motion = _attack.Impact.Motion;
        PhysicsAxes translationAxes = PhysicsAxesUtility.Sanitize(
            motion.TranslationAxes
        );
        PhysicsAxes rotationAxes = PhysicsAxesUtility.Sanitize(
            motion.RotationAxes
        );
        PhysicsAxes motionAxes = translationAxes | rotationAxes;

        Vector3 linearDirection = ShapeLinearDirection(
            _hit.GetDirection(translationAxes),
            motion
        );
        linearDirection = ClampToForwardArc(
            linearDirection,
            _hit.AttackerForward,
            motion
        );
        Vector3 angularDirection = _hit.GetDirection(translationAxes);

        return new ImpactData(
            linearDirection,
            motion.LinearForce,
            _hit.Point,
            translationAxes,
            rotationAxes,
            _hit.GetSpeed(motionAxes) * motion.AngularMultiplier,
            angularDirection
        );
    }

    private static Vector3 ShapeLinearDirection(
        Vector3 runtimeDirection,
        PhysicsResponseSettings motion)
    {
        bool supportsVertical =
            (motion.TranslationAxes & PhysicsAxes.Y) != 0;
        float minimumVertical = Mathf.Clamp01(
            motion.MinimumVerticalRatio
        );

        if (!supportsVertical ||
            motion.VerticalMode == VerticalImpactMode.Preserve ||
            minimumVertical <= 0f)
        {
            return runtimeDirection;
        }

        float verticalSign = motion.VerticalMode ==
            VerticalImpactMode.Upward
                ? 1f
                : -1f;

        Vector3 horizontal = runtimeDirection;
        horizontal.y = 0f;

        if (horizontal.sqrMagnitude <= Mathf.Epsilon)
            return Vector3.up * verticalSign;

        float horizontalRatio = Mathf.Sqrt(
            1f - minimumVertical * minimumVertical
        );

        return horizontal.normalized * horizontalRatio +
            Vector3.up * (verticalSign * minimumVertical);
    }

    private static Vector3 ClampToForwardArc(
        Vector3 runtimeDirection,
        Vector3 attackerForward,
        PhysicsResponseSettings motion)
    {
        if (!motion.ClampToForwardArc ||
            (motion.TranslationAxes & PhysicsAxes.YZ) != PhysicsAxes.YZ)
        {
            return runtimeDirection;
        }

        Vector3 forward = PhysicsAxesUtility.Project(
            attackerForward,
            PhysicsAxes.YZ
        );
        forward.y = 0f;

        if (forward.sqrMagnitude <= Mathf.Epsilon)
            return runtimeDirection;

        forward.Normalize();

        float minimumAngle = Mathf.Clamp(
            Mathf.Min(
                motion.MinimumForwardAngle,
                motion.MaximumForwardAngle
            ),
            0f,
            90f
        );
        float maximumAngle = Mathf.Clamp(
            Mathf.Max(
                motion.MinimumForwardAngle,
                motion.MaximumForwardAngle
            ),
            minimumAngle,
            90f
        );
        Vector3 planarDirection = PhysicsAxesUtility.Project(
            runtimeDirection,
            PhysicsAxes.YZ
        );
        float forwardMagnitude = Mathf.Abs(
            Vector3.Dot(planarDirection, forward)
        );
        float upwardMagnitude = Mathf.Max(0f, planarDirection.y);
        float elevation = Mathf.Atan2(
            upwardMagnitude,
            forwardMagnitude
        ) * Mathf.Rad2Deg;
        elevation = Mathf.Clamp(
            elevation,
            minimumAngle,
            maximumAngle
        );

        float elevationRadians = elevation * Mathf.Deg2Rad;
        return forward * Mathf.Cos(elevationRadians) +
            Vector3.up * Mathf.Sin(elevationRadians);
    }
}
