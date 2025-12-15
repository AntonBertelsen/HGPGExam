using Unity.Entities;
using UnityEngine;

public struct CleanupTag : IComponentData { }

public class CleanupAuthoring : MonoBehaviour
{
    public class CleanupBaker : Baker<CleanupAuthoring>
    {
        public override void Bake(CleanupAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new CleanupTag());
        }
    }
}