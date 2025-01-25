using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using NueGames.NueDeck.Scripts.Managers;

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

    public bool gameStartPending = false;
    public int levelCnt = 0;
    public int startGameMsgCnt = -1;
    public int startTurnMsgCnt = -1;
    private List<Message> _cardGenConversationSoFar = new();
    public int cardGenMessageMerged = -1;
    void CutConversation(int c)
    {
        _conversationSoFar = _conversationSoFar.Take(c).ToList();
    }

    void MergeCardGenMessages()
    {
        for(int i = cardGenMessageMerged; i < _cardGenConversationSoFar.Count; i++)
            _conversationSoFar.Add(_cardGenConversationSoFar[i]);
        cardGenMessageMerged = _cardGenConversationSoFar.Count;
    }
    public void StartTurnCutConversation()
    {
        if(startTurnMsgCnt > 0)
            CutConversation(startTurnMsgCnt);
        else
            startGameMsgCnt = _conversationSoFar.Count;
    }
    
    [SerializeField]
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
        if(str.Contains("##"))
            Debug.LogError("Unfilled key found:\n" + str);
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
        if(str.Contains("##"))
            Debug.LogError("Unfilled key found:\n" + str);
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

    public void SendStartGamePrompt()
    {
        startGameMsgCnt = _conversationSoFar.Count;
        startTurnMsgCnt = -1;
        
        Debug.Log("<b>Game Start</b>");
        levelCnt++;
        AI_CardEffect.instance.cardInTurnCnt = 0;
        AI_CardEffect.instance.turnCnt = 0;
        gameStartPending = true;
        AI_CardEffect.instance.cardsUsedInBattle = 0;
        string promptToSend = startGamePrompt.Replace("##HeroName##", heroName)
            .Replace("##HeroDesc##", heroDesc);
        promptToSend = promptToSend.Replace("##EnemyCnt##", CombatManager.Instance.CurrentEnemiesList.Count.ToString());

        string enemyDescs = "";
        foreach (var i in CombatManager.Instance.CurrentEnemiesList)
        {
            enemyDescs += i.EnemyCharacterData.CharacterName + ", " + i.EnemyCharacterData.CharacterDescription + ".\n";
        }

        promptToSend = promptToSend.Replace("##EnemyDescs##", enemyDescs);
        
        Request(promptToSend, _ => { gameStartPending = false; 
            startTurnMsgCnt = _conversationSoFar.Count;});
    }

    public void OnEndGame()
    {
        CutConversation(startGameMsgCnt);
        if (levelCnt == 1)
            AI_DeckGenerator.instance.GenerateEpicCards();
        if (levelCnt == 2)
            AI_DeckGenerator.instance.GenerateLegendCards();
        MergeCardGenMessages();
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
        cardGenMessageMerged = _conversationSoFar.Count;
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
