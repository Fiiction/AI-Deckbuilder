using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class LLMDropdown : MonoBehaviour
{
    TMP_Dropdown dropdown;
    public void ValueChange(int value)
    {
        dropdown = GetComponent<TMP_Dropdown>();
        value = dropdown.value;
        Debug.Log("ValueChange: " + value);
        switch (value)
        {
            case 0:
                AI_IntegrationManager.instance.paramType = AI_IntegrationManager.ParamType.Ali;
                break;
            case 1:
                AI_IntegrationManager.instance.paramType = AI_IntegrationManager.ParamType.Gemini;
                break;
            case 2:
                AI_IntegrationManager.instance.paramType = AI_IntegrationManager.ParamType.Official;
                break;
            case 3:
                AI_IntegrationManager.instance.paramType = AI_IntegrationManager.ParamType.Qwen;
                break;
        }

        AI_IntegrationManager.instance.alternativeParamType = AI_IntegrationManager.instance.paramType;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ValueChange(0);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
