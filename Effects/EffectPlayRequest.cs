using System;
using System.Collections.Generic;
using Polaris.Drawing;
using XX;

namespace Polaris.Particles.Effects
{
    /// <summary>
    /// 一次 SETTER 时间线播放请求：位置或跟随目标、层与每次调用独立的变量。
    /// 每次 <see cref="EffectAPI.PlayTimeline"/> 都会为请求单独创建一份 VariableP，不使用原版静态 PreVar，
    /// 因此互不干扰。
    /// </summary>
    public sealed class EffectPlayRequest
    {
        private readonly List<EffectParameter> _parameters = new List<EffectParameter>();

        private EffectPlayRequest(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>在固定地图坐标播放；不跟随任何 owner 时使用这份坐标。</summary>
        public static EffectPlayRequest At(float x, float y) => new EffectPlayRequest(x, y);

        /// <summary>
        /// 跟着一个 Polaris 侧的可追踪物体播放：魔法对象、魔法实体、角色包装器——任何
        /// <see cref="IMapDrawTarget"/> 实现都行，调用方不需要碰原生类型。用来做轨迹/拖尾这类
        /// "发射源跟着东西跑"的特效。
        ///
        /// 跟随只驱动时间线的发射位置：每帧被重设坐标的是被线程 stock 的子粒子（<c>.peffect</c> 里
        /// key 带 <c>*</c> 前缀的那些），普通子粒子只是在当帧的位置出生，之后自己飞。跟随通道也只有
        /// 一个坐标，物体的旋转与缩放传不过去，要转向请用 <see cref="Set(string, double)"/> 在起播时
        /// 传一次角度。
        ///
        /// 起播坐标直接问目标要；目标此刻就不可用时留 (0, 0)，由 <c>TryPlayTimeline</c> 报
        /// <see cref="EffectPlayFailure.TargetUnavailable"/>。
        /// </summary>
        public static EffectPlayRequest Following(
            IMapDrawTarget target,
            EffectTargetLostBehavior onTargetLost = EffectTargetLostBehavior.StopTimeline)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.TryGetMapPosition(out DrawPoint position);
            return new EffectPlayRequest(position.X, position.Y)
            {
                DrawTarget = target,
                TargetLostBehavior = onTargetLost,
            };
        }

        public float X { get; }
        public float Y { get; }
        public EffectLayer Layer { get; private set; } = EffectLayer.World;
        public IEfPInteractale Owner { get; private set; }
        public EffectFollowPoint FollowPoint { get; private set; } = EffectFollowPoint.None;

        /// <summary><see cref="Following"/> 指定的可追踪目标；没用那条路径时为 <c>null</c>。</summary>
        public IMapDrawTarget DrawTarget { get; private set; }

        /// <summary><see cref="DrawTarget"/> 失效后的处理策略；只在跟随可追踪目标时有意义。</summary>
        public EffectTargetLostBehavior TargetLostBehavior { get; private set; } = EffectTargetLostBehavior.StopTimeline;

        internal IReadOnlyList<EffectParameter> Parameters => _parameters;

        /// <summary>
        /// 跟随一个原生游戏对象。owner 必须实现 <see cref="IEfPInteractale"/>（角色、怪物等原版可交互对象）；
        /// 无法解析到原生对象时不要冒充跟随，改用不带 Follow 的固定坐标播放。
        /// </summary>
        public EffectPlayRequest Follow(IEfPInteractale owner, EffectFollowPoint point)
        {
            if (DrawTarget != null)
            {
                throw new InvalidOperationException(
                    "This request already follows an IMapDrawTarget; pick either the native owner or the draw target, not both.");
            }

            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            FollowPoint = point;
            return this;
        }

        public EffectPlayRequest OnLayer(EffectLayer layer)
        {
            Layer = layer;
            return this;
        }

        /// <summary>为本次播放设置一个数值变量，SETTER 脚本中以 <c>&amp;key</c> 引用。</summary>
        public EffectPlayRequest Set(string key, double value)
        {
            _parameters.Add(EffectParameter.Number(key, value));
            return this;
        }

        /// <summary>为本次播放设置一个字符串变量。</summary>
        public EffectPlayRequest Set(string key, string value)
        {
            _parameters.Add(EffectParameter.Text(key, value));
            return this;
        }
    }
}
