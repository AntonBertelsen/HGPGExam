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
    private NativeArray<float3> _directions;

    public void OnCreate(ref SystemState state)
    {
        _directions = BoidHelper.Directions;
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
            Velocities = velocities,
            Directions = _directions,
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

    [ReadOnly] public NativeArray<float3> Directions;

    // These arrays contain the data for ALL boids in the simulation.
    [ReadOnly] public NativeArray<Entity> Entities;
    [ReadOnly] public NativeArray<LocalTransform> Transforms;
    [ReadOnly] public NativeArray<Velocity> Velocities;

    // This 'Execute' method runs for EACH boid.
    private void Execute(Entity currentEntity, ref Velocity currentVelocity, in LocalTransform currentTransform,
        in ObstacleAvoidance obstacleAvoidance)
    {
        var flockSize = 0;
        var flockCentre = float3.zero;
        var flockVelocity = float3.zero;
        var flockSeparation = float3.zero;

        // --- BRUTE-FORCE LOOP ---
        // Loop through every other boid to find neighbors.
        for (var i = 0; i < Entities.Length; i++)
        {
            var otherEntity = Entities[i];

            // Skip self
            if (currentEntity == otherEntity)
                continue;

            var otherTransform = Transforms[i];
            var distance = math.distance(currentTransform.Position, otherTransform.Position);

            // Check if the other boid is a neighbor
            if (distance > Config.ViewRadius) continue;

            flockSize++;
            flockCentre += otherTransform.Position;

            var otherVelocity = Velocities[i];
            flockVelocity += otherVelocity.Value;

            if (distance < Config.SeparationRadius)
            {
                var offset = currentTransform.Position - otherTransform.Position;
                flockSeparation += offset / distance;
            }
        }

        var acceleration = float3.zero;

        if (flockSize != 0)
        {
            flockCentre /= flockSize;
            var flockOffset = flockCentre - currentTransform.Position;

            var cohesion = SteerTowards(flockOffset, currentVelocity.Value) * Config.CohesionWeight;
            var alignment = SteerTowards(flockVelocity, currentVelocity.Value) * Config.AlignmentWeight;
            var separation = SteerTowards(flockSeparation, currentVelocity.Value) * Config.SeparationWeight;

            acceleration += cohesion;
            acceleration += alignment;
            acceleration += separation;
        }

        if (obstacleAvoidance.DirectionIndex != 0)
        {
            acceleration +=
                SteerTowards(
                    BoidHelperMath.RelativeDirection(currentTransform.Rotation,
                        Directions[obstacleAvoidance.DirectionIndex]), currentVelocity.Value) *
                Config.AvoidanceWeight;
        }

        /*
         velocity += acceleration * Time.deltaTime;
           float speed = velocity.magnitude;
           Vector3 dir = velocity / speed;
           speed = Mathf.Clamp (speed, settings.minSpeed, settings.maxSpeed);
           velocity = dir * speed;

           cachedTransform.position += velocity * Time.deltaTime;
           cachedTransform.forward = dir;
           position = cachedTransform.position;
           forward = dir;
         */

        currentVelocity.Value += acceleration;

        // Limit the final speed
        currentVelocity.Value = math.clamp(math.length(currentVelocity.Value), Config.MinSpeed, Config.MaxSpeed) *
                                math.normalizesafe(currentVelocity.Value);
    }

    private float3 SteerTowards(float3 vector, float3 velocity)
    {
        var v = math.normalizesafe(vector) * Config.MaxSpeed - velocity;
        return math.normalizesafe(v) * math.min(math.length(vector), Config.MaxSteerForce);
    }
}