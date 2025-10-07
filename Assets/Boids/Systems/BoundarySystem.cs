using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(BoidSystem))]   // Run after the main boids logic
[UpdateBefore(typeof(MoveSystem))]  // Run before the final movement is applied
public partial struct BoundarySystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // We only run if there is a config
        state.RequireForUpdate<BoidSettings>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<BoidSettings>();

        var boundaryJob = new BoundaryJob
        {
            Config = config
        };
        state.Dependency = boundaryJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
// This job now operates on Velocity as well as Position
public partial struct BoundaryJob : IJobEntity
{
    // We pass the whole config in
    public BoidSettings Config;

    // We read the transform, but we read AND write the velocity
    public void Execute(in LocalTransform transform, ref Velocity velocity)
    {
        var steer = float3.zero;
        
        // --- Calculate Steering Force ---
        // If the boid is in the "margin" near a wall, apply a steering force
        // that is proportional to the boid's max speed.

        if (transform.Position.x < -Config.BoundaryBounds + Config.BoundaryTurnDistance)
            steer.x = Config.MaxSpeed; // Steer right
        if (transform.Position.x > Config.BoundaryBounds - Config.BoundaryTurnDistance)
            steer.x = -Config.MaxSpeed; // Steer left

        if (transform.Position.y < -Config.BoundaryBounds + Config.BoundaryTurnDistance)
            steer.y = Config.MaxSpeed; // Steer up
        if (transform.Position.y > Config.BoundaryBounds - Config.BoundaryTurnDistance)
            steer.y = -Config.MaxSpeed; // Steer down

        if (transform.Position.z < -Config.BoundaryBounds + Config.BoundaryTurnDistance)
            steer.z = Config.MaxSpeed; // Steer forward
        if (transform.Position.z > Config.BoundaryBounds - Config.BoundaryTurnDistance)
            steer.z = -Config.MaxSpeed; // Steer backward
            
        // If there is any steering force to apply...
        if (!steer.Equals(float3.zero))
        {
            // Apply the steering force to the current velocity
            var steerForce = steer - velocity.Value;
            steerForce = math.clamp(math.length(steerForce), 0, Config.MaxSteerForce) * math.normalizesafe(steerForce);
            velocity.Value += steerForce;
            
            // Re-clamp the final velocity to the max speed
            velocity.Value = math.clamp(math.length(velocity.Value), 0, Config.MaxSpeed) * math.normalizesafe(velocity.Value);
        }
    }
}