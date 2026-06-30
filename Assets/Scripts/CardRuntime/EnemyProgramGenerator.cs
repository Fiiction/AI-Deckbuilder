using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AIDeckbuilder.CardRuntime;
using Newtonsoft.Json;
using NueGames.NueDeck.Scripts.Characters;
using NueGames.NueDeck.Scripts.Data.Characters;
using UnityEngine;

namespace AIDeckbuilder.CardRuntime
{
    public static class EnemyProgramGenerator
    {
        private sealed class AbilityRequest
        {
            public string AbilityId;
            public string CacheKey;
            public EnemyCharacterData EnemyData;
            public EnemyAbilityData Ability;
        }

        public static IEnumerator PrepareEncounter(IEnumerable<EnemyBase> enemies)
        {
            var requests = CollectMissingAbilities(enemies);
            if (requests.Count == 0)
                yield break;

            var processingCanvas = AI_CardEffect.instance != null
                ? AI_CardEffect.instance.processingCanvas : null;
            processingCanvas?.StartProcessing("Enemy Program Generation:");

            string basePrompt = BuildPrompt(requests);
            int maxAttempts = Mathf.Max(1, AI_IntegrationManager.CardEffectRetryCount + 1);
            int timeoutSeconds = Mathf.Max(1, AI_IntegrationManager.CardEffectTimeoutSeconds);
            string validationError = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                string reply = null;
                string failure = null;
                string prompt = basePrompt;
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    prompt += "\nThe previous response was invalid: " + validationError
                              + "\nReturn the complete corrected JSON bundle only.";
                }

                Action cancel = AI_IntegrationManager.instance.Request(prompt,
                    value => reply = value,
                    false, null,
                    (statusCode, message) => failure = statusCode + ": " + message,
                    timeoutSeconds);

                yield return new WaitUntil(() => reply != null || failure != null);

                if (reply != null && TryCompileBundle(reply, requests,
                        out var programs, out validationError))
                {
                    foreach (var request in requests)
                    {
                        var program = programs[request.AbilityId];
                        request.Ability.SetRuntimeProgram(program, request.CacheKey);
                        
                        CardRuntimeDiagnostics.LogGeneration("EnemyAction",
                            request.EnemyData.CharacterName + "/" + request.Ability.Name,
                            new
                            {
                                request.AbilityId,
                                Enemy = request.EnemyData.CharacterName,
                                Ability = request.Ability.Name,
                                Program = program
                            });
                        foreach (var status in program.statusDefinitions ?? new List<CardStatusDefinitionData>())
                            CardRuntimeDiagnostics.LogGeneration("EnemyStatus", status.id, status);
CardStatusRuntime.RegisterProgram(program);
                    }

                    cancel?.Invoke();
                    processingCanvas?.EndProcessing();
                    Debug.Log("<color=#77ddaa><b>Enemy programs compiled offline: "
                              + requests.Count + "</b></color>");
                    yield break;
                }

                cancel?.Invoke();
                if (reply == null)
                    validationError = failure ?? "Enemy program request failed.";

                bool willRetry = attempt < maxAttempts;
                string warning = willRetry ? "Retry" : "Failed";
                if (AI_DebugCanvas.instance != null)
                    AI_DebugCanvas.instance.AddWarning(warning);
                AI_IntegrationManager.instance.debugStr +=
                    "\n<b><color=#FF8844>EnemyProgram " + warning + " (" + attempt + "/"
                    + maxAttempts + "): " + validationError + "</color></b>\n";

                if (!willRetry)
                    break;
            }

            foreach (var request in requests)
            {
                request.Ability.SetRuntimeProgram(new CardProgramData
                {
                    schemaVersion = CardProgramData.CurrentSchemaVersion,
                    tags = new List<string> { "enemy_action", request.AbilityId, "generation_failed" },
                    onPlay = new List<CardEffectData>(),
                    statusDefinitions = new List<CardStatusDefinitionData>()
                }, request.CacheKey);
            }

            processingCanvas?.EndProcessing();
            Debug.LogWarning("Enemy program generation failed. Empty offline fallbacks were installed.");
        }

        private static List<AbilityRequest> CollectMissingAbilities(IEnumerable<EnemyBase> enemies)
        {
            var result = new List<AbilityRequest>();
            var seen = new HashSet<EnemyAbilityData>();
            foreach (var enemy in enemies ?? Enumerable.Empty<EnemyBase>())
            {
                if (!enemy || enemy.EnemyCharacterData?.EnemyAbilityList == null)
                    continue;

                foreach (var ability in enemy.EnemyCharacterData.EnemyAbilityList)
                {
                    if (ability == null || !seen.Add(ability))
                        continue;

                    string cacheKey = BuildCacheKey(enemy.EnemyCharacterData, ability);
                    if (ability.HasRuntimeProgram(cacheKey))
                    {
                        CardStatusRuntime.RegisterProgram(ability.RuntimeProgram);
                        CardRuntimeDiagnostics.LogGeneration("EnemyActionCacheHit",
                            enemy.EnemyCharacterData.CharacterName + "/" + ability.Name,
                            ability.RuntimeProgram);
                        continue;
                    }

                    result.Add(new AbilityRequest
                    {
                        AbilityId = "enemy_action_" + (result.Count + 1),
                        CacheKey = cacheKey,
                        EnemyData = enemy.EnemyCharacterData,
                        Ability = ability
                    });
                }
            }

            return result;
        }

private static string BuildCacheKey(EnemyCharacterData enemy, EnemyAbilityData ability)
        {
            string hero = AI_IntegrationManager.instance != null
                ? AI_IntegrationManager.instance.heroName + "|"
                  + AI_IntegrationManager.instance.heroDesc + "|"
                  + AI_IntegrationManager.instance.heroPlaystyle + "|"
                  + AI_IntegrationManager.instance.heroMechanicPlan
                : string.Empty;
            string ownedDeck = CardDesignContext.BuildOwnedDeckReference();
            return hero + "|" + ownedDeck + "|" + enemy.CharacterName + "|"
                   + enemy.CharacterDescription + "|" + ability.Name + "|" + ability.Desc;
        }

        private static string BuildPrompt(List<AbilityRequest> requests)
        {
            var sample = new GeneratedEnemyProgramBundle
            {
                schemaVersion = CardProgramData.CurrentSchemaVersion,
                actions = requests.Select(request => new GeneratedActionSpec
                {
                    abilityId = request.AbilityId,
                    effects = new List<CardEffectData>
                    {
                        new CardEffectData { op = "damage", target = "selected_enemy", value = 1 }
                    },
                    statuses = new List<CardStatusDefinitionData>()
                }).ToList()
            };

            var descriptions = new StringBuilder();
            foreach (var request in requests)
            {
                descriptions.Append("- abilityId: ").Append(request.AbilityId)
                    .Append("\n  enemy: ").Append(request.EnemyData.CharacterName)
                    .Append(" — ").Append(request.EnemyData.CharacterDescription)
                    .Append("\n  action: ").Append(request.Ability.Name)
                    .Append(" — ").Append(request.Ability.Desc).AppendLine();
            }

            return "Compile every listed enemy action into deterministic local effects. "
                   + "The LLM is used now only for design; these programs will later execute without AI.\n"
                   + descriptions + "\nReturn exactly one action for every abilityId using this JSON shape:\n"
                   + JsonConvert.SerializeObject(sample, Formatting.Indented) + "\n\n"
                   + CardEffectCatalog.BuildPromptReference() + "\n"
                   + CardDesignContext.BuildHeroPlanReference() + "\n"
                   + CardDesignContext.BuildOwnedDeckReference() + "\n"
                   + CardStatusRuntime.BuildDefinitionReference() + "\n"
                   + "ENEMY TARGET RULES:\n"
                   + "- self means the acting enemy only in the direct action effects array; never use self inside a status trigger.\n"
                   + "- In status triggers, status_owner is the bearer and effect_source is the original applier. "
                   + "Poison, virus, burn, regeneration, and other effects that change their bearer must target status_owner.\n"
                   + "- selected_enemy means the current main hero/opponent.\n"
                   + "- all_enemies means all opposing heroes.\n"
                   + "- Do not use exhaust_self for enemy actions.\n"
                   
                   + "- Inspect the owned deck and its statuses/tags. When thematically appropriate, make enemy "
                   + "actions apply pressure to, react to, cleanse, exploit, or race those mechanics.\n"
                   + "- Preserve counterplay: enemy interactions should create tactical decisions, not silently "
                   + "invalidate the hero's entire archetype.\n"
                   + "- Lifesteal is not an op; represent it with damage plus heal or a status trigger.\n"
+ "- Put all sub-effects of an action in its single effects array.\n"
                   + "- Enemy statuses that act immediately before that enemy acts use "
                   + "eventType before_enemy_action and eventRole owner_source.\n"
                   + "- Hero statuses that act at the hero turn start use eventType turn_start "
                   + "and eventRole owner_turn.\n"
                   + "Return JSON only; do not add markdown.";
        }

        private static bool TryCompileBundle(string json, List<AbilityRequest> requests,
            out Dictionary<string, CardProgramData> programs, out string error)
        {
            programs = new Dictionary<string, CardProgramData>(StringComparer.OrdinalIgnoreCase);
            error = null;
            try
            {
                int start = json.IndexOf('{');
                int end = json.LastIndexOf('}');
                if (start < 0 || end < start)
                    throw new JsonException("No JSON object was found.");

                var settings = new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                var bundle = JsonConvert.DeserializeObject<GeneratedEnemyProgramBundle>(
                    json.Substring(start, end - start + 1), settings);
                if (bundle == null || bundle.schemaVersion != CardProgramData.CurrentSchemaVersion)
                {
                    error = "Unsupported or missing schemaVersion.";
                    return false;
                }

                var expectedIds = new HashSet<string>(
                    requests.Select(item => item.AbilityId), StringComparer.OrdinalIgnoreCase);
                if (bundle.actions == null || bundle.actions.Count != expectedIds.Count)
                {
                    error = "The action count does not match the requested ability count.";
                    return false;
                }

                var localDefinitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var action in bundle.actions)
                {
                    if (action == null || !expectedIds.Remove(action.abilityId))
                    {
                        error = "An abilityId is missing, duplicated, or unknown.";
                        return false;
                    }

                    var compileResult = CardProgramCompiler.CompileAction(action);
                    if (!compileResult.Success)
                    {
                        error = action.abilityId + ": " + string.Join(" | ", compileResult.Errors);
                        return false;
                    }

                    if (!CardStatusRuntime.ValidateProgramDefinitions(compileResult.Program,
                            out string statusError))
                    {
                        error = statusError;
                        return false;
                    }

                    foreach (var definition in compileResult.Program.statusDefinitions)
                    {
                        string definitionJson = JsonConvert.SerializeObject(definition, Formatting.None);
                        if (localDefinitions.TryGetValue(definition.id, out string existing)
                            && !string.Equals(existing, definitionJson, StringComparison.Ordinal))
                        {
                            error = "Conflicting enemy status definition: " + definition.id + ".";
                            return false;
                        }
                        localDefinitions[definition.id] = definitionJson;
                    }

                    programs[action.abilityId] = compileResult.Program;
                }

                if (expectedIds.Count > 0)
                {
                    error = "One or more requested abilityIds are missing.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
