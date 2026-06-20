using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AI_ProcessingCanvas : MonoBehaviour
{
    public GameObject root;
    public TMP_Text text;

    public float processTime = 0;
    public bool processing = false;
    public string processingText = "";

    public void StartProcessing(string str)
    {
        if(processing)
            return;
        processTime = 0f;
        processing = true;
        processingText = str;
        root.SetActive(true);
    }

    public void EndProcessing()
    {
        root.SetActive(false);
        processing = false;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (processing)
        {
            processTime += Time.deltaTime;
            text.text = processingText + "\n" + processTime.ToString("0.00");
        }
    }
}
