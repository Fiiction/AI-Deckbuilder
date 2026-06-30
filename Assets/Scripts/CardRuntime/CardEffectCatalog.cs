using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NueGames.NueDeck.Scripts.Enums;

namespace AIDeckbuilder.CardRuntime
{
    public sealed class CardEffectDescriptor
    {
        public readonly string Id;
        public readonly string PromptDescription;
        public readonly CardActionType? LegacyActionType;
        public readonly bool RequiresStatusId;
        public readonly bool OnPlayOnly;
        public readonly HashSet<string> AllowedTargets;

        public CardEffectDescriptor(string id, string promptDescription, CardActionType? legacyActionType,
            bool requiresStatusId, bool onPlayOnly, params string[] allowedTargets)
        {
            Id = id;
            PromptDescription = promptDescription;
            LegacyActionType = legacyActionType;
            RequiresStatusId = requiresStatusId;
            OnPlayOnly = onPlayOnly;
            AllowedTargets = new HashSet<string>(allowedTargets, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static class CardEffectCatalog
    {
        public const string Self = "self";
        public const string EffectSource = "effect_source";
        public const string SelectedEnemy = "selected_enemy";
        public const string AllEnemies = "all_enemies";
        public const string OtherEnemies = "other_enemies";
        public const string RandomEnemy = "random_enemy";
        public const string LowestHealthEnemy = "lowest_health_enemy";
        public const string HighestHealthEnemy = "highest_health_enemy";
        public const string AllAllies = "all_allies";
        public const string OtherAllies = "other_allies";
        public const string RandomAlly = "random_ally";
        public const string EventSource = "event_source";
        public const string EventTarget = "event_target";
        public const string StatusOwner = "status_owner";

        private static readonly Dictionary<string, CardEffectDescriptor> Descriptors =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["damage"] = new CardEffectDescriptor("damage",
                    "Deal value damage. pierceBlock=true bypasses block.", CardActionType.Attack, false, false,
                    SelectedEnemy, AllEnemies, OtherEnemies, RandomEnemy, LowestHealthEnemy, HighestHealthEnemy,
                    Self, EffectSource, AllAllies, OtherAllies, RandomAlly, EventSource, EventTarget, StatusOwner),
                ["heal"] = new CardEffectDescriptor("heal",
                    "Restore value health.", CardActionType.Heal, false, false,
                    Self, EffectSource, SelectedEnemy, AllEnemies, OtherEnemies, RandomEnemy, LowestHealthEnemy, HighestHealthEnemy,
                    AllAllies, OtherAllies, RandomAlly, EventSource, EventTarget, StatusOwner),
                ["gain_block"] = new CardEffectDescriptor("gain_block",
                    "Grant value block.", CardActionType.Block, false, false,
                    Self, EffectSource, SelectedEnemy, AllEnemies, OtherEnemies, RandomEnemy, LowestHealthEnemy, HighestHealthEnemy,
                    AllAllies, OtherAllies, RandomAlly, EventSource, EventTarget, StatusOwner),
                ["draw"] = new CardEffectDescriptor("draw",
                    "Draw value cards for the player.", CardActionType.Draw, false, false, Self, EffectSource, StatusOwner),
                ["gain_mana"] = new CardEffectDescriptor("gain_mana",
                    "Gain value mana for the current turn.", CardActionType.EarnMana, false, false, Self, EffectSource, StatusOwner),
                ["modify_event_amount"] = new CardEffectDescriptor("modify_event_amount",
                    "Add value to the current event amount. Use only in before_damage triggers to modify that one damage instance without creating another damage event.",
                    null, false, false, EventTarget),
                ["stun"] = new CardEffectDescriptor("stun",
                    "Prevent the target acting for value turns.", CardActionType.Stun, false, false,
                    SelectedEnemy, AllEnemies, OtherEnemies, RandomEnemy, LowestHealthEnemy, HighestHealthEnemy,
                    EventTarget, StatusOwner),
                ["exhaust_self"] = new CardEffectDescriptor("exhaust_self",
                    "Exhaust the played card after resolving. Use target self and value 0.", CardActionType.Exhaust,
                    false, true, Self),
                ["apply_status"] = new CardEffectDescriptor("apply_status",
                    "Add value stacks of statusId. duration overrides the status default when positive.", null,
                    true, false, Self, EffectSource, SelectedEnemy, AllEnemies, OtherEnemies, RandomEnemy,
                    LowestHealthEnemy, HighestHealthEnemy, AllAllies, OtherAllies, RandomAlly,
                    EventSource, EventTarget, StatusOwner),
                ["set_status"] = new CardEffectDescriptor("set_status",
                    "Set statusId to exactly value stacks; value 0 clears it.", null,
                    true, false, Self, EffectSource, SelectedEnemy, AllEnemies, OtherEnemies, RandomEnemy,
                    LowestHealthEnemy, HighestHealthEnemy, AllAllies, OtherAllies, RandomAlly,
                    EventSource, EventTarget, StatusOwner),
                ["remove_status"] = new CardEffectDescriptor("remove_status",
                    "Remove statusId. value 0 removes all stacks; positive value removes that many stacks.", null,
                    true, false, Self, EffectSource, SelectedEnemy, AllEnemies, OtherEnemies, RandomEnemy,
                    LowestHealthEnemy, HighestHealthEnemy, AllAllies, OtherAllies, RandomAlly,
                    EventSource, EventTarget, StatusOwner)
            };

        public static IEnumerable<CardEffectDescriptor> All => Descriptors.Values;

        public static bool TryGet(string id, out CardEffectDescriptor descriptor)
        {
            return Descriptors.TryGetValue(Normalize(id), out descriptor);
        }

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(' ', '_').ToLowerInvariant();
        }

        public static bool IsKnownEvent(string eventType)
        {
            switch (Normalize(eventType))
            {
                case "turn_start":
                case "turn_end":
                case "before_card_played":
                case "after_card_played":
                case "before_enemy_action":
                case "before_damage":
                case "after_damage":
                case "status_applied":
                case "status_removed":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsKnownEventRole(string eventRole)
        {
            switch (Normalize(eventRole))
            {
                case "owner_turn":
                case "owner_source":
                case "owner_target":
                case "any":
                    return true;
                default:
                    return false;
            }
        }

public static string BuildPromptReference()
        {
            var builder = new StringBuilder();
            builder.AppendLine("CARD PROGRAM RULES (schemaVersion 1):");
            builder.AppendLine("Each effect requires op, target and integer value. "
                               + "Optional fields: statusId, duration, pierceBlock, valueFormula, condition.");
            builder.AppendLine("Target selectors:");
            builder.AppendLine("- self: only the direct card/action actor. Never use self inside a status trigger.");
            builder.AppendLine("- status_owner: the character carrying the triggered status. Damage-over-time, healing-over-time, and owner turn-start/end effects must target status_owner.");
            builder.AppendLine("- effect_source: the character that originally applied the status (or the direct actor outside triggers). Use it only when the mechanic intentionally affects the applier.");
            builder.AppendLine("- selected_enemy: the player-selected enemy.");
            builder.AppendLine("- all_enemies: every opponent, including the selected target.");
            builder.AppendLine("- other_enemies: every opponent except selected_enemy.");
            builder.AppendLine("- random_enemy: one random opponent.");
            builder.AppendLine("- lowest_health_enemy / highest_health_enemy: one opponent by current Health.");
            builder.AppendLine("- all_allies: the source and every character on the source's side.");
            builder.AppendLine("- other_allies: allies except the source; random_ally: one same-side character.");
            builder.AppendLine("- event_source and event_target are the participants of the event that caused a trigger; they are not automatically the status owner.");
            builder.AppendLine("Effects:");
            foreach (var descriptor in Descriptors.Values.OrderBy(item => item.Id))
            {
                builder.Append("- ").Append(descriptor.Id).Append(": ")
                    .Append(descriptor.PromptDescription)
                    .Append(" Allowed targets: ")
                    .Append(string.Join(", ", descriptor.AllowedTargets.OrderBy(item => item)))
                    .AppendLine(".");
            }

            builder.AppendLine("Every named mechanic, including poison and strength, is a normal generated status: "
                               + "use apply_status plus a complete status definition. "
                               + "Do not assume built-in poison or strength behavior.");
            builder.AppendLine("Status definitions require id, displayName, color, stackKey, stackMode, "
                               + "maxStacks, duration and triggers.");
            builder.AppendLine("color must be a thematic HTML hex color (#RRGGBB or #RRGGBBAA) shared by "
                               + "floating feedback and character status text. Example: poison uses #4B1F6F.");
            builder.AppendLine("stackMode: add, refresh, replace_newer, replace_stronger.");
            builder.AppendLine("Trigger eventType: turn_start, turn_end, before_card_played, after_card_played, "
                               + "before_enemy_action, before_damage, after_damage, status_applied, status_removed.");
            builder.AppendLine("Trigger eventRole: owner_turn, owner_source, owner_target, any.");
            builder.AppendLine("STATUS TARGET SAFETY: In every status trigger, explicitly choose status_owner, effect_source, event_source, or event_target. For poison, virus, burn, regeneration, and similar effects that change the bearer, always use status_owner; never self.");
            builder.AppendLine("Damage modifiers must use modify_event_amount inside a before_damage trigger. "
                               + "Never use damage to event_target/status_owner merely to increase the current hit, "
                               + "because that creates a new damage event and can recursively trigger itself.");
            builder.AppendLine("For an owner_target before_damage trigger: use modify_event_amount for vulnerability, "
                               + "or damage event_source for retaliation. Do not use damage self.");
            builder.AppendLine("valueFormula.source: constant, status_stacks, current_health, max_health, "
                               + "missing_health, block, cards_played_this_turn, opponent_count, ally_count.");
            builder.AppendLine("valueFormula.target: effect_target, selected_target, source, status_owner, "
                               + "event_source, event_target.");
            builder.AppendLine("selected_target always means the originally selected enemy, even while the effect "
                               + "is resolving on other_enemies. Later effects read state changed by earlier effects.");
            builder.AppendLine("condition.type: always, has_status, not_has_status, health_below_percent, "
                               + "health_above_percent, status_stacks_at_least, status_stacks_at_most, "
                               + "opponent_count_at_least, ally_count_at_least, last_card_has_tag.");
            builder.AppendLine("condition.target: effect_target, selected_target, self, effect_source, status_owner, "
                               + "event_source, event_target. In status triggers prefer explicit effect_source or status_owner.");
            builder.AppendLine("Spread example: first apply 5 poison to selected_enemy; then apply_status to "
                               + "other_enemies with valueFormula {source:status_stacks,target:selected_target,"
                               + "statusId:poison,baseValue:0,multiplier:1}. This reads the updated selected target.");
            
            builder.AppendLine("Lifesteal is not an operation. Compose it from damage plus heal effects, "
                               + "or implement healing through an after_damage status trigger.");
builder.AppendLine("Use set_status when the resulting stack count must equal a value exactly; "
                               + "use apply_status to add and remove_status to subtract or clear.");
            builder.AppendLine("Do not invent fields, ops, targets, events, formulas or conditions. "
                               + "If the mechanic cannot be represented, return only "
                               + "{\"kind\":\"review_required\",\"reason\":\"unsupported_mechanic\"}.");
            return builder.ToString();
        }
    }
}
