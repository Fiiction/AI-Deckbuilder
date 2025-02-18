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
using NueGames.NueDeck.Scripts.Managers;

public class AI_DeckGenerator : MonoBehaviour
{
    public static AI_DeckGenerator instance;
    [SerializeField, TextArea(15,20)] private string prompt_description;
    //[SerializeField, TextArea(15,20)] private string prompt_jsonFormat;
    [SerializeField, TextArea(8,12)] private string prompt_effects;
    [Header("----- Basic Cards -----")]
    [SerializeField, TextArea(8,12)] private string prompt_basicCard1;
    [SerializeField, TextArea(8,12)] private string prompt_basicCard2;
    [SerializeField, TextArea(8,12)] private string prompt_basicCard3;
    [SerializeField, TextArea(8,12)] private string prompt_basicCard4;

    [Header("----- <color=#8888FF>Rare Cards</color> -----")] 
    [SerializeField] private int rareCardCnt = 6;
    [SerializeField, TextArea(8,12)] private string prompt_rareDescription;
    [SerializeField, TextArea(8,12)] private string prompt_rareCard;
    public List<CardData> rareCards = new List<CardData>();
    
    [Header("----- <color=#BB77FF>Epic Cards</color> -----")] 
    [SerializeField] private int epicCardCnt = 3;
    [SerializeField, TextArea(8,12)] private string prompt_epicDescription;
    [SerializeField, TextArea(8,12)] private string prompt_epicCard;
    public List<CardData> epicCards = new List<CardData>();
    
    [Header("----- <color=#FFAA55>Legend Cards</color> -----")] 
    [SerializeField] private int legendCardCnt = 3;
    [SerializeField, TextArea(8,12)] private string prompt_legendDescription;
    [SerializeField, TextArea(8,12)] private string prompt_legendCard;
    public List<CardData> legendCards = new List<CardData>();
    
    public int cardGenerated = 0;
    string[] numbers = new []{"1st", "2nd", "3rd", "4th", "5th", "6th",
        "7th", "8th", "9th", "10th", "11th", "12th"};
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("AI_DeckGenerator already exists.");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    private string reply1 = "";
    private string reply2 = "";
    IEnumerator GenerateStartDeckCoroutine()
    {
        string prompt1Send = prompt_description;
        prompt1Send = prompt1Send.Replace("##HeroName##", AI_IntegrationManager.instance.heroName);
        prompt1Send = prompt1Send.Replace("##HeroDesc##", AI_IntegrationManager.instance.heroDesc);
        
        reply1 = "";
        AI_IntegrationManager.instance.Request(prompt1Send, str =>{reply1 = str;});
        
        yield return new WaitWhile( () => reply1 == "");
        
        int n = AI_IntegrationManager.instance._conversationSoFar.Count;
        //Basic Card 1
        reply2 = "";
        AI_IntegrationManager.instance.Request(prompt_basicCard1, str =>{reply2 = str;}, true);
        yield return new WaitWhile( () => reply2 == "");
        CardDataReply cdr1 = AI_CardEffect.Decode<CardDataReply>(reply2);
        CardData cd1 = ConvertCardData(cdr1);
        for(int i = 0;i<4;i++)
            GameManager.Instance.AddCardToDeck(cd1);

        //Basic Card 2
        reply2 = "";
        AI_IntegrationManager.instance.Request(prompt_basicCard2, str =>{reply2 = str;}, true);
        yield return new WaitWhile( () => reply2 == "");
        CardDataReply cdr2 = AI_CardEffect.Decode<CardDataReply>(reply2);
        CardData cd2 = ConvertCardData(cdr2);
        for(int i = 0;i<4;i++)
            GameManager.Instance.AddCardToDeck(cd2);
        
        //Basic Card 3
        reply2 = "";
        AI_IntegrationManager.instance.Request(prompt_basicCard3, str =>{reply2 = str;}, true);
        yield return new WaitWhile( () => reply2 == "");
        CardDataReply cdr3 = AI_CardEffect.Decode<CardDataReply>(reply2);
        CardData cd3 = ConvertCardData(cdr3);
        for(int i = 0;i<2;i++)
            GameManager.Instance.AddCardToDeck(cd3);
        
        
        //Basic Card 4
        reply2 = "";
        AI_IntegrationManager.instance.Request(prompt_basicCard4, str =>{reply2 = str;}, true);
        yield return new WaitWhile( () => reply2 == "");
        CardDataReply cdr4 = AI_CardEffect.Decode<CardDataReply>(reply2);
        CardData cd4 = ConvertCardData(cdr4);
        for(int i = 0;i<2;i++)
            GameManager.Instance.AddCardToDeck(cd4);
        
        AI_IntegrationManager.instance._conversationSoFar =
            AI_IntegrationManager.instance._conversationSoFar.Take(n).ToList();
        AI_IntegrationManager.instance.Request(prompt_effects.Replace("##Rarity##", "basic"), str => { });
        //Debug.Log("Initial Deck Generation Complete!");
    }
    
    
    public void GenerateStartDeck()
    {
        StartCoroutine(GenerateStartDeckCoroutine());
    }
    
    
    IEnumerator GenerateRareCardsCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        
        string prompt1Send = prompt_rareDescription;
        prompt1Send = prompt1Send.Replace("##HeroName##", AI_IntegrationManager.instance.heroName);
        prompt1Send = prompt1Send.Replace("##HeroDesc##", AI_IntegrationManager.instance.heroDesc);
        prompt1Send = prompt1Send.Replace("##Total##", rareCardCnt.ToString());
        
        reply1 = "";
        AI_IntegrationManager.instance.CardQueueRequest(prompt1Send, str =>{reply1 = str;});
        
        yield return new WaitWhile( () => reply1 == "");
        
        int n = AI_IntegrationManager.instance._cardGenConversationSoFar.Count;
        for (int i = 0; i < rareCardCnt; i++)
        {
            reply2 = "";
            string prompt2Send = prompt_rareCard;
            prompt2Send = prompt2Send.Replace("##Number##", numbers[i]);
            prompt2Send = prompt2Send.Replace("##Total##", rareCardCnt.ToString());
            
            AI_IntegrationManager.instance.CardQueueRequest(prompt2Send, str =>{reply2 = str;}, true);
            
            yield return new WaitWhile( () => reply2 == "");
            
            CardDataReply cdr = AI_CardEffect.Decode<CardDataReply>(reply2);
            CardData cd = ConvertCardData(cdr, RarityType.Rare);
            rareCards.Add(cd);
            
        }
        
        AI_IntegrationManager.instance._cardGenConversationSoFar =
            AI_IntegrationManager.instance._cardGenConversationSoFar.Take(n).ToList();
        AI_IntegrationManager.instance.CardQueueRequest(prompt_effects.Replace("##Rarity##", "rare"), str => { });
        //Debug.Log("Initial Deck Generation Complete!");
    }
    
    public void GenerateRareCards()
    {
        //Debug.Break();
        StartCoroutine(GenerateRareCardsCoroutine());
    }
    
    IEnumerator GenerateEpicCardsCoroutine()
    {
        yield return new WaitForSeconds(6f);
        
        string prompt1Send = prompt_epicDescription;
        prompt1Send = prompt1Send.Replace("##HeroName##", AI_IntegrationManager.instance.heroName);
        prompt1Send = prompt1Send.Replace("##HeroDesc##", AI_IntegrationManager.instance.heroDesc);
        prompt1Send = prompt1Send.Replace("##Total##", epicCardCnt.ToString());
        
        reply1 = "";
        AI_IntegrationManager.instance.CardQueueRequest(prompt1Send, str =>{reply1 = str;});
        
        yield return new WaitWhile( () => reply1 == "");
        
        int n = AI_IntegrationManager.instance._cardGenConversationSoFar.Count;
        for (int i = 0; i < epicCardCnt; i++)
        {
            reply2 = "";
            string prompt2Send = prompt_epicCard;
            prompt2Send = prompt2Send.Replace("##Number##", numbers[i]);
            prompt2Send = prompt2Send.Replace("##Total##", epicCardCnt.ToString());
            
            AI_IntegrationManager.instance.CardQueueRequest(prompt2Send, str =>{reply2 = str;}, true);
            
            yield return new WaitWhile( () => reply2 == "");
            
            CardDataReply cdr = AI_CardEffect.Decode<CardDataReply>(reply2);
            CardData cd = ConvertCardData(cdr, RarityType.Epic);
            epicCards.Add(cd);
            
        }
        
        AI_IntegrationManager.instance._cardGenConversationSoFar =
            AI_IntegrationManager.instance._cardGenConversationSoFar.Take(n).ToList();
        AI_IntegrationManager.instance.CardQueueRequest(prompt_effects.Replace("##Rarity##", "epic"), str => { });
        //Debug.Log("Initial Deck Generation Complete!");
    }
    
    public void GenerateEpicCards()
    {
        //Debug.Break();
        StartCoroutine(GenerateEpicCardsCoroutine());
    }

    IEnumerator GenerateLegendCardsCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        string prompt1Send = prompt_legendDescription;
        prompt1Send = prompt1Send.Replace("##HeroName##", AI_IntegrationManager.instance.heroName);
        prompt1Send = prompt1Send.Replace("##HeroDesc##", AI_IntegrationManager.instance.heroDesc);
        prompt1Send = prompt1Send.Replace("##Total##", legendCardCnt.ToString());
        
        reply1 = "";
        AI_IntegrationManager.instance.CardQueueRequest(prompt1Send, str =>{reply1 = str;});
        
        yield return new WaitWhile( () => reply1 == "");
        
        int n = AI_IntegrationManager.instance._cardGenConversationSoFar.Count;
        for (int i = 0; i < legendCardCnt; i++)
        {
            reply2 = "";
            string prompt2Send = prompt_legendCard;
            prompt2Send = prompt2Send.Replace("##Number##", numbers[i]);
            prompt2Send = prompt2Send.Replace("##Total##", legendCardCnt.ToString());
            
            AI_IntegrationManager.instance.CardQueueRequest(prompt2Send, str =>{reply2 = str;}, true);
            
            yield return new WaitWhile( () => reply2 == "");
            
            CardDataReply cdr = AI_CardEffect.Decode<CardDataReply>(reply2);
            CardData cd = ConvertCardData(cdr, RarityType.Legendary);
            legendCards.Add(cd);
            
        }
        AI_IntegrationManager.instance._cardGenConversationSoFar =
            AI_IntegrationManager.instance._cardGenConversationSoFar.Take(n).ToList();
        AI_IntegrationManager.instance.CardQueueRequest(prompt_effects.Replace("##Rarity##", "legendary"), str => { });
        //Debug.Log("Initial Deck Generation Complete!");
    }
    
    public void GenerateLegendCards()
    {
        //Debug.Break();
        StartCoroutine(GenerateLegendCardsCoroutine());
    }
    
    
    // Update is called once per frame
    void Update()
    {
        
    }
    
    #region DataStructures

    public CardData ConvertCardData(CardDataReply r, RarityType rarity = RarityType.Common)
    {
        cardGenerated++;
        AI_IntegrationManager.instance.initInformation += "card designed.\n";
        CardData d = new CardData(r.cardName, r.description, r.manaCost, r.needTarget, rarity);
        AI_ImageGeneration.instance.GenerateCardSprite(r.prompt, s => d.SetCardSprite(s));
        return d;
    }
    public struct CardDataReply
    {
        public string cardName;
        public string description;
        public int manaCost;
        public bool needTarget;
        public string prompt;
    }
    
    #endregion
}
