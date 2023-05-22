using Unity.Entities;
using Unity.Mathematics;



namespace Fundering.Components.Properties
{
    /// <summary>
    /// Supposed to use as texture ST to locate actual texture on atlas if used (if not use default value). In shader every float2 UV would be multiplied to value.xy and offsetted with value.zw
    /// like UV * value.xy + value.zw
    /// </summary>
    public struct UVAtlas : IComponentData
    {
        public float4 Value;

        public static UVAtlas Default => new() { Value = new float4(1f, 1f, 0f, 0f) };
    }
}