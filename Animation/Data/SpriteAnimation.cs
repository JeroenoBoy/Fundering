using Unity.Mathematics;
using UnityEngine;


namespace Fundering.Animation.Data
{
    [CreateAssetMenu(fileName = "NewNSpriteAnimation", menuName = "NSprites/Animation (frame sequence)")]
    public class SpriteAnimation : ScriptableObject
    {
        public Sprite  SpriteSheet;
        public int2    FrameCount     = new(1);
        public float[] FrameDurations = new float[1] { 0.1f };

        #region Editor
#if UNITY_EDITOR
        private const float DefaultFrameDuration = .1f;
        private void OnValidate()
        {
            int frameCount = FrameCount.x * FrameCount.y;
            if (FrameDurations.Length != frameCount)
            {
                float[] correctedFrameDurations = new float[frameCount];
                int minLength               = math.min(FrameDurations.Length, correctedFrameDurations.Length);
                for (int i = 0; i < minLength; i++)
                    correctedFrameDurations[i] = FrameDurations[i];
                for (int i = minLength; i < correctedFrameDurations.Length; i++)
                    correctedFrameDurations[i] = DefaultFrameDuration;
                FrameDurations = correctedFrameDurations;
            }
        }
#endif
        #endregion
    }
}