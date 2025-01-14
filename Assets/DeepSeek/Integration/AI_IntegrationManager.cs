using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class AI_IntegrationManager : MonoBehaviour
{
    public static AI_IntegrationManager instance;
    
    public DeepseekParams deepseekParams;
    [SerializeField, TextArea(8, 12)] private string initialPrompt;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private List<Message> _conversationSoFar = new();
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("AI_IntegrationManager already exists.");
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
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
    
    void Start()
    {
        instance = this;
        Request(initialPrompt, str => { });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
