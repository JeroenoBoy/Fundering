using Unity.Entities;
using UnityEngine;



namespace Fundering.Transform2D.Authoring
{
    [TemporaryBakingType]
    public class Transform2DRequest : IComponentData
    {
        public GameObject Source;
    }
}
