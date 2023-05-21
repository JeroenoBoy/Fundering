﻿using System.Collections.Generic;
using System.Linq;
using Fundering.Animation.Components;
using Fundering.Animation.Data;
using Fundering.Base.Authoring;
using Fundering.Base.Components.Properties;
using NSprites;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;



namespace Fundering.Animation.Authoring
{
    /// <summary>
    /// Advanced <see cref="SpriteRendererAuthoring"/> which also bakes animation data as blob asset and adds animation components.
    /// </summary>
    public class SpriteAnimationAuthoring : SpriteRendererAuthoring
    {
        private class Baker : Baker<SpriteAnimationAuthoring>
        {
            public override void Bake(SpriteAnimationAuthoring authoring)
            {
                if(!authoring.IsValid)
                    return;

                Entity entity = GetEntity(TransformUsageFlags.None);

                BakeSpriteAnimation(this, entity, authoring.AnimationSet, authoring.InitialAnimationIndex);

                SpriteAnimation initialAnimData = authoring.AnimationSet.Animations.ElementAt(authoring.InitialAnimationIndex).data;
                float4 initialAnimUVAtlas = (float4)NSpritesUtils.GetTextureST(initialAnimData.SpriteSheet);

                BakeSpriteRender
                (
                    this,
                    entity,
                    authoring,
                    new float4(new float2(initialAnimUVAtlas.xy / initialAnimData.FrameCount), initialAnimUVAtlas.zw),
                    authoring._tilingAndOffset,
                    authoring._pivot,
                    authoring.VisualSize,
                    flipX: authoring._flip.x,
                    flipY: authoring._flip.y
                );
                if(!authoring._disableSorting)
                    BakeSpriteSorting
                    (
                        this,
                        entity,
                        authoring._sortingIndex,
                        authoring._sortingLayer,
                        authoring._staticSorting
                    );
            }
        }

        [Header("Animation Data")]
        [FormerlySerializedAs("_animationSet")] public SpriteAnimationSet AnimationSet;
        [FormerlySerializedAs("_initialAnimationIndex")] public int InitialAnimationIndex;

        public override float2 VisualSize
        {
            get
            {
                SpriteAnimation animationData = AnimationSet.Animations.ElementAt(InitialAnimationIndex).data;
                return GetSpriteSize(animationData.SpriteSheet) / animationData.FrameCount;
            }
        }

        protected override bool IsValid
        {
            get
            {
                if (AnimationSet == null)
                {
                    Debug.LogWarning(new NSpritesException($"{nameof(AnimationSet)} is null"));
                    return false;
                }

                if (InitialAnimationIndex >= AnimationSet.Animations.Count)
                {
                    Debug.LogWarning(new NSpritesException($"{nameof(InitialAnimationIndex)} can't be greater than animations count. {nameof(InitialAnimationIndex)}: {InitialAnimationIndex}, animation count: {AnimationSet.Animations.Count}"));
                    return false;
                }

                if (InitialAnimationIndex < 0)
                {
                    Debug.LogWarning(new NSpritesException($"{nameof(InitialAnimationIndex)} can't be lower 0. Currently it is {InitialAnimationIndex}"));
                    return false;
                }

                return true;
            }
        }

        public static void BakeSpriteAnimation<TAuthoring>(Baker<TAuthoring> baker, in Entity entity, SpriteAnimationSet animationSet, int initialAnimationIndex = 0)
            where TAuthoring : MonoBehaviour
        {
            if(baker == null)
            {
                Debug.LogError(new NSpritesException("Passed Baker is null"));
                return;
            }
            if (animationSet == null)
            {
                Debug.LogError(new NSpritesException("Passed AnimationSet is null"));
                return;
            }

            baker.DependsOn(animationSet);

            if (animationSet == null)
                return;

            if (initialAnimationIndex >= animationSet.Animations.Count || initialAnimationIndex < 0)
            {
                Debug.LogError(new NSpritesException($"Initial animation index {initialAnimationIndex} can't be less than 0 or great/equal to animation count {animationSet.Animations.Count}"));
                return;
            }
            
            #region create animation blob asset
            BlobBuilder                                            blobBuilder    = new BlobBuilder(Allocator.Temp); //can't use `using` keyword because there is extension which use this + ref
            ref BlobArray<SpriteAnimationBlobData>                 root           = ref blobBuilder.ConstructRoot<BlobArray<SpriteAnimationBlobData>>();
            IReadOnlyCollection<SpriteAnimationSet.NamedAnimation> animations     = animationSet.Animations;
            BlobBuilderArray<SpriteAnimationBlobData>              animationArray = blobBuilder.Allocate(ref root, animations.Count);

            int animIndex = 0;
            foreach (SpriteAnimationSet.NamedAnimation anim in animations)
            {
                SpriteAnimation animData = anim.data;
                float animationDuration = 0f;
                for (int i = 0; i < animData.FrameDurations.Length; i++)
                    animationDuration += animData.FrameDurations[i];

                animationArray[animIndex] = new SpriteAnimationBlobData
                {
                    ID = Animator.StringToHash(anim.name),
                    GridSize = animData.FrameCount,
                    UVAtlas = NSpritesUtils.GetTextureST(animData.SpriteSheet),
                    Scale2D = new float2(animData.SpriteSheet.bounds.size.x, animData.SpriteSheet.bounds.size.y),
                    AnimationDuration = animationDuration
                    // FrameDuration - allocate lately
                };

                BlobBuilderArray<float> durations = blobBuilder.Allocate(ref animationArray[animIndex].FrameDurations, animData.FrameDurations.Length);
                for (int di = 0; di < durations.Length; di++)
                    durations[di] = animData.FrameDurations[di];

                animIndex++;
            }

            BlobAssetReference<BlobArray<SpriteAnimationBlobData>> blobAssetReference = blobBuilder.CreateBlobAssetReference<BlobArray<SpriteAnimationBlobData>>(Allocator.Persistent);
            baker.AddBlobAsset(ref blobAssetReference, out _);
            blobBuilder.Dispose();
            #endregion

            ref SpriteAnimationBlobData initialAnim = ref blobAssetReference.Value[initialAnimationIndex];

            baker.AddComponent(entity, new AnimationSetLink { value = blobAssetReference });
            baker.AddComponent(entity, new AnimationIndex { value = initialAnimationIndex });
            baker.AddComponent(entity, new AnimationTimer { value = initialAnim.FrameDurations[0] });
            baker.AddComponent<FrameIndex>(entity);
            
            baker.AddComponent(entity, new MainTexSTInitial { value = initialAnim.UVAtlas });
        }
    }
}