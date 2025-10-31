﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct KdTreeBuilderSystem : ISystem
{
    private EntityQuery _landingAreaQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _landingAreaQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LandingArea, LocalToWorld>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var landingAreas = _landingAreaQuery.ToComponentDataArray<LandingArea>(Allocator.TempJob);
        var localToWorlds = _landingAreaQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);

        using var allLandingSpots = new NativeList<float3>(Allocator.Temp);
        for (var i = 0; i < landingAreas.Length; i++)
        {
            var landingArea = landingAreas[i];
            var localToWorld = localToWorlds[i];
            using var landingSpots = CalculateLandingSpots(landingArea, localToWorld);
            allLandingSpots.AddRange(landingSpots);
        }

        using var spots = allLandingSpots.ToArray(Allocator.Temp);
        var tree = KdTree.Create(spots);
        state.EntityManager.AddComponentData(state.SystemHandle, tree);

        state.Enabled = false;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        state.EntityManager.RemoveComponent<KdTree>(state.SystemHandle);
    }

    private NativeArray<float3> CalculateLandingSpots(LandingArea landingArea, LocalToWorld localToWorld)
    {
        ref var vertices = ref landingArea.MeshBlob.Value.Vertices;
        ref var normals = ref landingArea.MeshBlob.Value.Normals;
        ref var triangles = ref landingArea.MeshBlob.Value.Triangles;

        var triangleCount = triangles.Length / 3;
        var normalMatrix = math.transpose(math.inverse(new float3x3(localToWorld.Value)));

        using var passingPoints = new NativeList<float3>(Allocator.Temp);
        var upThreshold = math.cos(math.radians(landingArea.MaxInclineDegrees));
        var up = new float3(0, 1, 0);

        for (var t = 0; t < triangleCount; t++)
        {
            var i0 = triangles[t * 3];
            var i1 = triangles[t * 3 + 1];
            var i2 = triangles[t * 3 + 2];

            // Transform triangle vertices to world space
            var v0 = math.transform(localToWorld.Value, vertices[i0]);
            var v1 = math.transform(localToWorld.Value, vertices[i1]);
            var v2 = math.transform(localToWorld.Value, vertices[i2]);
            var n0 = normals[i0];
            var n1 = normals[i1];
            var n2 = normals[i2];

            // World-space edge vectors
            var e0 = v1 - v0;
            var e1 = v2 - v0;
            var len0 = math.length(e0);
            var len1 = math.length(e1);

            var steps0 = math.max(1, (int)math.floor(len0 / landingArea.SpotSpacing));
            var steps1 = math.max(1, (int)math.floor(len1 / landingArea.SpotSpacing));

            for (var i = 0; i <= steps0; i++)
            {
                var a = (float)i / steps0;
                for (var j = 0; j <= steps1 - i * steps1 / steps0; j++)
                {
                    var b = (float)j / steps1;
                    if (a + b > 1f) continue;
                    var c = 1f - a - b;

                    // Barycentric interpolation in world space
                    var pos = a * v1 + b * v2 + c * v0;
                    var normal = math.normalize(a * n1 + b * n2 + c * n0);
                    var worldNormal = math.normalize(math.mul(normalMatrix, normal));

                    if (math.dot(worldNormal, up) >= upThreshold)
                    {
                        passingPoints.Add(pos);
                    }
                }
            }
        }

        // Deduplicate points within a small epsilon
        const float epsilon = 0.01f;
        using var uniquePoints = new NativeList<float3>(Allocator.Temp);
        foreach (var point in passingPoints)
        {
            var isDuplicate = false;
            foreach (var other in uniquePoints)
            {
                if (math.distancesq(point, other) < epsilon)
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (!isDuplicate)
            {
                uniquePoints.Add(point);
            }
        }

        return uniquePoints.ToArray(Allocator.Temp);
    }
}