using Unity.Burst;
using Unity.Collections;
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
        state.RequireForUpdate<BoundaryComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<BoidSettings>();
        var boundary = SystemAPI.GetSingleton<BoundaryComponent>();

        var job = new BoundaryJob
        {
            Config = config,
            Bounds = boundary
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(BoidTag))]
public partial struct BoundaryJob : IJobEntity
{
    public BoidSettings Config;
    public BoundaryComponent Bounds;

    public void Execute(ref LocalTransform transform, ref Velocity velocity)
    {
        float3 pos = transform.Position;
        float3 steer = float3.zero;
        
        float3 center = Bounds.Center;
        float3 extents = Bounds.Size * 0.5f; 
        float3 offset = pos - center;

        // --- X Axis ---
        // Right Wall (+X)
        float posMarginX = Bounds.PositiveMargins.x;
        float innerRight = extents.x - posMarginX;
        
        if (offset.x > innerRight)
        {
            float distIntoZone = offset.x - innerRight;
            float t = math.saturate(distIntoZone / posMarginX);
            steer.x = -math.lerp(0, Config.MaxSpeed, t);
        }
        else 
        {
            // Left Wall (-X)
            float negMarginX = Bounds.NegativeMargins.x;
            float innerLeft = -extents.x + negMarginX;

            if (offset.x < innerLeft)
            {
                float distIntoZone = innerLeft - offset.x;
                float t = math.saturate(distIntoZone / negMarginX);
                steer.x = math.lerp(0, Config.MaxSpeed, t);
            }
        }

        // --- Y Axis ---
        // Top Wall (+Y)
        float posMarginY = Bounds.PositiveMargins.y;
        float innerTop = extents.y - posMarginY;

        if (offset.y > innerTop)
        {
            float distIntoZone = offset.y - innerTop;
            float t = math.saturate(distIntoZone / posMarginY);
            steer.y = -math.lerp(0, Config.MaxSpeed, t);
        }
        else
        {
            // Bottom Wall (-Y) e.g., Ocean
            float negMarginY = Bounds.NegativeMargins.y;
            float innerBottom = -extents.y + negMarginY;

            if (offset.y < innerBottom)
            {
                float distIntoZone = innerBottom - offset.y;
                float t = math.saturate(distIntoZone / negMarginY);
                steer.y = math.lerp(0, Config.MaxSpeed, t);
            }
        }

        // --- Z Axis ---
        // Front Wall (+Z)
        float posMarginZ = Bounds.PositiveMargins.z;
        float innerFront = extents.z - posMarginZ;

        if (offset.z > innerFront)
        {
            float distIntoZone = offset.z - innerFront;
            float t = math.saturate(distIntoZone / posMarginZ);
            steer.z = -math.lerp(0, Config.MaxSpeed, t);
        }
        else
        {
            // Back Wall (-Z)
            float negMarginZ = Bounds.NegativeMargins.z;
            float innerBack = -extents.z + negMarginZ;

            if (offset.z < innerBack)
            {
                float distIntoZone = innerBack - offset.z;
                float t = math.saturate(distIntoZone / negMarginZ);
                steer.z = math.lerp(0, Config.MaxSpeed, t);
            }
        }

        // --- Apply Steering ---
        if (!steer.Equals(float3.zero))
        {
            var steerForce = steer - velocity.Value;
            steerForce = math.clamp(math.length(steerForce), 0, Config.MaxSteerForce) * math.normalizesafe(steerForce);
            velocity.Value += steerForce;
            
            velocity.Value = math.clamp(math.length(velocity.Value), 0, Config.MaxSpeed) * math.normalizesafe(velocity.Value);
        }

        // --- Hard Clamp Position ---
        float3 clampedOffset = math.clamp(offset, -extents, extents);
        transform.Position = center + clampedOffset;
    }
}