using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIDeckbuilder.CardRuntime
{
    public sealed class CardProgramCompileResult
    {
        public readonly CardProgramData Program;
        public readonly bool NeedsSelectedEnemy;
        public readonly IReadOnlyList<string> Errors;

        public bool Success => Program != null && Errors.Count == 0;

        public CardProgramCompileResult(CardProgramData program, bool needsSelectedEnemy, List<string> errors)
        {
            Program = program;
            NeedsSelectedEnemy = needsSelectedEnemy;
            Errors = errors;
        }
    }

    public static class CardProgramCompiler
    {
        private const int MaxEffectsPerList = 16;
        private const int MaxStatusesPerCard = 8;
        private const int MaxTriggersPerStatus = 8;
        private const int MaxTotalNodes = 128;
        private static readonly Regex LogicIdPattern =
            new("^[a-z][a-z0-9_]{0,47}$", RegexOptions.Compiled);
        private static readonly Regex ColorPattern =
            new("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$", RegexOptions.Compiled);

        public static CardProgramCompileResult Compile(GeneratedCardSpec spec)
        {
            var errors = new List<string>();
            if (spec == null)
            {
                errors.Add("Card response is null.");
                return new CardProgramCompileResult(null, false, errors);
            }

            if (spec.schemaVersion != CardProgramData.CurrentSchemaVersion)
                errors.Add("Unsupported schemaVersion: " + spec.schemaVersion + ".");

            if (string.IsNullOrWhiteSpace(spec.cardName))
                errors.Add("cardName is required.");
            if (string.IsNullOrWhiteSpace(spec.description))
                errors.Add("description is required.");
            if (string.IsNullOrWhiteSpace(spec.prompt))
                errors.Add("prompt is required.");
            if (spec.manaCost < 0 || spec.manaCost > 20)
                errors.Add("manaCost must be between 0 and 20.");

            spec.effects ??= new List<CardEffectData>();
            spec.statuses ??= new List<CardStatusDefinitionData>();
            spec.tags ??= new List<string>();

            if (spec.effects.Count == 0)
                errors.Add("At least one on-play effect is required.");
            if (spec.effects.Count > MaxEffectsPerList)
                errors.Add("Too many on-play effects.");
            if (spec.statuses.Count > MaxStatusesPerCard)
                errors.Add("Too many status definitions.");

            int nodeCount = 0;
            bool needsSelectedEnemy = ValidateEffects(spec.effects, true, "effects", errors, ref nodeCount);

            var statusIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var status in spec.statuses)
            {
                nodeCount++;
                ValidateStatus(status, statusIds, errors, ref nodeCount);
            }

            foreach (var tag in spec.tags)
            {
                if (!IsLogicId(tag))
                    errors.Add("Invalid card tag: " + tag + ".");
            }

            if (nodeCount > MaxTotalNodes)
                errors.Add("Card program exceeds the " + MaxTotalNodes + " node budget.");

            if (errors.Count > 0)
                return new CardProgramCompileResult(null, needsSelectedEnemy, errors);

            var program = new CardProgramData
            {
                schemaVersion = spec.schemaVersion,
                tags = spec.tags.Select(CardEffectCatalog.Normalize).Distinct().ToList(),
                onPlay = spec.effects,
                statusDefinitions = spec.statuses
            };

            return new CardProgramCompileResult(program, needsSelectedEnemy, errors);
        }

        public static CardProgramCompileResult CompileAction(GeneratedActionSpec spec)
        {
            var errors = new List<string>();
            if (spec == null)
            {
                errors.Add("Enemy action response is null.");
                return new CardProgramCompileResult(null, false, errors);
            }

            spec.abilityId = CardEffectCatalog.Normalize(spec.abilityId);
            spec.effects ??= new List<CardEffectData>();
            spec.statuses ??= new List<CardStatusDefinitionData>();

            if (!IsLogicId(spec.abilityId))
                errors.Add("abilityId is invalid.");
            if (spec.effects.Count == 0)
                errors.Add("At least one enemy action effect is required.");
            if (spec.effects.Count > MaxEffectsPerList)
                errors.Add("Too many enemy action effects.");
            if (spec.statuses.Count > MaxStatusesPerCard)
                errors.Add("Too many enemy action status definitions.");

            int nodeCount = 0;
            bool needsSelectedEnemy = ValidateEffects(spec.effects, true, "effects", errors, ref nodeCount);
            var statusIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var status in spec.statuses)
            {
                nodeCount++;
                ValidateStatus(status, statusIds, errors, ref nodeCount);
            }

            if (nodeCount > MaxTotalNodes)
                errors.Add("Enemy action program exceeds the " + MaxTotalNodes + " node budget.");

            if (errors.Count > 0)
                return new CardProgramCompileResult(null, needsSelectedEnemy, errors);

            return new CardProgramCompileResult(new CardProgramData
            {
                schemaVersion = CardProgramData.CurrentSchemaVersion,
                tags = new List<string> { "enemy_action", spec.abilityId },
                onPlay = spec.effects,
                statusDefinitions = spec.statuses
            }, needsSelectedEnemy, errors);
        }


        private static void ValidateStatus(CardStatusDefinitionData status, HashSet<string> statusIds,
            List<string> errors, ref int nodeCount)
        {
            if (status == null)
            {
                errors.Add("Status definition cannot be null.");
                return;
            }

            status.id = CardEffectCatalog.Normalize(status.id);
            status.stackKey = CardEffectCatalog.Normalize(
                string.IsNullOrWhiteSpace(status.stackKey) ? status.id : status.stackKey);
            status.stackMode = CardEffectCatalog.Normalize(status.stackMode);
            
            

            if (string.IsNullOrWhiteSpace(status.color))
                status.color = DefaultStatusColor(status.id);
            else
            {
                status.color = status.color.Trim();
                if (!status.color.StartsWith("#"))
                    status.color = "#" + status.color;
                if (!ColorPattern.IsMatch(status.color))
                    errors.Add("color for status " + status.id + " must be #RRGGBB or #RRGGBBAA.");
                else
                    status.color = status.color.ToUpperInvariant();
            }
status.triggers ??= new List<CardTriggerData>();

            if (!IsLogicId(status.id))
                errors.Add("Invalid status id: " + status.id + ".");
            else if (!statusIds.Add(status.id))
                errors.Add("Duplicate status id: " + status.id + ".");

            if (!IsLogicId(status.stackKey))
                errors.Add("Invalid stackKey for status " + status.id + ".");

            switch (status.stackMode)
            {
                case "add":
                case "refresh":
                case "replace_newer":
                case "replace_stronger":
                    break;
                default:
                    errors.Add("Unknown stackMode on status " + status.id + ": " + status.stackMode + ".");
                    break;
            }

            if (status.maxStacks <= 0 || status.maxStacks > 999)
                errors.Add("maxStacks for " + status.id + " must be between 1 and 999.");
            if (status.duration < 0 || status.duration > 999)
                errors.Add("duration for " + status.id + " must be between 0 and 999.");
            if (status.triggers.Count > MaxTriggersPerStatus)
                errors.Add("Too many triggers on status " + status.id + ".");

            for (int i = 0; i < status.triggers.Count; i++)
            {
                var trigger = status.triggers[i];
                nodeCount++;
                if (trigger == null)
                {
                    errors.Add("Null trigger on status " + status.id + ".");
                    continue;
                }

                trigger.eventType = CardEffectCatalog.Normalize(trigger.eventType);
                trigger.eventRole = CardEffectCatalog.Normalize(trigger.eventRole);
                

                // In a status trigger, "self" is naturally read as the bearer. Canonicalize legacy/LLM output
                // to the explicit selector so generated damage-over-time effects cannot hit their applier.
                if (trigger.condition != null
                    && CardEffectCatalog.Normalize(trigger.condition.target) == CardEffectCatalog.Self)
                    trigger.condition.target = CardEffectCatalog.StatusOwner;
trigger.effects ??= new List<CardEffectData>();

                if (!CardEffectCatalog.IsKnownEvent(trigger.eventType))
                    errors.Add("Unknown eventType on status " + status.id + ": " + trigger.eventType + ".");
                if (!CardEffectCatalog.IsKnownEventRole(trigger.eventRole))
                    errors.Add("Unknown eventRole on status " + status.id + ": " + trigger.eventRole + ".");
                if (trigger.limitPerTurn < 0 || trigger.limitPerTurn > 99)
                    errors.Add("limitPerTurn on status " + status.id + " must be between 0 and 99.");
                if (trigger.effects.Count == 0)
                    errors.Add("Trigger " + i + " on status " + status.id + " has no effects.");

                foreach (var effect in trigger.effects)
                {
                    if (effect != null
                        && CardEffectCatalog.Normalize(effect.target) == CardEffectCatalog.Self)
                        effect.target = CardEffectCatalog.StatusOwner;
                    if (effect?.condition != null
                        && CardEffectCatalog.Normalize(effect.condition.target) == CardEffectCatalog.Self)
                        effect.condition.target = CardEffectCatalog.StatusOwner;

                    string op = CardEffectCatalog.Normalize(effect?.op);
                    string target = CardEffectCatalog.Normalize(effect?.target);
                    if (op == "modify_event_amount" && trigger.eventType != "before_damage")
                        errors.Add("modify_event_amount is only valid in before_damage triggers.");

                    bool recursiveDamageModifier = trigger.eventType == "before_damage"
                                                   && op == "damage"
                                                   && ((trigger.eventRole == "owner_source"
                                                        && target == "event_target")
                                                       || (trigger.eventRole == "owner_target"
                                                           && (target == "event_target"
                                                               || target == "status_owner"
                                                               || target == "self")));
                    if (recursiveDamageModifier)
                        errors.Add("A before_damage damage modifier must use modify_event_amount instead of damage.");
                }

                ValidateCondition(trigger.condition, "status " + status.id + " trigger " + i, errors);
                ValidateEffects(trigger.effects, false, "status " + status.id + " trigger " + i,
                    errors, ref nodeCount);
            }
        }

private static bool ValidateEffects(List<CardEffectData> effects, bool onPlay, string path,
            List<string> errors, ref int nodeCount)
        {
            bool needsSelectedEnemy = false;
            if (effects == null)
                return false;

            if (effects.Count > MaxEffectsPerList)
                errors.Add(path + " contains too many effects.");

            for (int i = 0; i < effects.Count; i++)
            {
                nodeCount++;
                var effect = effects[i];
                if (effect == null)
                {
                    errors.Add(path + "[" + i + "] is null.");
                    continue;
                }

                effect.op = CardEffectCatalog.Normalize(effect.op);
                effect.target = CardEffectCatalog.Normalize(effect.target);
                effect.statusId = CardEffectCatalog.Normalize(effect.statusId);

                if (!CardEffectCatalog.TryGet(effect.op, out var descriptor))
                {
                    errors.Add(path + "[" + i + "] uses unknown op " + effect.op + ".");
                    continue;
                }

                if (!descriptor.AllowedTargets.Contains(effect.target))
                    errors.Add(path + "[" + i + "] target " + effect.target
                               + " is not allowed for " + effect.op + ".");

                if (descriptor.OnPlayOnly && !onPlay)
                    errors.Add(path + "[" + i + "] uses on-play-only op " + effect.op + ".");

                if (onPlay && effect.op == "modify_event_amount")
                    errors.Add(path + "[" + i + "] uses trigger-only op modify_event_amount.");

                if (descriptor.RequiresStatusId && !IsLogicId(effect.statusId))
                    errors.Add(path + "[" + i + "] requires a valid statusId.");

                if (effect.duration < 0 || effect.duration > 999)
                    errors.Add(path + "[" + i + "] duration must be between 0 and 999.");

                ValidateFormula(effect.valueFormula, path + "[" + i + "]", errors);
                ValidateCondition(effect.condition, path + "[" + i + "]", errors);

                needsSelectedEnemy |= effect.target == CardEffectCatalog.SelectedEnemy
                                      || effect.target == CardEffectCatalog.OtherEnemies
                                      || CardEffectCatalog.Normalize(effect.valueFormula?.target) == "selected_target"
                                      || CardEffectCatalog.Normalize(effect.condition?.target) == "selected_target";
            }

            return needsSelectedEnemy;
        }

private static void ValidateFormula(CardValueExpressionData formula, string path,
            List<string> errors)
        {
            if (formula == null)
                return;

            formula.source = CardEffectCatalog.Normalize(formula.source);
            formula.target = CardEffectCatalog.Normalize(
                string.IsNullOrWhiteSpace(formula.target) ? "effect_target" : formula.target);
            formula.statusId = CardEffectCatalog.Normalize(formula.statusId);

            switch (formula.target)
            {
                case "effect_target":
                case "selected_target":
                case "source":
                case "status_owner":
                case "event_source":
                case "event_target":
                    break;
                default:
                    errors.Add(path + " uses unknown valueFormula target " + formula.target + ".");
                    break;
            }

            switch (formula.source)
            {
                case "constant":
                case "current_health":
                case "max_health":
                case "missing_health":
                case "block":
                case "cards_played_this_turn":
                case "opponent_count":
                case "ally_count":
                    break;
                case "status_stacks":
                    if (!IsLogicId(formula.statusId))
                        errors.Add(path + " status_stacks formula requires statusId.");
                    break;
                default:
                    errors.Add(path + " uses unknown valueFormula source " + formula.source + ".");
                    break;
            }
        }

private static void ValidateCondition(CardConditionData condition, string path,
            List<string> errors)
        {
            if (condition == null)
                return;

            condition.type = CardEffectCatalog.Normalize(condition.type);
            condition.target = CardEffectCatalog.Normalize(
                string.IsNullOrWhiteSpace(condition.target) ? "effect_target" : condition.target);
            condition.statusId = CardEffectCatalog.Normalize(condition.statusId);
            condition.tag = CardEffectCatalog.Normalize(condition.tag);

            switch (condition.target)
            {
                case "effect_target":
                case "selected_target":
                case "self":
                case "effect_source":
                case "status_owner":
                case "event_source":
                case "event_target":
                    break;
                default:
                    errors.Add(path + " uses unknown condition target " + condition.target + ".");
                    break;
            }

            switch (condition.type)
            {
                case "always":
                    break;
                case "has_status":
                case "not_has_status":
                case "status_stacks_at_least":
                case "status_stacks_at_most":
                    if (!IsLogicId(condition.statusId))
                        errors.Add(path + " condition " + condition.type + " requires statusId.");
                    break;
                case "health_below_percent":
                case "health_above_percent":
                    if (condition.value < 0 || condition.value > 100)
                        errors.Add(path + " condition " + condition.type + " value must be 0..100.");
                    break;
                case "opponent_count_at_least":
                case "ally_count_at_least":
                    if (condition.value < 0 || condition.value > 99)
                        errors.Add(path + " condition " + condition.type + " value must be 0..99.");
                    break;
                case "last_card_has_tag":
                    if (!IsLogicId(condition.tag))
                        errors.Add(path + " last_card_has_tag requires a valid tag.");
                    break;
                default:
                    errors.Add(path + " uses unknown condition " + condition.type + ".");
                    break;
            }
        }

        private static bool IsLogicId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && LogicIdPattern.IsMatch(value);
        }
    

private static string DefaultStatusColor(string statusId)
        {
            switch (CardEffectCatalog.Normalize(statusId))
            {
                case "poison": return "#4B1F6F";
                case "strength": return "#C44732";
            }

            string[] palette =
            {
                "#3F7CAC", "#8E5AA9", "#B85C38", "#2F8F83",
                "#B58B2A", "#A7445C", "#5967B0", "#5F8C3A"
            };
            int hash = 17;
            string normalized = CardEffectCatalog.Normalize(statusId);
            foreach (char character in normalized)
                hash = unchecked(hash * 31 + character);
            return palette[(hash & int.MaxValue) % palette.Length];
        }
}
}
