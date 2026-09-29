using System;
using System.IO;
using System.Linq;
using Mishi.Battle;
using Mishi.Networking;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class EffectTestLabWindow : EditorWindow
{
    private string[] files = Array.Empty<string>();
    private int scenarioIndex, cardIndex, owner, nodeIndex, stackOrder, cardCount = 1, offFieldNode;
    private string search = "", message = "", saveName = "custom-combo";
    private Vector2 scroll;
    private TestCardZone zone = TestCardZone.Hand;
    private EffectTestScenario draft;
    private BattleNetworkSession Session => FindObjectsByType<BattleNetworkSession>(FindObjectsSortMode.None).FirstOrDefault(s => s.IsEffectTest);
    private string TestRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../Content/Tests"));
    [MenuItem("Tools/Mishi/Effect Test Lab")]
    public static void Open() => GetWindow<EffectTestLabWindow>("效果实验室");
    private void OnEnable() { RefreshFiles(); EditorApplication.playModeStateChanged += OnPlayState; }
    private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayState;
    private void OnPlayState(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) draft = null; Repaint(); }
    private void RefreshFiles()
    {
        files = Directory.Exists(TestRoot) ? Directory.GetFiles(TestRoot, "*.json").OrderBy(p => p).ToArray() : Array.Empty<string>();
        scenarioIndex = Mathf.Clamp(scenarioIndex, 0, Math.Max(0, files.Length - 1));
    }
    private void Run(Action action) { try { action(); message = ""; } catch (Exception e) { message = e.GetBaseException().Message; } }
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("预设初始摆放不会触发登场。测试登场效果时，将卡加入手牌，再在场景内正常召唤。所有后续操作使用正式结算内核。", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            if (GUILayout.Button("打开 EffectTestLab 场景")) Run(() => {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene("Assets/Scenes/EffectTestLab.unity");
            });
        if (GUILayout.Button("刷新预设列表")) RefreshFiles();
        if (files.Length > 0) scenarioIndex = EditorGUILayout.Popup("测试预设", scenarioIndex, files.Select(Path.GetFileNameWithoutExtension).ToArray());
        var session = Session;
        if (!EditorApplication.isPlaying || session == null)
        {
            EditorGUILayout.HelpBox("打开实验室场景后进入 Play Mode，再使用下面的测试控制。", MessageType.Info);
            EditorGUILayout.EndScrollView(); return;
        }
        if (draft == null && session.CurrentEffectTest != null) draft = Clone(session.CurrentEffectTest);
        if (files.Length > 0 && GUILayout.Button("载入预设并重置")) Run(() => { session.LoadEffectTest("Tests/" + Path.GetFileName(files[scenarioIndex])); draft = Clone(session.CurrentEffectTest); });
        session.EffectTestPaused = EditorGUILayout.Toggle("暂停所有倒计时", session.EffectTestPaused);
        int viewer = EditorGUILayout.Popup("操作／观看玩家", session.EffectTestPlayer, new[] { "P1", "P2" });
        if (viewer != session.EffectTestPlayer) session.SwitchEffectTestPlayer(viewer);
        if (GUILayout.Button("模拟经过 21 秒（测试选择超时）")) session.StepEffectTestTime(21);
        if (draft != null)
        {
            EditorGUILayout.LabelField(draft.Name, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(draft.Instructions, MessageType.None);
            if (GUILayout.Button("重置当前局面")) Run(() => session.BeginEffectTest(draft));
            if (GUILayout.Button("重载 Lua 并重置")) Run(() => session.BeginEffectTest(draft, true));
            draft.ActivePlayer = EditorGUILayout.Popup("初始回合玩家", draft.ActivePlayer, new[] { "P1", "P2" });
            draft.Phase = (TestTurnPhase)EditorGUILayout.EnumPopup("初始阶段", draft.Phase);
            for (int p = 0; p < 2; p++)
            {
                draft.Clocks[p] = (ClockKind)EditorGUILayout.EnumPopup($"P{p + 1} 时钟", draft.Clocks[p]);
                draft.Cost[p] = EditorGUILayout.IntSlider($"P{p + 1} 费用", draft.Cost[p], 0, 11);
                draft.Damage[p] = EditorGUILayout.IntSlider($"P{p + 1} 伤害指针", draft.Damage[p], 1, 11);
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("配置单卡／组合（修改后重置应用）", EditorStyles.boldLabel);
            search = EditorGUILayout.TextField("卡名／ID 搜索", search);
            var cards = CardDatabaseService.Instance.AllCards.Where(c => !c.IsPlayer && (c.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || c.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(c => c.Id).ToArray();
            if (cards.Length > 0)
            {
                cardIndex = EditorGUILayout.Popup("卡片", Mathf.Clamp(cardIndex, 0, cards.Length - 1), cards.Select(c => c.Id + " · " + c.Name).ToArray());
                owner = EditorGUILayout.Popup("所属玩家", owner, new[] { "P1", "P2" });
                zone = (TestCardZone)EditorGUILayout.EnumPopup("初始区域", zone);
                var nodes = session.EffectTestNodes;
                if (zone == TestCardZone.Board && nodes.Length > 0) nodeIndex = EditorGUILayout.Popup("圆阵", Mathf.Clamp(nodeIndex, 0, nodes.Length - 1), nodes.Select(n => n.ToString()).ToArray());
                if (zone == TestCardZone.OffField) offFieldNode = EditorGUILayout.IntField("场外位置编号", offFieldNode);
                if (zone == TestCardZone.Board || zone == TestCardZone.OffField) stackOrder = EditorGUILayout.IntField("叠放顺序（大值在上）", stackOrder);
                else cardCount = EditorGUILayout.IntSlider("数量", cardCount, 1, 100);
                EditorGUILayout.HelpBox(cards[cardIndex].EffectText, MessageType.None);
                if (GUILayout.Button(cards[cardIndex].IsContract ? "替换该玩家契约（自动关联玩家卡）" : "加入测试配置"))
                {
                    var card = cards[cardIndex];
                    if (card.IsContract)
                    {
                        var old = draft.Cards.Single(c => c.Owner == owner && CardDatabaseService.Instance.AllCards.Single(d => d.Id == c.Id).IsContract);
                        old.Id = card.Id; draft.Clocks[owner] = card.Clock;
                    }
                    else draft.Cards = draft.Cards.Concat(new[] { new EffectTestCard { Id = card.Id, Owner = owner, Zone = zone,
                        Count = zone == TestCardZone.Board || zone == TestCardZone.OffField ? 1 : cardCount,
                        StackOrder = zone == TestCardZone.Board || zone == TestCardZone.OffField ? stackOrder : 0,
                        Node = zone == TestCardZone.Board && nodes.Length > 0 ? nodes[nodeIndex] : zone == TestCardZone.OffField ? offFieldNode : -1 } }).ToArray();
                }
            }
            for (int i = 0; i < draft.Cards.Length; i++)
            {
                var item = draft.Cards[i];
                EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField($"P{item.Owner + 1} {item.Id} · {item.Zone} · 圆阵{item.Node} ×{item.Count}");
                if (GUILayout.Button("移除", GUILayout.Width(45))) { draft.Cards = draft.Cards.Where((_, index) => index != i).ToArray(); EditorGUILayout.EndHorizontal(); break; }
                EditorGUILayout.EndHorizontal();
            }
            saveName = EditorGUILayout.TextField("保存预设名", saveName);
            if (GUILayout.Button("保存新预设到 Content/Tests")) Run(() => {
                if (string.IsNullOrWhiteSpace(saveName) || saveName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || saveName.Contains("..")) throw new ArgumentException("预设名无效。");
                string path = Path.Combine(TestRoot, saveName + ".json");
                using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew))) writer.Write(JsonConvert.SerializeObject(draft, Formatting.Indented));
                RefreshFiles();
            });
        }
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Error);
        EditorGUILayout.LabelField(session.EffectTestStatus, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }
    private static EffectTestScenario Clone(EffectTestScenario value) => JsonConvert.DeserializeObject<EffectTestScenario>(JsonConvert.SerializeObject(value));
}
