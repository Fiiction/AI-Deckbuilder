using System.Collections.Generic;
using System.Text;

namespace AIDeckbuilder.CardRuntime
{
    public static class CardProgramTextRenderer
    {
        public static string Build(CardProgramData program, string authoredDescription = null)
        {
            if (!string.IsNullOrWhiteSpace(authoredDescription))
                return authoredDescription.Trim().Replace("\r", " ").Replace("\n", " ");

            if (program?.onPlay == null)
                return string.Empty;

            var statusNames = new Dictionary<string, string>();
            if (program.statusDefinitions != null)
            {
                foreach (var status in program.statusDefinitions)
                {
                    if (status == null || string.IsNullOrWhiteSpace(status.id))
                        continue;
                    statusNames[CardEffectCatalog.Normalize(status.id)] = StatusName(status);
                }
            }

            var lines = new List<string>();
            foreach (var effect in program.onPlay)
            {
                string line = BuildEffect(effect, statusNames, false);
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }

            if (program.statusDefinitions != null)
            {
                foreach (var status in program.statusDefinitions)
                {
                    if (status?.triggers == null)
                        continue;

                    foreach (var trigger in status.triggers)
                    {
                        if (trigger?.effects == null)
                            continue;

                        var triggerEffects = new List<string>();
                        foreach (var effect in trigger.effects)
                        {
                            string effectText = BuildEffect(effect, statusNames, true);
                            if (!string.IsNullOrWhiteSpace(effectText))
                                triggerEffects.Add(effectText);
                        }

                        if (triggerEffects.Count == 0)
                            continue;

                        string condition = ConditionText(trigger.condition, statusNames);
                        string duration = status.duration > 0
                            ? " (" + status.duration + " turns)" : string.Empty;
                        string limit = trigger.limitPerTurn > 0
                            ? " Up to " + trigger.limitPerTurn + " time"
                              + (trigger.limitPerTurn == 1 ? "" : "s") + " per turn."
                            : string.Empty;
                        lines.Add(StatusName(status) + duration + " — "
                                  + EventText(trigger.eventType, trigger.eventRole) + ": "
                                  + condition + string.Join(" ", triggerEffects) + limit);
                    }
                }
            }

            return string.Join(" ", lines);
        }

private static string BuildEffect(CardEffectData effect,
            Dictionary<string, string> statusNames, bool triggered)
        {
            if (effect == null)
                return string.Empty;

            string target = TargetText(effect.target, triggered);
            string value = ValueText(effect, statusNames, triggered);
            bool dynamicValue = effect.valueFormula != null
                                && CardEffectCatalog.Normalize(effect.valueFormula.source) != "constant";
            string condition = ConditionText(effect.condition, statusNames);
            switch (CardEffectCatalog.Normalize(effect.op))
            {
                case "damage":
                    return dynamicValue
                        ? condition + "Deal damage to " + target + " equal to " + value
                          + (effect.pierceBlock ? ", ignoring Block." : ".")
                        : condition + "Deal " + value + " damage to " + target
                          + (effect.pierceBlock ? ", ignoring Block." : ".");
                case "heal":
                    return condition + "Heal " + target + " for " + value + ".";
                case "gain_block":
                    return condition + Capitalize(target) + " gains " + value + " Block.";
                case "draw":
                    return condition + "Draw " + value + " card" + (effect.value == 1 ? "." : "s.");
                case "gain_mana":
                    return condition + "Gain " + value + " Mana this turn.";
                case "modify_event_amount":
                    return condition + "Increase this damage by " + value + ".";
                case "stun":
                    return condition + "Stun " + target + " for " + value + " turn"
                           + (effect.value == 1 ? "." : "s.");
                case "exhaust_self":
                    return "Exhaust this card.";
                case "apply_status":
                    return condition + "Apply " + value + " " + FriendlyStatus(effect.statusId, statusNames)
                           + " to " + target
                           + (effect.duration > 0 ? " for " + effect.duration + " turns." : ".");
                case "set_status":
                    return condition + "Set " + target + "'s "
                           + FriendlyStatus(effect.statusId, statusNames) + " to exactly " + value
                           + (effect.duration > 0 ? " for " + effect.duration + " turns." : ".");
                case "remove_status":
                    return condition + (effect.value > 0 ? "Remove " + value + " stacks of " : "Remove all ")
                           + FriendlyStatus(effect.statusId, statusNames) + " from " + target + ".";
                default:
                    return string.Empty;
            }
        }

private static string ValueText(CardEffectData effect,
            Dictionary<string, string> statusNames, bool triggered)
        {
            var formula = effect.valueFormula;
            if (formula == null || CardEffectCatalog.Normalize(formula.source) == "constant")
                return effect.value.ToString();

            string subject = FormulaTargetText(formula.target, triggered);
            string source;
            switch (CardEffectCatalog.Normalize(formula.source))
            {
                case "status_stacks":
                    source = subject + FriendlyStatus(formula.statusId, statusNames) + " stacks";
                    break;
                case "current_health":
                    source = subject + "current Health";
                    break;
                case "max_health":
                    source = subject + "maximum Health";
                    break;
                case "missing_health":
                    source = subject + "missing Health";
                    break;
                case "block":
                    source = subject + "Block";
                    break;
                case "cards_played_this_turn":
                    source = "cards played this turn";
                    break;
                case "opponent_count":
                    source = "the number of opponents";
                    break;
                case "ally_count":
                    source = "the number of allies";
                    break;
                default:
                    source = "value";
                    break;
            }

            var builder = new StringBuilder();
            if (formula.baseValue != 0)
                builder.Append(formula.baseValue).Append(" + ");
            if (formula.multiplier != 1f)
                builder.Append(formula.multiplier.ToString("0.##")).Append(" x ");
            builder.Append(source);
            return builder.ToString();
        }

private static string TargetText(string target, bool triggered)
        {
            switch (CardEffectCatalog.Normalize(target))
            {
                case "self": return triggered ? "the status bearer" : "you";
                case "effect_source": return triggered ? "the status applier" : "you";
                case "selected_enemy": return "the selected enemy";
                case "all_enemies": return "all enemies";
                case "other_enemies": return "all other enemies";
                case "random_enemy": return "a random enemy";
                case "lowest_health_enemy": return "the enemy with the lowest current Health";
                case "highest_health_enemy": return "the enemy with the highest current Health";
                case "all_allies": return "all allies";
                case "other_allies": return "all other allies";
                case "random_ally": return "a random ally";
                case "event_source": return "the event source";
                case "event_target": return "the event target";
                case "status_owner": return "the status bearer";
                default: return "the target";
            }
        }

private static string ConditionText(CardConditionData condition,
            Dictionary<string, string> statusNames)
        {
            if (condition == null || CardEffectCatalog.Normalize(condition.type) == "always")
                return string.Empty;

            string subject = ConditionTargetText(condition.target);
            switch (CardEffectCatalog.Normalize(condition.type))
            {
                case "has_status":
                    return "If " + subject + " has " + FriendlyStatus(condition.statusId, statusNames) + ", ";
                case "not_has_status":
                    return "If " + subject + " does not have "
                           + FriendlyStatus(condition.statusId, statusNames) + ", ";
                case "status_stacks_at_least":
                    return "If " + subject + " has at least " + condition.value + " "
                           + FriendlyStatus(condition.statusId, statusNames) + ", ";
                case "status_stacks_at_most":
                    return "If " + subject + " has at most " + condition.value + " "
                           + FriendlyStatus(condition.statusId, statusNames) + ", ";
                case "health_below_percent":
                    return "If " + subject + " is below " + condition.value + "% Health, ";
                case "health_above_percent":
                    return "If " + subject + " is above " + condition.value + "% Health, ";
                case "opponent_count_at_least":
                    return "If there are at least " + condition.value + " opponents, ";
                case "ally_count_at_least":
                    return "If there are at least " + condition.value + " allies, ";
                case "last_card_has_tag":
                    return "If the previous card had the " + FriendlyName(condition.tag) + " tag, ";
                default:
                    return string.Empty;
            }
        }

        private static string StatusName(CardStatusDefinitionData status)
        {
            return string.IsNullOrWhiteSpace(status.displayName) ? status.id : status.displayName;
        }
    

private static string Capitalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }


private static string FriendlyName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "Unnamed";
            return Capitalize(id.Replace("_", " "));
        }


private static string FriendlyStatus(string id, Dictionary<string, string> statusNames)
        {
            string normalized = CardEffectCatalog.Normalize(id);
            return statusNames.TryGetValue(normalized, out string displayName)
                ? displayName : FriendlyName(normalized);
        }


private static string ConditionTargetText(string target)
        {
            switch (CardEffectCatalog.Normalize(target))
            {
                case "selected_target": return "the selected enemy";
                case "self": return "you";
                case "effect_source": return "the status applier";
                case "status_owner": return "the status bearer";
                case "event_source": return "the event source";
                case "event_target": return "the event target";
                default: return "the target";
            }
        }


private static string FormulaTargetText(string target, bool triggered)
        {
            switch (CardEffectCatalog.Normalize(target))
            {
                case "selected_target": return "the selected enemy's ";
                case "source": return triggered ? "the applier's " : "your ";
                case "status_owner": return "the status bearer's ";
                case "event_source": return "the event source's ";
                case "event_target": return "the event target's ";
                default: return "that target's ";
            }
        }


private static string EventText(string eventType, string eventRole)
        {
            string role = CardEffectCatalog.Normalize(eventRole);
            switch (CardEffectCatalog.Normalize(eventType))
            {
                case "turn_start":
                    return role == "owner_turn" ? "At the start of its bearer's turn" : "At turn start";
                case "turn_end":
                    return role == "owner_turn" ? "At the end of its bearer's turn" : "At turn end";
                case "before_enemy_action": return "Immediately before an enemy acts";
                case "before_card_played": return "Before a card is played";
                case "after_card_played": return "After a card is played";
                case "before_damage": return "Before damage is dealt";
                case "after_damage": return "After damage is dealt";
                case "status_applied": return "When a status is applied";
                case "status_removed": return "When a status is removed";
                default: return "When triggered";
            }
        }
}
}
