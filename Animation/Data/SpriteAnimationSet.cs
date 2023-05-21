using System;
using System.Collections.Generic;
using UnityEngine;



namespace Fundering.Animation.Data
{
    [CreateAssetMenu(fileName = "NewNSpriteAnimationSet", menuName = "NSprites/Animation Set")]
    public class SpriteAnimationSet : ScriptableObject
    {
        [Serializable]
        public struct NamedAnimation
        {
            public string name;
            public SpriteAnimation data;
        }

        [SerializeField] private NamedAnimation[] _animations;

        public IReadOnlyCollection<NamedAnimation> Animations => _animations;
    }
}