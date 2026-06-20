using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class LLMParams : ISerializationCallbackReceiver
{
    public string name;
    public string apiKey;
    public string url;
    public string modelName;
    public float temperature;
    [CanBeNull]
    public string role;

    public int timeout;
    public int throttle;

    [SerializeField, HideInInspector, FormerlySerializedAs("serialized")]
    private bool _serialized;

    public LLMParams(string apiKey) {
        this.apiKey = apiKey;
    }

    public LLMParams(string _url, string _modelName, string _apiKey, float _temperature = 0.4f)
    {
        url = _url;
        modelName = _modelName;
        apiKey = _apiKey;
        temperature = _temperature;
        role = "";
        _serialized = false;
        throttle = -1;
        timeout = -1;
    }
    public LLMParams(LLMParams parameters) {
        apiKey = parameters.apiKey;
        temperature = parameters.temperature;
        timeout = parameters.timeout;
        role = parameters.role;
        _serialized = parameters._serialized;
        throttle = parameters.throttle;
    }

    public void OnBeforeSerialize() {
        if (_serialized) return;
        _serialized = true;
        temperature = 1;
        timeout = 0;
        throttle = 0;
    }

    public void OnAfterDeserialize() { }
}