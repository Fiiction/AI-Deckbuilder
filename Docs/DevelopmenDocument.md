# AI-Deckbuilder 可执行AI内容系统开发路径文档

生成日期：2026-07-01
项目基础：Unity 6000 + NueDeck + 当前 AI_Deckbuilder 原型
核心目标：从“出牌时请求LLM计算效果”逐步迁移到“LLM生成可执行内容，本地确定性执行”

---

## 0. 前置步骤：优化当前 NueDeck 框架与基础效果集

在进入 EffectSpec / Rule DSL 迁移前，应该先对当前 NueDeck 战斗底座做一次“减法”。这一步不是为了增加新功能，而是为了让后续规则系统建立在更小、更稳定、更容易验证的动作集合上。

当前项目中，`CardActionType` 与 `CardActionProcessor` 已经支持较多 NueDeck 原始动作：

```text
Attack
Heal
Block
IncreaseStrength
IncreaseMaxHealth
Draw
EarnMana
LifeSteal
Stun
Exhaust
CustomEffect
Unknown
```

其中一部分动作对当前 AI 生成卡牌系统来说并不适合作为第一阶段的基础语义。例如：

```text
LifeSteal
Stun
IncreaseMaxHealth
```

原因是它们要么可以由更基础的动作组合表达，要么会过早引入平衡和状态生命周期复杂度：

```text
LifeSteal = DealDamage + Heal
Stun = ApplyStatus(Stun) 或后续 StatusDefinition
IncreaseMaxHealth = 更偏角色成长/遗物/事件奖励，不适合作为普通卡牌 EffectSpec v0 的核心动作
```

### 0.1 当前阶段建议保留的最小核心动作

建议把第一阶段本地可执行系统的核心动作收敛为：

```text
DealDamage
Heal
GainBlock
DrawCards
GainMana
ApplyStatus
ExhaustThisCard
```

它们分别映射到现有 NueDeck 能力：

```text
DealDamage      → CardActionType.Attack
Heal            → CardActionType.Heal
GainBlock       → CardActionType.Block
DrawCards       → CardActionType.Draw
GainMana        → CardActionType.EarnMana
ApplyStatus     → 先映射到 IncreaseStrength / CustomEffect，后续迁移到 StatusDefinition
ExhaustThisCard → CardActionType.Exhaust
```

### 0.2 建议删除或降级的基础效果

短期内不建议让 LLM 主动选择以下效果：

```text
Steal Life
Stun
Increase Max Health
```

处理方式建议分两步：

1. 先从 prompt、AI_CardEffect 白名单、EffectSpec v0 文档中移除或标记为 deprecated；
2. 确认没有生成内容依赖后，再考虑删除对应 `CardActionType` 和 `CardActions/*.cs`。

不要一开始直接物理删除所有脚本。原因是 `CardActionProcessor` 通过反射收集所有 `CardActionBase` 子类，直接删除 enum 或 action 类容易造成旧资源、旧 prefab、旧 prompt 输出引用失效。更安全的顺序是：

```text
停止生成
→ 停止解析
→ 搜索并清理引用
→ 删除代码
→ 编译和场景验证
```

### 0.3 这一阶段的完成标准

```text
AI prompt 中不再鼓励生成 LifeSteal / Stun / IncreaseMaxHealth
AI_CardEffect 的推荐/白名单动作与 EffectSpec v0 保持一致
开发文档中的 Action 命名统一为 DSL 语义，而不是直接暴露 NueDeck enum 名
旧 NueDeck action 若暂时保留，也被标记为 legacy/deprecated
后续所有新系统只依赖最小核心动作集
```

这个第 0 步很重要。否则后续 DSL 会被 NueDeck 原始 Action 牵着走，过早承载“吸血、晕眩、最大生命提升”这类复合或高风险效果，导致规则系统还没稳定就开始膨胀。

---

## 1. 新开发目标

当前项目已经具备一个很有价值的原型闭环：

```text
玩家输入角色设定
→ LLM生成角色与卡牌
→ ComfyUI生成图像
→ 战斗中出牌/回合开始/敌人行动请求LLM解释效果
→ LLM返回JSON
→ 转换为CardActionData
→ NueDeck执行
```

下一阶段的核心目标不是推翻这套结构，而是逐步把其中最不稳定的部分替换掉：

```text
当前：
战斗中实时请求LLM解释卡牌效果

目标：
生成阶段让LLM产出结构化规则
战斗阶段只由Unity本地规则解释器执行
```

最终系统应该满足：

1. 同一张卡每次使用效果一致；
2. ResetTurn 后不需要重新询问LLM；
3. 回合开始、受伤、造成伤害、出牌后、死亡前等时机都能由本地规则触发；
4. 自定义状态不再只是字符串堆叠，而是带有可执行规则；
5. 卡牌、角色被动、怪物意图、遗物、状态都能使用同一套规则系统；
6. AI生成内容在进入战斗前通过Schema校验、语义校验和自动测试。

---

## 2. 总体迁移策略

不建议一次性重构成完整DSL系统。更安全的路线是分阶段迁移：

```text
阶段0：优化当前NueDeck框架，收敛基础Action集合
阶段0.5：冻结当前运行时AI解释能力，补充日志与测试
阶段1：让LLM在卡牌生成时同时生成固定EffectSpec
阶段2：用本地EffectExecutor替代部分AI_CardEffect实时请求
阶段3：引入事件驱动Rule DSL，支持被动、状态、回合钩子
阶段4：引入Modifier系统，支持伤害修正、费用修正、替换效果
阶段5：形成完整内容生成管线：生成 → 校验 → 编译 → 测试 → 入库
```

迁移原则：

```text
旧系统继续可用
新系统优先接管简单效果
复杂效果暂时回退到旧AI解释
每完成一层，就减少一类运行时LLM请求
```

---

## 3. 目标架构

最终架构建议改为：

```text
AI_IntegrationManager
  负责角色设定、会话上下文、内容生成请求

AI_DeckGenerator
  生成卡牌文本、图片prompt、EffectSpec / RuleSpec

AI_ContentValidator
  校验LLM生成内容是否合法

AI_EffectCompiler
  将JSON DSL编译成Unity内部RuntimeEffect

RuntimeRuleExecutor
  在战斗中执行本地规则

CombatEventBus
  广播TurnStarted、CardPlayed、BeforeDealDamage等事件

ModifierPipeline
  处理伤害、费用、抽牌、状态变化等可修改流程

GeneratedContentDatabase
  保存本局生成的卡牌、状态、怪物、遗物、角色被动

AI_CardEffect
  逐步降级为兼容层、调试层和旧内容fallback
```

对应关系：

```text
现在的 AI_CardEffect：
  Prompt → LLM → SuperActions JSON → CardActionData → 执行

未来的 RuntimeRuleExecutor：
  Event → Rule匹配 → ActionSpec → CardActionData / GameCommand → 执行
```

---

## 4. 阶段0.5：稳定现有原型

### 4.1 目标

在完成基础 Action 收敛后，再把当前系统的行为记录清楚，避免之后无法判断重构是否破坏了已有功能。

### 4.2 需要完成的工作

#### 4.2.1 为AI_CardEffect增加结构化日志

当前已有 `AI_DebugCanvas` 和 Unity Console，但建议把每次效果解释记录成结构化对象：

```json
{
  "requestId": "combat_1_turn_2_card_5",
  "contextType": "CardUse",
  "cardId": "generated_card_001",
  "cardName": "影焰刺击",
  "cardDescription": "造成伤害并施加灼烧",
  "target": "enemy_0",
  "rawLLMResponse": "...",
  "parsedEffects": [
    {
      "effectType": "Deal Damage",
      "target": "targetEnemy",
      "value": 6
    }
  ],
  "finalCardActionData": "...",
  "success": true
}
```

#### 4.2.2 建立当前Action覆盖表

把当前 `StringToActionType()` 支持的效果整理为正式白名单。注意这里的“正式白名单”应与第 0 步收敛后的核心动作保持一致，而不是照搬 NueDeck 当前所有历史 Action：

```text
Deal Damage
Heal
Add Block
Draw
Gain Mana
Exhaust
Add Custom Status
```

`Increase Strength` 可以暂时作为 `ApplyStatus(Strength)` 的兼容映射，但不建议作为 DSL 顶层动作长期保留。`Steal Life`、`Stun`、`Increase Max Health` 不应进入 EffectSpec v0 核心白名单。

#### 4.2.3 明确旧系统的fallback条件

新系统执行失败时，可以临时回退到旧AI解释，但必须有明确条件：

```text
EffectSpec为空
EffectSpec校验失败
RuleSpec包含未支持字段
本地Executor抛出异常
开发者开启LegacyAIExecution模式
```

---

## 5. 阶段1：卡牌生成时固定效果

### 5.1 当前问题

现在卡牌生成阶段主要生成：

```json
{
  "cardName": "string",
  "description": "string",
  "manaCost": 1,
  "needTarget": true,
  "prompt": "image prompt"
}
```

卡牌描述只作为后续出牌时的LLM输入。也就是说，卡牌的真实效果没有被保存下来。

这会导致：

```text
同一张卡每次使用可能不同
Reset后重新请求可能不同
无法提前测试卡牌平衡
无法在奖励界面准确展示实际效果
```

### 5.2 修改方向

让 `AI_DeckGenerator` 在生成卡牌时同时生成 `effectSpec`。

新增结构：

```json
{
  "cardName": "影焰刺击",
  "description": "造成6点火焰伤害。若目标有Poison，抽1张牌。",
  "manaCost": 1,
  "needTarget": true,
  "prompt": "image prompt",
  "effectSpec": {
    "actions": [
      {
        "op": "DealDamage",
        "target": "SelectedEnemy",
        "damageType": "Fire",
        "amount": { "const": 6 }
      },
      {
        "op": "DrawCards",
        "target": "Self",
        "amount": { "const": 1 },
        "condition": {
          "gt": [
            { "status": ["SelectedEnemy", "Poison"] },
            { "const": 0 }
          ]
        }
      }
    ]
  }
}
```

### 5.3 Unity侧改动

#### CardData 增加字段

```csharp
public string GeneratedEffectJson;
public EffectSpec RuntimeEffectSpec;
public bool UseLocalEffectExecution;
```

#### ConvertCardData 增加解析流程

```text
LLM返回Card JSON
→ 解析基础卡牌数据
→ 解析effectSpec
→ AI_ContentValidator.ValidateEffectSpec()
→ 成功：UseLocalEffectExecution = true
→ 失败：UseLocalEffectExecution = false，继续走旧AI_CardEffect
```

### 5.4 这个阶段只支持最小Action集合

第一版EffectSpec只支持：

```text
DealDamage
Heal
GainBlock
DrawCards
GainMana
ApplyStatus
ExhaustThisCard
```

不要一开始就支持复杂被动和触发器，也不要把 LifeSteal / Stun / IncreaseMaxHealth 作为第一版原子动作。需要吸血时使用 `DealDamage + Heal` 组合，需要眩晕时使用 `ApplyStatus(Stun)` 并等 StatusDefinition 稳定后再本地执行。

---

## 6. 阶段2：本地执行主动卡牌效果

### 6.1 目标

把“玩家出牌”这条链路先从运行时LLM中剥离出来。

当前链路：

```text
CardBase.Use()
→ AI_CardEffect.CardUse()
→ 请求LLM
→ SuperActions
→ CardActionData
→ CardActionProcessor
```

新链路：

```text
CardBase.Use()
→ RuntimeEffectExecutor.Execute(card.effectSpec)
→ GameCommand / CardActionData
→ CardActionProcessor
```

### 6.2 保留旧入口

`CardBase.Use()` 中可以这样分支：

```csharp
if (cardData.UseLocalEffectExecution)
{
    RuntimeEffectExecutor.ExecuteCardEffect(cardData.RuntimeEffectSpec, context);
}
else
{
    AI_CardEffect.CardUse(...);
}
```

这样不影响旧卡。

### 6.3 RuntimeContext

本地执行器需要一个上下文对象：

```csharp
public class EffectExecutionContext
{
    public CharacterBase Source;
    public CharacterBase SelectedTarget;
    public CardBase SourceCard;
    public CombatManager Combat;
    public CollectionManager Collection;
    public DeterministicRng Rng;
}
```

所有Action只能通过Context操作游戏状态，不能直接散落调用Manager。

### 6.4 第一版Action到现有CardActionData的映射

```text
DealDamage     → CardActionType.Attack
Heal           → CardActionType.Heal
GainBlock      → CardActionType.Block
DrawCards      → CardActionType.Draw
GainMana       → CardActionType.EarnMana
ApplyStatus    → IncreaseStrength / Stun / Poison / CustomEffect
ExhaustThis    → Exhaust
```

这个阶段的目标不是设计完美系统，而是让已有 `CardActionProcessor` 继续发挥作用。这里的 `Stun` 只应被视为 `ApplyStatus` 的一种参数值，而不是独立顶层 Action；`LifeSteal` 不进入映射表，应拆成伤害和治疗两个动作。

---

## 7. 阶段3：自定义状态升级为可执行StatusDefinition

### 7.1 当前问题

现在 `Add Custom Status` 主要是：

```text
buffname + value
```

它可以显示和堆叠，但没有稳定的本地规则解释。状态真正产生什么效果，仍然依赖后续LLM理解它。

这会限制很多典型卡牌效果，例如：

```text
你的每个回合开始时，多抽一张牌
每有一层Poison，回合开始时受到1点伤害并减少1层
每当你受到火焰伤害，加入一张烧伤
本局游戏中，每使用一张攻击牌，狂热+1
```

### 7.2 新增StatusDefinition

```json
{
  "id": "status.poison",
  "name": "Poison",
  "stacking": "IntStack",
  "maxStack": 999,
  "isDebuff": true,
  "rules": [
    {
      "trigger": {
        "event": "TurnStarted",
        "owner": "Holder"
      },
      "condition": {
        "gt": [
          { "status": ["Holder", "Poison"] },
          { "const": 0 }
        ]
      },
      "actions": [
        {
          "op": "DealDamage",
          "source": "ThisStatus",
          "target": "Holder",
          "damageType": "Poison",
          "amount": { "status": ["Holder", "Poison"] },
          "bypassBlock": true
        },
        {
          "op": "AddStatus",
          "target": "Holder",
          "status": "Poison",
          "amount": { "const": -1 }
        }
      ]
    }
  ]
}
```

### 7.3 CharacterStats改造

当前 `CharacterStats` 已经有基础状态和自定义状态。下一步应把它统一成：

```csharp
public class RuntimeStatusInstance
{
    public string StatusId;
    public int Stack;
    public int Duration;
    public StatusDefinition Definition;
    public CharacterBase Holder;
}
```

基础状态也逐步迁移到StatusDefinition：

```text
Poison
Strength
Dexterity
Stun
Block
```

但迁移顺序可以是：

```text
先迁移Custom Status
再迁移Poison/Stun
最后考虑Block/Strength/Dexterity
```

因为Block和Strength已经深度接入现有战斗计算，先不急着动。

---

## 8. 阶段4：事件系统 CombatEventBus

### 8.1 目标

让所有被动、状态、遗物、怪物阶段技能都通过统一事件触发。

新增事件：

```text
CombatStarted
TurnStarted
TurnEnded
CardPlayed
CardResolved
BeforeDealDamage
AfterDamageDealt
BeforeTakeDamage
AfterDamageTaken
StatusApplied
StatusStackChanged
EntityKilled
CardDrawn
CardDiscarded
CardExhausted
```

### 8.2 事件结构

```csharp
public class CombatEvent
{
    public CombatEventType Type;
    public CharacterBase Actor;
    public CharacterBase Target;
    public CardBase Card;
    public DamagePacket Damage;
    public Dictionary<string, object> Extra;
}
```

### 8.3 RuleSpec

卡牌、状态、遗物、角色被动都可以携带RuleSpec：

```json
{
  "trigger": {
    "event": "TurnStarted",
    "owner": "Self"
  },
  "condition": {
    "always": true
  },
  "actions": [
    {
      "op": "DrawCards",
      "target": "Self",
      "amount": { "const": 1 }
    }
  ]
}
```

这样，“每回合开始多抽一张牌”不再是AI解释出来的临时效果，而是本地规则。

---

## 9. 阶段5：Modifier Pipeline

### 9.1 为什么需要Modifier

简单Action只能表达：

```text
造成6点伤害
抽1张牌
获得3点格挡
施加2层Poison
```

但很多卡牌游戏效果是“修改正在发生的事”：

```text
攻击牌伤害+狂热层数
第一次受到伤害时减半
火焰伤害改为治疗
手牌中每有一张技能牌，本牌费用-1
每当你抽到状态牌，将其消耗
```

这类效果不能只靠普通Action，需要 `BeforeX` 时机和Modifier。

### 9.2 DamagePacket

新增：

```csharp
public class DamagePacket
{
    public CharacterBase Source;
    public CharacterBase Target;
    public CardBase SourceCard;
    public DamageType DamageType;
    public int BaseAmount;
    public int AdditiveBonus;
    public float Multiplier;
    public bool BypassBlock;
    public bool Cancelled;

    public int FinalAmount;
}
```

### 9.3 Modifier规则示例

“本局游戏中，你每使用一张攻击牌，使造成的伤害增加等于狂热层数，再让狂热+1”：

```json
{
  "id": "status.fervor",
  "rules": [
    {
      "trigger": {
        "event": "BeforeDealDamage",
        "owner": "Self"
      },
      "condition": {
        "all": [
          { "eventSourceIs": "Self" },
          { "eventCardHasTag": "Attack" }
        ]
      },
      "actions": [
        {
          "op": "ModifyDamage",
          "mode": "Add",
          "amount": { "status": ["Self", "Fervor"] }
        }
      ]
    },
    {
      "trigger": {
        "event": "CardResolved",
        "owner": "Self"
      },
      "condition": {
        "eventCardHasTag": "Attack"
      },
      "actions": [
        {
          "op": "AddStatus",
          "target": "Self",
          "status": "Fervor",
          "amount": { "const": 1 }
        }
      ]
    }
  ]
}
```

### 9.4 优先级

所有Modifier需要优先级：

```text
Base
Additive
Multiplicative
Replacement
Prevention
Aftermath
```

建议伤害流程固定为：

```text
创建DamagePacket
→ BeforeDealDamage
→ 攻击者加法修正
→ 攻击者乘法修正
→ 防御者减免/替换
→ Block结算
→ HP变化
→ AfterDamageTaken
→ AfterDamageDealt
```

---

## 10. 阶段6：怪物意图DSL

### 10.1 当前问题

现在敌人行动也是实时请求LLM解释：

```text
EnemyBase.ActionRoutine()
→ 传入NextAbility.Name和Desc
→ AI_CardEffect.EnemyTurn()
→ LLM返回效果
→ 执行
```

这和玩家卡牌一样有不确定性。

### 10.2 新设计

怪物生成时就生成IntentSpec：

```json
{
  "monsterId": "monster.ash_witch",
  "name": "灰烬女巫",
  "hp": 42,
  "intents": [
    {
      "id": "firebolt",
      "name": "火焰箭",
      "weight": 5,
      "cooldown": 0,
      "actions": [
        {
          "op": "DealDamage",
          "target": "Player",
          "damageType": "Fire",
          "amount": { "const": 7 }
        }
      ]
    },
    {
      "id": "ignite",
      "name": "点燃",
      "weight": 3,
      "condition": {
        "lt": [
          { "status": ["Player", "Burning"] },
          { "const": 3 }
        ]
      },
      "actions": [
        {
          "op": "AddStatus",
          "target": "Player",
          "status": "Burning",
          "amount": { "const": 2 }
        }
      ]
    }
  ]
}
```

敌人每回合只需要本地选择一个合法Intent，然后执行其actions。

### 10.3 好处

```text
敌人意图可以提前显示
Reset后敌人行为稳定
可以进行自动战斗模拟
可以给怪物设计阶段变化
可以让AI生成怪物但不参与战斗执行
```

---

## 11. 阶段7：角色主动、被动与遗物系统

当卡牌和状态规则稳定后，可以扩展到角色技能与物品。

### 11.1 角色主动技能

角色主动技能可以直接复用Card EffectSpec：

```json
{
  "id": "hero_skill.shadow_guard",
  "name": "影之护壁",
  "cooldown": 3,
  "cost": {
    "mana": 1
  },
  "actions": [
    {
      "op": "GainBlock",
      "target": "Self",
      "amount": { "const": 8 }
    },
    {
      "op": "DrawCards",
      "target": "Self",
      "amount": { "const": 1 }
    }
  ]
}
```

### 11.2 角色被动

角色被动就是常驻RuleSpec：

```json
{
  "id": "hero_passive.pyromancer",
  "name": "余烬体质",
  "rules": [
    {
      "trigger": {
        "event": "AfterDamageTaken",
        "owner": "Self"
      },
      "condition": {
        "eq": [
          { "event": "damageType" },
          { "const": "Fire" }
        ]
      },
      "actions": [
        {
          "op": "CreateCard",
          "card": "card.burn",
          "zone": "Hand",
          "owner": "Self",
          "amount": { "const": 1 }
        }
      ]
    }
  ]
}
```

### 11.3 遗物 / 物品

遗物也使用RuleSpec：

```json
{
  "id": "relic.cracked_lens",
  "name": "裂纹透镜",
  "rules": [
    {
      "trigger": {
        "event": "CombatStarted",
        "owner": "Self"
      },
      "actions": [
        {
          "op": "DrawCards",
          "target": "Self",
          "amount": { "const": 1 }
        }
      ]
    }
  ]
}
```

这样你的系统会变成统一规则框架，而不是卡牌、状态、遗物、怪物分别写一套逻辑。

---

## 12. 阶段8：自动化QA与测试执行系统

### 12.1 内容进入战斗前必须经过测试

生成内容管线：

```text
LLM生成内容
→ JSON解析
→ Schema校验
→ 引用校验
→ 语义校验
→ 编译成RuntimeEffect
→ 自动单元测试
→ 随机战斗模拟
→ 通过后加入GeneratedContentDatabase
```

### 12.2 Schema校验

检查字段结构：

```text
是否有id
是否有name
manaCost是否为整数
target是否合法
op是否在白名单中
amount是否是合法表达式
trigger事件是否合法
damageType是否合法
statusId是否合法
```

### 12.3 语义校验

检查逻辑风险：

```text
是否可能无限触发
是否创建过多卡牌
是否有负伤害
是否引用不存在的状态
是否目标选择器与卡牌needTarget冲突
是否在错误时机访问event.damage
是否在非伤害事件里使用ModifyDamage
```

### 12.4 自动测试

每个生成内容可以让LLM同时生成测试用例，但测试执行必须由Unity完成。

示例：

```json
{
  "name": "Poison deals damage and decays",
  "initialState": {
    "player": {
      "hp": 30,
      "statuses": {
        "Poison": 5
      }
    }
  },
  "event": {
    "type": "TurnStarted",
    "owner": "Player"
  },
  "expect": {
    "player": {
      "hp": 25,
      "statuses": {
        "Poison": 4
      }
    }
  }
}
```

### 12.5 战斗模拟

对于每张新卡，进行快速模拟：

```text
单卡使用测试
随机目标测试
空手牌测试
敌人死亡测试
ResetTurn测试
连续10回合测试
100场随机战斗平衡测试
```

输出评分：

```text
平均伤害
平均抽牌
平均能量收益
平均战斗回合数
异常次数
无限事件链次数
```

---

## 13. Reset系统需要同步升级

当前项目已有ResetTurn/ResetLevel，这是很大的优势。新系统加入后，需要把以下内容纳入快照：

```text
RuntimeStatusInstance
GeneratedContentDatabase引用状态
CombatEventBus队列
Modifier临时状态
当前DamagePacket处理阶段
怪物Intent选择历史
本回合已触发过的一次性规则
确定性RNG状态
创建出的临时卡牌实例
```

建议新增接口：

```csharp
public interface ICombatSnapshotParticipant
{
    object CaptureSnapshot();
    void RestoreSnapshot(object snapshot);
}
```

让每个新增系统自己负责快照，而不是把所有字段都塞进 `TurnResetController`。

---

## 14. LLM在新架构中的职责变化

当前LLM职责偏重：

```text
生成创意
解释卡牌效果
根据战斗上下文计算结果
纠正JSON
```

未来LLM职责应该变成：

```text
生成角色主题
生成卡牌文本
生成图片prompt
生成结构化EffectSpec
生成StatusDefinition
生成MonsterIntentSpec
生成测试用例草案
解释错误并尝试修复内容
```

LLM不再负责：

```text
战斗中临时判断卡牌效果
计算伤害结果
决定目标是否合法
在Reset后重新生成效果
直接修改游戏状态
```

---

## 15. 目录结构建议

建议新增：

```text
Assets/Scripts/GeneratedContent/
  GeneratedContentDatabase.cs
  GeneratedCardDefinition.cs
  GeneratedStatusDefinition.cs
  GeneratedMonsterDefinition.cs
  GeneratedRelicDefinition.cs

Assets/Scripts/Rules/
  RuleSpec.cs
  EffectSpec.cs
  ActionSpec.cs
  ConditionSpec.cs
  FormulaSpec.cs
  TargetSelectorSpec.cs
  RuntimeRuleExecutor.cs
  RuntimeEffectExecutor.cs
  AI_EffectCompiler.cs
  AI_ContentValidator.cs

Assets/Scripts/CombatEvents/
  CombatEventBus.cs
  CombatEvent.cs
  CombatEventType.cs
  DamagePacket.cs
  ModifierPipeline.cs

Assets/Scripts/Testing/
  GeneratedContentTestRunner.cs
  CombatSimulationRunner.cs
  RuleTestCase.cs
```

原有：

```text
Assets/Scripts/Integration/AI_CardEffect.cs
```

逐步变成：

```text
Legacy AI runtime effect interpreter
```

而不是继续承担核心战斗逻辑。

---

## 16. 具体里程碑

### Milestone 1：EffectSpec v0

目标：卡牌生成时保存固定效果。

完成标准：

```text
AI_DeckGenerator能生成effectSpec
CardData能保存effectSpec
本地能执行DealDamage / Heal / GainBlock / DrawCards
旧AI_CardEffect仍可fallback
```

风险：

```text
LLM生成effectSpec字段不稳定
描述文本与实际效果不一致
```

应对：

```text
Prompt中强制要求description与effectSpec一致
Validator检查description中的关键词与效果大致匹配
```

---

### Milestone 2：本地卡牌执行

目标：普通主动卡不再请求LLM。

完成标准：

```text
至少80%的生成卡牌使用本地EffectExecutor
战斗中出牌不出现AI等待Canvas
ResetTurn后卡牌效果完全一致
```

保留：

```text
复杂卡牌仍走Legacy AI
Debug模式可对比本地执行与AI解释结果
```

---

### Milestone 3：StatusDefinition

目标：自定义状态拥有本地规则。

完成标准：

```text
Poison类状态可用本地规则实现
回合开始触发状态效果
状态可以衰减
状态可以监听受伤/出牌事件
```

示例必须支持：

```text
每回合开始多抽一张牌
每层Poison造成1点伤害并减少1层
受到火焰伤害时加入烧伤
```

---

### Milestone 4：CombatEventBus

目标：统一触发时机。

完成标准：

```text
TurnStarted / TurnEnded / CardPlayed / CardResolved 可触发规则
AfterDamageTaken 可触发规则
规则触发顺序稳定
事件链有最大预算
```

需要增加：

```text
事件日志面板
事件链调试器
规则触发记录
```

---

### Milestone 5：ModifierPipeline

目标：支持真正的卡牌联动。

完成标准：

```text
BeforeDealDamage可修改伤害
攻击牌伤害可被状态层数影响
费用、抽牌、受到伤害可以被规则修改
```

示例必须支持：

```text
每使用一张攻击牌，伤害增加等于狂热层数，然后狂热+1
第一次受到伤害时减少50%
火焰伤害额外施加Burning
```

---

### Milestone 6：MonsterIntentSpec

目标：敌人行动不再实时请求LLM。

完成标准：

```text
怪物生成时带有本地Intent列表
敌人回合不请求LLM
意图可提前显示
Reset后敌人行动一致
```

---

### Milestone 7：自动QA

目标：生成内容入库前自动验证。

完成标准：

```text
Schema校验
语义校验
单元测试
10回合随机模拟
异常内容自动退回给LLM修复
```

---

## 17. 推荐的短期优先级

如果只安排最近两到三周，我建议优先做：

```text
0. 收敛 NueDeck 基础 Action 集，停用 LifeSteal / Stun / IncreaseMaxHealth 这类非核心原子动作
1. EffectSpec v0 数据结构
2. CardData 保存 GeneratedEffectJson
3. RuntimeEffectExecutor 执行简单Action
4. AI_CardEffect fallback
5. GeneratedContentValidator 基础校验
6. 出牌效果本地化
```

暂时不要先做：

```text
完整Lua系统
复杂公式语言
完整事件总线
完整ModifierPipeline
大规模怪物AI生成
复杂遗物系统
```

原因是当前项目最大痛点是“同一张卡战斗中反复请求AI”。只要先把主动卡牌效果固定下来，项目体验就会明显稳定。

---

## 18. 最终目标形态

最终项目的运行方式应该是：

```text
战斗外：
LLM生成角色、卡牌、状态、怪物、遗物、图像prompt、测试用例

进入战斗前：
Unity校验并编译生成内容

战斗中：
Unity本地执行所有规则
事件系统触发被动与状态
ModifierPipeline处理复杂联动
ResetTurn可以完整恢复
Debug系统显示规则触发链

战斗后：
LLM继续生成奖励、升级、后续敌人和新内容
```

最终架构可以概括为：

```text
NueDeck稳定战斗框架
+ AI生成内容层
+ 结构化规则DSL
+ 本地确定性解释器
+ 自动QA与模拟测试
```

这条路线的关键不是把AI从游戏里拿掉，而是把AI从“实时裁判”改造成“内容设计师”。
AI负责创造内容，Unity负责执行规则。
