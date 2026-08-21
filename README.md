# PolarisParticles

Polaris 的自定义粒子与特效能力组件。依赖同级 `PolarisCore`，并由 [Polaris](https://github.com/New-Cradle-of-Stella/Polaris) 聚合仓库作为 Git submodule 引用。

## `.peffect` 注册

把文件设为嵌入资源；PolarisParticles 会在启动时自动扫描所有插件程序集：

```xml
<ItemGroup>
  <EmbeddedResource Include="Effects\**\*.peffect" />
</ItemGroup>
```

PolarisTools 的 `.peffect` 新建项模板会自动设置这个 Build Action。运行时用不带扩展名的文件名作为
全局虚拟文件名，因此建议使用模组前缀，例如 `mymod_fireball.peffect`。

需要自定义虚拟名或登记非标准资源名时，可在模组 `Awake` 中显式注册：

```csharp
ParticlesAPI.Effects.Files.RegisterEmbedded(
    typeof(ExamplePlugin).Assembly,
    "ExampleMod.Effects.fireball.peffect",
    "example_fireball");
```

## 运行时播放

登记完成后可以在任意游戏逻辑里播放。查询与播放分属 Particle / SETTER / AGD 三张原版目录，接口不做模糊猜测：

```csharp
EffectAPI fx = ParticlesAPI.Effects;

// 单粒子：在地图坐标生成一次
fx.SpawnParticle("mymod_fire_spark", ParticleSpawnRequest.At(x, y));

// SETTER 时间线：固定坐标播放
fx.PlayTimeline("mymod_fireball_hit", EffectPlayRequest.At(x, y));

// SETTER 时间线：跟随一个原生游戏对象（需实现 XX.IEfPInteractale）
using EffectScope scope = fx.BeginScope();
EffectRuntime charge = scope.PlayTimeline(
    "mymod_fireball_prepare",
    EffectPlayRequest.At(x, y)
        .Follow(self, EffectFollowPoint.MagicCircle)
        .Set("angle", angle)
        .Set("facing", facingRight ? 1 : -1));

charge.Stop(EffectStopMode.IncludeSpawnedEffects);
// scope 结束时（using 退出）会自动停止它播放过的全部实例。
```

### 跟随 Polaris 侧的可追踪物体（轨迹特效）

任何实现 `Polaris.Drawing.IMapDrawTarget` 的东西都能直接当跟随目标，不需要碰原生类型 —— PolarisMagic 的
`MagicObject`（魔法对象）与 `MagicEntity`（魔法实体）、PolarisCore 的 `GameCharacter` 都已经实现：

```csharp
// PolarisMagic 的 .pmagic.cs 里：让时间线跟着一个魔法对象跑，做拖尾/轨迹
private async Task RunAsync(MagicRuntimeContext context, CancellationToken token)
{
    MagicObject head = context.Magic.CreateObject();
    head.Position = context.Self.Position;
    head.Velocity = new GameVector2(6f, 0f);

    using EffectScope fx = ParticlesAPI.Effects.BeginScope();
    fx.PlayTimeline("mymod_fireball_trail", EffectPlayRequest.Following(head));

    while (!token.IsCancellationRequested)
    {
        head.Advance(context.Clock);
        await context.NextTickAsync(token);
    }
}
```

`Following` 的目标不可用（魔法对象已 `Dispose`、魔法实体已回池、角色已离场）之后按
`EffectTargetLostBehavior` 处理，默认 `StopTimeline`：停止发射，已经拖在后面的余迹自然播完。另两档是
`Freeze`（不再重定位，时间线继续跑）和 `StopAll`（连 stock 子粒子一起清掉）。起播时目标就不可用会以
`EffectPlayFailure.TargetUnavailable` 返回。

这条路径能做到的和做不到的（都是原版 `PTCThread` 的既有语义，API 不做美化）：

- **跟的是发射位置**。每帧被重设坐标的只有被线程 stock 的子粒子，也就是 `.peffect` 里 key 带 `*` 前缀的
  那些；普通子粒子在当帧的位置出生之后就自己飞了。要"整团粒子跟着走"必须在 `.peffect` 里写 `*`。
- **只有单粒子的 `SpawnParticle` 不能跟随**：原版 `PtcN` 只吃一次性坐标，没有 listener。
- **只有一个坐标**：物体的旋转与缩放传不过去（也没有 z），要转向就在起播时 `Set("agR", angle)` 传一次。
- **没有骨骼锚点**：`IMapDrawTarget` 只给中心坐标，所以脚本里的 `%FOLLOW HEAD` / `HIP` /
  `MAGICCIRCLE` 在这条路径上一律退化成中心点。要骨骼跟随得用
  `Follow(owner, EffectFollowPoint.Head)` 那条原生 owner 路径。
- 魔法对象**不需要挂任何图片**也能当轨迹的锚：没有挂载项时它不会创建 Drawing Surface，就是一个纯粹的
  会动的坐标。
- 粒子走原版特效层（`EffectLayer.World` / `WorldTop`），和 Drawing 的 `DrawPlane` 是两套独立的深度体系，
  粒子不会夹在魔法对象自己的绘制节点之间。

- `PlayTimeline` / `SpawnParticle` 失败时抛异常；`TryPlayTimeline` / `TrySpawnParticle` 把地图未加载、
  key 不存在以及粒子容器已满这类可恢复失败以 `EffectPlayFailure` 返回，不抛异常。
- `ContainsParticle` / `ContainsTimeline` / `ContainsAttackGhost` 用于播放前的存在性检查。
- 时间线参数使用本次请求独立的 `VariableP`，不同调用之间不会互相污染参数。
- `EffectRuntime` 只是原版 `PTCThread` / `EffectItem` token 的稳定包装；`Stop`/`Dispose` 幂等，
  对已经自然结束或已被池回收的实例调用是安全的空操作。
- `EffectScope` 收集它播放过的实例，`Dispose` 时统一停止，适合包住一次施法或一个阶段。
- AGD（攻击残影）目前只提供 `ContainsAttackGhost` 查询；它依赖动作采样点，不适合通用的“在某坐标播放”接口。

## 游戏内 `.peffect` 调试

在模组的 `BepInPlugin` 类上启用调试通道：

```csharp
[BepInPlugin("example.mod", "Example Mod", "1.0.0")]
[PEffectDebugEnabled]
public sealed class ExamplePlugin : BaseUnityPlugin
{
}
```

启动游戏后，在 PolarisTools 打开任意 `.peffect` 并点击 **Debug in game**。工具会把同一项目中的全部
`.peffect` 作为一份快照推送给游戏；游戏使用原版 `EfParticleManager` 完整重载。按 **F9** 打开 IMGUI
调试页，可选择 Particle、SETTER 或 AGD。粒子由独立的 Effect 和相机渲染到 RenderTexture，再显示在
IMGUI 预览画布中；不会生成到当前地图。可调整局部坐标、时长与缩放后重播。
`Life(fr)` 以帧为单位（60 帧约 1 秒），直接预览 Particle 时会覆盖其 `maxt`；SETTER 可通过
`&maxt` / `&time` 使用这个值。开启 `Loop` 后，效果结束 0.6 秒会自动重播。

调试通道只在至少一个已加载插件标了 `[PEffectDebugEnabled]` 时启动；生产版本移除该特性即可关闭命名管道与 F9 页面。
