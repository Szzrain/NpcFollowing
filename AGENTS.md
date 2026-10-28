# NPC Following：Agent 上手指南

本文件适用于整个仓库。用户说明、内容包示例和游戏内验证清单见 `README.md`；修改前先以当前源码和工作区状态为准。

## 项目概况

- Stardew Valley 的 SMAPI 同伴模组：指定 NPC 跟随、携带物品、参与普通近战。目前仅支持单人游戏。
- C# / .NET 6，`global.json` 指定 SDK `6.0.418`，项目目标为 `net6.0`；构建依赖 `Pathoschild.Stardew.ModBuildConfig`，通过 Harmony 拦截少量原版方法。
- 核心项目：`NpcFollowing/NpcFollowing.csproj`；解决方案：`NpcFollowing.sln`。当前核心和默认内容包版本均为 `1.1.1`。
- 核心模组 ID：`com.szzrain.npc_following`；默认 CP 包 ID：`com.szzrain.npc_following.cp`，依赖 Content Patcher 和核心模组。
- 默认 CP 包位于 `NpcFollowing/[CP]NpcFollowing/`；源码中的嵌套位置不代表安装布局，安装时核心和 CP 是 `Mods` 下的两个文件夹。

## 从哪里读起

以下路径相对于仓库根目录。

| 入口 | 职责 |
| --- | --- |
| `NpcFollowing/ModEntry.cs` | 组装服务、注册 Harmony 补丁、连接读档/每日/日终/更新/返回标题事件。 |
| `NpcFollowing/Loader/CompanionContent.cs` | 提供空候选资产，处理内容失效和语言变化，加载并校验定义。 |
| `NpcFollowing/CompanionManager.cs` | 当前存档的普通候选注册、刷新、临时调试同伴、NPC 引用、当日不可招募记录和整体清理。 |
| `NpcFollowing/Model/CompanionDefinition.cs` | 内容包公开数据模型、默认值、校验和快照。 |
| `NpcFollowing/Patches/CompanionPatches.cs` | 原版交互衔接、Shift 快捷、调试棒优先强制同行、跟随期间回家拦截及既有结婚日兼容逻辑。 |
| `NpcFollowing/StateMachine/ComPanionStateMachine.cs` | 每位同伴的状态、背包、日程备份和物品返还。注意文件名实际拼写。 |
| `NpcFollowing/StateMachine/State/` | `AvailableState` 招募；`RecruitedState` 持有 AI/背包菜单/事件；`ResetState` 恢复日程；`UnavailableState` 禁用交互。 |
| `NpcFollowing/Recruitment/` | 纯招募判定 `RecruitmentRules`，台词选择与兜底 `RecruitmentService`。 |
| `NpcFollowing/AI/`、`NpcFollowing/Movement/` | AI 状态切换、跟随/战斗控制、寻路和移动；泳装逻辑在 AI 的 partial 文件中。 |
| `NpcFollowing/Combat/` | 纯武器选择顺序 `WeaponSelection`，真实物品解析和专属映射 `CompanionWeaponResolver`。 |
| `NpcFollowing/Utils/Extensions.cs` | 当前同伴伤害、掉落和击杀归属处理；战斗控制器在 `AI/Controller/FightController.cs`。 |
| `NpcFollowing/Driver/StuffDriver.cs`、`NpcFollowing/Objects/Package.cs` | 包裹交互，以及保存前转原版箱子、保存后/读档后恢复包裹。 |

实际调用链：`ModEntry → CompanionManager → CompanionStateMachine → RecruitedState → AI_StateMachine → FollowController/FightController → Movement`。

## 内容与行为约定

- 普通招募候选的唯一数据入口是 `Mods/com.szzrain.npc_following/Companions`，类型为 `Dictionary<string, CompanionDefinition>`，键为 NPC **内部名称**。无内容包时普通候选名单为空；不要恢复按 `CanSocialize` 或加载日程自动登记所有 NPC 的做法。调试棒创建的临时同行状态不属于普通候选名单。
- 默认包的定义在 `NpcFollowing/[CP]NpcFollowing/assets/Data/Companions.json`，通过 CP `EditData` 添加 12 位可结婚 NPC。NPC 台词用该包 `i18n/default.json` 和 `i18n/zh.json`；核心菜单及兜底台词在 `NpcFollowing/i18n/`。
- 定义字段为 `Enabled`、`MinimumHearts`、`LatestRecruitTime`、`AcceptDialogue`、`RejectDialogue`、`RejectLateDialogue`、`DefaultWeaponId`。默认 10 心、22:00 前；好感合法范围 0–14，截止时间为 600–2600 的十分钟刻度。字段细节和示例见 README。
- 节日不能正常招募；拒绝或解散后当天不可再次正常招募。记录存在管理器的 `unavailableToday` 中，内容刷新/移除再添加不应绕过它。
- 调试棒分支优先于普通候选检查，可强制名单外及不可社交 NPC 同行，不受 `CanSocialize`、候选名单、好感、时间、节日或当天拒绝/解散记录限制；再次使用则解散。不要永久修改 NPC 的 `CanSocialize`，不要把调试同伴自动写入普通候选资产。
- 临时调试同伴同行期间跨地图、CP 刷新仍须保留状态与背包操作。名单外状态在解散或日终后归还实际物品并释放，次日/读档不自动登记为普通候选；返回标题清理状态与事件。调试功能也仅支持单人游戏，特殊 NPC 的动画和日程未保证完全兼容。
- 调试会话必须保留被点击的 NPC 实例，不要在状态切换中按名字重新查找并替换。无日程的强制同伴使用招募前的位置/朝向和状态恢复，不要为其新建日程或好感关系。`NpcSpriteHelper` 保护 Bouncer 等单帧精灵的移动、攻击和朝向，不能直接对其套用普通 NPC 的 16 帧动画。
- Shift 交互可绕过普通 NPC 台词队列直接进入同伴交互；仍需保留赠礼、睡眠、不可见、骑马等原版交互保护。接受时只招募一次，取消不改变招募状态。
- 内容刷新在主线程、玩家空闲且无菜单时进行。已招募者保持本次定义快照，直到解散后再应用候选移除或配置变化；不要在背包菜单打开时销毁状态。
- 读档初始化、每日刷新、日终解散并归还物品；返回标题必须释放 NPC 状态和事件订阅，不能将旧存档 NPC 引用留到新存档。事件订阅应有对应退订，所有游戏对象操作留在主线程。

### 武器与物品

- 选择顺序：背包按槽位排列的首件可用近战武器 → 有效 `DefaultWeaponId` → 原版专属映射 → 生锈短剑 `(W)0`。`MeleeWeapon` 包括镰刀，须显式排除；弹弓也不参与选择。
- 每次攻击开始重新选择，当前攻击固定武器实例和属性快照。读取真实背包实例以保留锻造数值，不写其 `lastUser`、外观/贴图索引，不复制或消耗它。内部默认武器不能塞进背包。
- 原版专属武器只有 10 人有对应条目，映射以 `CompanionWeaponResolver` 为准；Emily、Shane 没有专属条目。不要按名称猜测或给所有 NPC 分配同一隐藏武器。
- 当前仅实现普通近战和已有数值属性；未接入弹弓、玩家特殊技及依赖玩家的附魔触发。原版无通用 NPC 挥武器 API，Abigail 剧情帧是人物特例；当前没有新增通用挥武器渲染。
- 背包中的物品须转移实际引用，避免复制或丢失。日终、移除候选或解散名单外临时同伴时送至农舍包裹；农舍无空位时转原版 `team.returnedDonations` 失物招领，并设置新物品标志。
- `Package` 保存前由 `StuffDriver` 转为原版 `Chest`，随后恢复；转移后清空旧容器、重复恢复不得重复发物品。兼容现有 `dumped-bags` 保存键与 `BagDumpInfo` 格式。
- 日程恢复使用原版当前 API 和招募前备份，注意配偶农舍、像素/地砖坐标、活动动画收尾；不要重新引入复制的旧版日程解析器、忙等或后台线程延迟操作。

## 构建与验证

**验证构建关闭自动部署。** 项目常规 `Build` 可能部署核心，并通过 `CopyDirectories` / `xcopy` 复制 CP 包到游戏安装目录；验证时传入 `EnableModDeploy=false` 和 `EnableModZip=false`。不必为了编译而安装、启动游戏或修改存档。

在仓库根目录、有 .NET 6 SDK 的终端中：

```powershell
dotnet build NpcFollowing/NpcFollowing.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false -p:EnableGameDebugging=false
dotnet run --project tests/NpcFollowing.RuleTests
powershell -NoProfile -File tests/ValidateContent.ps1
```

找不到游戏时给构建追加 `-p:GamePath="实际游戏目录"`。`NpcFollowing.RuleTests` 是独立控制台测试，**不在解决方案中，也不是 `dotnet test` 测试项目**；无游戏或外部 NuGet 包依赖，自带无源 `NuGet.Config`，以退出码报告失败。

真实原版物品检查需先构建最新 Release，再启动新的 PowerShell 7 进程：

```powershell
pwsh -NoProfile -File tests/GameDataSmoke.ps1 -GamePath "实际游戏目录"
pwsh -NoProfile -File tests/DebugCompanionSmoke.ps1 -GamePath "实际游戏目录"
```

武器检查读取游戏程序集/XNB 和 Release DLL，验证真实武器实例、映射、过滤、回退及锻造物品不变；不启动游戏或创建图形设备，不读写存档。不要用旧 DLL 的结果证明新源码通过。

`DebugCompanionSmoke.ps1` 使用内存世界和 SMAPI 接口替身验证真实调试入口、临时注册、清理及物品返还。每个脚本都应在新的 PowerShell 7 进程执行；这些测试不等于启动游戏后的完整场景验证。

此前验证结果为 Release 编译通过、13 组纯规则检查、18 组原版数据/真实物品检查、14 组调试棒入口/临时会话检查及内容完整性校验通过。仓库仍有既有可空引用、过时 API、未使用成员警告。**完整游戏内招募、战斗、日程、跨日与保存流程尚未完成实测**；自动测试通过不能替代 README 的游戏内清单。

### 本机路径与 SDK 回退

以下为此工作环境中已确认的路径，不应硬编码进模组运行逻辑：

- 游戏/SMAPI：`D:\SteamLibrary\steamapps\common\Stardew Valley`。
- 原版源码参考：`C:\Users\MSI-NB\RiderProjects\Stardew Decomplied`（目录名拼写如此），对应已检查版本 `1.6.15.24356`；模组任务通常只查阅，不修改该项目。
- 用户目录 SDK：`C:\Users\MSI-NB\.dotnet\sdk\6.0.418`，运行器：`C:\Users\MSI-NB\.dotnet\dotnet.exe`。系统 PATH 中的 `dotnet` 未必能发现该 SDK。

若标准 CLI 因 SDK 发现或首次运行环境问题不可用，可直接调用已安装的 MSBuild，复用现有还原结果：

```powershell
$localDotnet = 'C:\Users\MSI-NB\.dotnet\dotnet.exe'
$localMSBuild = 'C:\Users\MSI-NB\.dotnet\sdk\6.0.418\MSBuild.dll'
& $localDotnet $localMSBuild NpcFollowing/NpcFollowing.csproj /nologo /t:Build /p:Configuration=Release /p:EnableModDeploy=false /p:EnableModZip=false /p:EnableGameDebugging=false '/p:GamePath=D:\SteamLibrary\steamapps\common\Stardew Valley' /v:minimal
& $localDotnet $localMSBuild tests/NpcFollowing.RuleTests/NpcFollowing.RuleTests.csproj /nologo '/t:Restore;Build' /v:minimal
& $localDotnet tests/NpcFollowing.RuleTests/bin/Debug/net6.0/NpcFollowing.RuleTests.dll
```

## 容易误判的旧代码与协作习惯

- `Model/Config.cs`、`Model/CompanionMetaData.cs`、`Loader/IContentLoader.cs`、`Dialogues/`、`Driver/DialogueDriver.cs`、`HUD/`、`Story/` 下仍有旧框架/骨架。它们不是当前招募、配置或内容加载的依据；从上述实际入口追踪调用关系。
- `CompanionStateMachine.HasSkill` 当前恒为 `false`，`GenerateLocationDialogue` 恒为 `null`。AI 枚举中的部分状态没有控制器，不代表采集、钓鱼、技能或主动邀约已经可用。
- 修改前先检查 `git status` 和相关 diff；此工作区存在用户及此前任务的未提交修改与未跟踪资源，不要重置、覆盖或清理掉无关文件。
- PowerShell 操作 `[CP]NpcFollowing` 等含方括号的路径时用 `-LiteralPath`，避免被当作通配符；读 JSON 明确指定 UTF-8，尤其在 Windows PowerShell 5 中。
- 保持现有标识符、内容资产名、保存键兼容。新增玩家可见文字同时更新中文和英文；修改公开定义时同步 README、默认包和相关测试。
- `bin/`、`obj/` 为构建产物。已有发布包在根目录 `bin/` 下，不是源码真相；源码变化后需要重新构建/打包，不能只修改或交付旧二进制。
- 按变更范围选择必要验证。仅文档修改不需要重新编译；涉及候选/招募/武器规则时运行对应规则和内容检查，涉及真实物品解析时再运行 GameDataSmoke，涉及生命周期/日程时补游戏内验证并说明实际完成范围。
