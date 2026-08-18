using System;

namespace Polaris.Particles.Effects
{
    /// <summary>PolarisParticles 的特效能力入口：.peffect 文件登记与运行时播放。</summary>
    public sealed class EffectAPI
    {
        internal EffectAPI() { }

        public EffectFileAPI Files { get; } = new EffectFileAPI();

        /// <summary>key 是否已注册为单粒子模板。</summary>
        public bool ContainsParticle(string key) => EffectPlaybackBackend.ContainsParticle(key);

        /// <summary>key 是否已注册为 SETTER 时间线。</summary>
        public bool ContainsTimeline(string key) => EffectPlaybackBackend.ContainsTimeline(key);

        /// <summary>key 是否已注册为攻击残影（AGD）。</summary>
        public bool ContainsAttackGhost(string key) => EffectPlaybackBackend.ContainsAttackGhost(key);

        /// <summary>播放一个 SETTER 时间线；失败时抛出 <see cref="InvalidOperationException"/>。</summary>
        public EffectRuntime PlayTimeline(string key, EffectPlayRequest request)
        {
            if (!TryPlayTimeline(key, request, out EffectRuntime runtime, out EffectPlayFailure failure))
                throw new InvalidOperationException(EffectPlaybackBackend.DescribeFailure("timeline", key, failure));
            return runtime;
        }

        /// <summary>
        /// 播放一个 SETTER 时间线。地图未加载或 key 不存在是可恢复失败，返回 false 而不抛异常。
        /// </summary>
        public bool TryPlayTimeline(string key, EffectPlayRequest request, out EffectRuntime runtime, out EffectPlayFailure failure)
        {
            RequireKey(key);
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EffectPlaybackBackend.TryPlayTimeline(key, request, out runtime, out failure);
        }

        /// <summary>生成一个单粒子；失败时抛出 <see cref="InvalidOperationException"/>。</summary>
        public EffectRuntime SpawnParticle(string key, ParticleSpawnRequest request)
        {
            if (!TrySpawnParticle(key, request, out EffectRuntime runtime, out EffectPlayFailure failure))
                throw new InvalidOperationException(EffectPlaybackBackend.DescribeFailure("particle", key, failure));
            return runtime;
        }

        /// <summary>
        /// 生成一个单粒子。地图未加载、key 不存在或容器已满都是可恢复失败，返回 false 而不抛异常。
        /// </summary>
        public bool TrySpawnParticle(string key, ParticleSpawnRequest request, out EffectRuntime runtime, out EffectPlayFailure failure)
        {
            RequireKey(key);
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EffectPlaybackBackend.TrySpawnParticle(key, request, out runtime, out failure);
        }

        /// <summary>开启一个播放 scope；scope.Dispose() 会统一停止它播放过的全部实例。</summary>
        public EffectScope BeginScope() => new EffectScope(this);

        private static void RequireKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(".peffect key is required.", nameof(key));
        }
    }
}
