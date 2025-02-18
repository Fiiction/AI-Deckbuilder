using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AI_DebugCanvas : MonoBehaviour
{
    public Image panel;
    public TMP_Text text;
    public static AI_DebugCanvas instance;
    public bool active = false;
    public float moveDist = 5f;
    public Vector3 basePosition;
    public Vector3 curPosition;
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("AI_DebugCanvas already exists.");
            Destroy(gameObject);
            return;
        }
        basePosition = text.rectTransform.anchoredPosition;
        curPosition = basePosition;
        panel.gameObject.SetActive(false);
        instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        text.text = AI_IntegrationManager.instance.debugStr;
        if (Input.GetKeyDown(KeyCode.K))
        {
            active = !active;
            panel.gameObject.SetActive(active);
            text.rectTransform.anchoredPosition = basePosition;
            curPosition = basePosition;
        }

        if (active)
        {
            curPosition += moveDist * Input.GetAxisRaw("Mouse ScrollWheel") * Vector3.up;
            text.rectTransform.anchoredPosition = curPosition;
        }
    }
}
