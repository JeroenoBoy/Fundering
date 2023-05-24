using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;



namespace Fundering.Transform2D.Authoring
{
    public class Transform2DAuthoring : MonoBehaviour
    {
        public class Transform2DAuthoringBaker : Baker<Transform2DAuthoring>
        {
            public override void Bake(Transform2DAuthoring authoring)
            {
                AddComponentObject(GetEntityWithoutDependency(), new Transform2DRequest { Source = authoring.gameObject });
            }
        }
    }
}
