using System;
using XX;

namespace Polaris.Particles.Effects
{
    /// <summary>
    /// 一次播放的句柄：包装原版 <c>PTCThread</c>（SETTER）或 <c>EffectItem</c>（单粒子）token。
    /// 存活判定复用原版自身的复用防护——PTCThread 靠 id，EffectItem 靠容器引用与 index——避免对象
    /// 被池回收后误停到别的效果上。
    /// </summary>
    public sealed class EffectRuntime : IDisposable
    {
        private readonly EffectItem _particle;
        private readonly uint _particleIndex;
        private readonly PTCThread _timeline;
        private readonly uint _timelineId;
        private bool _stopped;

        private EffectRuntime(string key)
        {
            Key = key;
        }

        private EffectRuntime(string key, EffectItem particle, uint particleIndex)
        {
            Key = key;
            _particle = particle;
            _particleIndex = particleIndex;
        }

        private EffectRuntime(string key, PTCThread timeline, uint timelineId)
        {
            Key = key;
            _timeline = timeline;
            _timelineId = timelineId;
        }

        internal static EffectRuntime ForParticle(EffectItem item, string key) =>
            new EffectRuntime(key, item, item.index);

        internal static EffectRuntime ForTimeline(PTCThread thread, string key) =>
            new EffectRuntime(key, thread, thread.id);

        internal static EffectRuntime Completed(string key) => new EffectRuntime(key);

        public string Key { get; }

        /// <summary>
        /// 是否仍然存活。
        /// </summary>
        public bool IsAlive
        {
            get
            {
                if (_stopped)
                    return false;
                if (_timeline != null)
                    return _timeline.isActive(_timelineId);
                return _particle != null && _particle.EF != null && _particle.index == _particleIndex;
            }
        }

        public EffectRuntimeState State
        {
            get
            {
                if (IsAlive)
                    return EffectRuntimeState.Playing;
                return _stopped ? EffectRuntimeState.Stopped : EffectRuntimeState.Completed;
            }
        }

        /// <summary>停止播放。对已经停止或已经自然结束/被回收的实例调用是安全的空操作。</summary>
        public void Stop(EffectStopMode mode = EffectStopMode.IncludeSpawnedEffects)
        {
            // IsAlive 已经涵盖了"之前停过"这一种情况。
            if (!IsAlive)
                return;

            if (_timeline != null)
                _timeline.kill(mode == EffectStopMode.TimelineOnly);
            else
                _particle.destruct();
            _stopped = true;
        }

        public void Dispose() => Stop();
    }
}
