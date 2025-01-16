using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
public class MainmenuManager : MonoBehaviour
{
    public TMP_InputField nameInputField, descInputField;
    public GameObject goButton;
    public TMP_Text initInfoText;
    public TMP_Text percentageText;
    private bool initProcessing = false;
    public void StartGame()
    {
        Debug.Log("StartGame");
        string _name = nameInputField.text;
        string _desc = descInputField.text;
        AI_IntegrationManager.instance.heroName = _name;
        AI_IntegrationManager.instance.heroDesc = _desc;
        AI_IntegrationManager.instance.Init();
        initProcessing = true;
        goButton.SetActive(false);
    }
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (initProcessing)
        {
            initInfoText.text = AI_IntegrationManager.instance.initInformation;
            percentageText.text = "... " + AI_IntegrationManager.instance.initPercentage + "%";
            if(AI_IntegrationManager.instance.initFinished)
                SceneManager.LoadScene(1);
                
        }
    }
}
