# NPC Following

让指定 NPC 陪同玩家探索、携带物品和参与战斗的 Stardew Valley / SMAPI 模组。目前仅支持单人游戏。

## 安装与使用

安装 SMAPI 和 Content Patcher，将主模组 `NpcFollowing` 与随附的 `[CP]NpcFollowing` 内容包放入游戏的 `Mods` 文件夹，通过 SMAPI 启动游戏。

默认内容包明确登记 12 位原版可结婚 NPC：Abigail、Alex、Elliott、Emily、Haley、Harvey、Leah、Maru、Penny、Sam、Sebastian、Shane。游戏中的其他 NPC 不会自动加入普通招募候选；未安装任何提供候选的内容包时，普通候选名单为空。

与候选交互可请求其同行；按住左 Shift 交互可直接调用同伴交互。默认需要 10 心好感，且须在 22:00 之前提出招募；节日不可正常招募。拒绝后该 NPC 当天不再接受普通招募请求。同行时可通过交互打开背包或解散。同伴的接受、好感不足拒绝和太晚拒绝台词均按 NPC 独立提供，包含中文和英文。

调试棒可强制 NPC 同行，再次使用则解散，包括名单外及不可社交的 NPC。它优先于普通招募，不受好感、时间、节日和当天拒绝或解散记录限制，也不会永久修改 NPC 的 `CanSocialize`。临时同伴在同行期间跨地图或刷新内容包仍会保留，可照常打开背包；名单外状态在解散或日终后返还物品并释放，次日或读档不会自动成为普通候选。特殊 NPC 的动画和日程尚未保证完全兼容。

Bouncer 等只有单帧精灵的 NPC 会保持静态外观移动和战斗。没有实际行走精灵或地图位置的角色无法强制移动；没有原生日程的临时同伴解散时会回到招募前的准确位置，不会被加入日程或社交关系。

过夜时、解散名单外临时同伴时，或内容包移除未在同行的候选时，背包物品会打包送到农舍内的空地砖。农舍没有可放置包裹的空位时，物品会转入镇长庄园的原版失物招领箱，并随游戏存档保存；SMAPI 日志会提示投递位置。此过程转移实际物品，不会复制背包内容。

## 战斗武器

攻击时按以下顺序选取武器：

1. 按同伴背包槽位顺序找到的第一件可用近战武器；无需放在第一个槽位。
2. 内容定义中的 `DefaultWeaponId`，未配置时使用该 NPC 对应的原版隐藏武器。
3. 生锈短剑 `(W)0`。

如果内容包指定的武器 ID 无效，先尝试该 NPC 的原版隐藏武器，仍无可用武器时使用生锈短剑。默认武器是战斗用的临时物品，不会凭空添加到背包中；选择武器不会复制或替换背包里的物品，已有的强化和附魔记录保留。镰刀和弹弓不属于本版本的近战武器选择范围。

原版有 10 件与 NPC 对应的隐藏武器，并非人人都有：

| NPC | 武器 ID |
| --- | --- |
| Alex | `(W)25` |
| Sam | `(W)30` |
| Elliott | `(W)35` |
| Maru | `(W)36` |
| Harvey | `(W)37` |
| Penny | `(W)38` |
| Leah | `(W)39` |
| Abigail | `(W)40` |
| Sebastian | `(W)41` |
| Haley | `(W)42` |

Emily 和 Shane 没有上述对应武器；未放入背包武器或指定默认武器时使用生锈短剑。本版本保留现有攻击表现，未添加通用 NPC 挥舞武器动画：本地原版代码未提供可直接用于所有普通 NPC 的通用武器挥舞流程。

## 编写内容包

候选数据为 Content Patcher 可编辑的字典资产：

```text
Mods/com.szzrain.npc_following/Companions
```

字典键必须使用 NPC 的内部名称，不能使用翻译后的显示名。自定义 NPC 必须已由其他模组加入游戏。

新建一个文件夹，在 `manifest.json` 中声明 Content Patcher 和主模组依赖：

```json
{
  "Name": "Example Companion Pack",
  "Author": "YourName",
  "Version": "1.0.0",
  "Description": "Adds a companion definition.",
  "UniqueID": "YourName.ExampleCompanions",
  "ContentPackFor": { "UniqueID": "Pathoschild.ContentPatcher" },
  "Dependencies": [
    { "UniqueID": "com.szzrain.npc_following", "MinimumVersion": "1.1.0", "IsRequired": true }
  ]
}
```

在 `content.json` 中添加 NPC 定义；以下 `ExampleNpc` 需要替换为实际内部名：

```json
{
  "Format": "2.0.0",
  "Changes": [
    {
      "Action": "EditData",
      "Target": "Mods/com.szzrain.npc_following/Companions",
      "Entries": {
        "ExampleNpc": {
          "Enabled": true,
          "MinimumHearts": 6,
          "LatestRecruitTime": 2200,
          "AcceptDialogue": "{{i18n:recruit.accept}}",
          "RejectDialogue": "{{i18n:recruit.reject}}",
          "RejectLateDialogue": "{{i18n:recruit.rejectLate}}",
          "DefaultWeaponId": "(W)0"
        }
      }
    }
  ]
}
```

在该包的 `i18n/default.json` 中提供台词；可另加 `i18n/zh.json` 等语言：

```json
{
  "recruit.accept": "I'd be happy to join you!$h",
  "recruit.reject": "Let's get to know each other better first.",
  "recruit.rejectLate": "It's getting late. Let's go another day."
}
```

`Enabled` 默认 `true`，设为 `false` 可停用候选。`MinimumHearts` 默认 `10`，合法范围为 `0` 至 `14`。`LatestRecruitTime` 默认 `2200`，使用游戏时间 `HHMM`，合法范围为 `600` 至 `2600`，分钟必须为 `00`、`10`、`20`、`30`、`40` 或 `50`；达到该时间即拒绝。非法好感或时间定义会记录警告并跳过该 NPC。缺失或留空的接受、普通拒绝台词使用主模组的本地化兜底台词；太晚拒绝台词先回退到该 NPC 的普通拒绝台词，两者都没有时再使用主模组的太晚拒绝台词。

`DefaultWeaponId` 可省略或设为 `null`，以使用原版对应武器及生锈短剑回退；指定时推荐使用带 `(W)` 前缀的武器 ID。自定义武器必须由其他模组正确注册。

覆盖默认包中同名 NPC 时，建议在扩展包的 `Dependencies` 中增加 `{ "UniqueID": "com.szzrain.npc_following.cp", "IsRequired": true }`，保证在默认包之后应用。修改单个字段可用 `EditData` 的 `Fields`，例如停用默认 Abigail：

```json
{
  "Action": "EditData",
  "Target": "Mods/com.szzrain.npc_following/Companions",
  "Fields": {
    "Abigail": { "Enabled": false }
  }
}
```

内容资产刷新在游戏主线程、玩家未忙碌且没有菜单时处理，避免打开背包或招募对话期间销毁同伴。已经同行的 NPC 保留本次招募使用的定义快照，直到解散；因此移除或停用正在同行的 NPC 不会突然中断本次同行。

## 构建与自动验证

使用 .NET SDK 6.0.418（目标框架仍为 `net6.0`）。构建时传入 `EnableModDeploy=false` 和 `EnableModZip=false` 可避免复制到游戏安装目录；默认内容包的复制也遵守该开关。

```powershell
dotnet build NpcFollowing/NpcFollowing.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false
dotnet run --project tests/NpcFollowing.RuleTests
powershell -NoProfile -File tests/ValidateContent.ps1
```

如果自动检测不到游戏目录，在构建命令中追加 `-p:GamePath="你的 Stardew Valley 安装目录"`。规则测试是无需游戏、无需 NuGet 包的控制台测试，失败会以非零状态退出。

另外可用 PowerShell 7 对安装的原版数据和真实武器实例做验证：

```powershell
pwsh -NoProfile -File tests/GameDataSmoke.ps1 -GamePath "你的 Stardew Valley 安装目录"
```

该检查读取 Release 构建结果和原版数据，不启动游戏、不读写存档，覆盖专属映射、弹弓/镰刀排除、无效配置回退、锻造附魔实例及背包物品不变。它不能替代下面的游戏内场景验证。

调试棒入口、临时会话清理及物品归还的回归检查也需要最新 Release 构建和新的 PowerShell 7 进程：

```powershell
pwsh -NoProfile -File tests/DebugCompanionSmoke.ps1 -GamePath "你的 Stardew Valley 安装目录"
```

该检查使用内存地图和 SMAPI 接口替身，调用实际补丁、管理器和状态机；不创建图形设备、不运行游戏循环，也不读写存档。

## 手动验证清单

- 安装默认包进入存档，确认只有上述 12 位 NPC 可发起普通招募，其他社交 NPC 不出现普通招募菜单；移走候选包后重新启动，确认普通候选名单为空。
- 分别以低于 10 心、达到 10 心、22:00 之后请求同一候选，检查专属拒绝、接受和太晚台词；拒绝分支请分日测试。
- 切换中文与英文，确认招募问题、同伴菜单和每位候选的三类台词均正确显示。检查节日不开放招募。
- 给同伴背包放入普通物品和两把武器，调换武器槽位，确认按槽位顺序使用首件近战武器；移除所有武器，检查原版对应武器或生锈短剑回退。
- 为 NPC 配置有效及无效 `DefaultWeaponId`，检查默认覆盖、无效 ID 警告以及原版武器、生锈短剑回退；确认没有生成可取走的默认武器。
- 添加自定义 NPC 定义，确认仅安装对应 NPC 模组时登记；测试禁用、缺失台词和非法好感/时间定义不会导致崩溃。
- 打开同伴背包后刷新内容包，再关闭菜单，确认物品未丢失；同行时停用该 NPC，确认本次同行可正常解散，之后不可再次普通招募。
- 将农舍可放置包裹的地砖全部占满，给同伴放入可区分的武器和物品，再过夜或移除候选，确认日志提示失物招领；保存并重载后到镇长庄园取回，检查数量、强化及附魔记录，确认没有丢失或重复。
- 解散、过夜、返回标题后加载另一个存档，确认 NPC 恢复原版日程，候选和事件订阅不残留或重复。
- 对名单外及不可社交 NPC 使用调试棒，确认可强制同行、再次使用可解散，且没有永久改变其 `CanSocialize` 或加入普通候选。
- 分别在低好感、深夜、节日、当天普通招募遭拒或已解散后使用调试棒，确认仍可强制同行；普通招募的条件保持有效。
- 带着名单外临时同伴跨地图、刷新内容包并操作背包，确认同行和物品保留；解散或过夜后确认实际物品返还，次日及保存重载后不自动成为普通候选。
- 临时同伴同行时返回标题再加载另一存档，确认旧状态与事件不残留；多人存档不启用普通招募或调试棒同行。
