using System;
using UnityEngine;
using UnityEngine.InputSystem;
using XX;

namespace Polaris.Particles.Debugging
{
    internal static class PEffectDebugRuntime
    {
        private static GameObject _root;

        internal static bool IsEnabled { get; private set; }

        internal static void Start(bool enabled)
        {
            IsEnabled = enabled;
            if (!enabled)
                return;

            // 原版的 @include 可能被外部文件白名单过滤；additional file 是它公开提供的扩展入口。
            EfParticleManager.addAdditionalFile(PEffectParticleScriptPatch.DebugRuntimeFile);

            _root = new GameObject("PolarisParticles Debug");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            PEffectDebugPump.EnsureInstance(_root);
            PEffectDebugServer.Start();
            Debug.Log("[PolarisParticles] .peffect live debug enabled; F9 opens the particle debug page.");
        }

        internal static void Update()
        {
            if (!IsEnabled)
                return;

            try
            {
                if (IN.getKD(Key.F9) && KEY.getModifier() == MODIF.NONE)
                    PEffectDebugPage.Toggle();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "PolarisParticles F9 debug hotkey", typeof(PEffectDebugRuntime).Assembly);
            }
        }

        internal static void Shutdown()
        {
            if (!IsEnabled)
                return;

            PEffectDebugPage.Shutdown();
            PEffectDebugServer.Stop();
            PEffectDebugStore.Clear();
            EfParticleManager.remAdditionalFile(PEffectParticleScriptPatch.DebugRuntimeFile);

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            IsEnabled = false;
        }
    }
}
