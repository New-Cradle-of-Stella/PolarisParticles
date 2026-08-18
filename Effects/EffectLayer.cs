namespace Polaris.Particles.Effects
{
    /// <summary>运行时播放目标使用的特效层。</summary>
    public enum EffectLayer
    {
        /// <summary>地图普通世界层，对应原版 <c>Map2d.getEffect()</c>。</summary>
        World,

        /// <summary>地图顶层合成特效，对应原版 <c>Map2d.getEffectTop()</c>。</summary>
        WorldTop,
    }
}
