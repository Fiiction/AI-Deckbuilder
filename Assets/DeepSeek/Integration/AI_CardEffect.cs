using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Characters;
using NueGames.NueDeck.Scripts.Data.Collection;
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

    
    [SerializeField, TextArea(15,20)] private string prompt1_withTarget;
    [SerializeField, TextArea(15,20)] private string prompt1_noTarget;
    [SerializeField, TextArea(25,35)] private string prompt2;
    [SerializeField, TextArea(15,20)] private string prompt3_Normal;
    [SerializeField, TextArea(15,20)] private string prompt3_CustomEffect;



    public string cardName;
    public string cardDesc;
    public string userName;
    public string userDesc;
    public string targetName;
    public string targetDesc;
    public string userStatusStr;
    public string targetStatusStr;
    public string prompt1Sent;

    private string reply1 = "";
    private string reply2 = "";
    
    public List<CardActionData> actionDatas = new();
    
    IEnumerator CardUseCoroutine(CardBase card, CharacterBase self, CharacterBase targetCharacter,
        Action<List<CardActionData>> callback)
    {
        reply1 = "";
        reply2 = "";
        actionDatas = new();
        
        cardName = card.CardData.CardName;
        cardDesc = card.CardData.CardDescription;
        if(! self is AllyBase)
            Debug.LogError($"{nameof(self)} is not an AllyBase");
        var userData = (self as AllyBase).AllyCharacterData;

        userName = userData.CharacterName;
        userDesc = userData.CharacterDescription;
        userStatusStr = CharacterStatusString(self);
        
        
        if(targetCharacter != null)
        {
            var targetData = (targetCharacter as EnemyBase).EnemyCharacterData;
            targetName = targetData.CharacterName;
            targetDesc = targetData.CharacterDescription;
            targetStatusStr = CharacterStatusString(targetCharacter);
            
            prompt1Sent = prompt1_withTarget.Replace("##PlayerDesc##", userDesc);
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
            prompt1Sent = prompt1Sent.Replace("##PlayerCustomStatus##", userStatusStr);
            prompt1Sent = prompt1Sent.Replace("##CardName##", cardName);
            prompt1Sent = prompt1Sent.Replace("##CardDesc##", cardDesc);
            prompt1Sent = prompt1Sent.Replace("##ManaSpent##", card.CardData.ManaCost.ToString());
        }
        AI_IntegrationManager.instance.Request(prompt1Sent, str =>{reply1 = str;});
        
        yield return new WaitWhile( () => reply1 == "");
        
        AI_IntegrationManager.instance.Request(prompt2, str =>{reply2 = str;});
        
        yield return new WaitWhile( () => reply2 == "");
        
        ActionArray actionArray = Decode<ActionArray>(reply2);

        foreach (var i in actionArray.effects)
        {
            string reply3 = "";
            string prompt3Sent;
            if (i == "Add Custom Status")
            {
                prompt3Sent = prompt3_CustomEffect;
                AI_IntegrationManager.instance.Request(prompt3Sent, str =>{reply3 = str;});
                yield return new WaitWhile( () => reply3 == "");
                CustomEffectParams p = Decode<CustomEffectParams>(reply3);
                var ad = new CardActionData(CardActionType.CustomEffect, StringToActionTarget(p.target),
                    p.value, p.buffname);
                actionDatas.Add(ad);
            }
            else
            {
                CardActionType actionType = StringToActionType(i);
                prompt3Sent = prompt3_Normal.Replace("##EffectName##", i);
                prompt3Sent = prompt3Sent.Replace("##ValueMeaning##", ValueMeaning(actionType));
                AI_IntegrationManager.instance.Request(prompt3Sent, str =>{reply3 = str;});
                yield return new WaitWhile( () => reply3 == "");
                ActionParams p = Decode<ActionParams>(reply3);
                var ad = new CardActionData(actionType, StringToActionTarget(p.target),
                    p.value, "");
                actionDatas.Add(ad);
            }
            
        }
        callback?.Invoke(actionDatas);
    }
    
    
    public void CardUse(CardBase card, CharacterBase self, CharacterBase targetCharacter, 
        Action<List<CardActionData>> callback)
    {
        StartCoroutine(CardUseCoroutine(card, self, targetCharacter, callback));
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

        if (cha.characterStats.Effects.Count == 0)
            return "";
        string s = ", with ";
        foreach (var i in cha.characterStats.Effects)
        {
            if(i.Value.effectValue == 1)
                s += "1 stack of " + i.Key + ", ";
            else
                s += i.Value.effectValue +" stacks of " + i.Key + ", ";
        }
        return s;
    }
    
    public static CardActionType StringToActionType(string str)
    {
        switch (str)
        {
           case "Deal Damage": return CardActionType.Attack;
           case "Heal": return CardActionType.Heal;
           case "Add Block": return CardActionType.Block;
           case "Increase Strength": return CardActionType.IncreaseStrength;
           case "Draw": return CardActionType.Draw;
           case "Gain Mana": return CardActionType.EarnMana;
           case "Steal Life": return CardActionType.LifeSteal;
           case "Stun": return CardActionType.Stun;
           case "Destroy the Card": return CardActionType.Exhaust;
           case "Add Custom Status": return CardActionType.CustomEffect;
           default: 
               Debug.LogWarning("Unknown card action type: " + str);
               return CardActionType.Attack;
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
            case CardActionType.Exhaust: return "Destroy the Card";
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
        switch (str)
        {
            case "self": return ActionTargetType.Self;
            case "targetEnemy": return ActionTargetType.Enemy;
            case "allEnemies": return ActionTargetType.AllEnemies;
            default:
                Debug.LogWarning("Unknown card action target: " + str);
                return ActionTargetType.AllEnemies;
        }
    }

    public static string ActionTargetToString(ActionTargetType actionTargetType)
    {
        switch (actionTargetType)
        {
            case ActionTargetType.Self: return "self";
            case ActionTargetType.Enemy: return "targetEnemy";
            case ActionTargetType.AllEnemies: return "allEnemies";
            default:
                Debug.LogWarning("Unknown card action target: " + actionTargetType);
                return "allEnemies";
        }
    }
    
    #endregion
}
