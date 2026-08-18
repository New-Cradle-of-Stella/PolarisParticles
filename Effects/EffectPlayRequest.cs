using System;
using System.Collections.Generic;
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

        public float X { get; }
        public float Y { get; }
        public EffectLayer Layer { get; private set; } = EffectLayer.World;
        public IEfPInteractale Owner { get; private set; }
        public EffectFollowPoint FollowPoint { get; private set; } = EffectFollowPoint.None;

        internal IReadOnlyList<EffectParameter> Parameters => _parameters;

        /// <summary>
        /// 跟随一个原生游戏对象。owner 必须实现 <see cref="IEfPInteractale"/>（角色、怪物等原版可交互对象）；
        /// 无法解析到原生对象时不要冒充跟随，改用不带 Follow 的固定坐标播放。
        /// </summary>
        public EffectPlayRequest Follow(IEfPInteractale owner, EffectFollowPoint point)
        {
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
