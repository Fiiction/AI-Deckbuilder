using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;


[Serializable]
public struct HeroInfo
{
    public string name;
    [SerializeField, TextArea(2, 3)]
    public string desc;
    
}

public class MainmenuManager : MonoBehaviour
{
    public TMP_InputField nameInputField, descInputField;
    public GameObject goButton;
    public TMP_Text initInfoText;
    public TMP_Text percentageText;
    private bool initProcessing = false;
    float initStartTime;
    [SerializeField]
    List<HeroInfo> heroInfos = new List<HeroInfo>();

    public GameObject netSettingPanel;
    public TMP_InputField serverAddressInputField;
    private int index = 0;
    public void StartGame()
    {
        Debug.Log("StartGame");
        string _name = nameInputField.text;
        string _desc = descInputField.text;
        AI_IntegrationManager.instance.heroName = _name;
        AI_IntegrationManager.instance.heroDesc = _desc;
        AI_IntegrationManager.instance.Init();
        AI_ImageGeneration.instance.SetServerAddress(serverAddressInputField.text);
        netSettingPanel.SetActive(false);
        initProcessing = true;
        goButton.SetActive(false);
        initStartTime = Time.time;
    }
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        index = Random.Range(0, heroInfos.Count);
        nameInputField.text = heroInfos[index].name;
        descInputField.text = heroInfos[index].desc;
    }

    public void GetNextHeroInfo()
    {
        index++;
        index %= heroInfos.Count;
        nameInputField.text = heroInfos[index].name;
        descInputField.text = heroInfos[index].desc;
        
    }
    // Update is called once per frame
    void Update()
    {
        if (initProcessing)
        {
            float initTime = Time.time - initStartTime;
            initInfoText.text = AI_IntegrationManager.instance.initInformation;
            percentageText.text = initTime.ToString("0.#") + 
                                  "s ... " + AI_IntegrationManager.instance.initPercentage + "%";
            if(AI_IntegrationManager.instance.initFinished)
                SceneManager.LoadScene(1);
                
        }
    }
}
