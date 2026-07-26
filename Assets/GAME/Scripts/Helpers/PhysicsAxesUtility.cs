using UnityEngine;

public static class PhysicsAxesUtility
{
    private const RigidbodyConstraints PositionConstraints =
        RigidbodyConstraints.FreezePositionX |
        RigidbodyConstraints.FreezePositionY |
        RigidbodyConstraints.FreezePositionZ;

    private const RigidbodyConstraints RotationConstraints =
        RigidbodyConstraints.FreezeRotationX |
        RigidbodyConstraints.FreezeRotationY |
        RigidbodyConstraints.FreezeRotationZ;

    public static PhysicsAxes Sanitize(PhysicsAxes axes)
    {
        return axes & PhysicsAxes.XYZ;
    }

    public static Vector3 Project(Vector3 vector, PhysicsAxes axes)
    {
        axes = Sanitize(axes);

        if ((axes & PhysicsAxes.X) == 0)
            vector.x = 0f;

        if ((axes & PhysicsAxes.Y) == 0)
            vector.y = 0f;

        if ((axes & PhysicsAxes.Z) == 0)
            vector.z = 0f;

        return vector;
    }

    public static Vector3 Direction(Vector3 vector, PhysicsAxes axes)
    {
        Vector3 projected = Project(vector, axes);

        return projected.sqrMagnitude > Mathf.Epsilon
            ? projected.normalized
            : Vector3.zero;
    }

    public static Vector3 ConstrainPoint(
        Vector3 point,
        Vector3 referencePoint,
        PhysicsAxes axes)
    {
        axes = Sanitize(axes);

        if ((axes & PhysicsAxes.X) == 0)
            point.x = referencePoint.x;

        if ((axes & PhysicsAxes.Y) == 0)
            point.y = referencePoint.y;

        if ((axes & PhysicsAxes.Z) == 0)
            point.z = referencePoint.z;

        return point;
    }

    public static void ApplyConstraints(
        Rigidbody body,
        PhysicsAxes translationAxes,
        PhysicsAxes rotationAxes)
    {
        if (body == null)
            return;

        translationAxes = Sanitize(translationAxes);
        rotationAxes = Sanitize(rotationAxes);
        RigidbodyConstraints constraints = body.constraints &
            ~(PositionConstraints | RotationConstraints);

        if ((translationAxes & PhysicsAxes.X) == 0)
            constraints |= RigidbodyConstraints.FreezePositionX;

        if ((translationAxes & PhysicsAxes.Y) == 0)
            constraints |= RigidbodyConstraints.FreezePositionY;

        if ((translationAxes & PhysicsAxes.Z) == 0)
            constraints |= RigidbodyConstraints.FreezePositionZ;

        if ((rotationAxes & PhysicsAxes.X) == 0)
            constraints |= RigidbodyConstraints.FreezeRotationX;

        if ((rotationAxes & PhysicsAxes.Y) == 0)
            constraints |= RigidbodyConstraints.FreezeRotationY;

        if ((rotationAxes & PhysicsAxes.Z) == 0)
            constraints |= RigidbodyConstraints.FreezeRotationZ;

        body.constraints = constraints;
        body.linearVelocity = Project(body.linearVelocity, translationAxes);
        body.angularVelocity = Project(body.angularVelocity, rotationAxes);
    }

    public static float SizeMeasure(Vector3 size, PhysicsAxes axes)
    {
        axes = Sanitize(axes);
        float measure = 1f;
        int dimensions = 0;

        if ((axes & PhysicsAxes.X) != 0)
        {
            measure *= Mathf.Abs(size.x);
            dimensions++;
        }

        if ((axes & PhysicsAxes.Y) != 0)
        {
            measure *= Mathf.Abs(size.y);
            dimensions++;
        }

        if ((axes & PhysicsAxes.Z) != 0)
        {
            measure *= Mathf.Abs(size.z);
            dimensions++;
        }

        return dimensions > 0 ? measure : 0f;
    }
}
