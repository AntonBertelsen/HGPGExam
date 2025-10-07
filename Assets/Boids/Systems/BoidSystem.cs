using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[UpdateBefore(typeof(MoveSystem))] // Calculate new velocity before it's used for movement
public partial struct BoidSystem : ISystem
{
    private EntityQuery boidQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // We require the config to exist.
        state.RequireForUpdate<BoidSettings>();
        // We create a query to find all boids.
        boidQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BoidTag, LocalTransform, Velocity>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<BoidSettings>();

        // This is the brute-force part. We copy all boid data into arrays.
        var boids = boidQuery.ToEntityArray(Allocator.TempJob);
        var transforms = boidQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var velocities = boidQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);

        var boidJob = new BoidJob
        {
            Config = config,
            Entities = boids,
            Transforms = transforms,
            Velocities = velocities
        };
        
        // Schedule the job and chain the disposal of our temporary arrays.
        var jobHandle = boidJob.ScheduleParallel(state.Dependency);
        jobHandle = boids.Dispose(jobHandle);
        jobHandle = transforms.Dispose(jobHandle);
        jobHandle = velocities.Dispose(jobHandle);
        
        state.Dependency = jobHandle;
    }
}

[BurstCompile]
public partial struct BoidJob : IJobEntity
{
    public BoidSettings Config;
    
    // These arrays contain the data for ALL boids in the simulation.
    [ReadOnly] public NativeArray<Entity> Entities;
    [ReadOnly] public NativeArray<LocalTransform> Transforms;
    [ReadOnly] public NativeArray<Velocity> Velocities;

    // This 'Execute' method runs for EACH boid.
    public void Execute(Entity currentEntity, ref Velocity currentVelocity, in LocalTransform currentTransform)
    {
        var separation = float3.zero;
        var alignment = float3.zero;
        var cohesion = float3.zero;
        int neighborCount = 0;

        // --- BRUTE-FORCE LOOP ---
        // Loop through every other boid to find neighbors.
        for (int i = 0; i < Entities.Length; i++)
        {
            var otherEntity = Entities[i];
            
            // Skip self
            if (currentEntity == otherEntity)
                continue;

            var otherTransform = Transforms[i];
            var distance = math.distance(currentTransform.Position, otherTransform.Position);

            // Check if the other boid is a neighbor
            if (distance > 0 && distance < Config.ViewRadius)
            {
                neighborCount++;
                
                // Rule 1: Cohesion - Steer towards center of mass
                cohesion += otherTransform.Position;

                // Rule 2: Alignment - Steer towards average heading
                alignment += Velocities[i].Value;

                // Rule 3: Separation - Steer to avoid crowding
                if (distance < Config.SeparationRadius)
                {
                    separation += (currentTransform.Position - otherTransform.Position) / distance;
                }
            }
        }

        if (neighborCount > 0)
        {
            // Calculate average cohesion and alignment
            cohesion = (cohesion / neighborCount) - currentTransform.Position;
            alignment /= neighborCount;

            // --- Apply forces ---
            // Normalize and clamp each force vector.
            cohesion = math.normalizesafe(cohesion) * Config.MaxSpeed - currentVelocity.Value;
            cohesion = math.clamp(math.length(cohesion), 0, Config.MaxSteerForce) * math.normalizesafe(cohesion);

            alignment = math.normalizesafe(alignment) * Config.MaxSpeed - currentVelocity.Value;
            alignment = math.clamp(math.length(alignment), 0, Config.MaxSteerForce) * math.normalizesafe(alignment);

            separation = math.normalizesafe(separation) * Config.MaxSpeed - currentVelocity.Value;
            separation = math.clamp(math.length(separation), 0, Config.MaxSteerForce) * math.normalizesafe(separation);
            
            // Apply weighted forces to our velocity
            var totalForce = 
                (cohesion * Config.CohesionWeight) + 
                (alignment * Config.AlignmentWeight) + 
                (separation * Config.SeparationWeight);
            
            currentVelocity.Value += totalForce;
        }

        // Limit the final speed
        currentVelocity.Value = math.clamp(math.length(currentVelocity.Value), Config.MinSpeed, Config.MaxSpeed) * math.normalizesafe(currentVelocity.Value);
    }
}