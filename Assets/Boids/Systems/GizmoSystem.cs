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
            .WithAll<BoidTag, LocalTransform, Velocity, ObstacleAvoidance>()
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
        var avoidances = _boidQuery.ToComponentDataArray<ObstacleAvoidance>(Allocator.TempJob);
        
        foreach (var (transform, velocity, avoidance) in transforms
                     .Zip(velocities, (t, v) => (t, v))
                     .Zip(avoidances, (z, a) => (z.t, z.v, a)))
        {
            Gizmos.color = Color.red;
            for (var i = 0; i < avoidance.DirectionIndex; i++)
            {
                Gizmos.DrawRay(transform.Position, BoidHelper.RelativeDirection(transform.Rotation, i));
            }
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.Position, BoidHelper.RelativeDirection(transform.Rotation, avoidance.DirectionIndex));
            
            Gizmos.color = Color.gray;
            for (var i = avoidance.DirectionIndex + 1; i < BoidHelper.NumViewDirections; i++)
            {
                Gizmos.DrawRay(transform.Position, BoidHelper.RelativeDirection(transform.Rotation, i));
            }
            
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.Position, velocity.Value);
        }

        transforms.Dispose();
        velocities.Dispose();
    }
}