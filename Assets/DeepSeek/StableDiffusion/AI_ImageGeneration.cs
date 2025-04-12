using System;
using UnityEngine;
using UnityEngine.Serialization;

public class AI_ImageGeneration : MonoBehaviour
{
    public static AI_ImageGeneration instance;
    SDProcessor sdProcessor;
    ComfyProcessor comfyProcessor;
    
    [SerializeField] private SDConfig characterConfig;
    [SerializeField] private SDConfig cardConfig;
    [SerializeField] private RembgConfig rembgConfig;

    public Sprite heroSprite;
    public bool heroSpriteSet = false;
    public int heroImgGenerated = 0;
    public int cardImgGenerated = 0;
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("AI_DeckGenerator already exists.");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public void SetServerAddress(string serverAddress)
    {
        sdProcessor._rootEndpoint = serverAddress;
        comfyProcessor.serverAddress = serverAddress;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        comfyProcessor = GetComponent<ComfyProcessor>();
        sdProcessor = GetComponent<SDProcessor>();
    }

    public void SetHeroSprite(Texture2D[] tex)
    {
        AI_IntegrationManager.instance.initInformation += AI_IntegrationManager.instance.heroArtStr + "\n";
        heroImgGenerated++;
        heroSprite = Sprite.Create(tex[0], new Rect(0, 0, tex[0].width, tex[0].height),
            new Vector2(0.5f, 0.5f), 600f);
        heroSpriteSet = true;
    }
    
    public void SetHeroSprite(Texture2D tex)
    {
        AI_IntegrationManager.instance.initInformation += AI_IntegrationManager.instance.heroArtStr + "\n";
        heroImgGenerated++;
        heroSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 600f);
        heroSpriteSet = true;
    }
    
    // public void GenerateHeroSprite()
    // {
    //     characterConfig.prompt = AI_IntegrationManager.instance.heroPrompts + characterConfig.prompt;
    //     StartCoroutine(sdProcessor.TextToImageRembgAsync(characterConfig, rembgConfig, SetHeroSprite));
    //     
    // }
    //
    // public void GenerateCardSprite(string prompts, Action<Sprite> callback)
    // {
    //     Debug.Log("Card Sprite Generation: " + prompts);
    //     SDConfig newConfig = new SDConfig(cardConfig);
    //     newConfig.prompt = prompts + newConfig.prompt;
    //     StartCoroutine(sdProcessor.TextToImageAsync(newConfig, 
    //         tex =>
    //     {
    //         cardImgGenerated ++;
    //         AI_IntegrationManager.instance.initInformation += "card image generated.\n";
    //         Sprite s = Sprite.Create(tex[0], new Rect(0, 0, tex[0].width, tex[0].height),
    //             new Vector2(0.5f, 0.5f), 400f);
    //         callback?.Invoke(s);
    //     }));
    // }
    public void GenerateHeroSprite()
    {
        //Comfy
        comfyProcessor.QueuePrompt(AI_IntegrationManager.instance.heroPrompts, false, SetHeroSprite);
    }

    public void GenerateCardSprite(string prompts, Action<Sprite> callback)
    {
        //Comfy
        Debug.Log("Card Sprite Generation: " + prompts);
        comfyProcessor.QueuePrompt(prompts, true, tex =>
        {
            cardImgGenerated ++;
            AI_IntegrationManager.instance.initInformation += AI_IntegrationManager.instance.cardArtStr + "\n";
            Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 400f);
            callback?.Invoke(s);
        });
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
