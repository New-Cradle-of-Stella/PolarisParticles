namespace Polaris.Particles.Effects
{
    /// <summary>
    /// 跟随的 <see cref="Polaris.Drawing.IMapDrawTarget"/> 变得不可用（对象已释放、已回池、角色已离场）之后怎么处理这条时间线。
    /// 刻意不复用 Drawing 的 <c>MapTargetLostBehavior</c>：那个枚举有 <c>Hide</c>，但原版一条 SETTER 线程要么继续播、要么被 kill，没有"隐藏但保留"的状态。
    /// </summary>
    public enum EffectTargetLostBehavior
    {
        /// <summary>
        /// 停掉时间线阅读器，已经派生并 stock 的子粒子继续播完——轨迹特效要的就是这个，源头没了以后不再发射，但余迹自然消散。
        /// 对应原版 <c>PTCThread.kill(true)</c>。
        /// </summary>
        StopTimeline,

        /// <summary>
        /// 不再重定位，时间线继续按脚本自己跑。这是原版对"listener 不给坐标"的原生反应
        /// （<c>PosEffectReposit.z = 0</c>），stock 子粒子停在最后一次跟到的位置。
        /// </summary>
        Freeze,

        /// <summary>连 stock 子粒子一起清掉，画面上立刻消失。对应原版 <c>PTCThread.kill(false)</c>。</summary>
        StopAll,
    }
}
