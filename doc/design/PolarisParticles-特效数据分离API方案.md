# PolarisParticles 特效数据分离 API 实现方案

## 1. 固定结论

本方案只使用一种作者文件：

~~~text
*.peffect
~~~

.peffect 的内容完全沿用 Alice in Cradle ver029 原版 .particle 格式：

- 普通段定义单粒子模板。
- SETTER 段定义复合时间线。
- AGD 段定义攻击残影。
- 保留 @include、%CLONE、%MERGE 以及原版时间线指令。
- 不设计任何其他资源格式、轨道模型或另一套表达式语法。

扩展名改为 .peffect 只是为了区分 Polaris 模组资源与游戏原始资源。送入游戏时仍交给 EfParticleManager 的原版解析器。

## 2. 目标目录

~~~text
MyMagicMod/
├─ Effects/
│  ├─ mymod_common.peffect
│  ├─ mymod_fireball.peffect
│  └─ mymod_boss.peffect
└─ MyMagicMod.csproj
~~~

建议把 .peffect 编译为 EmbeddedResource，模组不依赖游戏安装目录中的 Resources：

~~~xml
<ItemGroup>
  <EmbeddedResource Include="Effects\**\*.peffect" />
</ItemGroup>
~~~

运行时按程序集登记资源。游戏原生加载器看到的是虚拟文件名和文本内容，不需要真实的 .particle 文件。

## 3. 文件格式

### 3.1 一个文件可同时包含三类定义

~~~text
/* ___ mymod_fire_spark ___ */
count 12
maxt 20
type CIRCLE

/* ___ SETTER.mymod_fireball_hit ___ */
mymod_fire_spark
%WAIT 2
mymod_fire_smoke

/* ___ AGD.mymod_fire_slash ___ */
...
~~~

字段名、分词、注释、变量替换和指令语义都按原版处理。PolarisParticles 不翻译这些内容，也不维护第二份运行时模型。

### 3.2 include

~~~text
@mymod_common
@mymod_fireball
~~~

@ 后面的名字按原版先移除扩展名，再查找虚拟文件。建议所有文件名和段 key 都带模组前缀，因为原版目录是进程级全局字典。

### 3.3 clone 与 merge

~~~text
/* ___ mymod_large_spark ___ */
%CLONE mymod_base_spark
count 24

/* ___ SETTER.mymod_large_hit ___ */
%CLONE mymod_base_hit
%MERGE mymod_extra_flash
~~~

不重新实现 %CLONE/%MERGE。所有 .peffect 会进入原版同一次加载批次，使 EfParticleLoader、EfSetterP 和 AttackGhostDrawer 继续执行原生继承规则。

## 4. 总体实现

~~~mermaid
flowchart LR
    A["模组内嵌 .peffect"] --> B["EffectFileRegistry"]
    B --> C["虚拟文件目录"]
    C --> D["兼容层拦截 getParticleScript"]
    D --> E["原版 __main 追加 @虚拟文件"]
    E --> F["EfParticleManager.loadParticleCsv"]
    F --> G["OPtc / OPtcSetter / OAgd"]
    G --> H["PlayTimeline / SpawnParticle / GetAttackGhost"]
~~~

PolarisParticles 只负责文件登记、作者 API、句柄和生命周期。接触 unsafeAssem、Harmony 以及原版私有方法的代码放在游戏兼容层。

## 5. 为什么不直接依赖 addAdditionalFile

EfParticleManager.addAdditionalFile 可以加载一个 TextAsset，但不能独立满足完整需求：

1. 在 EfParticleManager 初始化前调用时，它只记住 key，不保存传入的 TextAsset；正式初始化时仍会去 Resources/Basic/DataParticle 查找 .particle。
2. 初始化后单独加载时，粒子 %CLONE 只在本次 APtcO loader 批次中解析，无法稳定继承原版加载批次里的 loader。
3. addAdditionalFile 会把 key 留在 Aload_other_file，下一次 reload 又会按原版 Resources 路径查找。
4. 已经存在的粒子模板不会被后加载文件稳定替换，热重载行为与 SETTER/AGD 也不一致。

因此正式实现应把虚拟 .peffect 接到原版主加载流程，而不是在完成后逐文件补灌。

## 6. 虚拟文件注入

### 6.1 EffectFileRegistry

注册表保存：

~~~csharp
internal sealed class EffectFileRecord
{
    public string VirtualName { get; }
    public string ResourceName { get; }
    public string Text { get; }
    public Assembly Owner { get; }
    public string ContentHash { get; }
}
~~~

规则：

- VirtualName 使用不带扩展名的文件名。
- 比较器固定 StringComparer.Ordinal。
- 同名虚拟文件直接报冲突，不允许后注册覆盖。
- 登记顺序不影响加载顺序；封存时按程序集全名、VirtualName 排序。
- 文本读取为 UTF-8，保留换行内容，保证末尾至少一个换行。
- 单文件大小和总大小设置上限，避免启动时无界占用内存。

### 6.2 拦截 getParticleScript

ver029 中 EfParticleManager.getParticleScript(string name) 是私有静态方法。兼容层安装两个钩子：

~~~csharp
// 伪代码：真实类型只存在于兼容层。
static bool Prefix(string name, ref string result)
{
    if (!virtualFiles.TryGetText(name, out result))
        return true;

    return false;
}

static void Postfix(string name, ref string result)
{
    if (name != "__main" || string.IsNullOrEmpty(result))
        return;

    result += virtualFiles.BuildRootIncludes();
}
~~~

BuildRootIncludes 只追加：

~~~text
@__polaris_effect_0001
@__polaris_effect_0002
~~~

每个内部名字映射到实际 .peffect 文本。内部名字固定使用字母、数字和下划线，避免触发原版分词差异。

### 6.3 加载时序

1. PolarisParticles 启动时收集所有 .peffect。
2. 读取文本，建立虚拟文件目录并执行轻量预检。
3. 兼容层安装 getParticleScript 钩子。
4. 如果 EfParticleManager 尚未初始化，等待原版首次 reloadParticleCsv。
5. 如果已经初始化，注册批次完成后只调用一次 reloadParticleCsv(true)。
6. 原版读取 __main 时，兼容层在原有 include 列表末尾追加虚拟文件。
7. 原版文件先加载，Polaris 文件后加载，并在同一个 APtcO 批次中统一 finalize。

必须在主线程执行注册封存与 reload。

## 7. include 的实现

有两种可行方式，推荐第一种。

### 7.1 原生虚拟文件方式

每份 .peffect 都登记为 getParticleScript 可返回的虚拟文件。文件中的 @mymod_common 由原版 loader 收集，之后再次调用 getParticleScript("mymod_common")，兼容层返回对应文本。

优点：

- 完全保留原版 include 顺序。
- %CLONE/%MERGE 和变量容器仍由原版控制。
- 不需要拼接或重写作者文本。

限制：

- 所有虚拟文件名处于全局命名空间。
- 循环 include 的行为继承原版，Polaris 预检必须提前拒绝循环。

### 7.2 不采用文本展开

不要把 @include 自行展开后拼成新文本。文本展开容易改变根文件、子文件的解析顺序，并使诊断行号失真。

## 8. 启动期预检

Polaris 不重新解析全部 DSL，但应做只读扫描，用于在进入原版 loader 前给出可定位错误：

- 提取 section header：Particle、SETTER、AGD。
- 提取 @include 文件名。
- 提取 %CLONE/%MERGE 的直接目标。
- 检查文件名、section key 和 include 是否重复。
- 检查 include 环。
- 检查自定义定义之间的显式引用。
- 检查自定义 key 是否与原版目录冲突。
- 报告文件、行号、section key 和 owner assembly。

原版解析器仍是最终语义权威。预检器不能修改文本、补默认值或尝试模拟 EfParticleLoader。

## 9. 冲突策略

原版会让部分后定义覆盖前定义，但模组框架不应依赖加载顺序解决冲突。

以下情况使整批 .peffect 注册失败：

- 两个模组定义相同 Particle key。
- 两个模组定义相同 SETTER key。
- 两个模组定义相同 AGD key。
- 自定义 key 与原版 key 相同。
- 虚拟文件名相同。

如果以后确实需要覆盖原版，应单独设计显式 override 清单；不能让普通文件通过同名产生隐式覆盖。

## 10. 对外 API

### 10.1 入口

~~~csharp
public static class PolarisParticlesAPI
{
    public static EffectAPI Effects { get; }
}
~~~

### 10.2 文件登记

~~~csharp
public sealed class EffectFileAPI
{
    public void RegisterEmbedded(
        Assembly owner,
        string resourceName,
        string virtualName = null);
}
~~~

也可以由 PolarisParticles 自动扫描已加载模组程序集内以 .peffect 结尾的 EmbeddedResource。若采用自动扫描，仍保留显式 API 供延迟加载程序集使用。

登记只在目录封存前允许。运行中新增文件必须进入一次受控 rebuild，不能直接修改原版三个字典。

### 10.3 查询与播放

~~~csharp
public sealed class EffectAPI
{
    public EffectFileAPI Files { get; }

    public bool ContainsTimeline(string key);
    public bool ContainsParticle(string key);
    public bool ContainsAttackGhost(string key);

    public EffectRuntime PlayTimeline(
        string key,
        EffectPlayRequest request);

    public bool TryPlayTimeline(
        string key,
        EffectPlayRequest request,
        out EffectRuntime runtime,
        out EffectPlayFailure failure);

    public EffectRuntime SpawnParticle(
        string key,
        ParticleSpawnRequest request);

    public EffectScope BeginScope(object owner = null);
}
~~~

API 不提供一种模糊的 Play(string) 自动猜类型。SETTER、Particle 和 AGD 存放在不同原版字典，入口必须明确。

### 10.4 播放请求

~~~csharp
public sealed class EffectPlayRequest
{
    public EffectAnchor Anchor { get; }
    public EffectSpace Space { get; }
    public EffectLayer Layer { get; }
    public EffectFollow Follow { get; }
    public EffectHold Hold { get; }
    public EffectParameterSet Parameters { get; }
    public float DelayFrames { get; }
    public float StartAgeFrames { get; }
    public object Owner { get; }
}
~~~

Parameters 最终转换为本次调用独立的 VariableP。禁止通过 PTCThreadRunner 的静态 PreVar 传参，避免不同播放继承上一次残留变量。

## 11. Runtime 与 Scope

EffectRuntime 只是原版 token 的稳定包装，不执行另一套特效时间线：

| 播放入口 | 原版 token |
| --- | --- |
| PlayTimeline | PTCThread |
| SpawnParticle | EffectItem |
| PostEffect 扩展 | PostEffectItem |
| 程序化 drawer 扩展 | EffectItem |

~~~csharp
public sealed class EffectRuntime : IDisposable
{
    public long InstanceId { get; }
    public string Key { get; }
    public EffectRuntimeState State { get; }

    public bool IsAlive { get; }
    public void Stop(
        EffectStopMode mode = EffectStopMode.IncludeSpawnedEffects);
    public void Dispose();
}
~~~

规则：

- Stop 和 Dispose 幂等。
- TimelineOnly 映射 PTCThread.kill(true)。
- IncludeSpawnedEffects 映射 PTCThread.kill(false)。
- 原版自然结束后，Runtime 转为 Completed。
- owner 失效、地图卸载和 scope 释放统一进入停止路径。
- 容器满、地图未加载和 anchor 无效是 TryPlay 的正常失败，不抛全局异常。

EffectScope 只保存自己创建的 Runtime。实例自然结束后立即从 scope 移除；scope Dispose 时复制活动列表再逐个停止。

## 12. 原版映射

| API | ver029 映射 |
| --- | --- |
| ContainsTimeline | OPtcSetter / GetSetterScript |
| ContainsParticle | OPtc / Get(no_load=true) |
| ContainsAttackGhost | OAgd / GetAGD |
| PlayTimeline | IEffectSetter.PtcST |
| SpawnParticle | IEffectSetter.PtcN |
| Timeline 参数 | 每次请求新建 VariableP |
| WorldTop | Map2d.getEffectTop |
| World | Map2d.getEffect |
| 动态跟随 | IEfPInteractale + FOLLOW |

公开 API 不引用 PTCThread、EffectItem、EfParticle、AttackGhostDrawer、VariableP、POSTM、MeshDrawer 或 Unity ParticleSystem。

## 13. 热重载

粒子模板不能靠 addAdditionalFile 稳定地原地覆盖。热重载采用全目录重建：

1. 编辑器写入新的 .peffect 文本。
2. Polaris 在 staging 中读取全部文件并执行预检。
3. 预检失败时保留当前目录。
4. 预检成功后原子替换虚拟文件快照。
5. 主线程调用 reloadParticleCsv(true)。
6. getParticleScript 钩子在本次 reload 中注入新快照。
7. 已存在的 PTCThread/EffectItem 按原版 reload 行为处理；编辑器预览应主动停止并重播。

不要通过反射直接修改 OPtc、OPtcSetter 或 OAgd 的单个条目。那会绕过 loader finalize、clone/merge 绑定与旧对象清理。

## 14. 程序化效果

.peffect 继续负责原版格式能表达的全部效果。只有以下内容保留 C# 入口：

- 依赖玩法对象私有状态的复杂 Mesh。
- 绳索、液体、连续轨迹等逐帧算法。
- 原版 setEffectWithSpecificFn 风格的自定义 drawer。
- 无法由 SETTER、Particle 或 AGD 表达的专用系统。

程序化效果按显式 ID 登记并使用独立实例，不写入 .peffect 新指令。不要为了调用 C# 而扩展原版 DSL。

## 15. 线程、卸载与错误

- 所有原版加载、播放、停止和 reload 必须在游戏主线程。
- 文件读取与预检可以在后台完成，但提交快照必须切回主线程。
- 模组卸载不能只删除虚拟文件；必须重建剩余完整目录。
- 单个播放失败只影响当前请求。
- 文件冲突、格式预检失败和兼容层目标签名失配阻止整个自定义特效目录启用。
- getParticleScript、reloadParticleCsv、PtcST 和 PtcN 的签名在换游戏版本时必须重新探测。

## 16. 实现顺序

1. 兼容层探针：确认 getParticleScript、reloadParticleCsv 与三个原版字典。
2. EffectFileRegistry 与 EmbeddedResource 读取。
3. getParticleScript 虚拟文件 Prefix 和 __main Postfix。
4. section/include/clone/merge 轻量预检与冲突报告。
5. 首次加载和 late registration 的单次全目录 reload。
6. ContainsTimeline、ContainsParticle、ContainsAttackGhost。
7. PlayTimeline、SpawnParticle 与 per-request VariableP。
8. EffectRuntime、EffectScope、owner 和地图清理。
9. 热重载 staging 与全目录 rebuild。
10. 程序化效果注册入口。

MVP 不实现新文件格式、新表达式、通用轨道、Definition 生成器、自有 ParticleSystem 或原版资源自动转换器。
