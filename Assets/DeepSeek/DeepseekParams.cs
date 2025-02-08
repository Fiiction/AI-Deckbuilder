using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class DeepseekParams : ISerializationCallbackReceiver {
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

    public DeepseekParams(string apiKey) {
        this.apiKey = apiKey;
    }

    public DeepseekParams(DeepseekParams parameters) {
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