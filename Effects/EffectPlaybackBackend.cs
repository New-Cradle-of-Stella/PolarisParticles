using System;
using m2d;
using Polaris.Drawing;
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

            if (!TryResolveListener(request, out IEfPInteractale listener, out PTCThread.StFollow follow, out failure))
                return false;

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
            EffectPlayFailure.TargetUnavailable => $"Cannot play {kind} '{key}': the follow target is not available.",
            _ => $"Cannot play {kind} '{key}'.",
        };

        /// <summary>
        /// 决定谁当这条时间线的 listener：原生 owner（支持骨骼 follow 点）、Polaris 侧可追踪目标（只有中心坐标，包一层动态 anchor）、
        /// 固定坐标，三者互斥。后两条都用 NO_FOLLOW，因为对应的 anchor 无条件汇报坐标，不看 follow 参数。
        /// </summary>
        private static bool TryResolveListener(
            EffectPlayRequest request,
            out IEfPInteractale listener,
            out PTCThread.StFollow follow,
            out EffectPlayFailure failure)
        {
            failure = EffectPlayFailure.None;

            if (request.Owner != null)
            {
                listener = request.Owner;
                follow = ToNative(request.FollowPoint);
                return true;
            }

            if (request.DrawTarget != null)
            {
                // 起播就取不到坐标的目标不值得开一条线程：它要么已经失效，要么还没进地图。
                if (!request.DrawTarget.TryGetMapPosition(out DrawPoint position))
                {
                    listener = null;
                    follow = PTCThread.StFollow.NO_FOLLOW;
                    failure = EffectPlayFailure.TargetUnavailable;
                    return false;
                }

                listener = new EffectDrawTargetAnchor(
                    request.DrawTarget, request.TargetLostBehavior, position.X, position.Y);
                follow = PTCThread.StFollow.NO_FOLLOW;
                return true;
            }

            listener = new EffectPositionAnchor(request.X, request.Y);
            follow = PTCThread.StFollow.NO_FOLLOW;
            return true;
        }

        private static bool TryResolveSetter(EffectLayer layer, out IEffectSetter setter, out EffectPlayFailure failure)
        {
            Map2d map = M2DBase.Instance?.curMap;
            if (map == null)
                setter = null;
            else if (layer == EffectLayer.WorldTop)
                setter = map.getEffectTop();
            else
                setter = map.getEffect();

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
