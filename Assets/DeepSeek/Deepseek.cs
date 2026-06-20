using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;

public enum Role {
    User,
    AI,
}

[Serializable]
public struct Message {
    public Role role;
    public string text;

    public Message(string text, Role role) {
        this.text = text;
        this.role = role;
    }
}

public static class Deepseek
{
    // public static string modelType = "deepseek-chat"; //deepseek-ai/DeepSeek-V3
    // public static string URL = "https://api.deepseek.com/chat/completions"; //https://api.siliconflow.cn/v1/chat/completions
    // public static string modelType = "deepseek-ai/DeepSeek-V3";
    // public static string URL = "https://api.siliconflow.cn/v1/chat/completions";
    private static readonly List<RequestRecord> _requestRecords = new List<RequestRecord>();
    private static string jsonFormat = "json_object";
    /// <summary>
    /// Send a request to ChatGPT.
    /// </summary>
    /// <param name="prompt">The text of the request, e.g. "Generate a character description".</param>
    /// <param name="parameters">Settings of the request.</param>
    /// <param name="completeCallback">The function to be called on successful completion. ChatGPT response is provided
    /// as a parameter.</param>
    /// <param name="failureCallback">The function to be called on failure. Error code and message are provided as
    /// parameters.</param>
    /// <param name="updateCallback">The function to be called when a new response chunk is generated. ChatGPT response
    /// data is provided as a parameter.</param>
    /// <returns>A function that can be called to cancel the request.</returns>
    // ReSharper disable once MemberCanBePrivate.Global
    
    public static Action Request(string prompt, DeepseekParams parameters, Action<string> completeCallback,
                                 Action<long, string> failureCallback, Action<string> updateCallback = null)
    {
        return Request(new List<Message> { new Message { role = Role.User, text = prompt } }, parameters,
                       completeCallback, failureCallback, updateCallback);
    }

    /// <summary>
    /// Send a request to ChatGPT.
    /// </summary>
    /// <param name="messages">Sequence of messages to send to ChatGPT. The order of messages should be the same as the
    /// chronological order of messages in the conversation, i.e. the first message should be the oldest one. The roles
    /// of the messages should switch between User and AI.</param> 
    /// <param name="parameters">Settings of the request.</param>
    /// <param name="completeCallback">The function to be called on successful completion. ChatGPT response is provided
    /// as a parameter.</param>
    /// <param name="failureCallback">The function to be called on failure. Error code and message are provided as
    /// parameters.</param>
    /// <param name="updateCallback">The function to be called when a new response chunk is generated. ChatGPT response
    /// data is provided as a parameter.</param>
    /// <returns>A function that can be called to cancel the request.</returns>
    // ReSharper disable once MemberCanBePrivate.Global
    public static Action Request(IEnumerable<Message> messages, DeepseekParams parameters,
                                 Action<string> completeCallback, Action<long, string> failureCallback,
                                 Action<string> updateCallback = null, bool replyWithJson = false, Type jsonType = null)
    {
        Debug.Assert(parameters != null, "Parameters cannot be null.");
        Debug.Assert(!string.IsNullOrEmpty(parameters!.apiKey), "API key cannot be null or empty.");
        Debug.Assert(messages != null, "Messages cannot be null.");

        if (updateCallback == null) {
            return QuickRequest(messages, parameters, completeCallback, failureCallback, replyWithJson, jsonType);
        }

        // Throttle.
        if (parameters.throttle > 0) {
            var requestCount = _requestRecords.Count;
            if (requestCount >= parameters.throttle) {
                Debug.LogWarning("Too many request records.");
                return () => { };
            }
        }

        var requestRecord = new RequestRecord();
        var enumerator = Stream(messages, parameters, updateCallback, completeCallback, failureCallback, requestRecord);
        var cancelCallback = new Action(() => {
            if (enumerator != null) {
                ChatGptContainer.Instance.StopCoroutine(enumerator);
            }

            _requestRecords.Remove(requestRecord);
        });

        requestRecord.SetCancelCallback(cancelCallback);
        _requestRecords.Add(requestRecord);

        ChatGptContainer.Instance.StartCoroutine(enumerator);
        return cancelCallback;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    /// <summary>
    /// Cancel all pending requests.
    /// </summary>
    public static void CancelAllRequests()
    {
        while (_requestRecords.Count > 0)
        {
            _requestRecords[0].Cancel();
        }

        _requestRecords.Clear();
    }

    private static Action QuickRequest(IEnumerable<Message> messages, DeepseekParams parameters,
                                       Action<string> completeCallback, Action<long, string> failureCallback, bool replyWithJson = false, Type jsonType = null)
    {
        
        var enumerator = QuickRequestCoroutine(messages, parameters, completeCallback, failureCallback, replyWithJson, jsonType);
        ChatGptContainer.Instance.StartCoroutine(enumerator);

        void CancelCallback() {
            ChatGptContainer.Instance.StopCoroutine(enumerator);
        }

        return CancelCallback;
    }

    private static IEnumerator QuickRequestCoroutine(IEnumerable<Message> messages, DeepseekParams parameters,
                                                     Action<string> completeCallback,
                                                     Action<long, string> failureCallback, bool replyWithJson = false, Type jsonType = null)
    {
        QuickRequestBlocking(messages, parameters, completeCallback, failureCallback, replyWithJson, jsonType);
        yield break;
    }
    private static void LogRequest(UnityWebRequest request)
    {
        StringBuilder log = new StringBuilder();
        log.AppendLine("=== Request Details ===");
        log.AppendLine($"URL: {request.url}");
        log.AppendLine($"Method: {request.method}");
        log.AppendLine("Headers:");
        // foreach (var header in request.SetRequestHeader())
        // {
        //     log.AppendLine($"  {header.Key}: {header.Value}");
        // }
        log.AppendLine($"Body: {Encoding.UTF8.GetString(request.uploadHandler.data)}");
        Debug.Log(log.ToString());
    }
    private static Action QuickRequestBlocking(IEnumerable<Message> messages, DeepseekParams parameters,
                                               Action<string> completeCallback, Action<long, string> failureCallback, bool replyWithJson = false, Type jsonType = null)
    {
        Debug.Assert(parameters != null, "Parameters cannot be null.");
        Debug.Assert(!string.IsNullOrEmpty(parameters!.apiKey), "API key cannot be null or empty.");
        Debug.Assert(messages != null, "Messages cannot be null.");

        // Throttle.
        if (parameters.throttle > 0) {
            var requestCount = _requestRecords.Count;
            if (requestCount >= parameters.throttle) {
                Debug.LogWarning("Too many request records.");
                return () => { };
            }
        }

        string requestJson = "";
        Dictionary<string, object> paramDict = new();
        paramDict.Add("model", parameters.modelName);
        paramDict.Add("temperature", parameters.temperature);
        paramDict.Add("messages", ConvertMessages(messages, parameters.role));
        if (replyWithJson)
            paramDict.Add("thinking", "{\"type\": \"enabled\"}");
        //     paramDict.Add("response_format", new JsonFormat{type = jsonFormat});
        if (parameters.modelName.Contains("gemini-2.5-flash"))
        {
            if(jsonType == typeof(AI_CardEffect.ActionParams) || jsonType == typeof(AI_CardEffect.CustomEffectParams))
                paramDict.Add("reasoning_effort", "none");
            else
                paramDict.Add("reasoning_effort", "low");
            Debug.Log($"<color=#33FFFF>Gemini 2.5 Reasoning: {paramDict["reasoning_effort"]}</color>");
        }
        requestJson = JsonConvert.SerializeObject(paramDict);
        // if (replyWithJson)
        // {
        //     var requestObject = new JsonRequestMessage
        //     {
        //         model = parameters.modelName,
        //         temperature = parameters.temperature,
        //         messages = ConvertMessages(messages, parameters.role),
        //         response_format = new JsonFormat{type = jsonFormat} 
        //     };
        //     //Debug.Log("ModelName: " + requestObject.model);
        //     requestJson = JsonUtility.ToJson(requestObject);
        // }
        // else
        // {
        //     var requestObject = new RequestMessage
        //     {
        //         model = parameters.modelName,
        //         temperature = parameters.temperature,
        //         messages = ConvertMessages(messages, parameters.role)
        //     };
        //     //Debug.Log("ModelName: " + requestObject.model);
        //     requestJson = JsonUtility.ToJson(requestObject);
        // }

        var requestRecord = new RequestRecord();
        var request = GetWebRequest(requestJson, parameters, failureCallback, requestRecord);
        LogRequest(request);
        // Debug.Log("Request Sent");
        var cancelCallback = new Action(() => {
            try {
                request?.Abort();
                request?.Dispose();
                _requestRecords.Remove(requestRecord);
            }
            catch (Exception) {
                // If the request is aborted, accessing the error property will throw an exception.
            }
        });
        requestRecord.SetCancelCallback(cancelCallback);
        _requestRecords.Add(requestRecord);
        //Debug.Log();
        request.SendWebRequest().completed += _ => {
            _requestRecords.Remove(requestRecord);
            Application.quitting -= cancelCallback;

            bool isErrorResponse;
            try {
                isErrorResponse = !string.IsNullOrEmpty(request.error);
            }
            catch (Exception) {
                // If the request is aborted, accessing the error property will throw an exception.
                return;
            }

            if (isErrorResponse) {
                failureCallback?.Invoke(request.responseCode, request.error);
                Debug.Log($"<color=red>Request error:</color> <b>{request.error}</b>\n" +
                          $"<b>URL: {parameters.url}</b>\n" +
                          $"<b>Response text:</b> {request.downloadHandler.text}");
                AI_IntegrationManager.instance.debugStr +=
                    $"\n<color=red>Request error:</color> <b>{request.error}</b>\n" +
                    $"<b>URL: {parameters.url}</b>\n" +
                    $"<b>Response text:</b> {request.downloadHandler.text}\n";
                AI_DebugCanvas.instance.AddWarning($"Request error: {request.error}");
                return;
            }

            var response = JsonUtility.FromJson<ResponseMessage>(request.downloadHandler.text);
            if (response.choices.Length == 0)
            {
                Debug.LogWarning("No response choices returned from the server.");
                return;
            }

            var responseMessage = response.choices[0].message.content;
            completeCallback?.Invoke(responseMessage);
            request.Dispose();
        };

        Application.quitting += cancelCallback;
        return cancelCallback;
    }

    private static IEnumerator Stream(IEnumerable<Message> messages, DeepseekParams parameters,
                                      Action<string> updateCallback, Action<string> completeCallback,
                                      Action<long, string> failureCallback, RequestRecord requestRecord) {
        var requestObject = new RequestMessage
        {
            model = parameters.modelName,
            temperature = parameters.temperature,
            messages = ConvertMessages(messages, parameters.role),
        };
        var requestJson = JsonUtility.ToJson(requestObject);
        using var request = GetWebRequest(requestJson, parameters, failureCallback, requestRecord);
        var webRequest = request.SendWebRequest();

        int textLength = 0;
        string completeText = "";
        var extraPass = false;

        while (!webRequest.isDone || !extraPass) {
            extraPass = webRequest.isDone;
            if (request.downloadHandler.text.Length > textLength) {
                if (!string.IsNullOrEmpty(request.error)) {
                    failureCallback(request.responseCode, request.error);
                    Debug.Log($"<color=red>Request error:</color> <b>{request.error}</b>\n" +
                              $"<b>URL: {parameters.url}</b>\n" +
                              $"<b>Response text:</b> {request.downloadHandler.text}");
                    _requestRecords.Remove(requestRecord);
                    yield break;
                }

                var text = request.downloadHandler.text;
                var newText = text.Substring(textLength);
                textLength = text.Length;
                while (newText.Contains("data: ")) {
                    var startTrimmed =
                        newText.Substring(newText.IndexOf("data: ", StringComparison.Ordinal) + "data: ".Length);
                    var dataEndPosition = startTrimmed.IndexOf("data: ", StringComparison.Ordinal);
                    var dataJson = dataEndPosition == -1 ? startTrimmed : startTrimmed.Substring(0, dataEndPosition);
                    newText = startTrimmed.Substring(dataJson.Length);
                    if (dataJson.Contains("[DONE]")) {
                        break;
                    }

                    try {
                        var data = JsonUtility.FromJson<ResponseMessage>(dataJson);

                        if (data.choices == null || data.choices.Length == 0)
                        {
                            Debug.LogWarning("No response choices returned from the server.");
                            _requestRecords.Remove(requestRecord);
                            yield break;
                        }

                        var finishReason = data.choices[0].finish_reason;
                        if (finishReason == "length")
                        {
                            Debug.LogWarning("MaxTokensExceeded");
                            _requestRecords.Remove(requestRecord);
                            yield break;
                        }

                        var delta = data.choices[0].delta.content;
                        completeText += delta;
                        updateCallback?.Invoke(delta);
                    }
                    catch (Exception e)
                    {
                        Debug.Log($"<color=red>Request error:</color> <b>{request.error}</b>\n" +
                                         $"<b>URL: {parameters.url}</b>\n" +
                                  $"<b>Response text:</b> {request.downloadHandler.text}");
                        _requestRecords.Remove(requestRecord);
                        yield break;
                    }
                }
            }

            yield return null;
        }

        if (!string.IsNullOrEmpty(request.error)) {
            failureCallback?.Invoke(request.responseCode, request.error);
            Debug.Log($"<color=red>Request error:</color> <b>{request.error}</b>\n" +
                      $"<b>Response text:</b> {request.downloadHandler.text}");
            _requestRecords.Remove(requestRecord);
            yield break;
        }

        if (!string.IsNullOrEmpty(completeText)) {
            completeCallback?.Invoke(completeText);
            _requestRecords.Remove(requestRecord);
        }
    }

    private static UnityWebRequest GetWebRequest(string requestJson, DeepseekParams parameters,
                                                 Action<long, string> failureCallback, RequestRecord requestRecord)
    {
        var baseUrl = parameters.url;
        Debug.Log("URL: " + baseUrl
        +"\nModel: " + parameters.modelName);
        var request = UnityWebRequest.Post(baseUrl, requestJson, "application/json");
        
        request.timeout = parameters.timeout;

        try {
            var apiKey = parameters.apiKey;

            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.SetRequestHeader("Content-Type", "application/json");
        }
        catch (Exception e) {
            //failureCallback?.Invoke((long)ErrorCodes.Unknown, e.Message);
            _requestRecords.Remove(requestRecord);
        }

        return request;
    }


    private static RoleContentMessage[] ConvertMessages(IEnumerable<Message> messages, string role) {
        var systemMessageOffset = string.IsNullOrEmpty(role) ? 0 : 1;
        var inputArray = messages as Message[] ?? messages.ToArray();
        var requestMessages = new RoleContentMessage[inputArray.Length + systemMessageOffset];

        if (systemMessageOffset > 0) {
            requestMessages[0] = new RoleContentMessage { role = "system", content = role };
        }

        for (var i = systemMessageOffset; i < requestMessages.Length; i++) {
            var message = inputArray[i - systemMessageOffset];
            requestMessages[i] = new RoleContentMessage {
                role = message.role == Role.User ? "user" : "assistant", content = message.text
            };
        }

        return requestMessages;
    }

    private class ChatGptContainer : MonoBehaviour {
        private static ChatGptContainer _instance;
        internal static ChatGptContainer Instance {
            get {
                if (_instance == null) {
                    var container = new GameObject("ChatGptContainer");
                    DontDestroyOnLoad(container);
                    container.hideFlags = HideFlags.HideInHierarchy;
                    _instance = container.AddComponent<ChatGptContainer>();
                }

                return _instance;
            }
        }

        private void OnApplicationQuit() {
            CancelAllRequests();
        }
    }

#pragma warning disable 0649
// ReSharper disable NotAccessedField.Local
    [Serializable]
    private struct RequestMessage
    {
        public string model;
        public RoleContentMessage[] messages;
        public float temperature;

        // Omitted fields: int n, string stop, int max_tokens,
        // float presence_penalty, float frequency_penalty;
    }
    [Serializable]
    private struct JsonRequestMessage
    {
        public string model;
        public RoleContentMessage[] messages;
        public float temperature;

        public JsonFormat response_format;
        // Omitted fields: int n, string stop, int max_tokens,
        // float presence_penalty, float frequency_penalty;
    }
    [Serializable]
    private struct JsonFormat
    {
        public string type;
    }
    [Serializable]
    private struct RoleContentMessage {
        public string role;
        public string content;
    }

    [Serializable]
    private struct ResponseMessage {
        public string id;
        public string created;
        public ResponseChoice[] choices;
        public Usage usage;
    }

    [Serializable]
    private struct ResponseChoice {
        public int index;
        public RoleContentMessage delta;
        public RoleContentMessage message;
        public string finish_reason;
    }

    [Serializable]
    private struct Usage {
        public int completion_tokens;
        public int prompt_tokens;
        public int total_tokens;
    }
}
