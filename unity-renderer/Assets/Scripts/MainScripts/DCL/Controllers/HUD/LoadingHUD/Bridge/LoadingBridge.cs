using System;
using DCL;
using UnityEngine;

public class LoadingBridge : MonoBehaviour
{
    [Serializable]
    public class Payload
    {
        public bool isVisible;
        public string message;
    }

    public void SetLoadingScreen(string jsonMessage)
    {
        Payload payload = JsonUtility.FromJson<Payload>(jsonMessage);
        if (string.IsNullOrEmpty(payload.message))
            DataStore.i.HUDs.loadingHUDMessage.Set(payload.message);
        DataStore.i.HUDs.loadingHUDVisible.Set(payload.isVisible);
    }
}