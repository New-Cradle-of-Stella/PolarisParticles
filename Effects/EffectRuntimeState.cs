namespace Polaris.Particles.Effects
{
    /// <summary><see cref="EffectRuntime"/> 的生命周期状态。</summary>
    public enum EffectRuntimeState
    {
        /// <summary>仍在播放。</summary>
        Playing,

        /// <summary>已被 <see cref="EffectRuntime.Stop"/> 或 <see cref="EffectRuntime.Dispose"/> 主动停止。</summary>
        Stopped,

        /// <summary>原版已自然播放结束（或底层对象已被复用于其他效果）。</summary>
        Completed,
    }
}
