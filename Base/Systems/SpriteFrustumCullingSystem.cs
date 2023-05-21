using Fundering.Base.Common;
using Fundering.Base.Components.Properties;
using Fundering.Base.Components.Regular;
using Fundering.FlatTransform.OldSystem.Components;
using NSprites;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;



namespace Fundering.Base.Systems
{
    [BurstCompile]
    public partial struct SpriteFrustumCullingSystem : ISystem
    {
        [BurstCompile]
        [WithAll(typeof(SpriteRenderID))]
        [WithNone(typeof(CullSpriteTag))]
        private partial struct DisableCulledJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
            public Bounds2D CameraBounds2D;

            private void Execute(Entity entity, [ChunkIndexInQuery]int chunkIndex, in WorldPosition2D worldPosition, in Scale2D size, in Pivot pivot)
            {
                float2 viewCenterPosition = worldPosition.value - size.value * pivot.value + size.value * .5f;
                if(!CameraBounds2D.Intersects(new Bounds2D(viewCenterPosition, size.value)))
                    EntityCommandBuffer.AddComponent<CullSpriteTag>(chunkIndex, entity);
            }
        }
        [BurstCompile]
        [WithAll(typeof(SpriteRenderID))]
        [WithAll(typeof(CullSpriteTag))]
        private partial struct EnableUnculledJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
            public Bounds2D CameraBounds2D;

            private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in WorldPosition2D worldPosition, in Scale2D size, in Pivot pivot)
            {
                float2 viewCenterPosition = worldPosition.value - size.value * pivot.value + size.value * .5f;
                if (CameraBounds2D.Intersects(new Bounds2D(viewCenterPosition, size.value)))
                    EntityCommandBuffer.RemoveComponent<CullSpriteTag>(chunkIndex, entity);
            }
        }
        public struct CameraData : IComponentData
        {
            public float2 Position;
            public Bounds2D CullingBounds2D;
        }

#if UNITY_EDITOR
        [MenuItem("NSprites/Toggle frustum culling system")]
        public static void ToggleFrustumCullingSystem()
        {
            SystemHandle systemHandle = World.DefaultGameObjectInjectionWorld.GetExistingSystem<SpriteFrustumCullingSystem>();

            if (systemHandle == SystemHandle.Null)
                return;

            ref SystemState systemState = ref World.DefaultGameObjectInjectionWorld.Unmanaged.ResolveSystemStateRef(systemHandle);

            systemState.Enabled = !systemState.Enabled;

            if (!systemState.Enabled)
                systemState.EntityManager.RemoveComponent(systemState.GetEntityQuery(typeof(CullSpriteTag)), ComponentType.ReadOnly<CullSpriteTag>());
        }
#endif

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            _ = state.EntityManager.AddComponentData(state.SystemHandle, new CameraData());
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            Bounds2D cullingBounds2D = SystemAPI.GetComponent<CameraData>(state.SystemHandle).CullingBounds2D;

            DisableCulledJob disableCulledJob = new DisableCulledJob
            {
                EntityCommandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                CameraBounds2D = cullingBounds2D
            };
            JobHandle disableCulledHandle = disableCulledJob.ScheduleParallelByRef(state.Dependency);
            

            EnableUnculledJob enableUnculledJob = new EnableUnculledJob
            {
                EntityCommandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                CameraBounds2D = cullingBounds2D
            };
            JobHandle enableUnculledHandle = enableUnculledJob.ScheduleParallelByRef(state.Dependency);
            
            state.Dependency = JobHandle.CombineDependencies(disableCulledHandle, enableUnculledHandle);
        }
    }
}
