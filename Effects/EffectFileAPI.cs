using System.Collections.Generic;
using System.Reflection;

namespace Polaris.Particles.Effects
{
    /// <summary>.peffect 文件登记入口。通常无需手动调用：组件会自动扫描插件里的 EmbeddedResource。</summary>
    public sealed class EffectFileAPI
    {
        internal EffectFileAPI() { }

        /// <summary>
        /// 显式登记一份内嵌 .peffect，须在 PolarisParticles 的 Start 阶段封存目录之前调用（推荐放在模组 Awake 中）。
        /// virtualName 为空时取资源名最后一段作为文件名。
        /// </summary>
        public void RegisterEmbedded(Assembly owner, string resourceName, string virtualName = null) =>
            EffectFileRegistry.Instance.RegisterEmbedded(owner, resourceName, virtualName);

        /// <summary>已封存并送入原版加载器的文件目录；Start 之前为空。</summary>
        public IReadOnlyList<EffectFileRecord> Registered => EffectFileRegistry.Instance.Files;

        /// <summary>正式目录是否已经封存；封存后不再接受启动期登记。</summary>
        public bool IsSealed => EffectFileRegistry.Instance.IsSealed;
    }
}
