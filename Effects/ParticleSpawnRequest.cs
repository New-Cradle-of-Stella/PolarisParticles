using System;

namespace Polaris.Particles.Effects
{
    /// <summary>一次单粒子生成请求：坐标、层、寿命与起播偏移。</summary>
    public sealed class ParticleSpawnRequest
    {
        private ParticleSpawnRequest(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static ParticleSpawnRequest At(float x, float y, float z = 0f) => new ParticleSpawnRequest(x, y, z);

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public int? LifeFrames { get; private set; }
        public int StartOffsetFrames { get; private set; }
        public EffectLayer Layer { get; private set; } = EffectLayer.World;

        /// <summary>
        /// 显式覆盖寿命帧数。只有模板本身声明了 <c>time maxt</c>（原版 rep_time）才会真正生效；
        /// 不设置时按模板自身的 <c>maxt</c> 播放。
        /// </summary>
        public ParticleSpawnRequest WithLife(int frames)
        {
            if (frames < 1)
                throw new ArgumentOutOfRangeException(nameof(frames));
            LifeFrames = frames;
            return this;
        }

        public ParticleSpawnRequest WithDelay(int frames)
        {
            if (frames < 0)
                throw new ArgumentOutOfRangeException(nameof(frames));
            StartOffsetFrames = frames;
            return this;
        }

        public ParticleSpawnRequest WithStartAge(int frames)
        {
            if (frames < 0)
                throw new ArgumentOutOfRangeException(nameof(frames));
            StartOffsetFrames = -frames;
            return this;
        }

        public ParticleSpawnRequest OnLayer(EffectLayer layer)
        {
            Layer = layer;
            return this;
        }
    }
}
