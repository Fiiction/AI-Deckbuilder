using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class WebSettingCanvas : MonoBehaviour
{
    public GameObject goButton;
    public TMP_Dropdown llmPresetDropdown;

    public TMP_InputField urlInputField;
    public TMP_InputField modelInputField;
    public TMP_InputField apikeyInputField;
    public TMP_InputField imgInputField;
    
    public List<DeepseekParams> deepseekParamsList;

    public bool setApiKey = true;
    public GameObject nextCanvas;
    public void PresetChanged(int value)
    {
        string str = llmPresetDropdown.options[value].text;
        Debug.Log("ValueChange: " + value);
        
        var p = deepseekParamsList.Find(p => p.name == str);
        if (p == null)
        {
            urlInputField.text = "";
            modelInputField.text = "";
            apikeyInputField.text = "";
            
        }
        else
        {
            urlInputField.text = p.url;
            modelInputField.text = p.modelName;
            if(setApiKey)
                apikeyInputField.text = p.apiKey;
            else
                apikeyInputField.text = "";
        }
    }

    public void Go()
    {
        var dp = new DeepseekParams(urlInputField.text, modelInputField.text, apikeyInputField.text);
        AI_IntegrationManager.activeParams = dp;
        AI_ImageGeneration.instance.SetServerAddress(imgInputField.text);
        gameObject.SetActive(false);
        nextCanvas.SetActive(true);
    }
    
    void Start()
    {
        llmPresetDropdown.onValueChanged.AddListener(PresetChanged);
        PresetChanged(0);
    }
    
    
}
