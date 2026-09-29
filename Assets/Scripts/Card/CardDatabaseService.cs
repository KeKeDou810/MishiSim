using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed partial class CardDatabaseService : MonoBehaviour
{
    private static CardDatabaseService instance;
    public static CardDatabaseService Instance
    {
        get
        {
            if (instance == null)
            {
                if (!Application.isPlaying) throw new InvalidOperationException("请在 Play Mode 使用数据库服务。");
                instance = FindFirstObjectByType<CardDatabaseService>();
                if (instance == null) instance = new GameObject("GameSession").AddComponent<CardDatabaseService>();
            }
            return instance;
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        ContentRoot = ExternalContentPath.Resolve(Application.dataPath, Application.isEditor);
    }
    public string ContentRoot { get; private set; }
    public bool IsLoaded { get; private set; }
    public bool IsBattleActive { get; private set; }
    public string LastError { get; private set; }
    public DeckModel SelectedDeck { get; private set; } = new DeckModel();
    public IReadOnlyList<GameMode> Modes { get; private set; } = Array.Empty<GameMode>();
    public GameMode ActiveMode => SelectedDeck.Mode;
    private readonly Dictionary<string, string> deckNames = new Dictionary<string, string>();
    public string SelectedDeckName => ActiveMode.Id != null && deckNames.TryGetValue(ActiveMode.Id, out var name) ? name : "未保存卡组";
    public string DeckDirectory => Path.Combine(ContentRoot, "Decks");
    public string[] SavedDeckNames() => DeckStorage.List(DeckDirectory);
    public void SaveDeck(string name)
    {
        EnsureLoaded();
        if (IsBattleActive) throw new InvalidOperationException("战斗期间不能保存卡组。");
        DeckStorage.Save(DeckDirectory, name, SelectedDeck);
        deckNames[ActiveMode.Id] = name;
    }
    public void LoadDeck(string name)
    {
        EnsureLoaded();
        if (IsBattleActive) throw new InvalidOperationException("战斗期间不能读取卡组。");
        var data = DeckStorage.Read(DeckDirectory, name);
        var mode = Modes.SingleOrDefault(m => m.Id == data.ModeId) ?? throw new InvalidDataException("存档使用的游戏模式不存在。");
        var empty = new DeckModel(mode);
        empty.LoadLimits(Path.Combine(ContentRoot, "Rules", "banlist.json"));
        var next = DeckStorage.Validate(data, empty, database.Get);
        modeDecks[mode.Id] = SelectedDeck = next;
        deckNames[mode.Id] = name;
        Notify(DeckChanged);
    }
    private readonly Dictionary<string, DeckModel> modeDecks = new Dictionary<string, DeckModel>();
    public void SelectMode(string id)
    {
        EnsureLoaded();
        if (IsBattleActive) throw new InvalidOperationException("战斗期间不能切换模式。");
        var mode = Modes.Single(m => m.Id == id);
        if (!modeDecks.TryGetValue(id, out var deck))
        {
            deck = new DeckModel(mode);
            deck.LoadLimits(Path.Combine(ContentRoot, "Rules", "banlist.json"));
            modeDecks.Add(id, deck);
        }
        SelectedDeck = deck;
        Notify(DeckChanged);
    }
    public DeckModel ValidateDeck(IEnumerable<string> ids)
    {
        var deck = SelectedDeck.EmptyCopy();
        if (ids == null) throw new InvalidDataException("未提交卡组。");
        var cards = ids.Take(ActiveMode.DeckSize + 1).Select(id => database.Get(id)).ToArray();
        foreach (var card in cards.OrderBy(c => c.IsContract ? 0 : 1))
            if (!deck.TryAdd(card, out string error)) throw new InvalidDataException(error);
        if (!deck.ValidateComplete(out string completeError)) throw new InvalidDataException(completeError);
        return deck;
    }
    public void LoadExampleDeck()
    {
        EnsureLoaded();
        if (IsBattleActive) throw new InvalidOperationException("战斗期间不能修改卡组。");
        var example = ActiveMode.ExampleDeck ?? throw new InvalidOperationException("此模式没有 exampleDeck。");
        var deck = ValidateDeck(example.SelectMany(e => Enumerable.Repeat(e.Key, e.Value)));
        modeDecks[ActiveMode.Id] = SelectedDeck = deck;
        deckNames.Remove(ActiveMode.Id);
        Notify(DeckChanged);
    }
    public IEnumerable<CardDefinition> AllCards => database.All;
    public int CardCount => database.Count;
    public event Action DatabaseReloaded;
    public event Action DeckChanged;
    public event Action<string> ReloadFailed;
    private CardDatabase database = new CardDatabase();
    private readonly HashSet<UnityEngine.Object> editors = new HashSet<UnityEngine.Object>();
    private float nextCheck;
    private string observedSignature;
    private string attemptedSignature;

    public void RegisterEditor(UnityEngine.Object owner) { editors.Add(owner); }
    public void UnregisterEditor(UnityEngine.Object owner) { editors.Remove(owner); }
    public void EnsureLoaded()
    {
        if (!IsLoaded && !ReloadDatabase()) throw new InvalidOperationException(LastError);
    }
    public CardDefinition GetCard(string id) { EnsureLoaded(); return database.Get(id); }
    public bool TryAdd(string id, out string error)
    {
        if (IsBattleActive) { error = "战斗期间不能修改选用卡组。"; return false; }
        EnsureLoaded();
        if (!SelectedDeck.TryAdd(database.Get(id), out error)) return false;
        Notify(DeckChanged);
        return true;
    }
    public bool TryRemove(string id, out string error)
    {
        if (IsBattleActive) { error = "战斗期间不能修改选用卡组。"; return false; }
        EnsureLoaded();
        if (database.Get(id).IsPlayer) { error = "玩家卡随契约附带，不能单独移除。"; return false; }
        if (!SelectedDeck.TryRemove(id, out error)) return false;
        Notify(DeckChanged);
        return true;
    }
    public BattleCardSnapshot BeginBattle()
    {
        EnsureLoaded();
        if (IsBattleActive) throw new InvalidOperationException("已有战斗正在进行。");
        var snapshot = new BattleCardSnapshot(database.All, SelectedDeck);
        IsBattleActive = true;
        return snapshot;
    }
    public void EndBattle() { IsBattleActive = false; }
    private void Update()
    {
        editors.RemoveWhere(owner => owner == null);
        if (editors.Count == 0 || IsBattleActive || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .5f;
        try
        {
            string signature = GetDatabaseSignature();
            if (signature != observedSignature) { observedSignature = signature; return; }
            if (signature == attemptedSignature) return;
            attemptedSignature = signature;
            ReloadDatabase();
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    public bool ReloadDatabase()
    {
        if (IsBattleActive) { ReportError("战斗期间已锁定卡牌数据，请结束战斗后重新加载。"); return false; }
        try
        {
            string before = GetDatabaseSignature();
            var nextDatabase = new CardDatabase();
            nextDatabase.LoadDirectory(Path.Combine(ContentRoot, "Cards"));
            var modes = GameMode.Load(Path.Combine(ContentRoot, "Rules", "game-modes.json"));
            string selectedId = ActiveMode.Id ?? modes[0].Id;
            if (!modes.Any(m => m.Id == selectedId)) throw new InvalidDataException("当前模式已删除，请恢复其配置后再切换。");
            var nextDecks = new Dictionary<string, DeckModel>();
            foreach (var mode in modes)
            {
                var nextDeck = new DeckModel(mode);
                nextDeck.LoadLimits(Path.Combine(ContentRoot, "Rules", "banlist.json"));
                var oldDeck = modeDecks.TryGetValue(mode.Id, out var saved) ? saved : mode.Id == selectedId ? SelectedDeck : new DeckModel(mode);
                foreach (var entry in oldDeck.Entries.OrderBy(e => oldDeck.Contract != null && e.Key == oldDeck.Contract.Id ? 0 : 1))
                    for (int i = 0; i < entry.Value; i++)
                        if (!nextDeck.TryAdd(nextDatabase.Get(entry.Key), out string error))
                            throw new InvalidDataException($"现有卡组无法使用新数据：{error}");
                nextDecks.Add(mode.Id, nextDeck);
            }
            string after = GetDatabaseSignature();
            if (before != after)
            {
                attemptedSignature = null;
                throw new IOException("文件正在修改，将自动重试。");
            }
            database = nextDatabase;
            Modes = Array.AsReadOnly(modes);
            modeDecks.Clear();
            foreach (var pair in nextDecks) modeDecks.Add(pair.Key, pair.Value);
            SelectedDeck = modeDecks[selectedId];
            IsLoaded = true;
            LastError = null;
            observedSignature = attemptedSignature = after;
        }
        catch (Exception e) { ReportError($"重新加载失败，保留上次数据：{e.Message}"); return false; }
        Notify(DatabaseReloaded);
        EnsureSleeveTexture(true);
        return true;
    }
    private string GetDatabaseSignature()
    {
        var text = new StringBuilder();
        string folder = Path.Combine(ContentRoot, "Cards");
        if (!Directory.Exists(folder)) return "missing-cards-directory";
        using (SHA256 hash = SHA256.Create())
        {
            foreach (string path in Directory.GetFiles(folder, "*.lua", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                text.Append(path).Append(':').Append(Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)))).Append('\n');
            string rules = Path.Combine(ContentRoot, "Rules");
            if (Directory.Exists(rules)) foreach (string path in Directory.GetFiles(rules, "*.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                text.Append(path).Append(Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))));
        }
        return text.ToString();
    }
    private void ReportError(string message)
    {
        LastError = message;
        Debug.LogWarning(message, this);
        if (ReloadFailed == null) return;
        foreach (Action<string> subscriber in ReloadFailed.GetInvocationList())
            try { subscriber(message); } catch (Exception e) { Debug.LogException(e); }
    }
    private static void Notify(Action handlers)
    {
        if (handlers == null) return;
        foreach (Action subscriber in handlers.GetInvocationList())
            try { subscriber(); } catch (Exception e) { Debug.LogException(e); }
    }
    private void OnDestroy() { DisposeSleeve(); if (instance == this) instance = null; }
}

