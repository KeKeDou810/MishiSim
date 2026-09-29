using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;


using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

public sealed class CardBrowser : MonoBehaviour
{
    [SerializeField] private Transform listContent;
    [SerializeField] private Transform deckContent;
    [SerializeField] private CardListItem itemPrefab;
    [SerializeField] private CardListItem libraryItemPrefab;
    [SerializeField] private CardPreview preview;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text deckSummaryText;
    [SerializeField] private CardBrowserFilters filters;
    private readonly List<KeyValuePair<CardDefinition, CardListItem>> libraryItems = new List<KeyValuePair<CardDefinition, CardListItem>>();
    [SerializeField] private bool autoReloadDatabase = true;
    private CardDatabaseService service;
    private DeckModel deck => service.SelectedDeck;
    private bool watching;
    private string contentRoot;
    private bool ready;
    private readonly Dictionary<string, Sprite> artworkCache = new Dictionary<string, Sprite>();
    private readonly List<CardListItem> deckItems = new List<CardListItem>();
    private string selectedId;
    private GridLayoutGroup deckGrid;
    private void OnEnable()
    {
        try
        {
            if (listContent == null || deckContent == null || itemPrefab == null || libraryItemPrefab == null || preview == null || listContent == deckContent)
                throw new InvalidOperationException("请绑定卡库、独立卡组 Content、条目 prefab 和 preview。");
            service = CardDatabaseService.Instance;
            if (filters != null) filters.Changed += ApplyFilters;
            if (filters != null) filters.OrganizationChanged += RebuildDeck;
            contentRoot = service.ContentRoot;
            service.DatabaseReloaded += RefreshDatabaseViews;
            service.DeckChanged += RebuildDeck;
            service.ReloadFailed += Status;
            UpdateWatchRegistration();
            bool wasLoaded = service.IsLoaded;
            service.EnsureLoaded();
            if (wasLoaded) RefreshDatabaseViews();
        }
        catch (Exception e) { Status(e.Message); Debug.LogException(e, this); }
    }
    private void OnDisable()
    {
        if (filters != null) filters.Changed -= ApplyFilters;
        if (filters != null) filters.OrganizationChanged -= RebuildDeck;
        if (service != null)
        {
            service.DatabaseReloaded -= RefreshDatabaseViews;
            service.DeckChanged -= RebuildDeck;
            service.ReloadFailed -= Status;
            service.UnregisterEditor(this);
        }
        watching = false;
        ready = false;
    }
    private void Update() { UpdateWatchRegistration(); }
    private void UpdateWatchRegistration()
    {
        if (service == null || watching == autoReloadDatabase) return;
        watching = autoReloadDatabase;
        if (watching) service.RegisterEditor(this); else service.UnregisterEditor(this);
    }
    private void RefreshDatabaseViews()
    {
        foreach (Transform child in listContent)
        {
            child.gameObject.SetActive(false); Destroy(child.gameObject);
        }
        ClearArtworkCache();
        libraryItems.Clear();
        foreach (var card in service.AllCards.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var item = Instantiate(libraryItemPrefab, listContent);
            item.Bind(card, false, 0, this);
            libraryItems.Add(new KeyValuePair<CardDefinition, CardListItem>(card, item));
        }
        ApplyFilters();
        RebuildDeck();
        var selection = service.AllCards.FirstOrDefault(c => c.Id == selectedId)
            ?? service.AllCards.OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        selectedId = null;
        if (selection != null) Select(selection); else preview.Clear();
        ready = true;
        deckGrid = deckContent.GetComponent<GridLayoutGroup>();
        Status($"卡库已更新：{service.CardCount} 张，当前卡组已保留。");
    }
    public void Select(CardDefinition card)
    {
        if (selectedId == card.Id) return;
        preview.Show(card, contentRoot);
        selectedId = card.Id;
    }
    private void ApplyFilters() => ApplyFilters(true);
    private void ApplyFilters(bool resetScroll)
    {
        if (filters == null) return;
        bool valid = filters.TryGetRange(out int? min, out int? max);
        valid &= filters.TryGetTimeRange(out int? minTime, out int? maxTime);
        int visible = 0;
        var ranks = CardSearch.Organize(libraryItems.Select(item => item.Key), filters.ByTime, filters.Reverse)
            .Select((card, index) => new { card.Id, Index = index }).ToDictionary(item => item.Id, item => item.Index);
        foreach (var entry in libraryItems.Select(item => new { Item = item, Score = CardSearch.Score(item.Key, filters.Query) })
                     .OrderBy(entry => ranks[entry.Item.Key.Id]))
        {
            bool show = valid && deck.IsLibraryEligible(entry.Item.Key) && entry.Score >= 0 && filters.Matches(entry.Item.Key) && CardSearch.MatchesPower(entry.Item.Key, min, max)
                && CardSearch.MatchesTime(entry.Item.Key, minTime, maxTime);
            entry.Item.Value.gameObject.SetActive(show);
            if (show) { entry.Item.Value.transform.SetSiblingIndex(visible); visible++; }
        }
        filters.ShowResult(visible, libraryItems.Count, valid);
        var scroll = listContent.GetComponentInParent<ScrollRect>();
        if (resetScroll && scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; }
    }
    public Sprite GetArtwork(CardDefinition card)
    {
        if (artworkCache.TryGetValue(card.ArtworkPath, out Sprite cached)) return cached;
        Texture2D texture = null;
        try
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(contentRoot, card.ArtworkPath));
            texture = new Texture2D(2, 2);
            if (!ImageConversion.LoadImage(texture, bytes, markNonReadable: true)) throw new InvalidDataException("图片解码失败");
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            artworkCache.Add(card.ArtworkPath, sprite);
            return sprite;
        }
        catch (Exception e)
        {
            if (texture != null) Destroy(texture);
            artworkCache[card.ArtworkPath] = null;
            Debug.LogWarning($"卡图加载失败：{card.ArtworkPath}，{e.Message}", this);
            return null;
        }
    }
    private void LateUpdate()
    {
        if (deckGrid == null) return;
        float width = ((RectTransform)deckContent).rect.width;
        float cellWidth = Mathf.Max(1, (width - deckGrid.padding.horizontal - deckGrid.spacing.x * 4) / 5);
        Vector2 size = new Vector2(cellWidth, cellWidth * 1.4f);
        if ((deckGrid.cellSize - size).sqrMagnitude > .01f) deckGrid.cellSize = size;
    }
    private void OnDestroy()
    {
        ClearArtworkCache();
    }
    private void ClearArtworkCache()
    {
        foreach (Sprite sprite in artworkCache.Values)
        {
            if (sprite == null) continue;
            Destroy(sprite.texture); Destroy(sprite);
        }
        artworkCache.Clear();
    }
    public void Add(string id)
    {
        if (!ready) return;
        if (!service.TryAdd(id, out string error)) { Status(error); return; }
        Status($"已添加 {id}，卡组 {deck.Count}/{deck.Mode.DeckSize} 张（含契约）。");
    }
    public void Remove(string id)
    {
        if (!ready) return;
        if (service.GetCard(id).IsPlayer) { Status("玩家卡随契约自动附带，不能单独移除。"); return; }
        if (!service.TryRemove(id, out string error)) { Status(error); return; }
        Status($"已移除一张 {id}，卡组 {deck.Count}/{deck.Mode.DeckSize} 张（含契约）。");
    }
    private void RebuildDeck()
    {
        int used = 0;
        void BindItem(CardDefinition card)
        {
            if (used == deckItems.Count) deckItems.Add(Instantiate(itemPrefab, deckContent));
            var item = deckItems[used++];
            item.gameObject.SetActive(true);
            item.Bind(card, true, 1, this);
        }
        var entries = deck.Entries.ToDictionary(entry => entry.Key, entry => entry.Value);
        foreach (var card in CardSearch.Organize(entries.Keys.Select(service.GetCard), filters != null && filters.ByTime, filters != null && filters.Reverse))
            for (int i = 0; i < entries[card.Id]; i++)
                BindItem(card);
        foreach (string playerId in deck.PlayerCards)
            if (playerId != null)
                BindItem(service.GetCard(playerId));
        for (int i = used; i < deckItems.Count; i++) deckItems[i].gameObject.SetActive(false);
        UpdateSummary();
        ApplyFilters(false);
    }
    private void UpdateSummary()
    {
        string summary = $"{deck.Mode.Name}\n卡组 {deck.Count}/{deck.Mode.DeckSize}（含契约）";
        if (deck.Contract == null) summary += "\n请先选择契约时魔。";
        else
        {
            summary += $"\n契约：{deck.Contract.Name}\n国家：{deck.Contract.Faction}（可用不明国家卡片）";
            for (int i = 0; i < deck.PlayerCards.Count; i++)
            {
                string id = deck.PlayerCards[i];
                string name = id == null ? "待配置" : $"{service.GetCard(id).Name} ({id})";
                summary += $"\n附带玩家卡 {i + 1}：{name}（不计入主卡组）";
            }
        }
        if (deckSummaryText != null) deckSummaryText.text = summary;
        else Debug.Log(summary, this);
    }
    private void Status(string message)
    {
        if (statusText != null) statusText.text = message; else Debug.Log(message, this);
    }

    [Button, ContextMenu("Reload Database")]
    public void ReloadDatabase()
    {
        if (!Application.isPlaying) { Debug.Log("请在 Play Mode 中重新加载卡库。", this); return; }
        CardDatabaseService.Instance.ReloadDatabase();
    }
}
