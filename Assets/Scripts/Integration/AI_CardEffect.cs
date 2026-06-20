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
    IEnumerator CardUseCoroutine(CardBase card, CharacterBase self, CharacterBase targetCharacter,
        Action<List<CardActionData>> callback)
    {
        ActionArray actionArray;
        reply1 = "";
        reply2 = "";
        actionDatas = new();
        
        cardName = card.CardData.CardName;
        cardDesc = card.CardData.CardDescription;
        var userData = (self as AllyBase).AllyCharacterData;

        userName = userData.CharacterName;
        userDesc = userData.CharacterDescription;
        userStatusStr = CharacterStatusString(self);
        
        processingCanvas.StartProcessing("Card Processing:");
        cardInTurnCnt++;
        
        if(targetCharacter != null)
        {
            var targetData = (targetCharacter as EnemyBase).EnemyCharacterData;
            targetName = targetData.CharacterName;
            targetDesc = targetData.CharacterDescription;
            targetStatusStr = CharacterStatusString(targetCharacter);
            
            prompt1Sent = prompt1_withTarget.Replace("##PlayerDesc##", userDesc);
            prompt1Sent = prompt1Sent.Replace("##PlayerName##", userName);
            prompt1Sent = prompt1Sent.Replace("##PlayerCustomStatus##", userStatusStr);
            prompt1Sent = prompt1Sent.Replace("##CardName##", cardName);
            prompt1Sent = prompt1Sent.Replace("##CardDesc##", cardDesc);
            prompt1Sent = prompt1Sent.Replace("##ManaSpent##", card.CardData.ManaCost.ToString());
            
            prompt1Sent = prompt1Sent.Replace("##EnemyName##", targetName);
            prompt1Sent = prompt1Sent.Replace("##EnemyDesc##", targetDesc);
            prompt1Sent = prompt1Sent.Replace("##EnemyCustomStatus##", targetStatusStr);
        }
        else
        {
            prompt1Sent = prompt1_noTarget.Replace("##PlayerDesc##", userDesc);
            prompt1Sent = prompt1Sent.Replace("##PlayerName##", userName);
            prompt1Sent = prompt1Sent.Replace("##PlayerCustomStatus##", userStatusStr);
            prompt1Sent = prompt1Sent.Replace("##CardName##", cardName);
            prompt1Sent = prompt1Sent.Replace("##CardDesc##", cardDesc);
            prompt1Sent = prompt1Sent.Replace("##ManaSpent##", card.CardData.ManaCost.ToString());
        }
        prompt1Sent = prompt1Sent.Replace("##Specific##",
            (cardInTurnCnt <= 1 ? prompt_specific : ""));
        
        AI_IntegrationManager.instance.Request(prompt1Sent, str =>{reply2 = str;}, true, typeof(ActionArray));
 
        yield return new WaitWhile( () => reply2 == "");
        actionArray = Decode<ActionArray>(reply2);

        for(int idx = 0; idx < actionArray.effects.Length;idx++)
        {
            var i = actionArray.effects[idx];
            if (StringToActionType(i) == CardActionType.Unknown)
            {
                Debug.Log("<b><color=#FF2222>Correction!</b></color>");
                AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
                AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
                AI_DebugCanvas.instance.AddWarning("Correction!");
                AI_IntegrationManager.instance.pendingPrompts += correctionPrompt_effectType;
                StartCoroutine(CardUseCoroutine(card, self, targetCharacter, callback));
                yield break;
            }
        }

        for(int idx = 0; idx < actionArray.effects.Length;idx++)
        {
            var i = actionArray.effects[idx];
            string reply3 = "";
            string prompt3Sent;
            if (i == "Add Custom Status")
            {
                prompt3Sent = prompt3_CustomEffect.Replace("##No##",numbers[idx]);
                if (targetCharacter == null)
                    prompt3Sent = prompt3Sent.Replace("or \"targetEnemy\" ", "");
                AI_IntegrationManager.instance.Request(prompt3Sent, str =>{reply3 = str;}, true, typeof(CustomEffectParams));
                yield return new WaitWhile( () => reply3 == "");
                CustomEffectParams p = Decode<CustomEffectParams>(reply3);
                
                if (StringToActionTarget(p.target) == ActionTargetType.Unknown)
                {
                    Debug.Log("<b><color=#FF2222>Correction!</b></color>");
                    AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
                    AI_DebugCanvas.instance.AddWarning("Correction!");
                    AI_IntegrationManager.instance.pendingPrompts += targetCharacter != null?
                        correctionPrompt_withTarget: correctionPrompt_noTarget;
                    StartCoroutine(CardUseCoroutine(card, self, targetCharacter, callback));
                    yield break;
                }
                
                var ad = new CardActionData(CardActionType.CustomEffect, StringToActionTarget(p.target),
                    p.value, p.buffname.ToLower());
                actionDatas.Add(ad);
            }
            else
            {
                CardActionType actionType = StringToActionType(i);
                prompt3Sent = prompt3_Normal.Replace("##EffectName##", i)
                    .Replace("##No##",numbers[idx]);
                if (targetCharacter == null)
                    prompt3Sent = prompt3Sent.Replace("or \"targetEnemy\" ", "");
                prompt3Sent = prompt3Sent.Replace("##ValueMeaning##", ValueMeaning(actionType));
                AI_IntegrationManager.instance.Request(prompt3Sent, str =>{reply3 = str;}, true, typeof(ActionParams));
                yield return new WaitWhile( () => reply3 == "");
                ActionParams p = Decode<ActionParams>(reply3);
                
                if (StringToActionTarget(p.target) == ActionTargetType.Unknown)
                {
                    Debug.Log("<b><color=#FF2222>Correction!</b></color>");
                    AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
                    AI_DebugCanvas.instance.AddWarning("Correction!");
                    AI_IntegrationManager.instance.pendingPrompts += targetCharacter != null?
                        correctionPrompt_withTarget: correctionPrompt_noTarget;
                    StartCoroutine(CardUseCoroutine(card, self, targetCharacter, callback));
                    yield break;
                }
                var ad = new CardActionData(actionType, StringToActionTarget(p.target),
                    p.value, "");
                actionDatas.Add(ad);
            }
            
        }
        processingCanvas.EndProcessing();
        callback?.Invoke(actionDatas);
    }
    
    public void CardUse(CardBase card, CharacterBase self, CharacterBase targetCharacter, 
        Action<List<CardActionData>> callback)
    {
        StartCoroutine(CardUseCoroutine(card, self, targetCharacter, callback));
    }

    IEnumerator AllyTurnStartEffectsCoroutine(CharacterBase self, Action callback)
    {
        yield return new WaitUntil(() => AI_IntegrationManager.instance.gameStartPending == false);
        Debug.Log("<b>Turn Start</b>");
        cardInTurnCnt = 0;
        processingCanvas.StartProcessing("Turn Start Processing:");
        float tick = Time.time;
        //Debug.Log("<color=cyan><b>Ally Turn Start</b></color>");
        reply1 = "";
        reply2 = "";
        actionDatas = new();
        var userData = (self as AllyBase).AllyCharacterData;

        userName = userData.CharacterName;
        userDesc = userData.CharacterDescription;
        userStatusStr = CharacterStatusString(self);

        string enemiesDesc = "";
        foreach (var e in CombatManager.Instance.CurrentEnemiesList)
        {
            enemiesDesc += " - " + e.EnemyCharacterData.CharacterName
                                 + ", " + CharacterStatusString(e) + "\n";
        }
        
        prompt1Sent = prompt_StartTurn1.Replace("##PlayerDesc##", userDesc)
            .Replace("##PlayerCustomStatus##", userStatusStr)
            .Replace("##No##", numbers[turnCnt])
            .Replace("##EnemiesDesc##", enemiesDesc)
            .Replace("##Specific##", prompt_specific);
         
        turnCnt++;
        
        AI_IntegrationManager.instance.Request(prompt1Sent, str =>{reply1 = str;}, true, typeof(ActionArray));
        
        yield return new WaitWhile( () => reply1 == "");
        
        ActionArray actionArray = Decode<ActionArray>(reply1);
        
        for(int idx = 0; idx < actionArray.effects.Length;idx++)
        {
            var i = actionArray.effects[idx];
            if (StringToActionType(i) == CardActionType.Unknown)
            {
                Debug.Log("<b><color=#FF2222>Correction!</b></color>");
                AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
                AI_DebugCanvas.instance.AddWarning("Correction!");
                AI_IntegrationManager.instance.pendingPrompts += correctionPrompt_effectType;
                StartCoroutine(AllyTurnStartEffectsCoroutine(self, callback));
                yield break;
            }
        }
        
        for(int idx = 0; idx < actionArray.effects.Length;idx++)
        {
            var i = actionArray.effects[idx];
            string reply3 = "";
            string prompt3Sent;
            if (i == "Add Custom Status")
            {
                prompt3Sent = prompt3_CustomEffect.Replace("##No##",numbers[idx]);
                prompt3Sent = prompt3Sent.Replace("or \"targetEnemy\" ", "");
                AI_IntegrationManager.instance.Request(prompt3Sent, str =>{reply3 = str;}, true, typeof(CustomEffectParams));
                yield return new WaitWhile( () => reply3 == "");
                CustomEffectParams p = Decode<CustomEffectParams>(reply3);
                
                if (StringToActionTarget(p.target) == ActionTargetType.Unknown)
                {
                    Debug.Log("<b><color=#FF2222>Correction!</b></color>");
                    AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
                    AI_DebugCanvas.instance.AddWarning("Correction!");
                    AI_IntegrationManager.instance.pendingPrompts += correctionPrompt_startTurn;
                    StartCoroutine(AllyTurnStartEffectsCoroutine(self, callback));
                    yield break;
                }
                var ad = new CardActionData(CardActionType.CustomEffect, StringToActionTarget(p.target),
                    p.value, p.buffname.ToLower());
                actionDatas.Add(ad);
            }
            else
            {
                CardActionType actionType = StringToActionType(i);
                prompt3Sent = prompt3_Normal.Replace("##EffectName##", i)
                    .Replace("##No##",numbers[idx]);
                prompt3Sent = prompt3Sent.Replace("or \"targetEnemy\" ", "");
                prompt3Sent = prompt3Sent.Replace("##ValueMeaning##", ValueMeaning(actionType));
                AI_IntegrationManager.instance.Request(prompt3Sent, str =>{reply3 = str;}, true, typeof(ActionParams));
                yield return new WaitWhile( () => reply3 == "");
                ActionParams p = Decode<ActionParams>(reply3);
                
                if (StringToActionTarget(p.target) == ActionTargetType.Unknown)
                {
                    Debug.Log("<b><color=#FF2222>Correction!</b></color>");
                    AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
                    AI_DebugCanvas.instance.AddWarning("Correction!");
                    AI_IntegrationManager.instance.pendingPrompts += correctionPrompt_startTurn;
                    StartCoroutine(AllyTurnStartEffectsCoroutine(self, callback));
                    yield break;
                }
                
                var ad = new CardActionData(actionType, StringToActionTarget(p.target),
                    p.value, "");
                actionDatas.Add(ad);
            }
        }

        processingCanvas.EndProcessing();
        Debug.Log("<color=cyan><b>Ally Turn Start: "+actionDatas.Count+" "+ (Time.time-tick).ToString("0.##") + "</b></color>");

        // --- Virtual Card Use ---
        foreach (var i in actionDatas)
        {
            var targetList = CardBase.DetermineTargets(self,null, 
                CombatManager.Instance.CurrentEnemiesList,
                CombatManager.Instance.CurrentAlliesList, i);
            Debug.Log("Action: " + i.CardActionType);
            foreach (var target in targetList)
                CardActionProcessor.GetAction(i.CardActionType)
                    .DoAction(new CardActionParameters(i.ActionValue,
                        target,self,null, null, i.StrParameter));
            yield return new WaitForSeconds(1f);
        }
        
        
        callback?.Invoke();
    }
    public void AllyTurnStartEffects(CharacterBase self, Action callback)
    {
        
        StartCoroutine(AllyTurnStartEffectsCoroutine(self, callback));
    }

    IEnumerator EnemyTurnCoroutine(CharacterBase enemySelf, string enemyActionName, string enemyActionDesc, Action callback)
    {
        
        processingCanvas.StartProcessing("Enemy Action Processing:");
        float tick = Time.time;
        //Debug.Log("<color=cyan><b>Ally Turn Start</b></color>");
        reply1 = "";
        reply2 = "";
        actionDatas = new();
        
        var userData = (enemySelf as EnemyBase).EnemyCharacterData;

        userName = userData.CharacterName;
        userDesc = userData.CharacterDescription;
        userStatusStr = CharacterStatusString(enemySelf);
        prompt1Sent = prompt_EnemyTurn1.Replace("##EnemyDesc##", userDesc)
            .Replace("##EnemyCustomStatus##", userStatusStr)
            .Replace("##EnemyName##", userName)
            .Replace("##EnemyActionName##", (enemySelf as EnemyBase).NextAbility.Name)
            .Replace("##EnemyActionDesc##", (enemySelf as EnemyBase).NextAbility.Desc);
        
        AI_IntegrationManager.instance.Request(prompt1Sent, str =>{reply1 = str;}, true, typeof(ActionArray));
        
        yield return new WaitWhile( () => reply1 == "");
        
        ActionArray actionArray = Decode<ActionArray>(reply1);
        
        for(int idx = 0; idx < actionArray.effects.Length;idx++)
        {
            var i = actionArray.effects[idx];
            if (StringToActionType(i) == CardActionType.Unknown)
            {
                Debug.Log("<b><color=#FF2222>Correction!</b></color>");
                AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
                AI_DebugCanvas.instance.AddWarning("Correction!");
                AI_IntegrationManager.instance.pendingPrompts += correctionPrompt_effectType;
                StartCoroutine( EnemyTurnCoroutine(enemySelf, enemyActionName, enemyActionDesc, callback));
                yield break;
            }
        }
        for(int idx = 0; idx < actionArray.effects.Length;idx++)
        {
            var i = actionArray.effects[idx];
            string reply3 = "";
            string prompt3Sent;
            if (i == "Add Custom Status")
            {
                prompt3Sent = prompt3_CustomEffect_Enemy.Replace("##No##",numbers[idx]);
                AI_IntegrationManager.instance.Request(prompt3Sent, str => { reply3 = str; }, true, typeof(CustomEffectParams));
                yield return new WaitWhile(() => reply3 == "");
                CustomEffectParams p = Decode<CustomEffectParams>(reply3);
                
                if (StringToActionTarget(p.target) == ActionTargetType.Unknown)
                {
                    Debug.Log("<b><color=#FF2222>Correction!</b></color>");
                    AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
                    AI_DebugCanvas.instance.AddWarning("Correction!");
                    AI_IntegrationManager.instance.pendingPrompts += correctionPrompt_enemy;
                    StartCoroutine( EnemyTurnCoroutine(enemySelf, enemyActionName, enemyActionDesc, callback));
                    yield break;
                }
                
                var ad = new CardActionData(CardActionType.CustomEffect, StringToActionTarget(p.target),
                    p.value, p.buffname.ToLower());
                actionDatas.Add(ad);
            }
            else
            {
                CardActionType actionType = StringToActionType(i);
                prompt3Sent = prompt3_Normal_Enemy.Replace("##EffectName##", i)
                    .Replace("##No##",numbers[idx]);
                prompt3Sent = prompt3Sent.Replace("##ValueMeaning##", ValueMeaning(actionType));
                AI_IntegrationManager.instance.Request(prompt3Sent, str => { reply3 = str; }, true, typeof(ActionParams));
                yield return new WaitWhile(() => reply3 == "");
                ActionParams p = Decode<ActionParams>(reply3);
                
                if (StringToActionTarget(p.target) == ActionTargetType.Unknown)
                {
                    Debug.Log("<b><color=#FF2222>Correction!</b></color>");
                    AI_IntegrationManager.instance.debugStr += "\n<b><color=#FF2222>Correction!</b></color>\n";
                    AI_DebugCanvas.instance.AddWarning("Correction!");
                    AI_IntegrationManager.instance.pendingPrompts += correctionPrompt_enemy;
                    StartCoroutine( EnemyTurnCoroutine(enemySelf, enemyActionName, enemyActionDesc, callback));
                    yield break;
                }

                var ad = new CardActionData(actionType, StringToActionTarget(p.target),
                    p.value, "");
                actionDatas.Add(ad);
            }

        }

        Debug.Log("<color=#ff7799><b>EnemyTurn: " + actionDatas.Count + " " +
                  (Time.time - tick).ToString("0.##") + "</b></color>");
        
        processingCanvas.EndProcessing();
        // --- Virtual Card Use ---
        foreach (var i in actionDatas)
        {
            var targetList = CardBase.DetermineTargets(enemySelf,null, 
                CombatManager.Instance.CurrentEnemiesList,
                CombatManager.Instance.CurrentAlliesList, i);
            Debug.Log("Action: " + i.CardActionType);
            foreach (var target in targetList)
                CardActionProcessor.GetAction(i.CardActionType)
                    .DoAction(new CardActionParameters(i.ActionValue,
                        target,enemySelf,null, null, i.StrParameter));
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
        SuperActionParams[] effects;
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
        string s = str.ToLower();
        s = s.Split(':')[0].Trim();
        switch (s)
        {
            case "self": return ActionTargetType.Hero;
            case "targetenemy": return ActionTargetType.Enemy;
            case "allenemies": return ActionTargetType.AllEnemies;
            case "hero" : return ActionTargetType.Hero;
            case "enemyself": return ActionTargetType.EnemySelf;
            default:
                Debug.LogError("Unknown card action target: " + str);
                return ActionTargetType.AllEnemies;
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
}
