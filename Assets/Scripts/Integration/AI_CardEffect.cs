using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Characters;
using NueGames.NueDeck.Scripts.Data.Collection;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NueGames.NueDeck.Scripts.Card;
using NueGames.NueDeck.Scripts.Card.CardActions;

public class AI_CardEffect : MonoBehaviour
{
    
    public static AI_CardEffect instance;
    private int requestGeneration;
    private readonly List<Action> activeRequestCancels = new();
    
    
    [SerializeField, TextArea(12,20)] private string prompt1_withTarget;
    [SerializeField, TextArea(12,20)] private string prompt1_noTarget;
    [SerializeField, TextArea(12,35)] private string prompt2;
    [SerializeField, TextArea(12,35)] private string prompt_specific;
    [SerializeField, TextArea(12,20)] private string prompt3_Normal;
    [SerializeField, TextArea(12,20)] private string prompt3_CustomEffect;
    [SerializeField, TextArea(12,20)] private string prompt_StartTurn1;
    [SerializeField, TextArea(12,20)] private string prompt_EnemyTurn1;
    [SerializeField, TextArea(12,20)] private string prompt3_Normal_Enemy;
    [SerializeField, TextArea(12,20)] private string prompt3_CustomEffect_Enemy;
    
    public AI_ProcessingCanvas processingCanvas;
    [Header("Correction Prompts")]
    [SerializeField, TextArea(4, 6)] private string correctionPrompt_withTarget;
    [SerializeField, TextArea(4, 6)] private string correctionPrompt_noTarget;
    [SerializeField, TextArea(4, 6)] private string correctionPrompt_startTurn;
    [SerializeField, TextArea(4, 6)] private string correctionPrompt_enemy;
    [SerializeField, TextArea(4, 6)] private string correctionPrompt_effectType;

    private string cardName;
    private string cardDesc;
    private string userName;
    private string userDesc;
    private string targetName;
    private string targetDesc;
    private string userStatusStr;
    private string targetStatusStr;
    private string prompt1Sent;

    private string reply1 = "";
    private string reply2 = "";
    string[] numbers = {"1st", "2nd", "3rd", "4th", "5th", "6th",
        "7th", "8th", "9th", "10th", "11th", "12th", "13th", "14th", "15th", "16th", "17th", "18th", "19th",
        "20th", "21st", "22nd", "23rd", "24th", "25th", "26th", "27th", "28th", "29th", "30th", "31th", "32th", "33th", "34th", "35th", "36th", "37th", "38th", "39th"};
    
    public List<CardActionData> actionDatas = new();
    public int cardsUsedInBattle = 0;
    public int turnCnt = 0;
    public int cardInTurnCnt = 0;
    
    public Dictionary<string, string> effectDict = new();
private IEnumerator CardUseCoroutine(CardBase card, CharacterBase self, CharacterBase targetCharacter,
        Action<List<CardActionData>> callback)
    {
        cardName = card.CardData.CardName;
        cardDesc = card.CardData.CardDescription;
        var userData = (self as AllyBase).AllyCharacterData;

        userName = userData.CharacterName;
        userDesc = userData.CharacterDescription;
        userStatusStr = CharacterStatusString(self);

        processingCanvas.StartProcessing("Card Processing:");
        cardInTurnCnt++;

        if (targetCharacter != null)
        {
            var targetData = (targetCharacter as EnemyBase).EnemyCharacterData;
            targetName = targetData.CharacterName;
            targetDesc = targetData.CharacterDescription;
            targetStatusStr = CharacterStatusString(targetCharacter);

            prompt1Sent = prompt1_withTarget.Replace("##PlayerDesc##", userDesc)
                .Replace("##PlayerName##", userName)
                .Replace("##PlayerCustomStatus##", userStatusStr)
                .Replace("##CardName##", cardName)
                .Replace("##CardDesc##", cardDesc)
                .Replace("##ManaSpent##", card.CardData.ManaCost.ToString())
                .Replace("##EnemyName##", targetName)
                .Replace("##EnemyDesc##", targetDesc)
                .Replace("##EnemyCustomStatus##", targetStatusStr);
        }
        else
        {
            prompt1Sent = prompt1_noTarget.Replace("##PlayerDesc##", userDesc)
                .Replace("##PlayerName##", userName)
                .Replace("##PlayerCustomStatus##", userStatusStr)
                .Replace("##CardName##", cardName)
                .Replace("##CardDesc##", cardDesc)
                .Replace("##ManaSpent##", card.CardData.ManaCost.ToString());
        }

        prompt1Sent = prompt1Sent.Replace("##Specific##", prompt_specific);

        SuperActions response = default;
        bool requestSucceeded = false;
        yield return StartCoroutine(RequestEffectsWithRetry(prompt1Sent, value =>
        {
            response = value;
            requestSucceeded = true;
        }));

        if (!requestSucceeded)
        {
            actionDatas = new List<CardActionData>();
            processingCanvas.EndProcessing();
            callback?.Invoke(actionDatas);
            yield break;
        }

        if (!TryBuildActionData(response, false, targetCharacter != null,
                targetCharacter != null ? correctionPrompt_withTarget : correctionPrompt_noTarget,
                out var parsedActions,
                out var correctionPrompt))
        {
            RegisterCorrection(correctionPrompt);
            StartCoroutine(CardUseCoroutine(card, self, targetCharacter, callback));
            yield break;
        }

        actionDatas = parsedActions;
        processingCanvas.EndProcessing();
        callback?.Invoke(actionDatas);
    }
    
    public void CardUse(CardBase card, CharacterBase self, CharacterBase targetCharacter, 
        Action<List<CardActionData>> callback)
    {
        StartCoroutine(CardUseCoroutine(card, self, targetCharacter, callback));
    }

private IEnumerator AllyTurnStartEffectsCoroutine(CharacterBase self, Action callback)
    {
        yield return new WaitUntil(() => AI_IntegrationManager.instance.gameStartPending == false);
        Debug.Log("<b>Turn Start</b>");
        cardInTurnCnt = 0;
        processingCanvas.StartProcessing("Turn Start Processing:");
        float tick = Time.time;

        var userData = (self as AllyBase).AllyCharacterData;
        userName = userData.CharacterName;
        userDesc = userData.CharacterDescription;
        userStatusStr = CharacterStatusString(self);

        string enemiesDesc = "";
        foreach (var enemy in CombatManager.Instance.CurrentEnemiesList)
        {
            enemiesDesc += " - " + enemy.EnemyCharacterData.CharacterName
                + ", " + CharacterStatusString(enemy) + "\n";
        }

        int ordinalIndex = Mathf.Clamp(turnCnt, 0, numbers.Length - 1);
        prompt1Sent = prompt_StartTurn1.Replace("##PlayerDesc##", userDesc)
            .Replace("##PlayerCustomStatus##", userStatusStr)
            .Replace("##No##", numbers[ordinalIndex])
            .Replace("##EnemiesDesc##", enemiesDesc)
            .Replace("##Specific##", prompt_specific);

        SuperActions response = default;
        bool requestSucceeded = false;
        yield return StartCoroutine(RequestEffectsWithRetry(prompt1Sent, value =>
        {
            response = value;
            requestSucceeded = true;
        }));

        if (!requestSucceeded)
        {
            actionDatas = new List<CardActionData>();
            processingCanvas.EndProcessing();
            callback?.Invoke();
            yield break;
        }

        if (!TryBuildActionData(response, false, false, correctionPrompt_startTurn,
                out var parsedActions, out var correctionPrompt))
        {
            RegisterCorrection(correctionPrompt);
            StartCoroutine(AllyTurnStartEffectsCoroutine(self, callback));
            yield break;
        }

        turnCnt++;
        actionDatas = parsedActions;
        processingCanvas.EndProcessing();
        Debug.Log("<color=cyan><b>Ally Turn Start: " + actionDatas.Count + " "
                  + (Time.time - tick).ToString("0.##") + "</b></color>");

        foreach (var actionData in actionDatas)
        {
            var targetList = CardBase.DetermineTargets(self, null,
                CombatManager.Instance.CurrentEnemiesList,
                CombatManager.Instance.CurrentAlliesList, actionData);
            Debug.Log("Action: " + actionData.CardActionType);
            foreach (var target in targetList)
            {
                CardActionProcessor.GetAction(actionData.CardActionType)
                    .DoAction(new CardActionParameters(actionData.ActionValue,
                        target, self, null, null, actionData.StrParameter));
            }
            yield return new WaitForSeconds(1f);
        }

        callback?.Invoke();
    }
    public void AllyTurnStartEffects(CharacterBase self, Action callback)
    {
        
        StartCoroutine(AllyTurnStartEffectsCoroutine(self, callback));
    }

private IEnumerator EnemyTurnCoroutine(CharacterBase enemySelf, string enemyActionName,
        string enemyActionDesc, Action callback)
    {
        processingCanvas.StartProcessing("Enemy Action Processing:");
        float tick = Time.time;

        var userData = (enemySelf as EnemyBase).EnemyCharacterData;
        userName = userData.CharacterName;
        userDesc = userData.CharacterDescription;
        userStatusStr = CharacterStatusString(enemySelf);

        prompt1Sent = prompt_EnemyTurn1.Replace("##EnemyDesc##", userDesc)
            .Replace("##EnemyCustomStatus##", userStatusStr)
            .Replace("##EnemyName##", userName)
            .Replace("##EnemyActionName##", enemyActionName)
            .Replace("##EnemyActionDesc##", enemyActionDesc)
            .Replace("##Specific##", prompt_specific);

        SuperActions response = default;
        bool requestSucceeded = false;
        yield return StartCoroutine(RequestEffectsWithRetry(prompt1Sent, value =>
        {
            response = value;
            requestSucceeded = true;
        }));

        if (!requestSucceeded)
        {
            actionDatas = new List<CardActionData>();
            processingCanvas.EndProcessing();
            callback?.Invoke();
            yield break;
        }

        if (!TryBuildActionData(response, true, false, correctionPrompt_enemy,
                out var parsedActions, out var correctionPrompt))
        {
            RegisterCorrection(correctionPrompt);
            StartCoroutine(EnemyTurnCoroutine(enemySelf, enemyActionName, enemyActionDesc, callback));
            yield break;
        }

        actionDatas = parsedActions;
        Debug.Log("<color=#ff7799><b>EnemyTurn: " + actionDatas.Count + " "
                  + (Time.time - tick).ToString("0.##") + "</b></color>");

        processingCanvas.EndProcessing();
        foreach (var actionData in actionDatas)
        {
            var targetList = CardBase.DetermineTargets(enemySelf, null,
                CombatManager.Instance.CurrentEnemiesList,
                CombatManager.Instance.CurrentAlliesList, actionData);
            Debug.Log("Action: " + actionData.CardActionType);
            foreach (var target in targetList)
            {
                CardActionProcessor.GetAction(actionData.CardActionType)
                    .DoAction(new CardActionParameters(actionData.ActionValue,
                        target, enemySelf, null, null, actionData.StrParameter));
            }
            yield return new WaitForSeconds(1f);
        }

        callback?.Invoke();
    }

    public void EnemyTurn(CharacterBase enemySelf, string enemyActionName, string enemyActionDesc, Action callback)
    {
        StartCoroutine(EnemyTurnCoroutine(enemySelf, enemyActionName, enemyActionDesc, callback));
    }
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("AI_CardEffect already exists.");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    #region DataStructures

    public struct SuperActionParams
    {
        public string effectType;
        public string buffname;
        public string target;
        public int value;
    }

    public struct SuperActions
    {
        public SuperActionParams[] effects;
    }
    
    public static T Decode<T>(string stringData)
    {
        int st = stringData.IndexOf("{");
        int ed = stringData.LastIndexOf("}");
        stringData = stringData.Substring(st, ed-st+1);
        Debug.Log("String to Parse:\n" + stringData);
        var jsonData = JsonConvert.DeserializeObject<T>(stringData);

        //Debug.Log("Loaded: " + jsonData);
        return jsonData;
    }
    public struct ActionArray
    {
        public string[] effects;
    }

    public struct ActionParams
    {
        public string target;
        public int value;
    }

    public struct CustomEffectParams
    {
        public string buffname;
        public string target;
        public int value;
        
    }
    
    
    #endregion
    
    #region Convertion

    public static string CharacterStatusString(CharacterBase cha)
    {

        string s = ", with ";
        
        if (cha.characterStats.Effects.Count == 0)
            s = "";
        else
            foreach (var i in cha.characterStats.Effects)
            {
                if(i.Value.effectValue == 1)
                    s += "1 stack of " + i.Key + ", ";
                else
                    s += i.Value.effectValue +" stacks of " + i.Key + ", ";
            }
        string t = ", health " +cha.characterStats.CurrentHealth
                               + " / " + cha.characterStats.MaxHealth +", ";
        return s + t;
    }
    
    public static CardActionType StringToActionType(string str)
    {
        string s = str.ToLower();
        s = s.Split(':')[0].Trim();
        switch (s)
        {
           case "deal damage": return CardActionType.Attack;
           case "heal": return CardActionType.Heal;
           case "add block": return CardActionType.Block;
           case "increase strength": return CardActionType.IncreaseStrength;
           case "draw": return CardActionType.Draw;
           case "gain mana": return CardActionType.EarnMana;
           case "steal life": return CardActionType.LifeSteal;
           case "stun": return CardActionType.Stun;
           case "destroy the card": return CardActionType.Exhaust;
           case "exhaust": return CardActionType.Exhaust;
           case "add custom status": return CardActionType.CustomEffect;
           default: 
               Debug.LogWarning("Unknown card action type: " + str);
               return CardActionType.Unknown;
        }
    }

    public static string ActionTypeToString(CardActionType actionType)
    {
        switch (actionType)
        {
            case CardActionType.Attack: return "Deal Damage";
            case CardActionType.Heal: return "Heal";
            case CardActionType.Block: return "Add Block";
            case CardActionType.IncreaseStrength: return "Increase Strength";
            case CardActionType.Draw: return "Draw";
            case CardActionType.EarnMana: return "Gain Mana";
            case CardActionType.LifeSteal: return "Steal Life";
            case CardActionType.Stun: return "Stun";
            case CardActionType.Exhaust: return "Exhaust";
            case CardActionType.CustomEffect: return "Add Custom Status";
            default:
                Debug.LogWarning("Unknown card action type: " + actionType);
                return "Deal Damage";
        }
    }

    public static string ValueMeaning(CardActionType actionType)
    {
        switch (actionType)
        {
            case CardActionType.Attack: return "the amount of damage";
            case CardActionType.Heal: return "the amount of healing";
            case CardActionType.Block: return "the amount of damage it could block";
            case CardActionType.IncreaseStrength: return "the amount of strength added";
            case CardActionType.Draw: return "the amount of cards drawn";
            case CardActionType.EarnMana: return "the amount of mana added for this turn";
            case CardActionType.LifeSteal: return "the amount of damage dealt to the target and the amount of healing for the user";
            case CardActionType.Stun: return "the amount of turns that the target would not move";
            case CardActionType.Exhaust: return "(please return any integer, destroying card don't need a value parameter)";
            case CardActionType.CustomEffect: return "the stack of custom effect casted to the target";
            default:
                Debug.LogWarning("Unknown card action type: " + actionType);
                return "Deal Damage";
        }
    }
public static ActionTargetType StringToActionTarget(string str)
    {
        switch (NormalizeToken(str))
        {
            case "self": return ActionTargetType.Hero;
            case "targetenemy": return ActionTargetType.Enemy;
            case "allenemies": return ActionTargetType.AllEnemies;
            case "hero": return ActionTargetType.Hero;
            case "enemyself": return ActionTargetType.EnemySelf;
            default:
                Debug.LogError("Unknown card action target: " + str);
                return ActionTargetType.Unknown;
        }
    }

    public static string ActionTargetToString(ActionTargetType actionTargetType)
    {
        switch (actionTargetType)
        {
            case ActionTargetType.Hero: return "self";
            case ActionTargetType.Enemy: return "targetEnemy";
            case ActionTargetType.AllEnemies: return "allEnemies";
            default:
                Debug.LogWarning("Unknown card action target: " + actionTargetType);
                return "allEnemies";
        }
    }
    
    #endregion


private bool TryBuildActionData(SuperActions response, bool enemyContext, bool allowTargetEnemy,
        string invalidTargetPrompt, out List<CardActionData> result, out string correctionPrompt)
    {
        result = new List<CardActionData>();
        correctionPrompt = correctionPrompt_effectType;

        if (response.effects == null)
            return false;

        foreach (var effect in response.effects)
        {
            var actionType = StringToActionType(effect.effectType ?? "");
            if (actionType == CardActionType.Unknown)
                return false;

            if (!IsAllowedTarget(effect.target, enemyContext, allowTargetEnemy))
            {
                correctionPrompt = invalidTargetPrompt;
                return false;
            }

            string parameter = "";
            if (actionType == CardActionType.CustomEffect)
            {
                parameter = (effect.buffname ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(parameter))
                {
                    correctionPrompt = "Every Add Custom Status effect must include a short, non-empty buffname. "
                                       + "Please reprocess the complete action.\n";
                    return false;
                }
            }

            result.Add(new CardActionData(actionType, StringToActionTarget(effect.target),
                effect.value, parameter));
        }

        return true;
    }

    private static bool IsAllowedTarget(string target, bool enemyContext, bool allowTargetEnemy)
    {
        string normalized = NormalizeToken(target);
        if (enemyContext)
            return normalized == "hero" || normalized == "enemyself" || normalized == "allenemies";

        if (normalized == "self" || normalized == "allenemies")
            return true;

        return allowTargetEnemy && normalized == "targetenemy";
    }

    private void RegisterCorrection(string correctionPrompt)
    {
        Debug.Log("<b><color=#FF2222>Correction!</b></color>");
        AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
        AI_DebugCanvas.instance.AddWarning("Correction!");
        AI_IntegrationManager.instance.pendingPrompts += correctionPrompt;
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Split(':')[0].Trim().Replace(" ", "").ToLowerInvariant();
    }


private IEnumerator RequestEffectsWithRetry(string prompt, Action<SuperActions> successCallback)
    {
        int generation = requestGeneration;
        int maxAttempts = Mathf.Max(1, AI_IntegrationManager.CardEffectRetryCount + 1);
        int timeoutSeconds = Mathf.Max(1, AI_IntegrationManager.CardEffectTimeoutSeconds);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            string reply = null;
            string failure = null;
            Action cancelRequest = null;

            cancelRequest = AI_IntegrationManager.instance.Request(prompt,
                str =>
                {
                    if (generation == requestGeneration)
                        reply = str;
                },
                true, typeof(SuperActions),
                (statusCode, message) =>
                {
                    if (generation == requestGeneration)
                        failure = statusCode + ": " + message;
                },
                timeoutSeconds);

            activeRequestCancels.Add(cancelRequest);
            yield return new WaitUntil(() => generation != requestGeneration
                                             || reply != null || failure != null);
            activeRequestCancels.Remove(cancelRequest);

            if (generation != requestGeneration)
                yield break;

            if (reply != null)
            {
                successCallback?.Invoke(Decode<SuperActions>(reply));
                yield break;
            }

            bool willRetry = attempt < maxAttempts;
            string warning = willRetry ? "Retry" : "Failed";
            AI_DebugCanvas.instance.AddWarning(warning);
            AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF8844>CardEffect "
                + warning + " (" + attempt + "/" + maxAttempts + "): "
                + failure + "</color></b>\n";

            if (!willRetry)
                yield break;
        }
    }


public void CancelForTurnReset()
    {
        requestGeneration++;
        foreach (var cancel in activeRequestCancels.ToArray())
            cancel?.Invoke();

        activeRequestCancels.Clear();
        StopAllCoroutines();
        actionDatas.Clear();
        processingCanvas?.EndProcessing();
    }
}
