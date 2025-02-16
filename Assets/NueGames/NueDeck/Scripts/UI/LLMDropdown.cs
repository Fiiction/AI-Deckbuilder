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
        string str = dropdown.options[value].text;
        Debug.Log("ValueChange: " + value);
        AI_IntegrationManager.instance.SetActiveParams(str);
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
