using System.Collections.Generic;
using Fundering.Base.Components.Properties;
using Fundering.Base.Components.Regular;
using Fundering.Sorting.Components;
using Fundering.Sorting.Systems;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using math = Fundering._2DTransform.math;

[assembly: RegisterGenericJobType(typeof(SpriteSortingSystem.SortArrayJob<int, SpriteSortingSystem.SortingDataComparer>))]

namespace Fundering.Sorting.Systems
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial struct SpriteSortingSystem : ISystem
    {
        #region data
        private struct SystemData : IComponentData
        {
            public EntityQuery sortingSpritesQuery;
            public EntityQuery sortingStaticSpritesQuery;
        }
        internal struct SortingData
        {
            internal struct GeneralComparer : IComparer<SortingData>
            {
                public int Compare(SortingData x, SortingData y)
                {
                    return x.sortingIndex.CompareTo(y.sortingIndex) * -4 //less index -> later in render
                        + x.position.y.CompareTo(y.position.y) * 2
                        + x.id.CompareTo(y.id);
                }
            }

            public int id;
            public int sortingIndex;
            public float2 position;
#if UNITY_EDITOR
            public override string ToString()
            {
                return $"id: {id}, sortIndex: {sortingIndex}, pos: {position}";
            }
#endif
        }
        internal struct SortingDataComparer : IComparer<int>
        {
            public NativeArray<SortingData> sourceData;
            public SortingData.GeneralComparer sourceDataComparer;

            public int Compare(int x, int y) => sourceDataComparer.Compare(sourceData[x], sourceData[y]);
        }
        #endregion

        #region jobs
        [BurstCompile]
        private struct GatherSortingDataJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle entityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<WorldPosition2D> worldPosition2D_CTH;
            [ReadOnly] public ComponentTypeHandle<SortingIndex> sortingIndex_CTH;
            [WriteOnly][NativeDisableContainerSafetyRestriction] public NativeArray<SortingData> sortingDataArray;
            [WriteOnly][NativeDisableContainerSafetyRestriction] public NativeArray<int> pointers;
            [ReadOnly] public NativeArray<int> chunkBasedEntityIndices;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Entity>       entityArray          = chunk.GetNativeArray(entityTypeHandle);
                var                       worldPosition2DArray = chunk.GetNativeArray(ref worldPosition2D_CTH);
                NativeArray<SortingIndex> sortingIndexes       = chunk.GetNativeArray(ref sortingIndex_CTH);
                int                       firstEntityIndex     = chunkBasedEntityIndices[unfilteredChunkIndex];
                for (int entityIndex = 0; entityIndex < entityArray.Length; entityIndex++)
                {
                    int arrayIndex = firstEntityIndex + entityIndex;
                    sortingDataArray[arrayIndex] = new SortingData
                    {
                        position = worldPosition2DArray[entityIndex].value,
                        sortingIndex = sortingIndexes[entityIndex].value,
                        id = entityArray[entityIndex].Index
                    };
                    pointers[arrayIndex] = arrayIndex;
                }
            }
        }
        [BurstCompile]
        internal struct SortArrayJob<TElement, TComparer> : IJob
            where TElement : unmanaged
            where TComparer : unmanaged, IComparer<TElement>
        {
            public NativeArray<TElement> array;
            public TComparer comparer;

            public void Execute() => array.Sort(comparer);
        }
        [BurstCompile]
        private struct GenerateSortingValuesJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeArray<int> pointers;
            [WriteOnly][NativeDisableParallelForRestriction] public NativeArray<SortingValue> sortingValues;
            public int layerIndex;

            public void Execute(int startIndex, int count)
            {
                int toIndex = startIndex + count;
                for (int i = startIndex; i < toIndex; i++)
                {
                    float from = PerLayerOffset * layerIndex;
                    float to = from + PerLayerOffset;
                    sortingValues[pointers[i]] = new SortingValue { value = math.lerp(from, to, 1f - (float)i / pointers.Length) };
                }
            }
        }
        [BurstCompile]
        private struct WriteSortingValuesToChunksJob : IJobChunk
        {
            [ReadOnly] public NativeArray<int> chunkBasedEntityIndices;
            [NativeDisableContainerSafetyRestriction] public ComponentTypeHandle<SortingValue> sortingValue_CTH_WO;
            [ReadOnly] public NativeArray<SortingValue> sortingValues;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<SortingValue> chunkSortingValues = chunk.GetNativeArray(ref sortingValue_CTH_WO);
                NativeArray<SortingValue>.Copy(sortingValues, chunkBasedEntityIndices[unfilteredChunkIndex], chunkSortingValues, 0, chunkSortingValues.Length);
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
            public NativeArray<TElement> array;

            public void Execute() {}
        }
        #endregion

        private const int LayerCount = 8;
        private const float PerLayerOffset = 1f / LayerCount;

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
                entityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                worldPosition2D_CTH = SystemAPI.GetComponentTypeHandle<WorldPosition2D>(true),
                sortingIndex_CTH = SystemAPI.GetComponentTypeHandle<SortingIndex>(true),
                pointers = dataPointers,
                sortingDataArray = sortingDataArray,
                chunkBasedEntityIndices = chunkBaseEntityIndices
            };
            JobHandle gatherSortingDataHandle = gatherSortingDataJob.ScheduleParallelByRef(sortingQuery, JobHandle.CombineDependencies(calculateChunkBaseEntityIndices, state.Dependency));

            // after sorting dataPointers get sorted while sortingDataArray stay the same
            JobHandle sortHandle = new SortArrayJob<int, SortingDataComparer>
            {
                array = dataPointers,
                comparer = new()
                {
                    sourceData = sortingDataArray,
                    sourceDataComparer = new()
                }
            }.Schedule(gatherSortingDataHandle);

            _ = new DisposeArray<SortingData> { array = sortingDataArray }.Schedule(sortHandle);
            //_ = sortingDataArray.Dispose(sortHandle);

            JobHandle genSortingValuesJob = new GenerateSortingValuesJob
            {
                layerIndex = sortingLayer,
                sortingValues = sortingValues,
                pointers = dataPointers,
            }.ScheduleBatch(sortingValues.Length, 32, sortHandle);

            //_ = dataPointers.Dispose(genSortingValuesJob);
            _ = new DisposeArray<int> { array = dataPointers }.Schedule(genSortingValuesJob);

            WriteSortingValuesToChunksJob writeBackChunkDataJob = new WriteSortingValuesToChunksJob
            {
                sortingValues = sortingValues,
                sortingValue_CTH_WO = SystemAPI.GetComponentTypeHandle<SortingValue>(false),
                chunkBasedEntityIndices = chunkBaseEntityIndices
            };
            JobHandle writeBackChunkDataHandle = writeBackChunkDataJob.ScheduleParallelByRef(sortingQuery, genSortingValuesJob);

            //_ = chunkBaseEntityIndices.Dispose(writeBackChunkDataHandle);
            //_ = sortingValues.Dispose(writeBackChunkDataHandle);
            _ = new DisposeArray<int> { array = chunkBaseEntityIndices }.Schedule(writeBackChunkDataHandle);
            _ = new DisposeArray<SortingValue> { array = sortingValues }.Schedule(writeBackChunkDataHandle);

            return writeBackChunkDataHandle;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            SystemData systemData = new SystemData();
            EntityQueryBuilder queryBuilder = new EntityQueryBuilder(Allocator.Temp)
                                             .WithNone<CullSpriteTag>()
                                             .WithAll<WorldPosition2D>()
                                             .WithAll<SortingValue>()
                                             .WithAll<SortingIndex>()
                                             .WithAll<SortingLayer>()
                                             .WithAll<VisualSortingTag>()
                                             .WithNone<SortingStaticTag>();
            systemData.sortingSpritesQuery = state.GetEntityQuery(queryBuilder);

            queryBuilder.Reset();
            _ = queryBuilder
                .WithNone<CullSpriteTag>()
                .WithAll<WorldPosition2D>()
                .WithAll<SortingValue>()
                .WithAll<SortingIndex>()
                .WithAll<SortingLayer>()
                .WithAll<VisualSortingTag>()
                .WithAll<SortingStaticTag>();
            systemData.sortingStaticSpritesQuery = state.GetEntityQuery(queryBuilder);

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
            bool sortingSpritesIsEmpty = systemData.sortingSpritesQuery.IsEmpty;
            systemData.sortingSpritesQuery.AddOrderVersionFilter();
            bool sortingStaticSpritesIsEmpty = systemData.sortingSpritesQuery.IsEmpty;
            systemData.sortingSpritesQuery.ResetFilter();

            if (sortingSpritesIsEmpty && sortingStaticSpritesIsEmpty)
                return;

            state.EntityManager.GetAllUniqueSharedComponents<SortingLayer>(out NativeList<SortingLayer> sortingLayers, Allocator.Temp);
            bool                   bothModes           = !sortingSpritesIsEmpty & !sortingSpritesIsEmpty;
            NativeArray<JobHandle> handles             = new NativeArray<JobHandle>(sortingLayers.Length * (bothModes ? 2 : 1), Allocator.Temp);
            int                    staticHandlesOffset = bothModes ? sortingLayers.Length : 0;

            if (!sortingSpritesIsEmpty)
            {
                for (int i = 0; i < sortingLayers.Length; i++)
                {
                    SortingLayer sortingLayer = sortingLayers[i];
                    systemData.sortingSpritesQuery.SetSharedComponentFilter(sortingLayer);
                    handles[i] = RegularSort(systemData.sortingSpritesQuery, sortingLayer.index, ref state);
                }
            }

            if (!sortingStaticSpritesIsEmpty)
            {
                for (int i = 0; i < sortingLayers.Length; i++)
                {
                    SortingLayer sortingLayer = sortingLayers[i];
                    systemData.sortingStaticSpritesQuery.SetSharedComponentFilter(sortingLayer);
                    handles[staticHandlesOffset + i] = RegularSort(systemData.sortingStaticSpritesQuery, sortingLayer.index, ref state);
                }
            }

            state.Dependency = JobHandle.CombineDependencies(handles);
        }
    }
}