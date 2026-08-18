using System;
using System.Linq;
using System.Reflection;
using Polaris.Components;
using Polaris.Particles.Debugging;
using Polaris.Particles.Effects;
using UnityEngine;
using XX;

namespace Polaris.Particles
{
    /// <summary>自定义粒子与特效能力的组件入口。</summary>
    public sealed class PolarisParticlesComponent : PolarisComponent
    {
        public override string Id => "PolarisParticles";
        public override int Order => 900;

        public override void Start()
        {
            Assembly[] assemblies = PolarisAPI.Modules.PluginAssemblies
                .Concat(PolarisAPI.Modules.ComponentAssemblies)
                .Where(assembly => assembly != null)
                .Distinct()
                .ToArray();

            try
            {
                EffectFileRegistry.Instance.ScanEmbedded(assemblies);
                EffectFileSnapshot snapshot = EffectFileRegistry.Instance.Seal();
                if (snapshot.Files.Count != 0)
                {
                    EfParticleManager.addAdditionalFile(PEffectParticleScriptPatch.ProductionRuntimeFile);
                    if (EfParticleManager.initted)
                        EfParticleManager.reloadParticleCsv(true);
                    Debug.Log($"[PolarisParticles] Registered {snapshot.Files.Count} embedded .peffect file(s).");
                }
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "PolarisParticles .peffect registration", typeof(PolarisParticlesComponent).Assembly);
            }

            bool enabled = PolarisAPI.Modules.PluginAssemblies.Any(HasDebugMarker);
            PEffectDebugRuntime.Start(enabled);
        }

        public override void Update()
        {
            PEffectDebugRuntime.Update();
        }

        public override void Shutdown()
        {
            PEffectDebugRuntime.Shutdown();
            if (EffectFileRegistry.Instance.Files.Count != 0)
                EfParticleManager.remAdditionalFile(PEffectParticleScriptPatch.ProductionRuntimeFile);
            EffectFileRegistry.Instance.Reset();
        }

        private static bool HasDebugMarker(Assembly assembly)
        {
            if (assembly == null)
                return false;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 部分类型加载失败时仍然检查已经加载出来的那些。
                types = ex.Types;
            }

            return types.Any(type => type?.GetCustomAttribute<PEffectDebugEnabledAttribute>() != null);
        }
    }
}
