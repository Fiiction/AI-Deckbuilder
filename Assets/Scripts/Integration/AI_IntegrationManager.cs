using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using NueGames.NueDeck.Scripts.Managers;
using Newtonsoft.Json;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;


public class AI_IntegrationManager : MonoBehaviour
{
    public static AI_IntegrationManager instance;
    public static LLMParams activeParams;
    public string heroName = "Hanzo";
    public string heroDesc = "skillful Japanese archer with the power of dragon, Hanzo Shimada";
    public string heroStory = "";
    public string heroPrompts = "";
    public string pendingPrompts = "";
    public string initInformation = "";
    private int baseInitPercentage = 0;
    public int initPercentage = 0;
    public bool initFinished = false;
    [SerializeField]
    public List<Message> _conversationSoFar = new();
    [SerializeField]
    public List<Message> _cardGenConversationSoFar = new();

    public string debugStr = "";

    [SerializeField]public List<LLMParams> LLMParams = new();
    [SerializeField, TextArea(8, 12)] private string initialPrompt;
    [SerializeField, TextArea(4, 12)] private string startGamePrompt;
    [SerializeField, TextArea(2, 4)] private string jsonCorrectionPrompt;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public void SetActiveParams(string paramName)
    {
        activeParams = LLMParams.Find(p => p.name == paramName);
        Debug.Log("Set Params: " + paramName + " " + activeParams.url);
    }
    
    public bool gameStartPending = false;
    public int levelCnt = 0;
    public int startGameMsgCnt = -1;
    public int startTurnMsgCnt = -1;
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
        Debug.Log("<b>StartTurnCutConversation</b>: " + startTurnMsgCnt);
        if(startTurnMsgCnt > 0)
            CutConversation(startTurnMsgCnt);
        else
            startTurnMsgCnt = _conversationSoFar.Count;
    }
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("AI_IntegrationManager already exists.");
            Destroy(gameObject);
            return;
        }

        //activeParams = GetParams();
        DontDestroyOnLoad(gameObject);
        instance = this;
    }

    public void Request(string str, Action<string> callback, bool replyWithJson = false, Type jsonType = null)
    {
        debugStr += "\n<b>Request</b>: \n" + str;
        str = pendingPrompts +"\n" + str;
        pendingPrompts = "";
        if(str.Contains("##"))
            Debug.LogError("Unfilled key found:\n" + str);
        _conversationSoFar.Add(new Message(str, Role.User));
        Debug.Log("Request:\n" + str);
        LLMService.Request(_conversationSoFar, activeParams,
            reply =>
            {
                // if(Random.value<0.6f && jsonType != null)
                //     reply = reply.Substring(0, reply.Length - 15);
                debugStr += "\n<b>Reply</b>: \n" + reply;
                Debug.Log("LLM Reply:\n" + reply);
                _conversationSoFar.Add(new Message(reply, Role.AI));

                if (replyWithJson && jsonType != null)
                {
                    if (IsValidJson(reply, jsonType))
                        callback.Invoke(reply);
                    else
                    {
                        Debug.Log("<b><color=#FF66CC> Json Correction! </b></color>");
                        debugStr += "\n<b><color=#FF66CC> Json Correction! </b></color>\n";
                        AI_DebugCanvas.instance.AddWarning("Json Correction!");
                        pendingPrompts += jsonCorrectionPrompt + "\n";
                        Request(str, callback, replyWithJson, jsonType);
                    }
                }
                else
                    callback.Invoke(reply);
            }, null, null, replyWithJson, jsonType);
        
    }

    public void CardQueueRequest(string str, Action<string> callback, bool replyWithJson = false, Type jsonType = null)
    {
        if(str.Contains("##"))
            Debug.LogError("Unfilled key found:\n" + str);
        
        debugStr += "\n<b><color=#AAAAFF>Card Req</b></color>: \n" + str;
        _cardGenConversationSoFar.Add(new Message(str, Role.User));
        Debug.Log("<color=#FFAA66>Card Req</color>:\n" + str);
        LLMService.Request(_cardGenConversationSoFar, activeParams,
            reply =>
            {
                debugStr += "\n<b><color=#AAAAFF>Card Reply</b></color>: \n" + reply;
                Debug.Log("Card Reply:\n" + reply);
                _cardGenConversationSoFar.Add(new Message(reply, Role.AI));
                if (replyWithJson && jsonType != null)
                {
                    if (IsValidJson(reply, jsonType))
                        callback.Invoke(reply);
                    else
                    {
                        Debug.Log("<b><color=#FF66CC> Json Correction! </b></color>");
                        debugStr += "\n<b><color=#FF66CC> Json Correction! </b></color>\n";
                        AI_DebugCanvas.instance.AddWarning("Json Correction!");
                        pendingPrompts += jsonCorrectionPrompt + "\n";
                        CardQueueRequest(str, callback, replyWithJson, jsonType);
                    }
                }
                else
                    callback.Invoke(reply);
            }, null, null, replyWithJson);
        
    }

    public void SendStartGamePrompt()
    {
        startGameMsgCnt = _conversationSoFar.Count;
        startTurnMsgCnt = -1;
        
        Debug.Log("<b>Game Start</b>");
        levelCnt++;
        AI_CardEffect.instance.cardInTurnCnt = 0;
        AI_CardEffect.instance.turnCnt = 0;
        //gameStartPending = true;
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

        pendingPrompts += promptToSend;

        // Request(promptToSend, _ => { gameStartPending = false; 
        //     startTurnMsgCnt = _conversationSoFar.Count;});
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
        initInformation = "";
        initPercentage = 0;
        _conversationSoFar.Clear();
        _cardGenConversationSoFar.Clear();
        
        int levelCnt = 0;
        int startGameMsgCnt = -1;
        int startTurnMsgCnt = -1;
        int cardGenMessageMerged = -1;
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

    void LogConversation()
    {
        string s = "Conv: " + _conversationSoFar.Count;
        for (int i = 0; i < _conversationSoFar.Count; i++)
        {
            s += "\n#" +i + ":\nRole: " +_conversationSoFar[i].role +"\nText: " +  _conversationSoFar[i].text;
        }
        Debug.Log(s);
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
        //LogConversation();
    }

    #region DataStructures

    struct HeroReply
    {
        public string backgroundStory;
        public string prompt;
    }
    

    #endregion
    
    
    public bool IsValidJson(string json, Type type)
    {
        
        int st = json.IndexOf("{");
        int ed = json.LastIndexOf("}");
        json = json.Substring(st, ed-st+1);
        
        try
        {
            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error
            };

            var obj = JsonConvert.DeserializeObject(json, type, settings);
            return true;
        }
        catch (JsonException ex)
        {
            return false;
        }
    }
}
