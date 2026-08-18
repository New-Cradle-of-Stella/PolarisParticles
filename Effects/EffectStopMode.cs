namespace Polaris.Particles.Effects
{
    /// <summary><see cref="EffectRuntime.Stop"/> 的停止方式。</summary>
    public enum EffectStopMode
    {
        /// <summary>只停止 SETTER 时间线阅读器本身，由它派生并 stock 的子效果继续播放。对应原版 <c>PTCThread.kill(true)</c>。</summary>
        TimelineOnly,

        /// <summary>连同派生的 stock 子效果与循环音一起停止。对应原版 <c>PTCThread.kill(false)</c>。</summary>
        IncludeSpawnedEffects,
    }
}
