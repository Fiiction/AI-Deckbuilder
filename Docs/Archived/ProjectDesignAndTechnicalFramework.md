# AI-Deckbuilder 项目设计与技术框架文档

生成日期：2026-07-01  
工程路径：`C:/Old/Work/2025/AI-Deckbuilder`  
Unity 版本：`6000.0.32f1`

## 1. 项目定位

本项目是一个基于 NueDeck 框架扩展的 AI 卡牌 Roguelike 原型。核心体验接近《杀戮尖塔》：玩家在战斗中使用手牌、管理法力、牌堆、弃牌堆和消耗堆，击败敌人后获得奖励并推进关卡。

项目的主要创新点是：角色、卡牌文本、卡牌效果和卡牌/角色图像不是完全静态配置，而是根据玩家输入的角色描述，通过 LLM 与图像生成服务动态生成。也就是说，传统卡牌游戏中由设计师预先写死的部分，被拆成两层：

- 稳定的玩法执行框架：回合、牌堆、伤害、格挡、治疗、抽牌、状态、奖励等。
- AI 生成层：角色背景、卡牌名称/描述、图片 prompt、战斗中卡牌/敌人/回合开始效果的 JSON 化解释。

项目当前采用“保留 NueDeck 作为底层 gameplay 框架，在 `Assets/Scripts` 下追加 AI 集成层”的方式实现。这种结构的好处是迭代速度快，缺点是 AI 层和 NueDeck 原框架之间有若干耦合点，需要在后续重构时持续收敛边界。

## 2. 主要目录结构

```text
Assets/
  Scripts/
    Integration/          # AI 与 NueDeck 的主要集成层
    LLM/                  # LLM 请求封装与参数模型
    StableDiffusion/      # 图像生成入口与 Stable Diffusion 旧接口
    Gameplay/             # 当前项目新增的 gameplay 支撑逻辑，例如 ResetTurn
  ComfyUI/                # ComfyUI HTTP/图片轮询接口
  Prefabs/
    ManagerRoot.prefab    # 全局 Manager 聚合 Prefab
  NueGames/NueDeck/
    Scripts/              # NueDeck 原始/改造后的核心战斗框架
    Prefabs/              # NueDeck 核心 UI、Manager、卡牌、角色等 Prefab
    Scenes/               # Main Menu、Map、Combat Scene、NueCore
  Resources/
Packages/
  manifest.json           # Unity 包依赖
```

关键脚本分布：

| 模块 | 代表脚本 | 职责 |
| --- | --- | --- |
| AI 总控 | `Assets/Scripts/Integration/AI_IntegrationManager.cs` | LLM 参数、会话上下文、初始角色生成、战斗上下文维护、JSON 校正入口 |
| 卡牌/战斗效果解释 | `Assets/Scripts/Integration/AI_CardEffect.cs` | 将出牌、回合开始、敌人行动转换为 LLM prompt，并将 JSON 结果转换为 `CardActionData` |
| 卡牌生成 | `Assets/Scripts/Integration/AI_DeckGenerator.cs` | 生成初始牌组与稀有/史诗/传奇奖励卡 |
| 图像生成 | `Assets/Scripts/StableDiffusion/AI_ImageGeneration.cs`、`Assets/ComfyUI/ComfyProcessor.cs` | 通过 ComfyUI 生成角色和卡牌图像 |
| LLM 请求 | `Assets/Scripts/LLM/LLMService.cs`、`Assets/Scripts/LLM/LLMParams.cs` | OpenAI-compatible Chat Completions 请求封装、超时、取消、错误记录 |
| 回合/关卡重置 | `Assets/Scripts/Gameplay/TurnResetController.cs` | 快照、ResetTurn、ResetLevel、AI 请求取消与状态恢复 |
| 战斗状态机 | `Assets/NueGames/NueDeck/Scripts/Managers/CombatManager.cs` | 战斗开始、玩家回合、敌人回合、胜负与奖励 |
| 牌堆管理 | `Assets/NueGames/NueDeck/Scripts/Managers/CollectionManager.cs` | 抽牌堆、手牌、弃牌堆、消耗堆 |
| 卡牌运行时 | `Assets/NueGames/NueDeck/Scripts/Card/CardBase.cs` | 出牌交互、AI 效果等待、效果执行、弃牌/消耗 |
| 卡牌数据 | `Assets/NueGames/NueDeck/Scripts/Data/Collection/CardData.cs` | 卡牌名称、描述、费用、目标、稀有度、图片 |

## 3. Unity 场景与全局 Prefab 组织

Build Settings 中当前启用 4 个场景：

1. `Assets/NueGames/NueDeck/Scenes/0- Main Menu.unity`
2. `Assets/NueGames/NueDeck/Scenes/1- Map.unity`
3. `Assets/NueGames/NueDeck/Scenes/2- Combat Scene.unity`
4. `Assets/NueGames/NueDeck/Scenes/NueCore.unity`

主菜单场景中存在 `CoreLoader`。`CoreLoader` 只在 build index 为 0 的主菜单场景工作，它会实例化 `Assets/Prefabs/ManagerRoot.prefab`，并将其实例设为 `DontDestroyOnLoad`。这意味着大部分系统级 Manager 不直接放在每个场景里，而是由 `ManagerRoot` 统一提供。

`ManagerRoot.prefab` 的核心层级：

```text
ManagerRoot
  AudioManager
  FxManager
  GameManager
  UIManager
    CombatCanvas
      ResetLevelButton
      ResetTurnButton
      DrawPile / DiscardPile / ExhaustPile / EndTurnButton / Mana...
    InformationCanvas
    RewardCanvas
    InventoryCanvas
  TooltipManager
  AIManager
    CardEffect
      ProcessingCanvas
    DeckGenerator
    ImageGeneration
    DebugCanvas
```

这套组织方式的设计意图比较明确：

- 常驻系统（GameManager、UIManager、AIManager）由一个根 Prefab 管理，跨场景保持状态。
- 战斗场景只负责提供当前战斗需要的 `CombatManager`、角色站位、背景等场景实体。
- UI 尽量放在 Prefab 上，通过 Inspector 绑定引用，符合“所见即所得”的维护方式。
- AI 相关 prompt 也主要挂在 `AIManager` 子对象上，而不是散落在代码中。

## 4. 技术栈与包依赖

项目使用的关键 Unity 包：

| 包 | 用途 |
| --- | --- |
| `com.unity.ugui` | 主要 UI 框架，项目当前 UI 基于 Canvas/uGUI |
| `com.unity.inputsystem` | 新输入系统依赖 |
| `com.unity.nuget.newtonsoft-json` | LLM 返回 JSON 解析与严格结构校验 |
| `com.coplaydev.unity-mcp` | 通过 MCP 让外部工具读取/修改 Unity 工程 |
| `com.simonoliver.unityfigma` | Figma 到 Unity 的 UI 桥接 |
| `com.unity.test-framework` | Unity 测试框架，当前文档未发现项目测试用例作为主流程依赖 |

此外项目集成了两个外部 AI 服务接口：

- LLM：通过 OpenAI-compatible Chat Completions 风格接口调用，具体 URL、model、key、temperature、timeout 等由 `LLMParams` 配置。
- 图像生成：当前主要走 ComfyUI HTTP `/prompt`、`/history/{id}`、`/view?filename=...` 流程；旧的 Stable Diffusion WebUI `/sdapi/v1/txt2img` 接口仍保留在 `SDProcessor` 中。

## 5. 核心运行流程

### 5.1 游戏启动与初始化

主菜单进入后，`CoreLoader` 实例化 `ManagerRoot`。`GameManager` 初始化持久 gameplay 数据，`AI_IntegrationManager` 保存 AI 会话上下文与玩家角色描述。

当调用 `AI_IntegrationManager.Init()` 时：

1. 清空 `_conversationSoFar` 与 `_cardGenConversationSoFar`。
2. 将玩家输入的 `heroName`、`heroDesc` 填入初始 prompt。
3. 请求 LLM 生成角色背景与图像 prompt。
4. `InitialResponse()` 解析返回内容：
   - 保存 `heroStory`
   - 保存 `heroPrompts`
   - 调用 `AI_ImageGeneration.GenerateHeroSprite()`
   - 调用 `AI_DeckGenerator.GenerateStartDeck()`

初始化进度由 `AI_IntegrationManager.Update()` 聚合：

- 基础信息处理进度
- 已生成卡牌数量
- 已生成角色图
- 已生成卡牌图

当初始牌组与图像达到预期数量后，调用 `OnInitFinished()`，将主对话上下文复制到卡牌生成上下文，并开始预生成 rare cards。

### 5.2 卡牌生成流程

`AI_DeckGenerator` 将卡牌生成拆成两个阶段：

1. 先请求 LLM 给出整体设计方向或某一稀有度卡组描述。
2. 再逐张请求 LLM 返回结构化卡牌数据。

LLM 返回的卡牌数据结构为：

```json
{
  "cardName": "string",
  "description": "string",
  "manaCost": 1,
  "needTarget": true,
  "prompt": "image prompt"
}
```

随后 `ConvertCardData()` 将返回内容转换成运行时 `CardData`：

- `cardName` → 卡牌名
- `description` → 卡牌描述
- `manaCost` → 费用
- `needTarget` → 转换为 `usableWithoutTarget`
- `prompt` → 发送给 ComfyUI 生成卡图
- `rarity` → 根据生成阶段设置为 Common/Rare/Epic/Legendary

初始牌组当前生成 4 张基础卡，并按固定数量加入牌组：

- 基础卡 1：4 张
- 基础卡 2：4 张
- 基础卡 3：2 张
- 基础卡 4：2 张

战斗胜利后，`AI_IntegrationManager.OnEndGame()` 根据关卡数触发后续稀有度卡牌生成：

- 第 1 场结束后生成 Epic
- 第 2 场结束后生成 Legendary

### 5.3 战斗启动流程

`CombatManager.StartCombat()` 负责战斗准备：

1. 根据 `GameManager.PersistentGameplayData.CurrentLevel` 构建敌人。
2. 调用 `AI_IntegrationManager.SendStartGamePrompt()`，把当前玩家与敌人信息加入待发送上下文。
3. 构建玩家角色。
4. 打开背景。
5. `CollectionManager.SetGameDeck()` 将当前牌组放入抽牌堆。
6. 打开 CombatCanvas 与 InformationCanvas。
7. 进入 `AllyTurn`。

战斗状态由 `CombatStateType` 驱动：

```text
PrepareCombat
  -> AllyTurn
      -> EnemyTurn
          -> AllyTurn
              ...
  -> EndCombat
```

### 5.4 玩家回合流程

进入 `AllyTurn` 时：

1. `TurnResetController.CaptureBeforeAllyTurn()` 捕获回合开始前快照。
2. `turnIndex++`
3. 当前法力恢复到最大法力。
4. 触发 `OnAllyTurnStarted`，让敌人显示下一行动等。
5. 调用 `AI_IntegrationManager.StartTurnCutConversation()`，把会话裁剪到本回合开始点。
6. 如果不是第一回合，调用 `AI_CardEffect.AllyTurnStartEffects()` 让 LLM 生成回合开始效果。
7. 回合开始效果执行结束后，`CollectionManager.DrawCards()` 抽牌，并允许玩家选牌。

回合开始效果与出牌效果使用同一个单元效果体系，最终都转成 `CardActionData`，再由 `CardActionProcessor` 执行。

### 5.5 出牌流程

`CardBase.Use()` 是玩家出牌入口。当前项目已将 NueDeck 原本的静态 `CardActionData` 执行方式改为 AI 驱动：

1. 检查卡牌可用性与 `CardBase.aiPending`。
2. 扣除费用。
3. 调用 `AI_CardEffect.CardUse()`。
4. 等待 LLM 返回完整效果 JSON。
5. 将 JSON 转换为 `List<CardActionData>`。
6. 对每个效果：
   - 根据 `ActionTargetType` 计算目标列表。
   - 调用 `CardActionProcessor.GetAction(type).DoAction(...)`。
7. 所有效果执行完后调用 `CollectionManager.OnCardPlayed()`。
8. 根据 `CardData.ExhaustAfterPlay` 决定进入弃牌堆或消耗堆。

注意：当前卡牌描述只作为 LLM 判断效果的输入，卡牌运行时没有保存一份固定效果列表。这意味着同一张卡在不同上下文下可以产生不同效果，也意味着可重复性依赖 LLM 输出稳定性与 Reset 机制。

### 5.6 敌人回合流程

敌人行动由 `CombatManager.EnemyTurnRoutine()` 驱动：

1. 回合开始时 `CollectionManager.DiscardHand()` 弃掉玩家手牌。
2. 遍历 `CurrentEnemiesList`。
3. 对每个未死亡敌人，执行 `EnemyBase.ActionRoutine()`。
4. `EnemyBase.ActionRoutine()` 将敌人的 `NextAbility.Name` 和 `NextAbility.Desc` 传给 `AI_CardEffect.EnemyTurn()`。
5. LLM 返回敌人行动的单元效果列表。
6. 效果转成 `CardActionData` 并执行。
7. 所有敌人行动结束后，如果战斗未结束则回到 `AllyTurn`。

敌人行动目前不是使用 NueDeck 原始 `EnemyActionData` 列表，而是使用敌人意图文本让 LLM 实时解释成 gameplay 效果。

## 6. AI CardEffect 设计

`AI_CardEffect` 是项目当前最核心的 AI/gameplay 桥接脚本。它处理三类时机：

- 玩家出牌：`CardUseCoroutine`
- 玩家回合开始：`AllyTurnStartEffectsCoroutine`
- 敌人行动：`EnemyTurnCoroutine`

这三类时机都会构造 prompt，并调用同一个请求与解析流程：

```text
上下文收集
  -> 填充 prompt
  -> RequestEffectsWithRetry()
  -> LLM 返回 SuperActions JSON
  -> TryBuildActionData()
  -> CardActionProcessor 执行
```

当前 LLM 效果 JSON 结构为：

```json
{
  "effects": [
    {
      "effectType": "Deal Damage",
      "buffname": "",
      "target": "targetEnemy",
      "value": 6
    }
  ]
}
```

字段含义：

| 字段 | 含义 |
| --- | --- |
| `effects` | 本次行为包含的所有单元效果 |
| `effectType` | 单元效果类型，例如 Deal Damage、Heal、Add Block、Draw、Exhaust |
| `buffname` | 自定义状态名，仅 `Add Custom Status` 必填 |
| `target` | 目标，例如 self、targetEnemy、allEnemies、hero、enemySelf |
| `value` | 数值；消耗类效果可填任意整数 |

当前支持的效果类型由 `StringToActionType()` 映射到 `CardActionType`：

| LLM 文本 | 内部动作 |
| --- | --- |
| `Deal Damage` | `Attack` |
| `Heal` | `Heal` |
| `Add Block` | `Block` |
| `Increase Strength` | `IncreaseStrength` |
| `Draw` | `Draw` |
| `Gain Mana` | `EarnMana` |
| `Steal Life` | `LifeSteal` |
| `Stun` | `Stun` |
| `Destroy the Card` / `Exhaust` | `Exhaust` |
| `Add Custom Status` | `CustomEffect` |

目标合法性由 `IsAllowedTarget()` 根据上下文检查：

- 玩家出牌且需要目标：允许 `self`、`allEnemies`、`targetEnemy`
- 玩家出牌但无目标：允许 `self`、`allEnemies`
- 玩家回合开始：允许 `self`、`allEnemies`
- 敌人行动：允许 `hero`、`enemySelf`、`allEnemies`

如果 LLM 返回未知效果类型、非法目标或自定义状态缺少 `buffname`，会追加 correction prompt 并重新处理。

## 7. LLM 请求、JSON 校正与失败恢复

LLM 请求由 `AI_IntegrationManager.Request()` 统一包裹，再下发到 `LLMService.Request()`。

关键机制：

- `replyWithJson = true` 时，会用 Newtonsoft Json 按指定类型校验返回 JSON。
- 如果 JSON 校验失败，会写入 `"Json Correction!"` 警告，并把 `jsonCorrectionPrompt` 加入 `pendingPrompts` 后重试。
- `AI_CardEffect` 自己还有效果语义层面的 correction，例如非法 target 或未知 effect type。
- CardEffect 请求有独立超时与重试全局参数：
  - `AI_IntegrationManager.CardEffectTimeoutSeconds`
  - `AI_IntegrationManager.CardEffectRetryCount`
- 重试或最终失败会写入 `AI_DebugCanvas`：
  - `Retry`
  - `Failed`

`LLMService` 的底层实现使用 `UnityWebRequest.Post()`，构造 OpenAI-compatible payload：

```json
{
  "model": "...",
  "temperature": 1,
  "messages": [...]
}
```

如果 `replyWithJson` 为 true，当前代码会额外添加：

```json
{
  "thinking": "{\"type\": \"enabled\"}"
}
```

对于包含 `gemini-2.5-flash` 的模型名，会根据 JSON 类型设置 `reasoning_effort`。这说明当前 LLM 接口兼容多个供应商，但字段兼容性需要按具体后端持续验证。

## 8. 图像生成框架

图像生成入口是 `AI_ImageGeneration`：

- `GenerateHeroSprite()`：使用角色 prompt 生成角色图。
- `GenerateCardSprite()`：使用卡牌 prompt 生成卡图。

当前实际启用的是 `ComfyProcessor`：

1. 根据角色/卡牌选择不同 ComfyUI workflow JSON。
2. 将 `##Prompt##` 替换为 AI 生成 prompt。
3. POST 到 `{serverAddress}/prompt`。
4. 保存返回的 `prompt_id`。
5. 每秒轮询 `/history/{prompt_id}`。
6. 找到输出文件后通过 `/view?filename=...` 下载图片。
7. 转成 `Texture2D`，再转成 Unity `Sprite`。

旧的 Stable Diffusion WebUI 接口仍保留在 `SDProcessor`，但 `AI_ImageGeneration` 中相关调用已经被注释，当前属于备用路径。

## 9. NueDeck Gameplay 基础框架

### 9.1 Manager 层

NueDeck 的 Manager 仍然是底层骨架：

- `GameManager`：持久 gameplay 数据、卡牌实例化、当前牌组。
- `CombatManager`：战斗状态机、角色生成、胜负判定、回合切换。
- `CollectionManager`：抽牌堆、手牌、弃牌堆、消耗堆。
- `UIManager`：Combat/Reward/Inventory/Information 等 Canvas 切换。
- `FxManager`、`AudioManager`：反馈表现。

这些 Manager 大多使用 singleton，并由 `ManagerRoot` 持有为常驻对象。

### 9.2 卡牌与牌堆

`CardData` 是卡牌数据模型。当前 AI 生成的卡牌是运行时构造的 `ScriptableObject` 派生对象实例，包含：

- id
- cardName
- description
- manaCost
- cardSprite
- rarity
- usableWithoutTarget
- exhaustAfterPlay

`CollectionManager` 管理四个列表：

- `DrawPile`
- `HandPile`
- `DiscardPile`
- `ExhaustPile`

出牌后：

- 如果 `CardData.ExhaustAfterPlay == true`，进入消耗堆。
- 否则进入弃牌堆。

此外，LLM 返回的 `Exhaust` 单元效果会调用 `ExhaustAction`，通过 `CardBase.Exhaust(false)` 把当前卡移动到消耗堆。这里 `destroy = false` 是为了避免效果执行中提前销毁对象；最终出牌流程结束后仍会调用 `CollectionManager.OnCardPlayed()`，这一块是后续最值得重点测试的交互点之一。

### 9.3 状态系统

`CharacterStats` 管理基础状态与自定义状态。

基础状态包括：

- Poison
- Block
- Strength
- Dexterity
- Stun

其中：

- Poison 会随触发造成穿甲伤害。
- Block 在下一次状态触发后清除。
- Strength/Dexterity 支持负数叠加。
- Stun 会影响角色能否行动。

自定义状态由 `Dictionary<string, CustomEffects>` 保存，`CustomEffectAction` 会调用 `ApplyCustomEffect(buffname, value)` 增加堆叠。当前自定义状态主要作为 LLM 可扩展语义层，并不自动内置 gameplay 逻辑；它们是否产生实际效果，需要后续通过 prompt 或额外规则系统解释。

## 10. ResetTurn 与 ResetLevel 设计

项目新增 `TurnResetController` 用于提升 AI 出错时的鲁棒性。

### 10.1 ResetTurn

目标：把局面恢复到“玩家本回合开始前，回合开始效果尚未执行”的状态。

捕获时机：

- `CombatManager.ExecuteCombatState(AllyTurn)` 进入玩家回合时调用 `CaptureBeforeAllyTurn()`。

快照内容包括：

- 战斗 turnIndex
- Unity Random.state
- 当前法力、最大法力、抽牌数、金币、关卡
- 当前牌组列表
- 抽牌堆/手牌/弃牌堆/消耗堆
- 玩家与敌人的血量、最大生命、基础状态、自定义状态
- 敌人 UsedAbilityCount
- AI 主对话上下文、卡牌生成上下文、pendingPrompts
- AI CardEffect 的 turn/card 计数

恢复流程：

1. 禁止出牌和选牌。
2. 重置手牌交互状态。
3. 取消正在进行的 AI CardEffect 请求。
4. 停止 CombatManager 协程。
5. 销毁当前场上卡牌、敌人、玩家角色、浮动文本。
6. 恢复 PersistentGameplayData。
7. 恢复 AI 会话与计数。
8. 恢复牌堆。
9. 重新实例化角色。
10. 重新实例化手牌表现。
11. 恢复 UI。
12. 重新进入 AllyTurn。

### 10.2 ResetLevel

目标：重置当前普通战斗到本场战斗初始状态，同时不影响其他长期逻辑，例如已生成/待生成卡牌。

实现上使用 `levelSnapshot`。第一次捕获回合快照时，如果 `levelSnapshot == null`，会将其设置为当前快照。`ResetLevel()` 使用 `levelSnapshot` 恢复，并调用：

```csharp
ResetTurnCoroutine(levelSnapshot, false)
```

其中 `restoreCardGenerationState = false`，表示不恢复卡牌生成对话上下文与相关进度，避免影响外部生成逻辑。

UI 上，`CombatCanvas` 持有静态挂载在 Prefab 上的：

- `resetTurnButton`
- `resetLevelButton`

并在 `Update()` 中根据 `TurnResetController.CanReset` / `CanResetLevel` 控制按钮 interactable。

## 11. 调试与可观测性

项目当前主要有三类调试反馈：

1. Unity Console  
   记录请求、回复、JSON 校正、效果转换、动作执行等。

2. `AI_DebugCanvas`  
   - `F1` 展开/隐藏详细 debug 面板。
   - 显示 `AI_IntegrationManager.debugStr`。
   - `AddWarning()` 显示短期 warning，例如 `Json Correction!`、`Retry`、`Failed`、`Correction!`。

3. `AI_ProcessingCanvas`  
   显示当前 AI 处理状态和耗时，例如：
   - `Card Processing:`
   - `Turn Start Processing:`
   - `Enemy Action Processing:`

这些工具对 AI 卡住、返回非法 JSON、目标非法、超时等问题很重要。当前调试信息偏文本堆叠，适合开发阶段；如果进入面向玩家的 Demo，建议拆成“玩家可见提示”和“开发者详细日志”两层。

## 12. 当前架构优点

1. 底层玩法与 AI 创意层分离相对清晰  
   NueDeck 提供稳定战斗框架，AI 层只负责生成内容与解释效果。

2. Prompt 大多挂在 Prefab Inspector 上  
   非程序人员可以调整 prompt，不必改代码。

3. 单次 LLM 返回完整效果列表  
   `AI_CardEffect` 当前已经不是“先判断效果数量/种类，再逐个细化”的多次请求结构，而是一次返回完整 `effects` JSON，效率更好。

4. 有 ResetTurn/ResetLevel 作为 AI 失败恢复手段  
   对 LLM 不稳定、网络超时、敌人回合卡住等问题有实际兜底价值。

5. CardEffect 有独立超时/重试  
   避免普通 LLM 参数 timeout 过长直接拖死战斗流程。

## 13. 当前主要风险与改进方向

### 13.1 AI 效果仍然偏运行时解释，确定性不足

同一张卡的效果在不同上下文下会重新请求 LLM。优点是动态性强，缺点是：

- 同卡多次使用可能不完全一致。
- Reset 后如果再次请求 LLM，结果可能变化。
- 平衡性难以控制。

可选改进：

- 生成卡牌时同时生成“基础效果模板”，战斗中只允许 LLM 做上下文微调。
- 给每张卡保存一个稳定 effect schema，再由数值/目标上下文驱动。
- 对 LLM 输出做 deterministic cache：同一战斗状态 hash + card id + target id 返回同一结果。

### 13.2 自定义状态缺少规则解释器

`Add Custom Status` 当前只会增加一个字符串状态及堆叠，真正 gameplay 影响还依赖后续 LLM 在 prompt 中理解该状态。这种方式表达力强，但状态生命周期、触发时机、数值影响不够确定。

可选改进：

- 设计 `CustomStatusDefinition`，包含触发时机、数值公式、持续时间、是否负面等。
- 让 LLM 生成状态定义 JSON，而不是只生成 `buffname`。
- 在回合开始/出牌/受击/造成伤害等固定钩子上执行规则。

### 13.3 牌堆与 Exhaust 的交互需要持续测试

当前 `ExhaustAction` 在效果执行时调用 `CardBase.Exhaust(false)`，而出牌流程最后仍会执行 `CollectionManager.OnCardPlayed(this)`。虽然 `CardBase.Exhaust()` 内部有 `IsExhausted` 防重入保护，但 `OnCardPlayed()` 中如果卡牌不是 `ExhaustAfterPlay`，仍会调用 `targetCard.Discard()`；`Discard()` 又会因为 `IsExhausted` 返回，从而避免重复进弃牌堆。

这个逻辑目前可以工作，但可读性较弱。建议后续显式表达“本次卡牌已由效果移动到消耗堆”，避免依赖 `IsExhausted` 间接防御。

### 13.4 LLM provider 字段兼容性

`LLMService` 会根据不同场景添加 `thinking`、`reasoning_effort` 等字段。这些字段不是所有 OpenAI-compatible 后端都支持。

可选改进：

- 在 `LLMParams` 中增加 provider type。
- 根据 provider type 构造请求字段。
- 将 response_format/json mode 与 prompt-only JSON 约束分离。

### 13.5 Reset 快照覆盖面需要随 gameplay 扩展同步维护

`TurnResetController.TurnSnapshot` 当前覆盖了大部分核心 gameplay 数据。但以后新增系统时，例如遗物、临时召唤物、场地效果、持续动画、队列中的奖励、敌人特殊阶段，都需要加入快照。

建议约定：任何会影响战斗结果的状态，都必须满足以下之一：

- 可从已有快照数据推导恢复。
- 明确加入 `TurnSnapshot`。
- 在 Reset 时被安全清空且不会影响后续逻辑。

## 14. 建议的开发规范

1. NueDeck 原框架尽量作为稳定底座  
   新 AI 逻辑优先放在 `Assets/Scripts` 下，只有当需要接入生命周期/状态机/牌堆时再小范围修改 NueDeck。

2. Prompt 修改优先走 Prefab Inspector  
   尤其是 `AIManager/CardEffect` 和 `DeckGenerator` 子对象，方便非代码调参。

3. 所有 LLM JSON 输出都应有 C# struct/class 对应  
   并用 Newtonsoft Json 做严格解析，避免“看起来像 JSON 但字段缺失”的隐性错误。

4. 新增 gameplay 状态时同步考虑 Reset  
   新状态一旦能影响战斗结果，就应评估是否加入 `TurnSnapshot`。

5. 新增单元效果时需要同时改四处  
   - Prompt 中的效果说明
   - `AI_CardEffect.StringToActionType()`
   - `AI_CardEffect.ActionTypeToString()` / `ValueMeaning()`
   - `CardActionProcessor` 对应 action 类

6. Debug 信息分层  
   开发者调试信息继续走 `AI_DebugCanvas`，玩家提示应另建更干净的 UI。

## 15. 关键源码入口

- `Assets/Scripts/Integration/AI_IntegrationManager.cs`
- `Assets/Scripts/Integration/AI_CardEffect.cs`
- `Assets/Scripts/Integration/AI_DeckGenerator.cs`
- `Assets/Scripts/StableDiffusion/AI_ImageGeneration.cs`
- `Assets/ComfyUI/ComfyProcessor.cs`
- `Assets/Scripts/LLM/LLMService.cs`
- `Assets/Scripts/Gameplay/TurnResetController.cs`
- `Assets/NueGames/NueDeck/Scripts/Managers/CombatManager.cs`
- `Assets/NueGames/NueDeck/Scripts/Managers/CollectionManager.cs`
- `Assets/NueGames/NueDeck/Scripts/Card/CardBase.cs`
- `Assets/NueGames/NueDeck/Scripts/Data/Collection/CardData.cs`
- `Assets/NueGames/NueDeck/Scripts/UI/CombatCanvas.cs`
- `Assets/Prefabs/ManagerRoot.prefab`

## 16. 总结

当前项目的技术框架可以概括为：

```text
NueDeck 战斗框架
  提供回合、牌堆、角色、UI、状态、奖励、动作执行

AI 集成层
  负责角色生成、卡牌生成、图像生成、战斗效果解释、LLM 会话上下文

鲁棒性支撑层
  负责 LLM JSON 校正、超时重试、Debug Canvas、ResetTurn/ResetLevel
```

这是一种适合 AI 原型快速迭代的架构：底层 gameplay 稳定，创意内容动态生成。后续如果目标是提高可控性和可测试性，应优先把“LLM 自由解释”逐步收敛为“LLM 生成结构化设计，运行时使用本地规则执行”。
