# Birdemic - High Performance Game Programming (HPGP)

![Birdemic Frontpage](frontpage.png)

## Authors
* **Jonas Ulsnes Jørgensen** (jouj)
* **Eik Boelt Bagge Petersen** (eikb)
* **Anton Dalsgaard Bertelsen** (adbe)

**High Performance Game Programming (HPGP)**

## Project Overview
This project demonstrates a high-performance boid simulation built using Unity's Data-Oriented Technology Stack, including ECS, the C# Job System, and the Burst Compiler. It works with hundreds of thousands of animated seaguls, spatial partitioning, turrets, exploding projectiles, and obstacle avoidance.

## How to Run

1.  **Open the Project:**
    * Add the project folder to Unity Hub and open it.

2.  **Load the Scene:**
    * Navigate to the project folder `Assets/Scenes/`.
    * Open the scene named **`EnvironmentScene`**.

3.  **Verify Sub-Scene Loading:**
    * Look at the **Hierarchy** window.
    * Ensure the sub-scene **`EnvironmentEntityScene`** is loaded / open for editing.

4.  **Play:**
    * Press the **Play** button in the editor to start the simulation.
    * Change the settings using the UI to see how it affects the simulation

## Controls

### Camera
* **W / A / S / D:** Move the camera
* **Left Mouse Button (Hold):** Orbit camera
* **Right Mouse Button (Hold):** Pan camera
* **Scroll Wheel:** Zoom in/out
* **F:** Toggle "Bird Follow" camera mode

### Interaction
* **Space:** Throw an egg or "Bird Bomb"
* **C:** Throw a Large Cube obstacle
    * *Note: Ensure **Dynamic Obstacle Avoidance** is enabled and weighted appropriately in the Simulation Settings for birds to react to this.*
* **T:** Place a Turret at the cursor's location
* **B:** Spawn a Birds at the cursor's location

## Debugging
* **Gizmos:** Enable Gizmos in the Game View to visualize:
    * Dynamic Collision Rays for birds
    * Landing spots
    * Landing spot KD tree
    * Birds landing state
    * Turret targeting rays
    * Spatial Grid heatmaps