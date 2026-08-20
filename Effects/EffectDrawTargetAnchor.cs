using System;
using Polaris.API;
using Polaris.Drawing;
using UnityEngine;
using XX;

namespace Polaris.Particles.Effects
{
    /// <summary>
    /// 把一个 <see cref="IMapDrawTarget"/> 包成 <see cref="IEfPInteractale"/>：每帧现读目标坐标，
    /// 而不是像 <see cref="EffectPositionAnchor"/> 那样钉死在构造时的那一点。让 SETTER 时间线可以跟着
    /// Polaris 侧的可追踪物体（魔法对象、魔法实体、角色包装器）跑，而调用方不必碰任何原生类型。
    ///
    /// 原版每帧在 <c>PTCThread.run</c> 里通过 <c>fineReposit</c> 调一次
    /// <see cref="getEffectReposition"/>（游戏主线程），所以这里不缓存、不插值——目标报什么坐标就用什么。
    ///
    /// 只有中心坐标：<see cref="IMapDrawTarget"/> 拿不到骨骼，因此 head/hip/magic-circle 这些 follow 点
    /// 在这条路径上一律退化成中心点，传进来的 follow 参数被忽略（与 <see cref="EffectPositionAnchor"/>
    /// 对 NO_FOLLOW 的处理一致：照样汇报坐标）。
    /// </summary>
    internal sealed class EffectDrawTargetAnchor : IEfPInteractale
    {
        private readonly IMapDrawTarget _target;
        private readonly EffectTargetLostBehavior _onTargetLost;

        private Vector3 _lastPosition;
        private bool _faulted;

        internal EffectDrawTargetAnchor(IMapDrawTarget target, EffectTargetLostBehavior onTargetLost, float x, float y)
        {
            _target = target;
            _onTargetLost = onTargetLost;
            _lastPosition = new Vector3(x, y, 0f);
        }

        public string snd_key => string.Empty;

        public bool getEffectReposition(PTCThread St, PTCThread.StFollow follow, float fcnt, out Vector3 V)
        {
            if (TryReadTarget())
            {
                V = _lastPosition;
                return true;
            }

            // 目标没了。Freeze 直接用原版对"listener 不给坐标"的反应（本帧不重定位，时间线继续跑）；
            // 另外两档要主动收尾——getEffectReposition 是唯一能拿到 St 的回调，也就是目标失效时
            // 唯一能停掉线程的入口。这里 kill 是安全的：原版随后只会跑一次 base.run，届时
            // CurrentReading 已被置空，stock 子粒子会被摘出线程而不是被 destruct（PTCThread 的
            // RBase 是 send_destruct: false）。
            switch (_onTargetLost)
            {
                case EffectTargetLostBehavior.StopTimeline:
                    St?.kill(do_not_kill_stock_effect: true);
                    break;
                case EffectTargetLostBehavior.StopAll:
                    St?.kill();
                    break;
            }

            V = _lastPosition;
            return false;
        }

        public bool readPtcScript(PTCThread rER)
        {
            if (rER.cmd == "%MYPOS")
            {
                TryReadTarget();
                rER.Def("cx", _lastPosition.x);
                rER.Def("cy", _lastPosition.y);
                return true;
            }
            if (rER.cmd == "%CALCPOS")
            {
                TryReadTarget();
                rER.Def("x", _lastPosition.x);
                rER.Def("y", _lastPosition.y);
                return true;
            }
            return false;
        }

        public bool isSoundActive(SndPlayer S) => false;

        public bool initSetEffect(PTCThread Thread, EffectItem Ef) => true;

        /// <summary>
        /// 读一次目标坐标并记为"最后已知位置"。目标可以是第三方实现，抛异常只报一次就永久按失效处理：
        /// 原版 <c>PTCThread.run</c> 外层是 <c>catch (Exception) { return false; }</c>，让异常漏上去
        /// 只会让整条时间线无声消失，什么线索都不留。
        /// </summary>
        private bool TryReadTarget()
        {
            if (_faulted)
            {
                return false;
            }

            try
            {
                if (!_target.TryGetMapPosition(out DrawPoint position))
                {
                    return false;
                }

                _lastPosition = new Vector3(position.X, position.Y, 0f);
                return true;
            }
            catch (Exception ex)
            {
                _faulted = true;
                PolarisAPI.Errors.Report(
                    ex,
                    "reading a follow target for a .peffect timeline",
                    typeof(EffectDrawTargetAnchor).Assembly);
                return false;
            }
        }
    }
}
