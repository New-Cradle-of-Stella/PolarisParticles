using Polaris.Components;

namespace Polaris.Particles
{
    /// <summary>自定义粒子与特效能力的组件入口。</summary>
    public sealed class PolarisParticlesComponent : PolarisComponent
    {
        public override string Id => "PolarisParticles";
        public override int Order => 900;
    }
}
