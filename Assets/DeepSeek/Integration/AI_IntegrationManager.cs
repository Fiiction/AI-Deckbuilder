using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class AI_IntegrationManager : MonoBehaviour
{
    public static AI_IntegrationManager instance;

    public string heroName = "Hanzo";
    public string heroDesc = "skillful Japanese archer with the power of dragon, Hanzo Shimada";
    public string heroStory = "";
    public string heroPrompts = "";

    public string initInformation = "";
    private int baseInitPercentage = 0;
    public int initPercentage = 0;
    public bool initFinished = false;
    
    
    public DeepseekParams deepseekParams;
    [SerializeField, TextArea(8, 12)] private string initialPrompt;
    [SerializeField, TextArea(8, 12)] private string startGamePrompt;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [SerializeField]
    private List<Message> _conversationSoFar = new();
    
    [SerializeField]
    private List<Message> _cardGenConversationSoFar = new();
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("AI_IntegrationManager already exists.");
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
        instance = this;
    }

    public void Request(string str, Action<string> callback)
    {
        _conversationSoFar.Add(new Message(str, Role.User));
        Debug.Log("Request:\n" + str);
        Deepseek.Request(_conversationSoFar, deepseekParams,
            reply =>
            {
                Debug.Log("Deepseek Reply:\n" + reply);
                _conversationSoFar.Add(new Message(reply, Role.AI));
                callback.Invoke(reply);
            }, null, null);
        
    }

    public void CardQueueRequest(string str, Action<string> callback)
    {
        _cardGenConversationSoFar.Add(new Message(str, Role.User));
        Debug.Log("Card Req:\n" + str);
        Deepseek.Request(_cardGenConversationSoFar, deepseekParams,
            reply =>
            {
                Debug.Log("Card Reply:\n" + reply);
                _cardGenConversationSoFar.Add(new Message(reply, Role.AI));
                callback.Invoke(reply);
            }, null, null);
        
    }
    public bool gameStartPending = false;
    public void SendStartGamePrompt()
    {
        gameStartPending = true;
        Request(startGamePrompt.Replace("##HeroName##", heroName)
            .Replace("##HeroDesc##", heroDesc),
            _ => { gameStartPending = false; });
    }

    public void InitialResponse(string s)
    {
        baseInitPercentage += 20;
        initInformation += "basic information processed.\n";
        HeroReply heroReply = AI_CardEffect.Decode<HeroReply>(s);

        heroStory = heroReply.backgroundStory;
        heroPrompts = heroReply.prompt;
        AI_ImageGeneration.instance.GenerateHeroSprite();
        AI_DeckGenerator.instance.GenerateStartDeck();
    }

    public void Init()
    {
        string initialPromptToSend = initialPrompt.Replace("##HeroName##", heroName);
        initialPromptToSend = initialPromptToSend.Replace("##HeroDesc##", heroDesc);
        Request(initialPromptToSend, InitialResponse);
    }
    
    void Start()
    {
    }

    void OnInitFinished()
    {
        // Copy Contexts
        _cardGenConversationSoFar = new List<Message>(_conversationSoFar);
        Debug.Log("<color=#779fff><b> Init Generation Complete!</b></color> ");
        AI_DeckGenerator.instance.GenerateRareCards();
    }
    // Update is called once per frame
    void Update()
    {
        if (!initFinished)
        {
            initPercentage = baseInitPercentage + 10 *AI_DeckGenerator.instance.cardGenerated
                + 20 * AI_ImageGeneration.instance.heroImgGenerated +
                5 * AI_ImageGeneration.instance.cardImgGenerated;
            if (AI_DeckGenerator.instance.cardGenerated >= 4 && AI_ImageGeneration.instance.heroImgGenerated >= 1
                && AI_ImageGeneration.instance.cardImgGenerated >= 4)
            {
                OnInitFinished();
                initFinished = true;
            }
        }
    }

    #region DataStructures

    struct HeroReply
    {
        public string backgroundStory;
        public string prompt;
    }
    

    #endregion
}
