using System;
using Newtonsoft.Json;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NueGames.NueDeck.Scripts.Card;
using NueGames.NueDeck.Scripts.Characters;
using NueGames.NueDeck.Scripts.Data.Collection;
using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;

namespace AIDeckbuilder.CardRuntime
{
    public enum CardBattleEventType
    {
        TurnStart,
        TurnEnd,
        BeforeCardPlayed,
        AfterCardPlayed,
        BeforeEnemyAction,
        BeforeDamage,
        AfterDamage,
        StatusApplied,
        StatusRemoved
    }

    public sealed class CardBattleRuntimeSnapshot
    {
        public int CardsPlayedThisTurn;
        public string[] LastPlayedCardTags;
    }

    
public sealed class CardBattleEventContext
    {
        public CardBattleEventType Type;
        public CharacterBase Source;
        public CharacterBase Target;
        public CharacterBase ActiveCharacter;
        public CardBase Card;
        public int Amount;
        public int ResolvedEffects;
        public bool Cancelled;
        public string StatusId;
    }

    public sealed class CardEffectContext
    {
        public CharacterBase Source;
        public CharacterBase SelectedTarget;
        public CharacterBase StatusOwner;
        public CardBase Card;
        public CardData CardData;
        public CardBattleEventContext Event;
        public RuntimeCardStatusInstance TriggeringStatus;
        public List<EnemyBase> Enemies;
        public List<AllyBase> Allies;
    }

    public sealed class RuntimeCardStatusInstance
    {
        public CardStatusDefinitionData Definition;
        public CharacterBase Owner;
        public CharacterBase Source;
        public CardData SourceCard;
        public int Stacks;
        public int RemainingTurns;
        public readonly Dictionary<int, int> TriggerCounts = new();
        public readonly HashSet<int> ActiveTriggers = new();
    }

    public sealed class RuntimeCardStatusSnapshot
    {
        public CardStatusDefinitionData Definition;
        public int Stacks;
        public int RemainingTurns;
        public Dictionary<int, int> TriggerCounts;
    }

    public static class CardBattleEventBus
    {
        private const int MaxEventDepth = 16;
        private const int MaxEventsPerRoot = 64;
        private static int eventDepth;
        
        public static bool SuppressStatusEvents { get; set; }
private static int eventsInRoot;

        public static int CardsPlayedThisTurn { get; private set; }
        public static IReadOnlyList<string> LastPlayedCardTags { get; private set; } = Array.Empty<string>();

public static CardBattleEventContext Publish(CardBattleEventContext context)
        {
            
            if (SuppressStatusEvents
                && (context.Type == CardBattleEventType.StatusApplied
                    || context.Type == CardBattleEventType.StatusRemoved))
                return context;
if (context == null)
                return null;

            bool root = eventDepth == 0;
            if (root)
                eventsInRoot = 0;

            if (eventDepth >= MaxEventDepth || eventsInRoot >= MaxEventsPerRoot)
            {
                Debug.LogWarning("Card event budget exceeded; event ignored.");
                return context;
            }

            eventDepth++;
            eventsInRoot++;
            CardRuntimeDiagnostics.LogEvent("BEGIN-IMMEDIATE", context);
            try
            {
                PrepareEvent(context);
                CardStatusRuntime.ProcessEvent(context);
                CompleteEvent(context);
            }
            finally
            {
                CardRuntimeDiagnostics.LogEvent("END-IMMEDIATE", context);
                eventDepth--;
            }

            return context;
        }

public static IEnumerator PublishRoutine(CardBattleEventContext context)
        {
            if (context == null)
                yield break;

            bool root = eventDepth == 0;
            if (root)
                eventsInRoot = 0;

            if (eventDepth >= MaxEventDepth || eventsInRoot >= MaxEventsPerRoot)
            {
                Debug.LogWarning("Card event budget exceeded; event ignored.");
                yield break;
            }

            eventDepth++;
            eventsInRoot++;
            CardRuntimeDiagnostics.LogEvent("BEGIN", context);
            try
            {
                PrepareEvent(context);
                yield return CardStatusRuntime.ProcessEventRoutine(context);
                CompleteEvent(context);
            }
            finally
            {
                CardRuntimeDiagnostics.LogEvent("END", context);
                eventDepth--;
            }
        }


        public static void ResetBattleState()
        {
            CardsPlayedThisTurn = 0;
            LastPlayedCardTags = Array.Empty<string>();
            eventDepth = 0;
            eventsInRoot = 0;
            CardStatusRuntime.ClearInstances();
        }
    

public static void RestoreState(CardBattleRuntimeSnapshot snapshot)
        {
            CardsPlayedThisTurn = snapshot?.CardsPlayedThisTurn ?? 0;
            LastPlayedCardTags = snapshot?.LastPlayedCardTags?.ToArray() ?? Array.Empty<string>();
            eventDepth = 0;
            eventsInRoot = 0;
        }


public static CardBattleRuntimeSnapshot CaptureState()
        {
            return new CardBattleRuntimeSnapshot
            {
                CardsPlayedThisTurn = CardsPlayedThisTurn,
                LastPlayedCardTags = LastPlayedCardTags?.ToArray() ?? Array.Empty<string>()
            };
        }


private static void CompleteEvent(CardBattleEventContext context)
        {
            if (context.Type == CardBattleEventType.AfterCardPlayed)
            {
                CardsPlayedThisTurn++;
                LastPlayedCardTags = context.Card != null
                                     && context.Card.CardData.CardProgram?.tags != null
                    ? context.Card.CardData.CardProgram.tags.ToArray()
                    : Array.Empty<string>();
            }

            if (context.Type == CardBattleEventType.TurnEnd)
                CardStatusRuntime.TickDurations(context.ActiveCharacter);
        }


private static void PrepareEvent(CardBattleEventContext context)
        {
            if (context.Type != CardBattleEventType.TurnStart)
                return;

            CardsPlayedThisTurn = 0;
            LastPlayedCardTags = Array.Empty<string>();
            CardStatusRuntime.ResetTriggerCounts();
        }
}

    public static class CardStatusRuntime
    {
        private static readonly Dictionary<string, CardStatusDefinitionData> Definitions =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<RuntimeCardStatusInstance> Instances = new();

        public static void RegisterProgram(CardProgramData program)
        {
            if (program?.statusDefinitions == null)
                return;

            foreach (var definition in program.statusDefinitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                    continue;

                string id = CardEffectCatalog.Normalize(definition.id);
                definition.id = id;
                definition.stackKey = CardEffectCatalog.Normalize(
                    string.IsNullOrWhiteSpace(definition.stackKey) ? id : definition.stackKey);

                if (!Definitions.ContainsKey(id))
                    Definitions.Add(id, definition);
            }
        }

        public static int GetStacks(CharacterBase owner, string statusId)
        {
            if (!owner || string.IsNullOrWhiteSpace(statusId))
                return 0;

            string id = CardEffectCatalog.Normalize(statusId);
            if (TryGetStandardStatus(id, out var standard))
                return owner.characterStats.StatusDict[standard].StatusValue;

            PruneDestroyedOwners();
            return Instances.Where(instance => instance.Owner == owner
                                               && string.Equals(instance.Definition.id, id,
                                                   StringComparison.OrdinalIgnoreCase))
                .Sum(instance => instance.Stacks);
        }

        public static bool HasStatus(CharacterBase owner, string statusId)
        {
            return GetStacks(owner, statusId) > 0;
        }

public static void Apply(CharacterBase owner, string statusId, int stacks, int duration,
            CharacterBase source, CardData sourceCard)
        {
            if (!owner || stacks == 0)
                return;

            string id = CardEffectCatalog.Normalize(statusId);
            int beforeStacks = GetStacks(owner, id);
            if (TryGetStandardStatus(id, out var standard))
            {
                owner.characterStats.ApplyStatus(standard, stacks);
                ShowStatusFeedback(owner, id, GetStacks(owner, id) - beforeStacks, false);
                CardBattleEventBus.Publish(new CardBattleEventContext
                {
                    Type = CardBattleEventType.StatusApplied,
                    Source = source,
                    Target = owner,
                    StatusId = id,
                    Amount = stacks
                });
                return;
            }

            if (!Definitions.TryGetValue(id, out var definition))
            {
                definition = sourceCard?.CardProgram?.FindStatus(id);
                if (definition != null)
                    Definitions[id] = definition;
            }

            if (definition == null)
            {
                Debug.LogWarning("No generated definition exists for status '" + id + "'.");
                return;
            }

            PruneDestroyedOwners();
            string stackKey = string.IsNullOrWhiteSpace(definition.stackKey) ? definition.id : definition.stackKey;
            var existing = Instances.FirstOrDefault(instance => instance.Owner == owner
                && string.Equals(instance.Definition.stackKey, stackKey, StringComparison.OrdinalIgnoreCase));

            int resolvedDuration = duration > 0 ? duration : definition.duration;
            if (existing == null)
            {
                existing = new RuntimeCardStatusInstance
                {
                    Definition = definition,
                    Owner = owner,
                    Source = source,
                    SourceCard = sourceCard,
                    Stacks = Mathf.Clamp(stacks, 0, definition.maxStacks),
                    RemainingTurns = resolvedDuration
                };
                if (existing.Stacks > 0)
                    Instances.Add(existing);
            }
            else
            {
                switch (CardEffectCatalog.Normalize(definition.stackMode))
                {
                    case "replace_newer":
                        existing.Stacks = Mathf.Clamp(stacks, 0, definition.maxStacks);
                        existing.RemainingTurns = resolvedDuration;
                        existing.Source = source;
                        existing.SourceCard = sourceCard;
                        break;
                    case "replace_stronger":
                        if (stacks > existing.Stacks)
                        {
                            existing.Stacks = Mathf.Clamp(stacks, 0, definition.maxStacks);
                            existing.RemainingTurns = resolvedDuration;
                            existing.Source = source;
                            existing.SourceCard = sourceCard;
                        }
                        break;
                    case "refresh":
                        existing.Stacks = Mathf.Clamp(existing.Stacks + stacks, 0, definition.maxStacks);
                        existing.RemainingTurns = resolvedDuration;
                        break;
                    default:
                        existing.Stacks = Mathf.Clamp(existing.Stacks + stacks, 0, definition.maxStacks);
                        break;
                }
            }

            SyncLegacyDisplay(owner, id);
            ShowStatusFeedback(owner, id, GetStacks(owner, id) - beforeStacks, false);
            CardBattleEventBus.Publish(new CardBattleEventContext
            {
                Type = CardBattleEventType.StatusApplied,
                Source = source,
                Target = owner,
                StatusId = id,
                Amount = stacks
            });
        }

public static void Remove(CharacterBase owner, string statusId, int stacks,
            CharacterBase source = null)
        {
            if (!owner)
                return;

            string id = CardEffectCatalog.Normalize(statusId);
            int beforeStacks = GetStacks(owner, id);
            if (TryGetStandardStatus(id, out var standard))
            {
                if (stacks <= 0 || stacks >= owner.characterStats.StatusDict[standard].StatusValue)
                    owner.characterStats.ClearStatus(standard);
                else
                    owner.characterStats.ApplyStatus(standard, -stacks);

                ShowStatusFeedback(owner, id, beforeStacks - GetStacks(owner, id), true);
                CardBattleEventBus.Publish(new CardBattleEventContext
                {
                    Type = CardBattleEventType.StatusRemoved,
                    Source = source,
                    Target = owner,
                    StatusId = id,
                    Amount = stacks
                });
                return;
            }

            PruneDestroyedOwners();
            var matching = Instances.Where(instance => instance.Owner == owner
                && string.Equals(instance.Definition.id, id, StringComparison.OrdinalIgnoreCase)).ToList();
            int remainingRemoval = stacks;
            foreach (var instance in matching)
            {
                if (stacks <= 0 || remainingRemoval >= instance.Stacks)
                {
                    remainingRemoval -= instance.Stacks;
                    Instances.Remove(instance);
                }
                else
                {
                    instance.Stacks -= remainingRemoval;
                    remainingRemoval = 0;
                }

                if (remainingRemoval <= 0 && stacks > 0)
                    break;
            }

            SyncLegacyDisplay(owner, id);
            ShowStatusFeedback(owner, id, beforeStacks - GetStacks(owner, id), true);
            CardBattleEventBus.Publish(new CardBattleEventContext
            {
                Type = CardBattleEventType.StatusRemoved,
                Source = source,
                Target = owner,
                StatusId = id,
                Amount = stacks
            });
        }

        public static void ProcessEvent(CardBattleEventContext eventContext)
        {
            PruneDestroyedOwners();
            var work = new List<(RuntimeCardStatusInstance instance, CardTriggerData trigger, int index)>();
            foreach (var instance in Instances.ToArray())
            {
                if (instance.Definition?.triggers == null)
                    continue;

                for (int i = 0; i < instance.Definition.triggers.Count; i++)
                {
                    var trigger = instance.Definition.triggers[i];
                    if (trigger != null && EventMatches(trigger.eventType, eventContext.Type)
                                        && RoleMatches(trigger.eventRole, instance.Owner, eventContext))
                        work.Add((instance, trigger, i));
                }
            }

            foreach (var item in work.OrderByDescending(entry => entry.trigger.priority))
            {
                if (!Instances.Contains(item.instance) || !item.instance.Owner)
                    continue;

                if (item.instance.ActiveTriggers.Contains(item.index))
                    continue;

                item.instance.TriggerCounts.TryGetValue(item.index, out int used);
                if (item.trigger.limitPerTurn > 0 && used >= item.trigger.limitPerTurn)
                    continue;

                var effectContext = new CardEffectContext
                {
                    Source = item.instance.Source ? item.instance.Source : item.instance.Owner,
                    StatusOwner = item.instance.Owner,
                    CardData = item.instance.SourceCard,
                    Event = eventContext,
                    TriggeringStatus = item.instance,
                    Enemies = CombatManager.Instance != null
                        ? CombatManager.Instance.CurrentEnemiesList : new List<EnemyBase>(),
                    Allies = CombatManager.Instance != null
                        ? CombatManager.Instance.CurrentAlliesList : new List<AllyBase>()
                };

                if (!CardProgramExecutor.EvaluateCondition(item.trigger.condition, effectContext,
                        item.instance.Owner))
                    continue;

                item.instance.TriggerCounts[item.index] = used + 1;
                eventContext.ResolvedEffects += item.trigger.effects?.Count ?? 0;
                item.instance.ActiveTriggers.Add(item.index);
                try
                {
                    CardProgramExecutor.ExecuteEffectsImmediate(item.trigger.effects, effectContext);
                }
                finally
                {
                    item.instance.ActiveTriggers.Remove(item.index);
                }
            }
        }

public static IEnumerator ProcessEventRoutine(CardBattleEventContext eventContext)
        {
            PruneDestroyedOwners();
            bool executedTrigger = false;
            var work = new List<(RuntimeCardStatusInstance instance, CardTriggerData trigger, int index)>();
            foreach (var instance in Instances.ToArray())
            {
                if (instance.Definition?.triggers == null)
                    continue;

                for (int i = 0; i < instance.Definition.triggers.Count; i++)
                {
                    var trigger = instance.Definition.triggers[i];
                    if (trigger != null && EventMatches(trigger.eventType, eventContext.Type)
                                        && RoleMatches(trigger.eventRole, instance.Owner, eventContext))
                        work.Add((instance, trigger, i));
                }
            }

            foreach (var item in work.OrderByDescending(entry => entry.trigger.priority))
            {
                if (!Instances.Contains(item.instance) || !item.instance.Owner)
                    continue;

                if (item.instance.ActiveTriggers.Contains(item.index))
                    continue;

                item.instance.TriggerCounts.TryGetValue(item.index, out int used);
                if (item.trigger.limitPerTurn > 0 && used >= item.trigger.limitPerTurn)
                    continue;

                var effectContext = new CardEffectContext
                {
                    Source = item.instance.Source ? item.instance.Source : item.instance.Owner,
                    StatusOwner = item.instance.Owner,
                    CardData = item.instance.SourceCard,
                    Event = eventContext,
                    TriggeringStatus = item.instance,
                    Enemies = CombatManager.Instance != null
                        ? CombatManager.Instance.CurrentEnemiesList : new List<EnemyBase>(),
                    Allies = CombatManager.Instance != null
                        ? CombatManager.Instance.CurrentAlliesList : new List<AllyBase>()
                };

                if (!CardProgramExecutor.EvaluateCondition(item.trigger.condition, effectContext,
                        item.instance.Owner))
                    continue;

                if (executedTrigger)
                    yield return new WaitForSeconds(CardProgramExecutor.EffectDelaySeconds);

                item.instance.TriggerCounts[item.index] = used + 1;
                eventContext.ResolvedEffects += item.trigger.effects?.Count ?? 0;
                item.instance.ActiveTriggers.Add(item.index);
                try
                {
                    yield return CardProgramExecutor.ExecuteEffectsRoutine(
                        item.trigger.effects, effectContext,
                        "status:" + item.instance.Definition.id + "/event:" + eventContext.Type);
                }
                finally
                {
                    item.instance.ActiveTriggers.Remove(item.index);
                }
                executedTrigger = true;
            }
        }


        public static List<RuntimeCardStatusSnapshot> Capture(CharacterBase owner)
        {
            PruneDestroyedOwners();
            return Instances.Where(instance => instance.Owner == owner)
                .Select(instance => new RuntimeCardStatusSnapshot
                {
                    Definition = instance.Definition,
                    Stacks = instance.Stacks,
                    RemainingTurns = instance.RemainingTurns,
                    TriggerCounts = new Dictionary<int, int>(instance.TriggerCounts)
                }).ToList();
        }

        public static void Restore(CharacterBase owner, IEnumerable<RuntimeCardStatusSnapshot> snapshots)
        {
            RemoveOwner(owner);
            if (!owner || snapshots == null)
                return;

            foreach (var snapshot in snapshots)
            {
                if (snapshot?.Definition == null)
                    continue;

                Definitions[snapshot.Definition.id] = snapshot.Definition;
                var instance = new RuntimeCardStatusInstance
                {
                    Definition = snapshot.Definition,
                    Owner = owner,
                    Source = owner,
                    Stacks = snapshot.Stacks,
                    RemainingTurns = snapshot.RemainingTurns
                };
                if (snapshot.TriggerCounts != null)
                    foreach (var pair in snapshot.TriggerCounts)
                        instance.TriggerCounts[pair.Key] = pair.Value;
                Instances.Add(instance);
                SyncLegacyDisplay(owner, snapshot.Definition.id);
            }
        }

        public static void ResetTriggerCounts()
        {
            foreach (var instance in Instances)
                instance.TriggerCounts.Clear();
        }

        public static void TickDurations(CharacterBase activeCharacter)
        {
            if (!activeCharacter)
                return;

            foreach (var instance in Instances.Where(item => item.Owner == activeCharacter).ToArray())
            {
                if (instance.RemainingTurns <= 0)
                    continue;

                instance.RemainingTurns--;
                if (instance.RemainingTurns <= 0)
                    Remove(instance.Owner, instance.Definition.id, 0, instance.Source);
            }
        }

        public static void RemoveOwner(CharacterBase owner)
        {
            if (!owner)
                return;
            Instances.RemoveAll(instance => instance.Owner == owner);
        }

        public static void ClearInstances()
        {
            Instances.Clear();
        }

private static void SyncLegacyDisplay(CharacterBase owner, string statusId)
        {
            if (!owner)
                return;

            owner.characterStats.SetCustomEffect(statusId, GetStacks(owner, statusId),
                GetStatusDisplayName(statusId), GetStatusColor(statusId));
        }

private static string GetStatusDisplayName(string statusId)
        {
            string id = CardEffectCatalog.Normalize(statusId);
            if (Definitions.TryGetValue(id, out var definition)
                && !string.IsNullOrWhiteSpace(definition.displayName))
                return definition.displayName;
            return id.Replace("_", " ");
        }

        public static string GetStatusColor(string statusId)
        {
            string id = CardEffectCatalog.Normalize(statusId);
            if (Definitions.TryGetValue(id, out var definition)
                && !string.IsNullOrWhiteSpace(definition.color))
                return definition.color;
            return "#FFFFFF";
        }



private static void ShowStatusFeedback(CharacterBase owner, string statusId, int amount,
            bool removed)
        {
            var fxManager = FxManager.Instance;
            if (!owner || fxManager == null || amount <= 0)
                return;

            fxManager.PlayFx(owner.transform, FxType.Buff);
            if (!owner.TextSpawnRoot)
                return;

            string prefix = removed ? "-" : "+";
            string color = GetStatusColor(statusId);
            fxManager.SpawnFloatingText(owner.TextSpawnRoot,
                "<Color=" + color + ">" + prefix + amount + " "
                + GetStatusDisplayName(statusId) + "</color>");
        }


        private static void PruneDestroyedOwners()
        {
            Instances.RemoveAll(instance => !instance.Owner);
        }

        private static bool EventMatches(string eventType, CardBattleEventType type)
        {
            return CardEffectCatalog.Normalize(eventType) == EventToId(type);
        }

private static string EventToId(CardBattleEventType type)
        {
            switch (type)
            {
                case CardBattleEventType.TurnStart: return "turn_start";
                case CardBattleEventType.TurnEnd: return "turn_end";
                case CardBattleEventType.BeforeCardPlayed: return "before_card_played";
                case CardBattleEventType.AfterCardPlayed: return "after_card_played";
                case CardBattleEventType.BeforeEnemyAction: return "before_enemy_action";
                case CardBattleEventType.BeforeDamage: return "before_damage";
                case CardBattleEventType.AfterDamage: return "after_damage";
                case CardBattleEventType.StatusApplied: return "status_applied";
                case CardBattleEventType.StatusRemoved: return "status_removed";
                default: return string.Empty;
            }
        }

        private static bool RoleMatches(string role, CharacterBase owner, CardBattleEventContext context)
        {
            switch (CardEffectCatalog.Normalize(role))
            {
                case "owner_source": return context.Source == owner;
                case "owner_target": return context.Target == owner;
                case "any": return true;
                default: return context.ActiveCharacter == owner;
            }
        }

private static bool TryGetStandardStatus(string id, out StatusType status)
        {
            switch (id)
            {
                case "block": status = StatusType.Block; return true;
                case "dexterity": status = StatusType.Dexterity; return true;
                case "stun": status = StatusType.Stun; return true;
                default:
                    status = StatusType.None;
                    return false;
            }
        }
    

public static void ClearDefinitions()
        {
            Definitions.Clear();
        }


public static string BuildDefinitionReference()
        {
            if (Definitions.Count == 0)
                return "No reusable generated statuses exist yet.";

            var builder = new StringBuilder("Reusable status definitions from earlier cards:\n");
            foreach (var pair in Definitions.OrderBy(item => item.Key))
                builder.AppendLine(JsonConvert.SerializeObject(pair.Value, Formatting.None));
            builder.AppendLine("When referencing one of these status ids, do not redefine it differently.");
            return builder.ToString();
        }


public static bool ValidateProgramDefinitions(CardProgramData program, out string error)
        {
            error = null;
            if (program?.statusDefinitions == null)
                return true;

            foreach (var definition in program.statusDefinitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                    continue;

                string id = CardEffectCatalog.Normalize(definition.id);
                if (!Definitions.TryGetValue(id, out var existing))
                    continue;

                string existingJson = JsonConvert.SerializeObject(existing, Formatting.None);
                string incomingJson = JsonConvert.SerializeObject(definition, Formatting.None);
                if (!string.Equals(existingJson, incomingJson, StringComparison.Ordinal))
                {
                    error = "Status id '" + id + "' conflicts with the definition generated by an earlier card. "
                            + "Reuse the earlier definition exactly or choose a new status id.";
                    return false;
                }
            }

            return true;
        }


public static bool IsRuntimeStatus(CharacterBase owner, string statusId)
        {
            if (!owner || string.IsNullOrWhiteSpace(statusId))
                return false;
            string id = CardEffectCatalog.Normalize(statusId);
            PruneDestroyedOwners();
            return Instances.Any(instance => instance.Owner == owner
                                             && string.Equals(instance.Definition.id, id,
                                                 StringComparison.OrdinalIgnoreCase));
        }


public static void Set(CharacterBase owner, string statusId, int stacks, int duration,
            CharacterBase source, CardData sourceCard)
        {
            if (!owner)
                return;

            string id = CardEffectCatalog.Normalize(statusId);
            int beforeStacks = GetStacks(owner, id);
            int requestedStacks = Mathf.Max(0, stacks);

            if (TryGetStandardStatus(id, out var standard))
            {
                owner.characterStats.ClearStatus(standard);
                if (requestedStacks > 0)
                    owner.characterStats.ApplyStatus(standard, requestedStacks);

                int delta = GetStacks(owner, id) - beforeStacks;
                ShowStatusFeedback(owner, id, Mathf.Abs(delta), delta < 0);
                if (delta != 0)
                {
                    CardBattleEventBus.Publish(new CardBattleEventContext
                    {
                        Type = delta > 0 ? CardBattleEventType.StatusApplied : CardBattleEventType.StatusRemoved,
                        Source = source,
                        Target = owner,
                        StatusId = id,
                        Amount = Mathf.Abs(delta)
                    });
                }
                return;
            }

            if (!Definitions.TryGetValue(id, out var definition))
            {
                definition = sourceCard?.CardProgram?.FindStatus(id);
                if (definition != null)
                    Definitions[id] = definition;
            }

            if (definition == null)
            {
                Debug.LogWarning("No generated definition exists for status '" + id + "'.");
                return;
            }

            PruneDestroyedOwners();
            string stackKey = string.IsNullOrWhiteSpace(definition.stackKey)
                ? definition.id : definition.stackKey;
            var existing = Instances.FirstOrDefault(instance => instance.Owner == owner
                && string.Equals(instance.Definition.stackKey, stackKey, StringComparison.OrdinalIgnoreCase));
            int resolvedStacks = Mathf.Clamp(requestedStacks, 0, definition.maxStacks);

            if (resolvedStacks <= 0)
            {
                Instances.RemoveAll(instance => instance.Owner == owner
                    && string.Equals(instance.Definition.stackKey, stackKey,
                        StringComparison.OrdinalIgnoreCase));
            }
            else if (existing == null)
            {
                Instances.Add(new RuntimeCardStatusInstance
                {
                    Definition = definition,
                    Owner = owner,
                    Source = source,
                    SourceCard = sourceCard,
                    Stacks = resolvedStacks,
                    RemainingTurns = duration > 0 ? duration : definition.duration
                });
            }
            else
            {
                existing.Definition = definition;
                existing.Source = source;
                existing.SourceCard = sourceCard;
                existing.Stacks = resolvedStacks;
                if (duration > 0)
                    existing.RemainingTurns = duration;
            }

            SyncLegacyDisplay(owner, id);
            int resolvedDelta = GetStacks(owner, id) - beforeStacks;
            ShowStatusFeedback(owner, id, Mathf.Abs(resolvedDelta), resolvedDelta < 0);
            if (resolvedDelta != 0)
            {
                CardBattleEventBus.Publish(new CardBattleEventContext
                {
                    Type = resolvedDelta > 0
                        ? CardBattleEventType.StatusApplied : CardBattleEventType.StatusRemoved,
                    Source = source,
                    Target = owner,
                    StatusId = id,
                    Amount = Mathf.Abs(resolvedDelta)
                });
            }
        }
}

    public static class CardProgramExecutor
    {
        public const float EffectDelaySeconds = 0.75f;
        public const float CharacterPreActionDelaySeconds = 1f;
public static IEnumerator ExecuteOnPlay(CardBase card, CharacterBase source,
            CharacterBase selectedTarget, List<EnemyBase> enemies, List<AllyBase> allies)
        {
            var program = card?.CardData?.CardProgram;
            if (program == null)
                yield break;

            CardStatusRuntime.RegisterProgram(program);
            var context = new CardEffectContext
            {
                Source = source,
                SelectedTarget = selectedTarget,
                Card = card,
                CardData = card.CardData,
                Enemies = enemies ?? new List<EnemyBase>(),
                Allies = allies ?? new List<AllyBase>()
            };

            CardRuntimeDiagnostics.LogProgram("CARD-BEGIN", program, source, selectedTarget);
            var before = new CardBattleEventContext
            {
                Type = CardBattleEventType.BeforeCardPlayed,
                Source = source,
                Target = selectedTarget,
                ActiveCharacter = source,
                Card = card
            };
            yield return CardBattleEventBus.PublishRoutine(before);
            if (before.Cancelled)
                yield break;

            yield return ExecuteEffectsRoutine(program.onPlay, context, "card:" + card.CardData.CardName);

            yield return CardBattleEventBus.PublishRoutine(new CardBattleEventContext
            {
                Type = CardBattleEventType.AfterCardPlayed,
                Source = source,
                Target = selectedTarget,
                ActiveCharacter = source,
                Card = card
            });
            CardRuntimeDiagnostics.LogProgram("CARD-END", program, source, selectedTarget);
        }

public static IEnumerator ExecuteProgram(CardProgramData program, CharacterBase source,
            CharacterBase selectedTarget, List<EnemyBase> enemies, List<AllyBase> allies)
        {
            if (program == null)
                yield break;

            CardStatusRuntime.RegisterProgram(program);
            var context = new CardEffectContext
            {
                Source = source,
                SelectedTarget = selectedTarget,
                Enemies = enemies ?? new List<EnemyBase>(),
                Allies = allies ?? new List<AllyBase>()
            };

            CardRuntimeDiagnostics.LogProgram("ACTION-BEGIN", program, source, selectedTarget);
            yield return ExecuteEffectsRoutine(program.onPlay, context, "character-action");
            CardRuntimeDiagnostics.LogProgram("ACTION-END", program, source, selectedTarget);
        }


public static void ExecuteEffectsImmediate(IEnumerable<CardEffectData> effects,
            CardEffectContext context)
        {
            var list = effects?.Where(effect => effect != null).ToList()
                       ?? new List<CardEffectData>();
            for (int i = 0; i < list.Count; i++)
                ExecuteEffect(list[i], context, i, list.Count);
        }

public static bool EvaluateCondition(CardConditionData condition, CardEffectContext context,
            CharacterBase defaultTarget)
        {
            if (condition == null || CardEffectCatalog.Normalize(condition.type) == "always")
                return true;

            var target = ResolveConditionTarget(condition.target, context, defaultTarget);
            switch (CardEffectCatalog.Normalize(condition.type))
            {
                case "has_status":
                    return CardStatusRuntime.HasStatus(target, condition.statusId);
                case "not_has_status":
                    return !CardStatusRuntime.HasStatus(target, condition.statusId);
                case "status_stacks_at_least":
                    return CardStatusRuntime.GetStacks(target, condition.statusId) >= condition.value;
                case "status_stacks_at_most":
                    return CardStatusRuntime.GetStacks(target, condition.statusId) <= condition.value;
                case "health_below_percent":
                    return target && target.characterStats.MaxHealth > 0
                                  && target.characterStats.CurrentHealth * 100
                                  < target.characterStats.MaxHealth * condition.value;
                case "health_above_percent":
                    return target && target.characterStats.MaxHealth > 0
                                  && target.characterStats.CurrentHealth * 100
                                  > target.characterStats.MaxHealth * condition.value;
                case "opponent_count_at_least":
                    return ResolveOpponents(context).Count >= condition.value;
                case "ally_count_at_least":
                    return ResolveAllies(context).Count >= condition.value;
                case "last_card_has_tag":
                    return CardBattleEventBus.LastPlayedCardTags.Contains(
                        CardEffectCatalog.Normalize(condition.tag));
                default:
                    return false;
            }
        }

private static void ExecuteEffect(CardEffectData effect, CardEffectContext context,
            int effectIndex, int totalEffects)
        {
            if (effect == null || !CardEffectCatalog.TryGet(effect.op, out var descriptor))
                return;

            if (descriptor.Id == "exhaust_self")
            {
                CardRuntimeDiagnostics.LogEffect("BEGIN", effectIndex, totalEffects,
                    effect, context.Source, 0);
                if (EvaluateCondition(effect.condition, context, context.Source))
                    context.Card?.Exhaust(false);
                CardRuntimeDiagnostics.LogEffect("END", effectIndex, totalEffects,
                    effect, context.Source, 0);
                return;
            }

            foreach (var target in ResolveTargets(effect.target, context))
            {
                if (!target || !EvaluateCondition(effect.condition, context, target))
                    continue;

                int value = EvaluateValue(effect, context, target);
                CardRuntimeDiagnostics.LogEffect("BEGIN", effectIndex, totalEffects,
                    effect, target, value);
                switch (descriptor.Id)
                {
                    case "damage":
                        if (context.Event?.Type == CardBattleEventType.BeforeDamage
                            && target == context.Event.Target)
                            context.Event.Amount = Mathf.Max(0, context.Event.Amount + value);
                        else
                            DealDamage(context, target, value, effect.pierceBlock);
                        break;
                    case "modify_event_amount":
                        if (context.Event != null)
                            context.Event.Amount = Mathf.Max(0, context.Event.Amount + value);
                        break;
                    case "apply_status":
                        CardStatusRuntime.Apply(target, effect.statusId, value, effect.duration,
                            context.Source, context.CardData);
                        break;
                    case "set_status":
                        CardStatusRuntime.Set(target, effect.statusId, value, effect.duration,
                            context.Source, context.CardData);
                        break;
                    case "remove_status":
                        CardStatusRuntime.Remove(target, effect.statusId, value, context.Source);
                        break;
                    default:
                        if (descriptor.LegacyActionType.HasValue)
                        {
                            CardActionProcessor.GetAction(descriptor.LegacyActionType.Value)
                                .DoAction(new CardActionParameters(value, target, context.Source,
                                    context.CardData, context.Card, effect.statusId ?? string.Empty));
                        }
                        break;
                }
                CardRuntimeDiagnostics.LogEffect("END", effectIndex, totalEffects,
                    effect, target, value);
            }
        }

private static void DealDamage(CardEffectContext context, CharacterBase target, int value,
            bool pierceBlock)
        {
            int beforeHealth = target.characterStats.CurrentHealth;            int beforeBlock = target.characterStats.StatusDict[StatusType.Block].StatusValue;
            var damageEvent = CardBattleEventBus.Publish(new CardBattleEventContext
            {
                Type = CardBattleEventType.BeforeDamage,
                Source = context.Source,
                Target = target,
                ActiveCharacter = context.Source,
                Card = context.Card,
                Amount = Mathf.Max(0, value)
            });
            if (damageEvent == null || damageEvent.Cancelled)
                return;

            target.characterStats.Damage(Mathf.Max(0, damageEvent.Amount), pierceBlock);
            int healthDamage = Mathf.Max(0, beforeHealth - target.characterStats.CurrentHealth);
            int blockDamage = Mathf.Max(0,
                beforeBlock - target.characterStats.StatusDict[StatusType.Block].StatusValue);
            int resolvedDamage = healthDamage + blockDamage;
            ShowDamageFeedback(target, resolvedDamage);

            CardBattleEventBus.Publish(new CardBattleEventContext
            {
                Type = CardBattleEventType.AfterDamage,
                Source = context.Source,
                Target = target,
                ActiveCharacter = context.Source,
                Card = context.Card,
                Amount = resolvedDamage
            });
        }

        private static void ShowDamageFeedback(CharacterBase target, int amount)
        {
            var fxManager = FxManager.Instance;
            if (!target || fxManager == null)
                return;

            fxManager.PlayFx(target.transform, FxType.Attack);
            if (target.TextSpawnRoot)
            {
                string text = amount > 0
                    ? "<Color=#ff5555>-" + amount + "</color>"
                    : "<Color=#aaaaee>Blocked</color>";
                fxManager.SpawnFloatingText(target.TextSpawnRoot, text);
            }
        }


private static int EvaluateValue(CardEffectData effect, CardEffectContext context,
            CharacterBase target)
        {
            var formula = effect.valueFormula;
            if (formula == null)
                return effect.value;

            CharacterBase formulaTarget = ResolveFormulaTarget(formula.target, context, target);
            float input = 0f;
            switch (CardEffectCatalog.Normalize(formula.source))
            {
                case "status_stacks":
                    input = CardStatusRuntime.GetStacks(formulaTarget, formula.statusId);
                    break;
                case "current_health":
                    input = formulaTarget ? formulaTarget.characterStats.CurrentHealth : 0;
                    break;
                case "max_health":
                    input = formulaTarget ? formulaTarget.characterStats.MaxHealth : 0;
                    break;
                case "missing_health":
                    input = formulaTarget
                        ? formulaTarget.characterStats.MaxHealth - formulaTarget.characterStats.CurrentHealth
                        : 0;
                    break;
                case "block":
                    input = formulaTarget
                        ? formulaTarget.characterStats.StatusDict[StatusType.Block].StatusValue : 0;
                    break;
                case "cards_played_this_turn":
                    input = CardBattleEventBus.CardsPlayedThisTurn;
                    break;
                case "opponent_count":
                    input = ResolveOpponents(context).Count;
                    break;
                case "ally_count":
                    input = ResolveAllies(context).Count;
                    break;
            }

            return Mathf.RoundToInt(formula.baseValue + input * formula.multiplier);
        }

private static CharacterBase ResolveFormulaTarget(string targetId, CardEffectContext context,
            CharacterBase effectTarget)
        {
            switch (CardEffectCatalog.Normalize(targetId))
            {
                case "selected_target": return context.SelectedTarget;
                case "source": return context.Source;
                case "status_owner": return context.StatusOwner;
                case "event_source": return context.Event?.Source;
                case "event_target": return context.Event?.Target;
                default: return effectTarget;
            }
        }


private static List<CharacterBase> ResolveTargets(string targetId, CardEffectContext context)
        {
            var targets = new List<CharacterBase>();
            var opponents = ResolveOpponents(context);
            var allies = ResolveAllies(context);

            switch (CardEffectCatalog.Normalize(targetId))
            {
                case CardEffectCatalog.Self:
                    // Legacy safety: inside a status trigger, natural-language "self" means the bearer.
                    targets.Add(context.StatusOwner ? context.StatusOwner : context.Source);
                    break;
                case CardEffectCatalog.EffectSource:
                    targets.Add(context.Source);
                    break;
                case CardEffectCatalog.SelectedEnemy:
                    targets.Add(context.SelectedTarget);
                    break;
                case CardEffectCatalog.AllEnemies:
                    targets.AddRange(opponents);
                    break;
                case CardEffectCatalog.OtherEnemies:
                    targets.AddRange(opponents.Where(item => item != context.SelectedTarget));
                    break;
                case CardEffectCatalog.RandomEnemy:
                    AddRandom(targets, opponents);
                    break;
                case CardEffectCatalog.LowestHealthEnemy:
                    AddHealthExtreme(targets, opponents, true);
                    break;
                case CardEffectCatalog.HighestHealthEnemy:
                    AddHealthExtreme(targets, opponents, false);
                    break;
                case CardEffectCatalog.AllAllies:
                    targets.AddRange(allies);
                    break;
                case CardEffectCatalog.OtherAllies:
                    targets.AddRange(allies.Where(item => item != context.Source));
                    break;
                case CardEffectCatalog.RandomAlly:
                    AddRandom(targets, allies);
                    break;
                case CardEffectCatalog.EventSource:
                    targets.Add(context.Event?.Source);
                    break;
                case CardEffectCatalog.EventTarget:
                    targets.Add(context.Event?.Target);
                    break;
                case CardEffectCatalog.StatusOwner:
                    targets.Add(context.StatusOwner);
                    break;
            }

            return targets.Where(item => item).Distinct().ToList();
        }

private static CharacterBase ResolveConditionTarget(string targetId,
            CardEffectContext context, CharacterBase defaultTarget)
        {
            switch (CardEffectCatalog.Normalize(targetId))
            {
                case "selected_target": return context.SelectedTarget;
                case "self": return context.StatusOwner ? context.StatusOwner : context.Source;
                case "effect_source": return context.Source;
                case "status_owner": return context.StatusOwner;
                case "event_source": return context.Event?.Source;
                case "event_target": return context.Event?.Target;
                default: return defaultTarget;
            }
        }
    

private static void AddHealthExtreme(List<CharacterBase> output,
            List<CharacterBase> candidates, bool lowest)
        {
            if (candidates.Count == 0)
                return;

            CharacterBase selected = lowest
                ? candidates.OrderBy(item => item.characterStats.CurrentHealth).First()
                : candidates.OrderByDescending(item => item.characterStats.CurrentHealth).First();
            output.Add(selected);
        }


private static void AddRandom(List<CharacterBase> output, List<CharacterBase> candidates)
        {
            if (candidates.Count > 0)
                output.Add(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        }


private static List<CharacterBase> ResolveAllies(CardEffectContext context)
        {
            IEnumerable<CharacterBase> candidates = context.Source is EnemyBase
                ? (context.Enemies ?? new List<EnemyBase>()).Cast<CharacterBase>()
                : (context.Allies ?? new List<AllyBase>()).Cast<CharacterBase>();
            var result = candidates.Where(item => item).Distinct().ToList();
            if (context.Source && !result.Contains(context.Source))
                result.Add(context.Source);
            return result;
        }


private static List<CharacterBase> ResolveOpponents(CardEffectContext context)
        {
            IEnumerable<CharacterBase> candidates = context.Source is EnemyBase
                ? (context.Allies ?? new List<AllyBase>()).Cast<CharacterBase>()
                : (context.Enemies ?? new List<EnemyBase>()).Cast<CharacterBase>();
            return candidates.Where(item => item).Distinct().ToList();
        }


public static IEnumerator ExecuteEffectsRoutine(IEnumerable<CardEffectData> effects,
            CardEffectContext context, string stage)
        {
            var list = effects?.Where(effect => effect != null).ToList()
                       ?? new List<CardEffectData>();
            for (int i = 0; i < list.Count; i++)
            {
                yield return ExecuteEffectRoutine(list[i], context, i, list.Count, stage);
                if (i < list.Count - 1)
                    yield return new WaitForSeconds(EffectDelaySeconds);
            }
        }


private static IEnumerator DealDamageRoutine(CardEffectContext context,
            CharacterBase target, int value, bool pierceBlock)
        {
            int beforeHealth = target.characterStats.CurrentHealth;
            int beforeBlock = target.characterStats.StatusDict[StatusType.Block].StatusValue;
            var damageEvent = new CardBattleEventContext
            {
                Type = CardBattleEventType.BeforeDamage,
                Source = context.Source,
                Target = target,
                ActiveCharacter = context.Source,
                Card = context.Card,
                Amount = Mathf.Max(0, value)
            };
            yield return CardBattleEventBus.PublishRoutine(damageEvent);
            if (damageEvent.Cancelled)
                yield break;

            if (damageEvent.ResolvedEffects > 0)
                yield return new WaitForSeconds(EffectDelaySeconds);

            target.characterStats.Damage(Mathf.Max(0, damageEvent.Amount), pierceBlock);
            int healthDamage = Mathf.Max(0, beforeHealth - target.characterStats.CurrentHealth);
            int blockDamage = Mathf.Max(0,
                beforeBlock - target.characterStats.StatusDict[StatusType.Block].StatusValue);
            int resolvedDamage = healthDamage + blockDamage;
            ShowDamageFeedback(target, resolvedDamage);

            yield return CardBattleEventBus.PublishRoutine(new CardBattleEventContext
            {
                Type = CardBattleEventType.AfterDamage,
                Source = context.Source,
                Target = target,
                ActiveCharacter = context.Source,
                Card = context.Card,
                Amount = resolvedDamage
            });
        }


private static IEnumerator ExecuteEffectRoutine(CardEffectData effect,
            CardEffectContext context, int effectIndex, int totalEffects, string stage)
        {
            if (effect == null || !CardEffectCatalog.TryGet(effect.op, out var descriptor))
                yield break;

            string beginPhase = stage + "/BEGIN";
            string endPhase = stage + "/END";
            if (descriptor.Id == "exhaust_self")
            {
                CardRuntimeDiagnostics.LogEffect(beginPhase, effectIndex, totalEffects,
                    effect, context.Source, 0);
                if (EvaluateCondition(effect.condition, context, context.Source))
                    context.Card?.Exhaust(false);
                CardRuntimeDiagnostics.LogEffect(endPhase, effectIndex, totalEffects,
                    effect, context.Source, 0);
                yield break;
            }

            foreach (var target in ResolveTargets(effect.target, context))
            {
                if (!target || !EvaluateCondition(effect.condition, context, target))
                    continue;

                int value = EvaluateValue(effect, context, target);
                CardRuntimeDiagnostics.LogEffect(beginPhase, effectIndex, totalEffects,
                    effect, target, value);

                if (descriptor.Id == "damage"
                    && context.Event?.Type == CardBattleEventType.BeforeDamage
                    && target == context.Event.Target)
                {
                    context.Event.Amount = Mathf.Max(0, context.Event.Amount + value);
                }
                else if (descriptor.Id == "damage")
                {
                    yield return DealDamageRoutine(context, target, value, effect.pierceBlock);
                }
                else if (descriptor.Id == "modify_event_amount")
                {
                    if (context.Event != null)
                        context.Event.Amount = Mathf.Max(0, context.Event.Amount + value);
                }
                else if (descriptor.Id == "apply_status"
                         || descriptor.Id == "set_status"
                         || descriptor.Id == "remove_status")
                {
                    int beforeStacks = CardStatusRuntime.GetStacks(target, effect.statusId);
                    CardBattleEventBus.SuppressStatusEvents = true;
                    try
                    {
                        if (descriptor.Id == "apply_status")
                            CardStatusRuntime.Apply(target, effect.statusId, value, effect.duration,
                                context.Source, context.CardData);
                        else if (descriptor.Id == "set_status")
                            CardStatusRuntime.Set(target, effect.statusId, value, effect.duration,
                                context.Source, context.CardData);
                        else
                            CardStatusRuntime.Remove(target, effect.statusId, value, context.Source);
                    }
                    finally
                    {
                        CardBattleEventBus.SuppressStatusEvents = false;
                    }

                    int afterStacks = CardStatusRuntime.GetStacks(target, effect.statusId);
                    int delta = afterStacks - beforeStacks;
                    if (delta != 0)
                    {
                        yield return CardBattleEventBus.PublishRoutine(new CardBattleEventContext
                        {
                            Type = delta > 0
                                ? CardBattleEventType.StatusApplied
                                : CardBattleEventType.StatusRemoved,
                            Source = context.Source,
                            Target = target,
                            ActiveCharacter = context.Source,
                            Card = context.Card,
                            StatusId = effect.statusId,
                            Amount = Mathf.Abs(delta)
                        });
                    }
                }
                else if (descriptor.LegacyActionType.HasValue)
                {
                    CardActionProcessor.GetAction(descriptor.LegacyActionType.Value)
                        .DoAction(new CardActionParameters(value, target, context.Source,
                            context.CardData, context.Card, effect.statusId ?? string.Empty));
                }

                CardRuntimeDiagnostics.LogEffect(endPhase, effectIndex, totalEffects,
                    effect, target, value);
            }
        }
}
}
