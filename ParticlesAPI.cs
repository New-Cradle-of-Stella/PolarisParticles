using Polaris.Particles.Effects;

namespace Polaris.Particles
{
    /// <summary>PolarisParticles 对外 API 根入口。</summary>
    public static class ParticlesAPI
    {
        public static EffectAPI Effects { get; } = new EffectAPI();
    }
}
