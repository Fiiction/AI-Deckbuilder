using System;

internal sealed class RequestRecord {
    private Action _cancelCallback;

    public void SetCancelCallback(Action callback) {
        _cancelCallback = callback;
    }

    public void Cancel() {
        _cancelCallback?.Invoke();
    }
}