using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;



namespace Fundering.Transform2D.Authoring
{
    [TemporaryBakingType]
    public class Transform2DRequest : IComponentData
    {
        public GameObject Source;
    }
}
