namespace Polaris.Particles.Effects
{
    /// <summary><c>TryPlayTimeline</c> / <c>TrySpawnParticle</c> 的可恢复失败原因。</summary>
    public enum EffectPlayFailure
    {
        /// <summary>没有失败。</summary>
        None,

        /// <summary>当前没有已加载的地图，或目标层的特效容器尚未就绪。</summary>
        MapNotLoaded,

        /// <summary>请求的 key 未在原版 Particle/SETTER 目录中注册。</summary>
        KeyNotFound,

        /// <summary>目标特效容器已满。</summary>
        EffectContainerFull,

        /// <summary>请求跟随的可追踪目标此刻不可用（已释放、已回池、已离开地图）。</summary>
        TargetUnavailable,
    }
}
