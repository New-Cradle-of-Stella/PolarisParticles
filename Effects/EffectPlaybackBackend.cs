using System;
using m2d;
using UnityEngine;
using XX;

namespace Polaris.Particles.Effects
{
    /// <summary>
    /// 运行时播放的原生 backend：解析当前地图的 IEffectSetter，调用原版 PtcN / PtcST，
    /// 并把结果包装成 <see cref="EffectRuntime"/>。公开 API（<see cref="EffectAPI"/>）不引用这里用到的原生类型。
    /// </summary>
    internal static class EffectPlaybackBackend
    {
        internal static bool ContainsParticle(string key) =>
            EfParticleManager.initted && !string.IsNullOrEmpty(key) && EfParticleManager.OPtc.ContainsKey(key);

        internal static bool ContainsTimeline(string key) =>
            EfParticleManager.initted && !string.IsNullOrEmpty(key) && EfParticleManager.OPtcSetter.ContainsKey(key);

        internal static bool ContainsAttackGhost(string key) =>
            EfParticleManager.initted && !string.IsNullOrEmpty(key) && EfParticleManager.OAgd.ContainsKey(key);

        internal static bool TrySpawnParticle(
            string key,
            ParticleSpawnRequest request,
            out EffectRuntime runtime,
            out EffectPlayFailure failure)
        {
            runtime = null;

            if (!TryResolveSetter(request.Layer, out IEffectSetter setter, out failure))
                return false;

            if (!ContainsParticle(key))
            {
                failure = EffectPlayFailure.KeyNotFound;
                return false;
            }

            int life = request.LifeFrames ?? ResolveDefaultLife(key);
            EffectItem item = setter.PtcN(key, request.X, request.Y, request.Z, life, request.StartOffsetFrames);
            if (item == null)
            {
                failure = EffectPlayFailure.EffectContainerFull;
                return false;
            }

            failure = EffectPlayFailure.None;
            runtime = EffectRuntime.ForParticle(item, key);
            return true;
        }

        internal static bool TryPlayTimeline(
            string key,
            EffectPlayRequest request,
            out EffectRuntime runtime,
            out EffectPlayFailure failure)
        {
            runtime = null;

            if (!TryResolveSetter(request.Layer, out IEffectSetter setter, out failure))
                return false;

            if (!ContainsTimeline(key))
            {
                failure = EffectPlayFailure.KeyNotFound;
                return false;
            }

            IEfPInteractale listener = request.Owner ?? new EffectPositionAnchor(request.X, request.Y);
            PTCThread.StFollow follow = request.Owner != null ? ToNative(request.FollowPoint) : PTCThread.StFollow.NO_FOLLOW;
            VariableP variables = BuildVariables(request);

            PTCThreadRunner.clearVars();
            PTCThread thread = setter.PtcST(key, listener, follow, variables);
            failure = EffectPlayFailure.None;
            runtime = thread == null ? EffectRuntime.Completed(key) : EffectRuntime.ForTimeline(thread, key);
            return true;
        }

        internal static string DescribeFailure(string kind, string key, EffectPlayFailure failure) => failure switch
        {
            EffectPlayFailure.MapNotLoaded => $"Cannot play {kind} '{key}': no map is currently loaded.",
            EffectPlayFailure.KeyNotFound => $"Cannot play {kind} '{key}': the key is not registered.",
            EffectPlayFailure.EffectContainerFull => $"Cannot play {kind} '{key}': the effect container is full.",
            _ => $"Cannot play {kind} '{key}'.",
        };

        private static bool TryResolveSetter(EffectLayer layer, out IEffectSetter setter, out EffectPlayFailure failure)
        {
            Map2d map = M2DBase.Instance?.curMap;
            setter = map == null ? null : (layer == EffectLayer.WorldTop ? map.getEffectTop() : map.getEffect());
            if (setter == null)
            {
                failure = EffectPlayFailure.MapNotLoaded;
                return false;
            }

            failure = EffectPlayFailure.None;
            return true;
        }

        private static VariableP BuildVariables(EffectPlayRequest request)
        {
            if (request.Parameters.Count == 0)
                return null;

            var variables = new VariableP(request.Parameters.Count);
            foreach (EffectParameter parameter in request.Parameters)
            {
                if (parameter.IsNumeric)
                    variables.Add(parameter.Key, parameter.NumberValue);
                else
                    variables.Add(parameter.Key, parameter.StringValue);
            }
            return variables;
        }

        private static int ResolveDefaultLife(string key)
        {
            EfParticle particle = EfParticleManager.Get(key, no_load: false, no_error: true);
            return particle == null ? 1 : Math.Max(1, Mathf.CeilToInt(particle.all_maxt));
        }

        private static PTCThread.StFollow ToNative(EffectFollowPoint point) => point switch
        {
            EffectFollowPoint.Caster => PTCThread.StFollow.FOLLOW_C,
            EffectFollowPoint.Target => PTCThread.StFollow.FOLLOW_T,
            EffectFollowPoint.Hip => PTCThread.StFollow.FOLLOW_HIP,
            EffectFollowPoint.Head => PTCThread.StFollow.FOLLOW_HEAD,
            EffectFollowPoint.Source => PTCThread.StFollow.FOLLOW_S,
            EffectFollowPoint.Destination => PTCThread.StFollow.FOLLOW_D,
            EffectFollowPoint.MagicCircle => PTCThread.StFollow.FOLLOW_MAGICCIRCLE,
            _ => PTCThread.StFollow.NO_FOLLOW,
        };
    }
}
