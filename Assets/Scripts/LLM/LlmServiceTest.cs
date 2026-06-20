using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Serialization;

public class LlmServiceTest : MonoBehaviour
{
    
    [FormerlySerializedAs("deepseekParams")] [FormerlySerializedAs("chatGptParameters")] public LLMParams llmParams;
    
    private List<Message> _conversationSoFar = new();
    void Send1()
    {
        
        string prompt =
            "Give me a random news title of 10~15 words. " +
            "It should be hilarious and easy to understand by kids." +
            " Please directly reply with the title with no other words.";
        _conversationSoFar.Add(new Message(prompt, Role.User));
        LLMService.Request(_conversationSoFar, llmParams, str => Debug.Log("Deepseek Response: " + str), null, null);
    }

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Send1();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
