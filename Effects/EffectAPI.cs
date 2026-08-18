namespace Polaris.Particles.Effects
{
    /// <summary>PolarisParticles 的特效能力入口。本阶段先提供 .peffect 文件登记。</summary>
    public sealed class EffectAPI
    {
        internal EffectAPI() { }

        public EffectFileAPI Files { get; } = new EffectFileAPI();
    }
}
