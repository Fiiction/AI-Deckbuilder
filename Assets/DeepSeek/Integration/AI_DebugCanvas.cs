using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AI_DebugCanvas : MonoBehaviour
{
    public Image panel;
    public TMP_Text text;
    public TMP_Text warningText;
    public string warnings;
    public float warningDuration = 8f;
    private float warningTime = 0f;
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

    public void AddWarning(string t)
    {
        warnings += "\n" + t;
        warningText.text = "Warnings:\n" + warnings +"\n - Press F1 for details";
        warningTime = warningDuration;
    }
    // Update is called once per frame
    void Update()
    {
        warningTime -= Time.deltaTime;
        warningText.color = new Color(warningText.color.r, warningText.color.g, warningText.color.b,
            Mathf.Clamp01(warningTime));
        
        text.text = AI_IntegrationManager.instance.debugStr;
        if (Input.GetKeyDown(KeyCode.F1))
        {
            active = !active;
            panel.gameObject.SetActive(active);
            text.rectTransform.anchoredPosition = basePosition;
            curPosition = basePosition;
        }

        if (active)
        {
            warningTime = 1f;
            curPosition += moveDist * Input.GetAxisRaw("Mouse ScrollWheel") * Vector3.up;
            text.rectTransform.anchoredPosition = curPosition;
        }
    }
}
