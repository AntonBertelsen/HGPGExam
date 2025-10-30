using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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

        using var passingCentroids = new NativeList<float3>(Allocator.Temp);
        const float upThreshold = 0.7071f; // cos(45 degrees)
        var up = new float3(0, 1, 0);

        for (var i = 0; i < triangleCount; i++)
        {
            var i0 = triangles[i * 3];
            var i1 = triangles[i * 3 + 1];
            var i2 = triangles[i * 3 + 2];

            var centroid = (vertices[i0] + vertices[i1] + vertices[i2]) / 3f;
            var worldCentroid = math.transform(localToWorld.Value, centroid);

            var avgNormal = (normals[i0] + normals[i1] + normals[i2]) / 3f;
            var worldNormal = math.normalize(math.mul(normalMatrix, avgNormal));

            if (!(math.dot(worldNormal, up) >= upThreshold)) continue;

            passingCentroids.Add(worldCentroid);
        }

        return passingCentroids.ToArray(Allocator.Temp);
    }
}