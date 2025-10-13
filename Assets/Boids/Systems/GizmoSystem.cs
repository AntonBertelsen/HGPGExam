using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public partial struct GizmoSystem : ISystem
{
    private EntityQuery _boidQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _boidQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BoidTag, LocalTransform, Velocity>()
            .Build(ref state);
    }

    public void DrawGizmos()
    {
        DrawBoidGizmos();
    }

    private void DrawBoidGizmos()
    {
        var transforms = _boidQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var velocities = _boidQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);

        foreach (var (transform, velocity) in transforms.Zip(velocities, (t, v) => (t, v)))
        {
            foreach (var direction in BoidHelper.Directions)
            {
                Gizmos.DrawRay(transform.Position, Vector3.Normalize(direction));
            }
        }
    }
}