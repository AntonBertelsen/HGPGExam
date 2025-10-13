// Shamelessly copied with modifications from https://github.com/SebLague/Boids/blob/master/Assets/Scripts/BoidHelper.cs

using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class BoidHelper
{
    public const int NumViewDirections = 300;
    public static readonly NativeArray<float3> Directions = new(NumViewDirections, Allocator.Persistent);

    static BoidHelper()
    {
        var goldenRatio = (1 + Mathf.Sqrt(5)) / 2;
        var angleIncrement = Mathf.PI * 2 * goldenRatio;

        for (var i = 0; i < NumViewDirections; i++)
        {
            var t = (float)i / NumViewDirections;
            var inclination = Mathf.Acos(1 - 2 * t);
            var azimuth = angleIncrement * i;

            var x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
            var y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
            var z = Mathf.Cos(inclination);
            Directions[i] = new float3(x, y, z);
        }
    }

    public static float3 RelativeDirection(quaternion rotation, int index) => BoidHelperMath.RelativeDirection(rotation, Directions[index]);
}

public static class BoidHelperMath
{
    public static float3 RelativeDirection(quaternion rotation, float3 direction) => math.mul(rotation, direction);
}