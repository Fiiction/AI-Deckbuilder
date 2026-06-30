using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NueGames.NueDeck.Scripts.Characters;
using NueGames.NueDeck.Scripts.Data.Collection;
using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;

namespace AIDeckbuilder.CardRuntime
{
    public static class CardDesignContext
    {
        private static readonly List<GeneratedCardSpec> GeneratedCards = new();

        public static void ClearGeneratedCards()
        {
            GeneratedCards.Clear();
        }

        public static void RegisterGeneratedCard(GeneratedCardSpec card)
        {
            if (card == null)
                return;

            string identity = CardEffectCatalog.Normalize(card.cardName);
            int existingIndex = GeneratedCards.FindIndex(existing =>
                CardEffectCatalog.Normalize(existing.cardName) == identity);
            if (existingIndex >= 0)
                GeneratedCards[existingIndex] = card;
            else
                GeneratedCards.Add(card);
        }

        public static string BuildGeneratedPoolReference()
        {
            if (GeneratedCards.Count == 0)
                return "GENERATED CARD POOL: no prior generated designs.";

            var builder = new StringBuilder(
                "GENERATED CARD POOL (design continuity; not necessarily owned):\n");
            foreach (var card in GeneratedCards)
            {
                builder.Append("- ").Append(card.cardName)
                    .Append(" | cost ").Append(card.manaCost)
                    .Append(" | tags=").Append(string.Join(",", card.tags ?? new List<string>()))
                    .Append(" | effects=")
                    .Append(JsonConvert.SerializeObject(card.effects, Formatting.None))
                    .Append(" | statuses=")
                    .Append(JsonConvert.SerializeObject(
                        card.statuses?.Select(status => status.id), Formatting.None))
                    .AppendLine();
            }

            builder.AppendLine(
                "Create deliberate bridges to this pool, but distinguish it from the owned deck.");
            return builder.ToString();
        }
        public static string BuildOwnedDeckReference()
        {
            var cards = GameManager.Instance?.PersistentGameplayData?.CurrentCardsList;
            if (cards == null || cards.Count == 0)
                return "OWNED DECK CONTEXT: no cards have been added yet.";

            var uniqueCards = cards.Where(card => card != null)
                .GroupBy(card => card.Id ?? card.CardName ?? string.Empty)
                .Select(group => group.First())
                .ToList();

            var builder = new StringBuilder("OWNED DECK CONTEXT (authoritative current cards):\n");
            foreach (var card in uniqueCards)
            {
                builder.Append("- ").Append(card.CardName)
                    .Append(" | cost ").Append(card.ManaCost)
                    .Append(" | description: ").Append(card.CardDescription)
                    .Append(" | tags/effects: ")
                    .Append(card.CardProgram == null
                        ? "no executable program"
                        : JsonConvert.SerializeObject(new
                        {
                            tags = card.CardProgram.tags,
                            effects = card.CardProgram.onPlay,
                            statuses = card.CardProgram.statusDefinitions?.Select(status => status.id)
                        }, Formatting.None))
                    .AppendLine();
            }

            builder.AppendLine("Prefer explicit interactions with these cards, tags, and statuses when coherent.");
            return builder.ToString();
        }

        public static string BuildHeroPlanReference()
        {
            var manager = AI_IntegrationManager.instance;
            if (manager == null)
                return "HERO DESIGN PLAN: unavailable.";

            return "HERO DESIGN PLAN:\n- playstyle: "
                   + (string.IsNullOrWhiteSpace(manager.heroPlaystyle)
                       ? "not established yet" : manager.heroPlaystyle)
                   + "\n- planned mechanics/statuses: "
                   + (string.IsNullOrWhiteSpace(manager.heroMechanicPlan)
                       ? "not established yet" : manager.heroMechanicPlan);
        }
    }

    public static class CardRuntimeDiagnostics
    {
        public static bool Enabled = true;

        public static void LogGeneration(string kind, string id, object payload)
        {
            if (!Enabled)
                return;
            Debug.Log("[AI-GENERATION][" + kind + "][" + Safe(id) + "] "
                      + JsonConvert.SerializeObject(payload, Formatting.None));
        }

        public static void LogProgram(string phase, CardProgramData program,
            CharacterBase source, CharacterBase selectedTarget)
        {
            if (!Enabled)
                return;
            Debug.Log("[CARD-RUNTIME][PROGRAM][" + phase + "] source=" + Character(source)
                      + " selected=" + Character(selectedTarget)
                      + " effects=" + (program?.onPlay?.Count ?? 0)
                      + " tags=" + string.Join(",", program?.tags ?? new List<string>()));
        }

        public static void LogEvent(string phase, CardBattleEventContext context)
        {
            if (!Enabled || context == null)
                return;
            Debug.Log("[CARD-RUNTIME][EVENT][" + phase + "] type=" + context.Type
                      + " source=" + Character(context.Source)
                      + " target=" + Character(context.Target)
                      + " amount=" + context.Amount
                      + " cancelled=" + context.Cancelled);
        }

        public static void LogEffect(string phase, int index, int total, CardEffectData effect,
            CharacterBase target, int value)
        {
            if (!Enabled || effect == null)
                return;
            Debug.Log("[CARD-RUNTIME][EFFECT][" + phase + "] " + (index + 1) + "/" + total
                      + " op=" + effect.op + " selector=" + effect.target
                      + " target=" + Character(target) + " value=" + value
                      + " status=" + Safe(effect.statusId)
                      + " result={" + Snapshot(target, effect.statusId) + "}");
        }

        private static string Snapshot(CharacterBase target, string statusId)
        {
            if (!target || target.characterStats == null)
                return "target=missing";

            int block = target.characterStats.StatusDict.TryGetValue(StatusType.Block, out var blockStatus)
                ? blockStatus.StatusValue : 0;
            string status = string.IsNullOrWhiteSpace(statusId)
                ? string.Empty
                : ",statusStacks=" + CardStatusRuntime.GetStacks(target, statusId);
            return "health=" + target.characterStats.CurrentHealth + "/"
                   + target.characterStats.MaxHealth + ",block=" + block + status;
        }

        private static string Character(CharacterBase character)
        {
            return character ? character.name : "none";
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }
    }
}
