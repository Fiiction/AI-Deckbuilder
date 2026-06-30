using NueGames.NueDeck.Scripts.Enums;
using AIDeckbuilder.CardRuntime;
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
    [SerializeField, TextArea(20,25)] private string prompt_description;
    [SerializeField, TextArea(8,12)] private string prompt_stableEnhance;
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
        string prompt1Send = BuildPlanningPrompt(prompt_description);
        prompt1Send = prompt1Send.Replace("##HeroName##", AI_IntegrationManager.instance.heroName);
        prompt1Send = prompt1Send.Replace("##HeroDesc##", AI_IntegrationManager.instance.heroDesc);
        prompt1Send = prompt1Send.Replace("##StableEnhance##", prompt_stableEnhance);
        
        reply1 = "";
        AI_IntegrationManager.instance.Request(prompt1Send, str =>{reply1 = str;});
        
        yield return new WaitWhile( () => reply1 == "");
        
        int n = AI_IntegrationManager.instance._conversationSoFar.Count;
        //Basic Card 1
        reply2 = "";
        AI_IntegrationManager.instance.Request(BuildExecutableCardPrompt(prompt_basicCard1), str =>{reply2 = str;}, true, typeof(CardDataReply));
        yield return new WaitWhile( () => reply2 == "");
        CardDataReply cdr1 = AI_CardEffect.Decode<CardDataReply>(reply2);
        CardData cd1 = ConvertCardData(cdr1);
        for(int i = 0;i<4;i++)
            GameManager.Instance.AddCardToDeck(cd1);

        //Basic Card 2
        reply2 = "";
        AI_IntegrationManager.instance.Request(BuildExecutableCardPrompt(prompt_basicCard2), str =>{reply2 = str;}, true, typeof(CardDataReply));
        yield return new WaitWhile( () => reply2 == "");
        CardDataReply cdr2 = AI_CardEffect.Decode<CardDataReply>(reply2);
        CardData cd2 = ConvertCardData(cdr2);
        for(int i = 0;i<4;i++)
            GameManager.Instance.AddCardToDeck(cd2);
        
        //Basic Card 3
        reply2 = "";
        AI_IntegrationManager.instance.Request(BuildExecutableCardPrompt(prompt_basicCard3), str =>{reply2 = str;}, true, typeof(CardDataReply));
        yield return new WaitWhile( () => reply2 == "");
        CardDataReply cdr3 = AI_CardEffect.Decode<CardDataReply>(reply2);
        CardData cd3 = ConvertCardData(cdr3);
        for(int i = 0;i<2;i++)
            GameManager.Instance.AddCardToDeck(cd3);
        
        
        //Basic Card 4
        reply2 = "";
        AI_IntegrationManager.instance.Request(BuildExecutableCardPrompt(prompt_basicCard4), str =>{reply2 = str;}, true, typeof(CardDataReply));
        yield return new WaitWhile( () => reply2 == "");
        CardDataReply cdr4 = AI_CardEffect.Decode<CardDataReply>(reply2);
        CardData cd4 = ConvertCardData(cdr4);
        for(int i = 0;i<2;i++)
            GameManager.Instance.AddCardToDeck(cd4);
        
        AI_IntegrationManager.instance._conversationSoFar =
            AI_IntegrationManager.instance._conversationSoFar.Take(n).ToList();
        //Debug.Log("Initial Deck Generation Complete!");
    }
    
    
public void GenerateStartDeck()
    {
        CardStatusRuntime.ClearDefinitions();
        CardDesignContext.ClearGeneratedCards();
        StartCoroutine(GenerateStartDeckCoroutine());
    }
    
    
    IEnumerator GenerateRareCardsCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        
        string prompt1Send = BuildPlanningPrompt(prompt_rareDescription);
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
            string prompt2Send = BuildExecutableCardPrompt(prompt_rareCard);
            prompt2Send = prompt2Send.Replace("##Number##", numbers[i]);
            prompt2Send = prompt2Send.Replace("##Total##", rareCardCnt.ToString());
            
            AI_IntegrationManager.instance.CardQueueRequest(prompt2Send, str =>{reply2 = str;}, true, typeof(CardDataReply));
            
            yield return new WaitWhile( () => reply2 == "");
            
            CardDataReply cdr = AI_CardEffect.Decode<CardDataReply>(reply2);
            CardData cd = ConvertCardData(cdr, RarityType.Rare);
            rareCards.Add(cd);
            
        }
        
        AI_IntegrationManager.instance._cardGenConversationSoFar =
            AI_IntegrationManager.instance._cardGenConversationSoFar.Take(n).ToList();
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
        
        string prompt1Send = BuildPlanningPrompt(prompt_epicDescription);
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
            string prompt2Send = BuildExecutableCardPrompt(prompt_epicCard);
            prompt2Send = prompt2Send.Replace("##Number##", numbers[i]);
            prompt2Send = prompt2Send.Replace("##Total##", epicCardCnt.ToString());
            
            AI_IntegrationManager.instance.CardQueueRequest(prompt2Send, str =>{reply2 = str;}, true, typeof(CardDataReply));
            
            yield return new WaitWhile( () => reply2 == "");
            
            CardDataReply cdr = AI_CardEffect.Decode<CardDataReply>(reply2);
            CardData cd = ConvertCardData(cdr, RarityType.Epic);
            epicCards.Add(cd);
            
        }
        
        AI_IntegrationManager.instance._cardGenConversationSoFar =
            AI_IntegrationManager.instance._cardGenConversationSoFar.Take(n).ToList();
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

        string prompt1Send = BuildPlanningPrompt(prompt_legendDescription);
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
            string prompt2Send = BuildExecutableCardPrompt(prompt_legendCard);
            prompt2Send = prompt2Send.Replace("##Number##", numbers[i]);
            prompt2Send = prompt2Send.Replace("##Total##", legendCardCnt.ToString());
            
            AI_IntegrationManager.instance.CardQueueRequest(prompt2Send, str =>{reply2 = str;}, true, typeof(CardDataReply));
            
            yield return new WaitWhile( () => reply2 == "");
            
            CardDataReply cdr = AI_CardEffect.Decode<CardDataReply>(reply2);
            CardData cd = ConvertCardData(cdr, RarityType.Legendary);
            legendCards.Add(cd);
            
        }
        AI_IntegrationManager.instance._cardGenConversationSoFar =
            AI_IntegrationManager.instance._cardGenConversationSoFar.Take(n).ToList();
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

public CardData ConvertCardData(CardDataReply reply, RarityType rarity = RarityType.Common)
    {
        var compileResult = CardProgramCompiler.Compile(reply);
        if (!compileResult.Success)
            throw new InvalidOperationException("Card program validation failed: "
                                                + string.Join(" | ", compileResult.Errors));

        if (!CardStatusRuntime.ValidateProgramDefinitions(compileResult.Program, out string statusError))
            throw new InvalidOperationException(statusError);

        cardGenerated++;
        AI_IntegrationManager.instance.initInformation += "executable card designed.\n";
        var cardData = ScriptableObject.CreateInstance<CardData>();
        string mechanicalDescription = CardProgramTextRenderer.Build(compileResult.Program, reply.description);
        cardData.Initialize(reply.cardName,
            string.IsNullOrWhiteSpace(mechanicalDescription) ? reply.description : mechanicalDescription,
            reply.manaCost, compileResult.NeedsSelectedEnemy, rarity, compileResult.Program);

        CardDesignContext.RegisterGeneratedCard(reply);
        CardRuntimeDiagnostics.LogGeneration("Card", reply.cardName, reply);
        foreach (var status in compileResult.Program.statusDefinitions ?? new List<CardStatusDefinitionData>())
            CardRuntimeDiagnostics.LogGeneration("Status", status.id, status);

        AI_ImageGeneration.instance.GenerateCardSprite(reply.prompt, sprite => cardData.SetCardSprite(sprite));
        return cardData;
    }
    public sealed class CardDataReply : GeneratedCardSpec
    {
    }
    
    #endregion


    private static string BuildExecutableCardPrompt(string designPrompt)
    {
        var sample = new CardDataReply
        {
            schemaVersion = CardProgramData.CurrentSchemaVersion,
            cardName = "Arcane Strike",
            description = "Drive an arcane fist through the air, dealing 8 damage to the selected enemy.",
            manaCost = 1,
            prompt = "arcane impact, dynamic magical strike",
            tags = new List<string> { "attack", "magic" },
            effects = new List<CardEffectData>
            {
                new CardEffectData { op = "damage", target = "selected_enemy", value = 8 }
            },
            statuses = new List<CardStatusDefinitionData>()
        };

        return StripLegacyJsonInstructions(designPrompt) + "\n\n"
             + "Return one executable card using this exact schema:\n"
             + JsonConvert.SerializeObject(sample, Formatting.Indented) + "\n\n"
             + CardEffectCatalog.BuildPromptReference()
             + CardDesignContext.BuildHeroPlanReference() + "\n"
             + CardDesignContext.BuildOwnedDeckReference() + "\n"
             + CardDesignContext.BuildGeneratedPoolReference() + "\n"
             + CardStatusRuntime.BuildDefinitionReference() + "\n"
             + "PRESENTATION AND INTERACTION GUIDANCE:\n"
             + "- Keep description brief: normally write exactly one short sentence, ideally 18-32 English words. "
             + "Use a second short sentence only when a trigger or condition would otherwise be ambiguous.\n"
             + "- Include every gameplay-relevant value, target, condition, duration and trigger, but remove "
             + "backstory, design rationale, repeated wording, labels, bullet points and parenthetical explanations.\n"
             + "- Weave flavor directly into the mechanical sentence through vivid verbs and nouns; never append "
             + "a separate lore sentence or split flavor and rules into separate sections.\n"
             + "- The executable effects/statuses are authoritative. The description must not omit, "
             + "invent, or contradict any mechanic in them.\n"
             
             + "- Interaction is a primary design goal: prefer cards that apply, amplify, spread, transform, "
             + "consume, cash out, or conditionally react to statuses and tags already present in the owned deck.\n"
             + "- Each generated set should contain setup cards, payoff cards, and at least one bridge between "
             + "two existing mechanics; avoid isolated one-card keywords unless they define a major archetype.\n"
             + "- Lifesteal is not an op. Express it as separate damage and heal effects, or as a character/status "
             + "trigger that heals after damage.\n"
+ "- Give every status a clear displayName, a thematic color, and fully described local triggers.\n"
             
             + "- In status triggers, status_owner is the bearer and effect_source is the original applier. "
             + "Poison, virus, burn, regeneration, and other effects that change their bearer must target "
             + "status_owner, never self or effect_source.\n"
+ "- Poison and Strength are not built-in mechanics; if used, define them exactly like any other status.\n"
             + "- Prefer reusing an existing status id instead of inventing a nearly identical status.\n"
             + "- Design coherent card families: some cards apply a status, while other cards consume it, "
             + "scale from its stacks, remove it for a payoff, or check it as a condition.\n"
             + "- Use valueFormula with selected_target to read the originally chosen enemy while another effect "
             + "resolves on other_enemies. Effects resolve in order, so later effects can read earlier changes.\n"
             + "- For spread/copy designs, modify selected_enemy first, then target other_enemies and read "
             + "status_stacks from selected_target. Use all_enemies only when the selected enemy should be hit again.\n"
             + "- Use set_status for an exact final stack count, apply_status to add, and remove_status to consume.\n"
             + "- Dynamic values may use current_health, max_health, missing_health, block, "
             + "cards_played_this_turn, opponent_count, ally_count, or status_stacks.\n"
             + "- Use selected_target/effect_target/source/status_owner references and health, stack, "
             + "party-size, or previous-card-tag conditions to create card and status synergy.\n"
             + "- Use meaningful shared card tags so later cards can react through last_card_has_tag.\n"
             + "- A persistent hero status that acts at the start of its owner's turn uses "
             + "eventType turn_start and eventRole owner_turn.\n"
             + "- A status on an enemy that acts immediately before that enemy's action uses "
             + "eventType before_enemy_action and eventRole owner_source.\n"
             + "- A status on the hero that reacts before an enemy acts can use "
             + "eventType before_enemy_action and eventRole owner_target.\n"
             + "Do not force synergy onto every card, but make rare and higher-rarity cards more likely "
             + "to connect with earlier cards and statuses.\n"
             + "Return JSON only. Every designed mechanic must be represented by effects or statuses.";
    }

    private static string StripLegacyJsonInstructions(string designPrompt)
    {
        if (string.IsNullOrWhiteSpace(designPrompt))
            return string.Empty;

        const string marker = "Please reply with following json format";
        int markerIndex = designPrompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return (markerIndex >= 0 ? designPrompt.Substring(0, markerIndex) : designPrompt).Trim();
    }



private static string BuildPlanningPrompt(string prompt)
    {
        return prompt + "\n\n" + CardDesignContext.BuildHeroPlanReference()
             + "\n" + CardDesignContext.BuildOwnedDeckReference()
             + "\n" + CardDesignContext.BuildGeneratedPoolReference()
             + "\nPlan explicit setup/payoff/bridge relationships before proposing cards. "
             + "Prioritize interactions with existing statuses and tags over isolated effects.";
    }
}
