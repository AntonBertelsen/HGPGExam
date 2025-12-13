using Unity.Entities;
using UnityEngine;

public class SimpleLODConfig : IComponentData
{
    public Mesh MeshLOD0;
    public Mesh MeshLOD1;
    public Mesh MeshLOD2;
    public Mesh MeshLOD3;
}

public struct SimpleLODTag : IComponentData { }