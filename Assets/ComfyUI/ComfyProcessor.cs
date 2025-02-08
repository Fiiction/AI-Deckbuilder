using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

[System.Serializable]
public class ResponseData
{
    public string prompt_id;
}

public class ComfyProcessor : MonoBehaviour
{

    // ---------- Comfy Prompt Ctr ----------
    // ---------- Comfy Prompt Ctr ----------
    // ---------- Comfy Prompt Ctr ----------
    
    public string serverAddress = "127.0.0.1:8188";
    private string clientId = Guid.NewGuid().ToString();
    private ClientWebSocket ws = new ClientWebSocket();
    public Dictionary<string, Action<Texture2D>> tasks = new Dictionary<string, Action<Texture2D>>();
    public Queue<string> promptIDs = new Queue<string>();
    [TextArea(5,50)]
    public string promptJson;
    [TextArea(5,50)]
    public string cardJson;
    async void Start()
    {
        //QueuePrompt();
        //await ws.ConnectAsync(new Uri($"ws://127.0.0.1:8188/ws?clientId={clientId}"), CancellationToken.None);
        StartListening();
    }

    public void QueuePrompt(string customPrompt, bool isCard, Action<Texture2D> callback)
    {
        StartCoroutine(QueuePromptCoroutine(customPrompt, isCard, callback));
    }

    private IEnumerator QueuePromptCoroutine(string customPrompt,bool isCard, Action<Texture2D> callback)
    {
        string url = $"{serverAddress}/prompt";
        string promptText = GeneratePromptJson(isCard);
        promptText = promptText.Replace("##Prompt##", customPrompt);
        Debug.Log("<b><color=#77FF22>Prompt Request Send!</b></color>");
        Debug.Log(promptText);
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(promptText);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(request.error);
        }
        else
        {
            //Debug.Log("Prompt queued successfully." + request.downloadHandler.text);

            ResponseData data = JsonUtility.FromJson<ResponseData>(request.downloadHandler.text);
            Debug.Log("Prompt ID: " + data.prompt_id);
            promptIDs.Enqueue(data.prompt_id);
            tasks.Add(data.prompt_id, callback);
            // GetComponent<ComfyImageCtr>().RequestFileName(data.prompt_id);
        }
    }


    private string GeneratePromptJson(bool isCard)
    {
        string guid = Guid.NewGuid().ToString();
        string prompt = isCard?cardJson:promptJson;
        string promptJsonWithGuid = $@"
        {{
            ""id"": ""{guid}"",
            ""prompt"": {prompt}
        }}";

        return promptJsonWithGuid;
    }
    
    
    // ---------- Comfy Image Ctr ----------
    // ---------- Comfy Image Ctr ----------
    // ---------- Comfy Image Ctr ----------
    
    public void RequestFileName(string id)
    {
        StartCoroutine(RequestFileNameRoutine(id));
    }

     IEnumerator RequestFileNameRoutine(string promptID)
    {
        string url = $"{serverAddress}/history/" + promptID;
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();
            
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError(": Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError(": HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    if(webRequest.downloadHandler.text.Length <= 10)
                        break;
                    //Debug.Log("File To Find: " + webRequest.downloadHandler.text);
                    string imageURL = $"{serverAddress}/view?filename=" +ExtractFilename(webRequest.downloadHandler.text);
                    StartCoroutine(DownloadImage(imageURL, promptID));
                    break;
            }
        }
    }
    
    string ExtractFilename(string jsonString)
    {
        // Step 1: Identify the part of the string that contains the "filename" key
        // Debug.Log("FileName: " + jsonString.Substring(2, 35));
        // return jsonString.Substring(2, 36);
        string keyToLookFor = "\"filename\":";
        int startIndex = jsonString.IndexOf(keyToLookFor);

        if (startIndex == -1)
        {
            Debug.Log("filename key not found!!");
            return "filename key not found";
        }

        // Adjusting startIndex to get the position right after the keyToLookFor
        startIndex += keyToLookFor.Length;

        // Step 2: Extract the substring starting from the "filename" key
        string fromFileName = jsonString.Substring(startIndex);

        // Assuming that filename value is followed by a comma (,)
        int endIndex = fromFileName.IndexOf(',');

        // Extracting the filename value (assuming it's wrapped in quotes)
        string filenameWithQuotes = fromFileName.Substring(0, endIndex).Trim();

        // Removing leading and trailing quotes from the extracted value
        string filename = filenameWithQuotes.Trim('"');
        //Debug.Log("FileName: " + filename);
        return filename;
    }

    List<string> imageDownloaded = new List<string>();
     IEnumerator DownloadImage(string imageUrl, string promptID)
    {
        if(imageDownloaded.Contains(imageUrl))
            yield break;
        imageDownloaded.Add(imageUrl);
        //Debug.Log("<b><color=#22FF22>Requesting new Image!</b></color>");
        yield return new WaitForSeconds(0.1f);
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                // Get the downloaded texture
                Debug.Log("<b><color=#5555FF>Image Downloaded!</b></color>\n" + promptID);
                Texture2D texture = ((DownloadHandlerTexture)webRequest.downloadHandler).texture;
                tasks[promptID].Invoke(texture);
                if(promptIDs.Peek() == promptID)
                    promptIDs.Dequeue();
            }
            else
            {
                Debug.LogError("Image download failed: " + webRequest.error);
            }
        }
    }
    
    
    // ---------- WebSocket ---------- 
    // ---------- WebSocket ---------- 
    // ---------- WebSocket ---------- 
    IEnumerator ListenImageCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if(promptIDs.Count > 0)
                RequestFileName(promptIDs.Peek());
        }
    }
    private async void StartListening()
    {
        StartCoroutine(ListenImageCoroutine());
        // var buffer = new byte[1024 * 4];
        // WebSocketReceiveResult result = null;
        //
        // while (ws.State == WebSocketState.Open)
        // {
        //     var stringBuilder = new StringBuilder();
        //     do
        //     {
        //         result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        //         if (result.MessageType == WebSocketMessageType.Close)
        //         {
        //             await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        //         }
        //         else
        //         {
        //             var str = Encoding.UTF8.GetString(buffer, 0, result.Count);
        //             Debug.Log("Substring: " + str);
        //             stringBuilder.Append(str);
        //         }
        //     }
        //     while (!result.EndOfMessage);
        //
        //     string response = stringBuilder.ToString();
        //     Debug.Log("Received: " + response);
        //
        //     if (response.Contains("\"queue_remaining\":") && promptIDs.Count > 0)
        //     {
        //         RequestFileName(promptIDs.Peek());
        //     }
        // }
    }
    
    
   

    void OnDestroy()
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
    }
}
