using System;
using System.Collections;
using System.Text;
using DevGame.Utility;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Random = UnityEngine.Random;

public class SDProcessor : MonoBehaviour
{
    [SerializeField] public string _rootEndpoint = "http://localhost:7860";
    //[TextArea(2, 20)] [SerializeField] public string p;
    // public IEnumerator ImageToImageAsync(ImgToImgConfig config, Action<Texture2D[]> OnSuccess)
    // {
    //     var url = $"{_rootEndpoint}/sdapi/v1/img2img";
    //     Debug.Log(url);
    //     var payload = JsonConvert.SerializeObject(config, Formatting.Indented);
    //     Debug.Log($"Try send request {url}");
    //     using (var request = new UnityWebRequest(url, "POST"))
    //     {
    //         request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
    //         request.downloadHandler = new DownloadHandlerBuffer();
    //         request.SetRequestHeader("accept", "application/json");
    //         request.SetRequestHeader("Content-Type", "application/json");
    //   
    //         yield return request.SendWebRequest();
    //         if (request.result == UnityWebRequest.Result.Success)
    //         {
    //             Debug.Log("Success!");
    //             var response = JsonConvert.DeserializeObject<ImgToImgResponse>(request.downloadHandler.text);
    //             var textures = new Texture2D[response.images.Length];
    //             for (int i = 0; i < textures.Length; i++)
    //             {
    //                 textures[i] = response.images[i].Base64ToTexture2D(config.width, config.height);
    //             }
    //             OnSuccess?.Invoke(textures);
    //         }
    //         else
    //         {
    //             Debug.LogError(request.error);
    //             Debug.LogError(request.downloadHandler.text);
    //         }
    //     }
    // }
    
    public IEnumerator TextToImageAsync(SDConfig config, Action<Texture2D[]> OnSuccess)
    {
        var url = $"{_rootEndpoint}/sdapi/v1/txt2img";
        config.seed = Random.Range(0, (int)1e7);
        var payload = JsonConvert.SerializeObject(config, Formatting.Indented);

        Debug.Log($"Try send request {url}");
        using (var request = new UnityWebRequest(url, "POST"))
        {
            //payload = p;
            Debug.Log(payload);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");
      
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Success!");
                var response = JsonConvert.DeserializeObject<ImgToImgResponse>(request.downloadHandler.text);
                var textures = new Texture2D[response.images.Length];
                for (int i = 0; i < textures.Length; i++)
                {
                    textures[i] = response.images[i].Base64ToTexture2D(config.width, config.height);
                }
                OnSuccess?.Invoke(textures);
            }
            else
            {
                Debug.LogError(request.result);
                Debug.LogError(request.error);
                Debug.LogError(request.downloadHandler.text);
            }
        }
    }

    public IEnumerator TextToImageRembgAsync(SDConfig config, RembgConfig rembgConfig, Action<Texture2D[]> OnSuccess)
    {
        var url = $"{_rootEndpoint}/sdapi/v1/txt2img";
        config.seed = Random.Range(0, (int)1e7);
        var payload = JsonConvert.SerializeObject(config, Formatting.Indented);
        Debug.Log($"Try send request {url}");
        using (var request = new UnityWebRequest(url, "POST"))
        {
            //payload = p;
            //Debug.Log(payload);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Image Generation Success!");
                var response = JsonConvert.DeserializeObject<ImgToImgResponse>(request.downloadHandler.text);
                rembgConfig.input_image = response.images[0];
                var rembg_url = $"{_rootEndpoint}/rembg";
                var rembg_payload = JsonConvert.SerializeObject(rembgConfig, Formatting.Indented);

                using (var rembg_request = new UnityWebRequest(rembg_url, "POST"))
                {
                    rembg_request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(rembg_payload));
                    rembg_request.downloadHandler = new DownloadHandlerBuffer();
                    rembg_request.SetRequestHeader("accept", "application/json");
                    rembg_request.SetRequestHeader("Content-Type", "application/json");
                    yield return rembg_request.SendWebRequest();
                    if (rembg_request.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("Rembg Success!");
                        var rembg_response = JsonConvert.DeserializeObject<RembgResponse>(rembg_request.downloadHandler.text);
                        //Debug.Log(rembg_request.downloadHandler.text);                          
                        //Debug.Log("images: " + rembg_response.images);
                        //Debug.Log("L: " + rembg_response.images.Length);


                        var textures = new Texture2D[1];
                        textures[0] = rembg_response.image.Base64ToTexture2D(config.width, config.height);
                        
                        OnSuccess?.Invoke(textures);
                    }
                    else
                    {
                        Debug.Log("Rembg Error:");
                        Debug.LogError(request.result);
                        Debug.LogError(request.error);
                        Debug.LogError(request.downloadHandler.text);
                    }
                }
            }
            else
            {
                Debug.LogError(request.result);
                Debug.LogError(request.error);
                Debug.LogError(request.downloadHandler.text);
            }
        }
    }
}
