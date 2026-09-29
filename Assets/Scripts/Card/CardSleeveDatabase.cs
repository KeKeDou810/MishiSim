using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public sealed partial class CardDatabaseService
{
    public Texture2D SleeveTexture { get; private set; }
    public event Action SleeveChanged;
    private Coroutine sleeveLoad;
    private UnityWebRequest sleeveRequest;
    private bool sleeveRequested;
    public void EnsureSleeveTexture(bool reload = false)
    {
        if (!reload && sleeveRequested) return;
        sleeveRequested = true;
        if (sleeveLoad != null) StopCoroutine(sleeveLoad);
        sleeveRequest?.Abort(); sleeveRequest?.Dispose(); sleeveRequest = null;
        sleeveLoad = StartCoroutine(LoadSleeve());
    }
    private IEnumerator LoadSleeve()
    {
        string path = Path.Combine(ContentRoot, "Artwork", "Sleeve.png");
        var request = sleeveRequest = UnityWebRequestTexture.GetTexture(new Uri(Path.GetFullPath(path)).AbsoluteUri, true);
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            var old = SleeveTexture;
            SleeveTexture = DownloadHandlerTexture.GetContent(request);
            SleeveTexture.wrapMode = TextureWrapMode.Clamp;
            SleeveTexture.filterMode = FilterMode.Bilinear;
            Notify(SleeveChanged);
            if (old != null) Destroy(old);
        }
        else Debug.LogWarning("无法加载外部卡背，保留原卡背：" + path + " · " + request.error, this);
        request.Dispose(); sleeveRequest = null; sleeveLoad = null;
    }
    private void DisposeSleeve()
    {
        if (sleeveLoad != null) StopCoroutine(sleeveLoad);
        sleeveRequest?.Abort(); sleeveRequest?.Dispose(); sleeveRequest = null;
        if (SleeveTexture != null) Destroy(SleeveTexture);
        SleeveTexture = null;
    }
}
