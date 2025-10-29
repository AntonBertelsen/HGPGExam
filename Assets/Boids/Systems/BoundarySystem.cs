using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(BoidSystem))]
[UpdateBefore(typeof(MoveSystem))]
public partial struct BoundarySystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSettings>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<BoidSettings>();

        var job = new BoundaryJob
        {
            Config = config
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(BoidTag))]
public partial struct BoundaryJob : IJobEntity
{
    public BoidSettings Config;

    public void Execute(ref LocalTransform transform, ref Velocity velocity)
    {
        float3 pos = transform.Position;
        float3 steer = float3.zero;

        float b = Config.BoundaryBounds;
        float tDist = Config.BoundaryTurnDistance;

        // --- X Axis ---
        if (pos.x < -b + tDist)
        {
            float distToEdge = math.abs(pos.x + b);
            float t = 1f - (distToEdge / tDist);
            steer.x = math.lerp(0, Config.MaxSpeed, math.saturate(t)); // smooth ramp
        }
        else if (pos.x > b - tDist)
        {
            float distToEdge = math.abs(b - pos.x);
            float t = 1f - (distToEdge / tDist);
            steer.x = -math.lerp(0, Config.MaxSpeed, math.saturate(t));
        }

        // --- Y Axis ---
        if (pos.y < -b + tDist)
        {
            float distToEdge = math.abs(pos.y + b);
            float t = 1f - (distToEdge / tDist);
            steer.y = math.lerp(0, Config.MaxSpeed, math.saturate(t));
        }
        else if (pos.y > b - tDist)
        {
            float distToEdge = math.abs(b - pos.y);
            float t = 1f - (distToEdge / tDist);
            steer.y = -math.lerp(0, Config.MaxSpeed, math.saturate(t));
        }

        // --- Z Axis ---
        if (pos.z < -b + tDist)
        {
            float distToEdge = math.abs(pos.z + b);
            float t = 1f - (distToEdge / tDist);
            steer.z = math.lerp(0, Config.MaxSpeed, math.saturate(t));
        }
        else if (pos.z > b - tDist)
        {
            float distToEdge = math.abs(b - pos.z);
            float t = 1f - (distToEdge / tDist);
            steer.z = -math.lerp(0, Config.MaxSpeed, math.saturate(t));
        }

        // --- Apply steering ---
        if (!steer.Equals(float3.zero))
        {
            var steerForce = steer - velocity.Value;
            steerForce = math.clamp(math.length(steerForce), 0, Config.MaxSteerForce) * math.normalizesafe(steerForce);
            velocity.Value += steerForce;

            // Re-clamp speed
            velocity.Value = math.clamp(math.length(velocity.Value), 0, Config.MaxSpeed) * math.normalizesafe(velocity.Value);
        }

        // --- Hard Clamp Position (safety) ---
        pos.x = math.clamp(pos.x, -b, b);
        pos.y = math.clamp(pos.y, -b, b);
        pos.z = math.clamp(pos.z, -b, b);
        transform.Position = pos;
    }
}