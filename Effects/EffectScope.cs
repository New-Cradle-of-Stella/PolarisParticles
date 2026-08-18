using System;
using System.Collections.Generic;

namespace Polaris.Particles.Effects
{
    /// <summary>
    /// 一次施法/阶段拥有的 <see cref="EffectRuntime"/> 集合。Dispose 时统一停止所有仍存活的实例，
    /// 适合放进 using 块，随施法函数退出自动清理。
    /// </summary>
    public sealed class EffectScope : IDisposable
    {
        private readonly EffectAPI _api;
        private readonly List<EffectRuntime> _tracked = new List<EffectRuntime>();
        private bool _disposed;

        internal EffectScope(EffectAPI api)
        {
            _api = api;
        }

        public EffectRuntime PlayTimeline(string key, EffectPlayRequest request)
        {
            RequireActive();
            return Track(_api.PlayTimeline(key, request));
        }

        public bool TryPlayTimeline(string key, EffectPlayRequest request, out EffectRuntime runtime, out EffectPlayFailure failure)
        {
            RequireActive();
            bool ok = _api.TryPlayTimeline(key, request, out runtime, out failure);
            if (ok)
                Track(runtime);
            return ok;
        }

        public EffectRuntime SpawnParticle(string key, ParticleSpawnRequest request)
        {
            RequireActive();
            return Track(_api.SpawnParticle(key, request));
        }

        public bool TrySpawnParticle(string key, ParticleSpawnRequest request, out EffectRuntime runtime, out EffectPlayFailure failure)
        {
            RequireActive();
            bool ok = _api.TrySpawnParticle(key, request, out runtime, out failure);
            if (ok)
                Track(runtime);
            return ok;
        }

        /// <summary>停止本 scope 播放过的全部实例，并清空跟踪列表。</summary>
        public void Stop(EffectStopMode mode = EffectStopMode.IncludeSpawnedEffects)
        {
            for (int i = 0; i < _tracked.Count; i++)
                _tracked[i].Stop(mode);
            _tracked.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Stop();
        }

        private EffectRuntime Track(EffectRuntime runtime)
        {
            _tracked.RemoveAll(item => !item.IsAlive);
            if (runtime.IsAlive)
                _tracked.Add(runtime);
            return runtime;
        }

        private void RequireActive()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EffectScope));
        }
    }
}
