using Fundering.Common;
using Fundering.Components.Properties;
using Fundering.Components.Regular;
using Fundering.Transform2D;
using NSprites;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;



namespace Fundering.Systems
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

            private void Execute(Entity entity, [ChunkIndexInQuery]int chunkIndex, in LocalToWorld2D transform, in Scale2D size, in Pivot pivot)
            {
                float2 viewCenterPosition = transform.Position - size.Value * pivot.Value + size.Value * .5f;
                if(!CameraBounds2D.Intersects(new Bounds2D(viewCenterPosition, size.Value)))
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

            private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in LocalToWorld2D worldPosition, in Scale2D size, in Pivot pivot)
            {
                float2 viewCenterPosition = worldPosition.Position - size.Value * pivot.Value + size.Value * .5f;
                if (CameraBounds2D.Intersects(new Bounds2D(viewCenterPosition, size.Value)))
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
