using System;
using HarmonyLib;
using Polaris.Particles.Effects;
using UnityEngine.InputSystem;
using XX;

namespace Polaris.Particles.Debugging
{
    [HarmonyPatch(typeof(EfParticleManager), "getParticleScript")]
    internal static class PEffectParticleScriptPatch
    {
        internal const string ProductionRuntimeFile = "__polaris_peffect_runtime";
        internal const string DebugRuntimeFile = "__polaris_peffect_debug_runtime";

        private static bool Prefix(string name, ref string __result)
        {
            if (string.Equals(name, DebugRuntimeFile, StringComparison.Ordinal))
            {
                __result = PEffectDebugStore.RuntimeText;
                return false;
            }

            if (string.Equals(name, ProductionRuntimeFile, StringComparison.Ordinal))
            {
                __result = EffectFileRegistry.Instance.RuntimeText;
                return false;
            }

            if (PEffectDebugRuntime.IsEnabled && PEffectDebugStore.TryGetText(name, out string debugText))
            {
                __result = debugText;
                return false;
            }

            if (!EffectFileRegistry.Instance.TryGetText(name, out string registeredText))
                return true;

            __result = registeredText;
            return false;
        }
    }

    /// <summary>粒子调试启用时独占无修饰键 F9，避免原版 ActiveDebugger 同帧触发文本重载。</summary>
    [HarmonyPatch(typeof(ActiveDebugger), "runIRD")]
    internal static class PEffectF9Patch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!PEffectDebugRuntime.IsEnabled || !IN.getKD(Key.F9) || KEY.getModifier() != MODIF.NONE)
                return true;

            __result = true;
            return false;
        }
    }
}
