using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Serialization;

public class DeepseekTest : MonoBehaviour
{
    
    [FormerlySerializedAs("chatGptParameters")] public DeepseekParams deepseekParams;
    
    private List<Message> _conversationSoFar = new();
    void Send1()
    {
        
        string prompt =
            "Give me a random news title of 10~15 words. " +
            "It should be hilarious and easy to understand by kids." +
            " Please directly reply with the title with no other words.";
        _conversationSoFar.Add(new Message(prompt, Role.User));
        Deepseek.Request(_conversationSoFar, deepseekParams, str => Debug.Log("Deepseek Response: " + str), null, null);
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
