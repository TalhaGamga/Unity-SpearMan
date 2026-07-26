using System;

[Flags]
public enum PhysicsAxes
{
    None = 0,
    X = 1 << 0,
    Y = 1 << 1,
    Z = 1 << 2,
    XY = X | Y,
    YZ = Y | Z,
    XYZ = X | Y | Z
}
