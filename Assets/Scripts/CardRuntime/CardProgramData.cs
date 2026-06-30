using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AIDeckbuilder.CardRuntime
{
    [Serializable]
    public class GeneratedCardSpec
    {
        [JsonProperty(Required = Required.Always)] public int schemaVersion = CardProgramData.CurrentSchemaVersion;
        [JsonProperty(Required = Required.Always)] public string cardName;
        [JsonProperty(Required = Required.Always)] public string description;        [JsonProperty(Required = Required.Always)] public int manaCost;
        [JsonProperty(Required = Required.Always)] public string prompt;
        public List<string> tags = new();
        [JsonProperty(Required = Required.Always)] public List<CardEffectData> effects = new();
        public List<CardStatusDefinitionData> statuses = new();
    }

    [Serializable]
    public sealed class GeneratedActionSpec
    {
        [JsonProperty(Required = Required.Always)] public string abilityId;
        [JsonProperty(Required = Required.Always)] public List<CardEffectData> effects = new();
        public List<CardStatusDefinitionData> statuses = new();
    }

    [Serializable]
    public sealed class GeneratedEnemyProgramBundle
    {
        [JsonProperty(Required = Required.Always)] public int schemaVersion = CardProgramData.CurrentSchemaVersion;
        [JsonProperty(Required = Required.Always)] public List<GeneratedActionSpec> actions = new();
    }

    [Serializable]
    public sealed class CardProgramData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public List<string> tags = new();
        public List<CardEffectData> onPlay = new();
        public List<CardStatusDefinitionData> statusDefinitions = new();

        public bool IsExecutable => schemaVersion == CurrentSchemaVersion && onPlay != null;

        public CardStatusDefinitionData FindStatus(string statusId)
        {
            if (statusDefinitions == null || string.IsNullOrWhiteSpace(statusId))
                return null;

            return statusDefinitions.Find(status =>
                string.Equals(status.id, statusId, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Serializable]
    public sealed class CardEffectData
    {
        [JsonProperty(Required = Required.Always)] public string op;
        [JsonProperty(Required = Required.Always)] public string target;
        public int value;
        public string statusId;
        public int duration;
        public bool pierceBlock;
        public CardValueExpressionData valueFormula;
        public CardConditionData condition;
    }

    [Serializable]
    public sealed class CardValueExpressionData
    {
        public string source = "constant";
        public string target = "effect_target";
        public int baseValue;
        public float multiplier = 1f;
        public string statusId;
    }

    [Serializable]
    public sealed class CardConditionData
    {
        public string type = "always";
        public string target = "effect_target";
        public string statusId;
        public string tag;
        public int value;
    }

    [Serializable]
    public sealed class CardStatusDefinitionData
    {
        [JsonProperty(Required = Required.Always)] public string id;
        public string color;
public string displayName;
        public string stackKey;
        public string stackMode = "add";
        public int maxStacks = 99;
        public int duration;
        public List<CardTriggerData> triggers = new();
    }

    [Serializable]
    public sealed class CardTriggerData
    {
        [JsonProperty(Required = Required.Always)] public string eventType;
        public string eventRole = "owner_turn";
        public int priority;
        public int limitPerTurn;
        public CardConditionData condition;
        [JsonProperty(Required = Required.Always)] public List<CardEffectData> effects = new();
    }
}