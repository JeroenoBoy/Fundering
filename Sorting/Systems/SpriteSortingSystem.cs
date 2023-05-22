using System.Collections.Generic;
using Fundering.Components.Properties;
using Fundering.Components.Regular;
using Fundering.Sorting.Components;
using Fundering.Sorting.Systems;
using Fundering.Transform2D;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;



[assembly: RegisterGenericJobType(typeof(SpriteSortingSystem.SortArrayJob<int, SpriteSortingSystem.SortingDataComparer>))]

namespace Fundering.Sorting.Systems
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct SpriteSortingSystem : ISystem
    {
        #region data
        private struct SystemData : IComponentData
        {
            public EntityQuery SortingSpritesQuery;
            public EntityQuery SortingStaticSpritesQuery;
        }
        internal struct SortingData
        {
            internal struct GeneralComparer : IComparer<SortingData>
            {
                public int Compare(SortingData x, SortingData y)
                {
                    return x.SortingIndex.CompareTo(y.SortingIndex) * -4 //less index -> later in render
                        + x.Position.y.CompareTo(y.Position.y) * 2
                        + x.ID.CompareTo(y.ID);
                }
            }

            public int ID;
            public int SortingIndex;
            public float2 Position;
#if UNITY_EDITOR
            public override string ToString()
            {
                return $"id: {ID}, sortIndex: {SortingIndex}, pos: {Position}";
            }
#endif
        }
        internal struct SortingDataComparer : IComparer<int>
        {
            public NativeArray<SortingData> SourceData;
            public SortingData.GeneralComparer SourceDataComparer;

            public int Compare(int x, int y) => SourceDataComparer.Compare(SourceData[x], SourceData[y]);
        }
        #endregion

        #region jobs
        [BurstCompile]
        private struct GatherSortingDataJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<LocalToWorld2D> WorldPosition2DCth;
            [ReadOnly] public ComponentTypeHandle<SortingIndex> SortingIndexCth;
            [WriteOnly][NativeDisableContainerSafetyRestriction] public NativeArray<SortingData> SortingDataArray;
            [WriteOnly][NativeDisableContainerSafetyRestriction] public NativeArray<int> Pointers;
            [ReadOnly] public NativeArray<int> ChunkBasedEntityIndices;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Entity>         entityArray          = chunk.GetNativeArray(EntityTypeHandle);
                NativeArray<LocalToWorld2D> worldPosition2DArray = chunk.GetNativeArray(ref WorldPosition2DCth);
                NativeArray<SortingIndex>   sortingIndexes       = chunk.GetNativeArray(ref SortingIndexCth);
                int                         firstEntityIndex     = ChunkBasedEntityIndices[unfilteredChunkIndex];
                for (int entityIndex = 0; entityIndex < entityArray.Length; entityIndex++)
                {
                    int arrayIndex = firstEntityIndex + entityIndex;
                    SortingDataArray[arrayIndex] = new SortingData
                    {
                        Position = worldPosition2DArray[entityIndex].Position,
                        SortingIndex = sortingIndexes[entityIndex].Value,
                        ID = entityArray[entityIndex].Index
                    };
                    Pointers[arrayIndex] = arrayIndex;
                }
            }
        }
        [BurstCompile]
        internal struct SortArrayJob<TElement, TComparer> : IJob
            where TElement : unmanaged
            where TComparer : unmanaged, IComparer<TElement>
        {
            public NativeArray<TElement> Array;
            public TComparer Comparer;

            public void Execute() => Array.Sort(Comparer);
        }
        [BurstCompile]
        private struct GenerateSortingValuesJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeArray<int> Pointers;
            [WriteOnly][NativeDisableParallelForRestriction] public NativeArray<SortingValue> SortingValues;
            public int LayerIndex;

            public void Execute(int startIndex, int count)
            {
                int toIndex = startIndex + count;
                for (int i = startIndex; i < toIndex; i++)
                {
                    float from = PER_LAYER_OFFSET * LayerIndex;
                    float to = from + PER_LAYER_OFFSET;
                    SortingValues[Pointers[i]] = new SortingValue { Value = math.lerp(from, to, 1f - (float)i / Pointers.Length) };
                }
            }
        }
        [BurstCompile]
        private struct WriteSortingValuesToChunksJob : IJobChunk
        {
            [ReadOnly] public NativeArray<int> ChunkBasedEntityIndices;
            [NativeDisableContainerSafetyRestriction] public ComponentTypeHandle<SortingValue> SortingValueCthWo;
            [ReadOnly] public NativeArray<SortingValue> SortingValues;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<SortingValue> chunkSortingValues = chunk.GetNativeArray(ref SortingValueCthWo);
                NativeArray<SortingValue>.Copy(SortingValues, ChunkBasedEntityIndices[unfilteredChunkIndex], chunkSortingValues, 0, chunkSortingValues.Length);
            }
        }
        [BurstCompile]
        private struct DisposeArray<TElement> : IJob
            where TElement : unmanaged
        {
            [ReadOnly]
            [NativeDisableParallelForRestriction]
            [NativeDisableContainerSafetyRestriction]
            [DeallocateOnJobCompletion]
            public NativeArray<TElement> Array;

            public void Execute() {}
        }
        #endregion

        private const int LAYER_COUNT = 8;
        private const float PER_LAYER_OFFSET = 1f / LAYER_COUNT;

        private JobHandle RegularSort(in EntityQuery sortingQuery, int sortingLayer, ref SystemState state)
        {
            int spriteEntitiesCount = sortingQuery.CalculateEntityCount();

            if (spriteEntitiesCount == 0)
                return default;

            NativeArray<SortingData> sortingDataArray = new NativeArray<SortingData>(spriteEntitiesCount, Allocator.TempJob);
            // will use it to optimize sorting
            NativeArray<int> dataPointers = new NativeArray<int>(spriteEntitiesCount, Allocator.TempJob);
            // will use it to write back result values
            NativeArray<SortingValue> sortingValues = new NativeArray<SortingValue>(spriteEntitiesCount, Allocator.TempJob);

            NativeArray<int> chunkBaseEntityIndices = sortingQuery.CalculateBaseEntityIndexArrayAsync(Allocator.TempJob, default, out JobHandle calculateChunkBaseEntityIndices);

            GatherSortingDataJob gatherSortingDataJob = new GatherSortingDataJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                WorldPosition2DCth = SystemAPI.GetComponentTypeHandle<LocalToWorld2D>(true),
                SortingIndexCth = SystemAPI.GetComponentTypeHandle<SortingIndex>(true),
                Pointers = dataPointers,
                SortingDataArray = sortingDataArray,
                ChunkBasedEntityIndices = chunkBaseEntityIndices
            };
            JobHandle gatherSortingDataHandle = gatherSortingDataJob.ScheduleParallelByRef(sortingQuery, JobHandle.CombineDependencies(calculateChunkBaseEntityIndices, state.Dependency));

            // after sorting dataPointers get sorted while sortingDataArray stay the same
            JobHandle sortHandle = new SortArrayJob<int, SortingDataComparer>
            {
                Array = dataPointers,
                Comparer = new SortingDataComparer
                {
                    SourceData = sortingDataArray,
                    SourceDataComparer = new SortingData.GeneralComparer()
                }
            }.Schedule(gatherSortingDataHandle);

            _ = new DisposeArray<SortingData> { Array = sortingDataArray }.Schedule(sortHandle);
            //_ = sortingDataArray.Dispose(sortHandle);

            JobHandle genSortingValuesJob = new GenerateSortingValuesJob
            {
                LayerIndex = sortingLayer,
                SortingValues = sortingValues,
                Pointers = dataPointers,
            }.ScheduleBatch(sortingValues.Length, 32, sortHandle);

            //_ = dataPointers.Dispose(genSortingValuesJob);
            _ = new DisposeArray<int> { Array = dataPointers }.Schedule(genSortingValuesJob);

            WriteSortingValuesToChunksJob writeBackChunkDataJob = new WriteSortingValuesToChunksJob
            {
                SortingValues = sortingValues,
                SortingValueCthWo = SystemAPI.GetComponentTypeHandle<SortingValue>(false),
                ChunkBasedEntityIndices = chunkBaseEntityIndices
            };
            JobHandle writeBackChunkDataHandle = writeBackChunkDataJob.ScheduleParallelByRef(sortingQuery, genSortingValuesJob);

            _ = new DisposeArray<int> { Array = chunkBaseEntityIndices }.Schedule(writeBackChunkDataHandle);
            _ = new DisposeArray<SortingValue> { Array = sortingValues }.Schedule(writeBackChunkDataHandle);

            return writeBackChunkDataHandle;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            SystemData systemData = new SystemData();
            EntityQueryBuilder queryBuilder = new EntityQueryBuilder(Allocator.Temp)
                                             .WithNone<CullSpriteTag>()
                                             .WithAll<LocalToWorld2D>()
                                             .WithAll<SortingValue>()
                                             .WithAll<SortingIndex>()
                                             .WithAll<SortingLayer>()
                                             .WithAll<VisualSortingTag>()
                                             .WithNone<SortingStaticTag>();
            systemData.SortingSpritesQuery = state.GetEntityQuery(queryBuilder);

            queryBuilder.Reset();
            _ = queryBuilder
                .WithNone<CullSpriteTag>()
                .WithAll<LocalToWorld2D>()
                .WithAll<SortingValue>()
                .WithAll<SortingIndex>()
                .WithAll<SortingLayer>()
                .WithAll<VisualSortingTag>()
                .WithAll<SortingStaticTag>();
            systemData.SortingStaticSpritesQuery = state.GetEntityQuery(queryBuilder);

            _ = state.EntityManager.AddComponentData(state.SystemHandle, systemData);

            queryBuilder.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemData systemData = SystemAPI.GetComponent<SystemData>(state.SystemHandle);
            bool sortingSpritesIsEmpty = systemData.SortingSpritesQuery.IsEmpty;
            systemData.SortingSpritesQuery.AddOrderVersionFilter();
            bool sortingStaticSpritesIsEmpty = systemData.SortingSpritesQuery.IsEmpty;
            systemData.SortingSpritesQuery.ResetFilter();

            if (sortingSpritesIsEmpty && sortingStaticSpritesIsEmpty)
                return;

            state.EntityManager.GetAllUniqueSharedComponents(out NativeList<SortingLayer> sortingLayers, Allocator.Temp);
            bool                   bothModes           = !sortingSpritesIsEmpty & !sortingSpritesIsEmpty;
            NativeArray<JobHandle> handles             = new NativeArray<JobHandle>(sortingLayers.Length * (bothModes ? 2 : 1), Allocator.Temp);
            int                    staticHandlesOffset = bothModes ? sortingLayers.Length : 0;

            if (!sortingSpritesIsEmpty)
            {
                for (int i = 0; i < sortingLayers.Length; i++)
                {
                    SortingLayer sortingLayer = sortingLayers[i];
                    systemData.SortingSpritesQuery.SetSharedComponentFilter(sortingLayer);
                    handles[i] = RegularSort(systemData.SortingSpritesQuery, sortingLayer.Index, ref state);
                }
            }

            if (!sortingStaticSpritesIsEmpty)
            {
                for (int i = 0; i < sortingLayers.Length; i++)
                {
                    SortingLayer sortingLayer = sortingLayers[i];
                    systemData.SortingStaticSpritesQuery.SetSharedComponentFilter(sortingLayer);
                    handles[staticHandlesOffset + i] = RegularSort(systemData.SortingStaticSpritesQuery, sortingLayer.Index, ref state);
                }
            }

            state.Dependency = JobHandle.CombineDependencies(handles);
        }
    }
}