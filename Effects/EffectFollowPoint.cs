namespace Polaris.Particles.Effects
{
    /// <summary>SETTER 时间线跟随一个 owner 时，取其身上的哪个锚点。对应原版 <c>PTCThread.StFollow</c>。</summary>
    public enum EffectFollowPoint
    {
        /// <summary>不跟随；播放期间使用固定坐标。</summary>
        None,
        Caster,
        Target,
        Hip,
        Head,
        Source,
        Destination,
        MagicCircle,
    }
}
