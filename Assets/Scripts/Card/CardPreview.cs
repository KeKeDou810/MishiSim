using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

public sealed class CardPreview : MonoBehaviour,
    MMEventListener<CardPreviewRequestEvent>
{

    [Header("UI")]
    [SerializeField] private Image artworkImage;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private ScrollRect detailsScroll;
    [SerializeField] private GameObject battleTabs;
    [SerializeField] private Button effectTab;
    [SerializeField] private Button logTab;
    [Header("Artwork cache")]
    [SerializeField, Min(1)] private int cachedArtworkLimit = 16;
    [SerializeField, Min(1)] private int cachedArtworkMegabytes = 64;
    private string effectContent = "暂无卡片。", logContent = "暂无操作记录。", logMatch;
    private bool showingLog;
    private float effectPosition = 1f, logPosition = 1f;
    private bool hasBattleTabs;
    // Battle overlays animate this same authored view; deck building keeps it embedded.
    public event Action Shown;

    private void Awake()
    {
        if (effectTab != null) effectTab.onClick.AddListener(() => SelectPage(false));
        if (logTab != null) logTab.onClick.AddListener(() => SelectPage(true));
        if (battleTabs != null) battleTabs.SetActive(false);
        if (battleTabs != null && detailsScroll != null)
        {
            var rect = (RectTransform)detailsScroll.transform;
            rect.anchoredPosition += new Vector2(0, 19); rect.sizeDelta += new Vector2(0, 38);
        }
    }
    public void SetBattleLog(string matchId, string[] entries)
    {
        if (battleTabs != null) battleTabs.SetActive(true);
        if (!hasBattleTabs && battleTabs != null && detailsScroll != null)
        {
            var rect = (RectTransform)detailsScroll.transform;
            rect.anchoredPosition -= new Vector2(0, 19); rect.sizeDelta -= new Vector2(0, 38);
        }
        hasBattleTabs = true;
        if (logMatch != matchId) { logMatch = matchId; logPosition = 1; }
        string content = entries == null || entries.Length == 0 ? "暂无操作记录。" : string.Join("\n\n", System.Linq.Enumerable.Reverse(entries));
        if (content == logContent) return;
        logContent = content;
        if (showingLog) RenderPage(false);
    }
    public void SelectPage(bool log)
    {
        if (showingLog == log) return;
        if (detailsScroll != null)
        {
            if (showingLog) logPosition = detailsScroll.verticalNormalizedPosition;
            else effectPosition = detailsScroll.verticalNormalizedPosition;
        }
        showingLog = log;
        RenderPage(true);
    }
    public void ScrollEffect(float delta)
    {
        if (detailsScroll == null || EventSystem.current == null || Mathf.Abs(delta) < .001f) return;
        if (showingLog) SelectPage(false);
        ApplyPendingLayout();
        // Use the same ScrollRect path and scroll sensitivity as wheeling over the text itself.
        detailsScroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, delta) });
        effectPosition = detailsScroll.verticalNormalizedPosition;
    }
    private void RenderPage(bool restore)
    {
        if (detailsText == null) return;
        float position = restore ? (showingLog ? logPosition : effectPosition) : detailsScroll != null ? detailsScroll.verticalNormalizedPosition : 1f;
        detailsText.richText = !showingLog;
        detailsText.text = showingLog ? logContent : effectContent;
        if (effectTab != null) effectTab.interactable = showingLog;
        if (logTab != null) logTab.interactable = !showingLog;
        if (detailsScroll == null) return;
        pendingPosition = position; layoutPending = true;
    }
    private bool layoutPending;
    private float pendingPosition;
    private void LateUpdate() { ApplyPendingLayout(); }
    private void ApplyPendingLayout()
    {
        if (!layoutPending || detailsScroll == null) return;
        layoutPending = false;
        // Rebuild only this description once per frame, never every canvas in the match.
        LayoutRebuilder.ForceRebuildLayoutImmediate(detailsScroll.content);
        detailsScroll.StopMovement(); detailsScroll.verticalNormalizedPosition = pendingPosition;
    }
    private sealed class Artwork
    {
        public Sprite Sprite;
        public long Bytes, LastUse;
    }
    private readonly Dictionary<string, Artwork> artworkCache = new Dictionary<string, Artwork>();
    private long cacheBytes, accessSequence;
    private string loadedKey, requestedKey;
    private UnityWebRequest artworkRequest;
    private Coroutine artworkLoading;
    public void Clear()
    {
        CancelArtworkLoad(); SetArtwork(null, null);
        effectContent = "暂无卡片。";
        RenderPage(false);
    }

    public void OnEnable()
    {
        MMEventManager.AddListener<CardPreviewRequestEvent>(this);
    }

    private void OnDisable()
    {
        MMEventManager.RemoveListener<CardPreviewRequestEvent>(this);
        CancelArtworkLoad();
    }

    public void Show(CardDefinition card, string contentRoot, int? effectivePower = null, int? effectiveTime = null)
    {
        if (artworkImage == null || detailsText == null)
        {
            Debug.LogError("请绑定 Artwork Image 和 Details Text。", this);
            return;
        }

        try
        {
            string content =
                $"{card.Name}\n" +
                $"ID：{card.Id}\n" +
                $"国家：{card.Faction}\n" +
                (card.IsPlayer ? "契约附带玩家卡（不计入50张）\n" :
                    $"时间：{effectiveTime ?? card.Level}\n" +
                    (card.IsDecision ? "" : $"力量：{effectivePower ?? card.Power}\n")) +
                (string.IsNullOrEmpty(card.Sign) ? "" : $"标识：{card.Sign}\n") +
                $"类型：{card.Type}\n" +
                (card.IsPlayer || card.IsDecision || string.IsNullOrEmpty(card.Race) ? "" : $"种族：{card.Race}\n") +
                (string.IsNullOrEmpty(card.EffectText) ? "" : $"\n效果：\n{card.EffectText}");
            if (effectContent != content)
            {
                effectContent = content;
                effectPosition = 1f;
                if (!showingLog) RenderPage(true);
            }
            RequestArtwork(Path.GetFullPath(Path.Combine(contentRoot, card.ArtworkPath)));
        }
        catch (Exception exception)
        {
            CancelArtworkLoad(); SetArtwork(null, null);
            effectContent = "卡片加载失败，请查看 Console。";
            if (!showingLog) RenderPage(true);
            Debug.LogException(exception, this);
        }
        Shown?.Invoke();
    }

    private void RequestArtwork(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("卡图不存在", path);
        // Invalidate external artwork edits without discarding every other cached card.
        string key = path + "\n" + file.LastWriteTimeUtc.Ticks + "\n" + file.Length;
        if (loadedKey == key || requestedKey == key) return;
        CancelArtworkLoad();
        if (artworkCache.TryGetValue(key, out var cached))
        {
            cached.LastUse = ++accessSequence;
            SetArtwork(cached.Sprite, key);
            return;
        }
        SetArtwork(null, null);
        requestedKey = key;
        artworkRequest = UnityWebRequestTexture.GetTexture(new Uri(path).AbsoluteUri, nonReadable: true);
        artworkLoading = StartCoroutine(LoadArtwork(artworkRequest, key));
    }
    private IEnumerator LoadArtwork(UnityWebRequest request, string key)
    {
        yield return request.SendWebRequest();
        if (artworkRequest != request || requestedKey != key) yield break;
        try
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("预览卡图加载失败：" + request.error, this);
                yield break;
            }
            var texture = DownloadHandlerTexture.GetContent(request);
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            long bytes = (long)texture.width * texture.height * 4;
            if (texture.mipmapCount > 1) bytes = bytes * 4 / 3;
            artworkCache.Add(key, new Artwork { Sprite = sprite, Bytes = bytes, LastUse = ++accessSequence });
            cacheBytes += bytes;
            SetArtwork(sprite, key);
            TrimArtworkCache();
        }
        finally
        {
            request.Dispose(); artworkRequest = null; requestedKey = null; artworkLoading = null;
        }
    }
    private void SetArtwork(Sprite sprite, string key)
    {
        loadedKey = key;
        if (artworkImage == null) return;
        artworkImage.sprite = sprite;
        artworkImage.enabled = sprite != null;
        artworkImage.preserveAspect = true;
    }
    private void TrimArtworkCache()
    {
        while (artworkCache.Count > 1 && (artworkCache.Count > Mathf.Max(1, cachedArtworkLimit) ||
            cacheBytes > (long)Mathf.Max(1, cachedArtworkMegabytes) * 1024 * 1024))
        {
            string oldest = null; long age = long.MaxValue;
            foreach (var pair in artworkCache)
                if (pair.Key != loadedKey && pair.Value.LastUse < age) { oldest = pair.Key; age = pair.Value.LastUse; }
            if (oldest == null) break;
            var entry = artworkCache[oldest];
            cacheBytes -= entry.Bytes; artworkCache.Remove(oldest);
            Destroy(entry.Sprite.texture); Destroy(entry.Sprite);
        }
    }
    private void CancelArtworkLoad()
    {
        if (artworkLoading != null) StopCoroutine(artworkLoading);
        artworkLoading = null;
        if (artworkRequest != null) { artworkRequest.Abort(); artworkRequest.Dispose(); }
        artworkRequest = null; requestedKey = null;
    }

    private void OnDestroy()
    {
        CancelArtworkLoad(); SetArtwork(null, null);
        foreach (var entry in artworkCache.Values)
        { Destroy(entry.Sprite.texture); Destroy(entry.Sprite); }
        artworkCache.Clear(); cacheBytes = 0;
    }

    public void OnMMEvent(CardPreviewRequestEvent e)
    {
        Show(e.Card, e.ContentRoot, e.EffectivePower, e.EffectiveTime);
    }
}
