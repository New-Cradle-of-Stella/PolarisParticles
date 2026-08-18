using System;

namespace Polaris.Particles
{
    /// <summary>
    /// 标注在模组的 BepInPlugin 类上，允许 PolarisTools 把 .peffect 调试快照推送到游戏进程，
    /// 并启用 F9 粒子调试页。未标注的游戏不会开放调试命名管道。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PEffectDebugEnabledAttribute : Attribute
    {
    }
}
