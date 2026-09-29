using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using NUnit.Framework;
using Object = UnityEngine.Object;

// Unity runtime geometry tests; run in EditMode Test Runner, not the standalone rule harness.
public sealed class BattleInteractionViewTests
{
    [Test] public void SteamHallAndPasswordInputsAreAuthoredInRoomPrefab()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RoomFlowUI.prefab");
        var fields = new UnityEditor.SerializedObject(prefab.GetComponent(RuntimeType("Mishi.Networking.SteamLobbyView")));
        foreach (string field in new[] { "panel", "status", "refresh", "previous", "next", "create", "join", "back", "playerName", "roomName", "password", "mode", "DirectConnect" })
            Assert.NotNull(fields.FindProperty(field).objectReferenceValue, field);
        var rows = fields.FindProperty("rows");
        Assert.AreEqual(6, rows.arraySize);
        for (int i = 0; i < rows.arraySize; i++) Assert.NotNull(rows.GetArrayElementAtIndex(i).objectReferenceValue);
        var flow = new UnityEditor.SerializedObject(prefab.GetComponent(RuntimeType("Mishi.Networking.RoomFlowView")));
        Assert.NotNull(flow.FindProperty("roomPassword").objectReferenceValue);
    }

    [Test] public void SteamTransportPrefabUsesDedicatedVirtualPort()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/SteamNetworkManager.prefab");
        var fields = new UnityEditor.SerializedObject(prefab.GetComponent(RuntimeType("FishySteamworks.FishySteamworks")));
        Assert.IsTrue(fields.FindProperty("_peerToPeer").boolValue);
        Assert.AreEqual(27341, fields.FindProperty("_port").intValue);
    }

    [Test] public void OpeningUiHasAuthoredChoicesAndPrivateAndPublicCardRows()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/OpeningMatchUI.prefab");
        var table = new UnityEditor.SerializedObject(prefab.GetComponent(RuntimeType("Mishi.Networking.OpeningGestureTable")));
        Assert.NotNull(table.FindProperty("cardPrefab").objectReferenceValue);
        var view = prefab.GetComponent(RuntimeType("Mishi.Networking.OpeningMatchView"));
        var fields = new UnityEditor.SerializedObject(view);
        foreach (var name in new[] { "panel", "gesturePanel", "handPanel", "title", "hint", "first", "second", "keep", "redraw" })
            Assert.NotNull(fields.FindProperty(name).objectReferenceValue, name);
        foreach (var name in new[] { "gestures", "ownSlots", "revealSlots" })
        {
            var array = fields.FindProperty(name);
            Assert.AreEqual(name == "gestures" ? 3 : 5, array.arraySize);
            for (int i = 0; i < array.arraySize; i++) Assert.NotNull(array.GetArrayElementAtIndex(i).objectReferenceValue);
        }
    }

    [Test] public void OpeningGestureCardHasAReadableAuthoredFaceAndOrdinaryCardMovement()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Card/OpeningGestureCard.prefab");
        Assert.NotNull(prefab.GetComponent(RuntimeType("BattleCard")));
        Assert.NotNull(prefab.GetComponent(RuntimeType("BattleCardPointer")));
        var fields = new UnityEditor.SerializedObject(prefab.GetComponent(RuntimeType("Mishi.Networking.OpeningGestureCard")));
        foreach (string field in new[] { "face", "front", "caption" }) Assert.NotNull(fields.FindProperty(field).objectReferenceValue, field);
        foreach (var transform in prefab.GetComponentsInChildren<Transform>(true)) Assert.IsFalse(transform.name.Contains("\n"));
    }

    [Test] public void ChatShortcutIgnoresDestroyedOrHiddenSelectionButProtectsOtherInputs()
    {
        var inputType = RuntimeType("TMPro.TMP_InputField");
        var check = RuntimeType("Mishi.Networking.ChatInputFocus").GetMethod("BlocksShortcut", new[] { typeof(GameObject), inputType });
        var stale = Create("destroyed selection");
        Object.DestroyImmediate(stale);
        Assert.AreEqual(false, check.Invoke(null, new object[] { stale, null }));
        var field = Create("other input");
        var input = field.AddComponent(inputType);
        Assert.AreEqual(true, check.Invoke(null, new object[] { field, null }));
        Assert.AreEqual(false, check.Invoke(null, new object[] { field, input }), "Own chat must be able to regain focus.");
        field.SetActive(false);
        Assert.AreEqual(false, check.Invoke(null, new object[] { field, null }));
    }

    [Test] public void RoomUiContainsConnectionReadinessChatAndHostSettingsReferences()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RoomFlowUI.prefab");
        var view = prefab.GetComponent(RuntimeType("Mishi.Networking.RoomFlowView"));
        var fields = new UnityEditor.SerializedObject(view);
        foreach (string name in new[] { "home", "connection", "lobby", "failure", "address", "port", "roomName", "playerName", "deck", "ready", "startMatch",
            "chatPanel", "chatInput", "openingHand", "drawPerTurn", "turnTime", "actionRefund", "responseTime", "applyRules" })
            Assert.NotNull(fields.FindProperty(name).objectReferenceValue, name);
    }

    [TestCase(90, 10, true)] [TestCase(-90, 10, true)]
    [TestCase(10, 90, false)] [TestCase(20, 20, false)]
    public void HandBrowsingGestureKeepsVerticalCardPlacementSeparate(float x, float y, bool expected)
    {
        var method = RuntimeType("BattleCardPointer").GetMethod("IsHorizontalHandGesture");
        Assert.AreEqual(expected, method.Invoke(null, new object[] { new Vector2(x, y) }));
    }

    [Test] public void CardPrefabHasDistinctFrontAndExternalSleeveRenderers()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Card/Card.prefab");
        var visual = prefab.GetComponentInChildren(RuntimeType("CardVisual"));
        var fields = new UnityEditor.SerializedObject(visual);
        var front = fields.FindProperty("_frontRenderer").objectReferenceValue;
        var back = fields.FindProperty("_backRenderer").objectReferenceValue;
        Assert.NotNull(front); Assert.NotNull(back); Assert.AreNotEqual(front, back);
    }

    [TestCase(0)] [TestCase(1)]
    public void HiddenHandCountUsesPublicCountsForEitherPerspective(int you)
    {
        var type = RuntimeType("Mishi.Networking.NetworkSnapshot");
        var state = Activator.CreateInstance(type);
        type.GetField("You").SetValue(state, you);
        type.GetField("HandCounts").SetValue(state, new[] { 7, 12 });
        var count = RuntimeType("Mishi.Networking.BattleNetworkSession").GetMethod("HiddenHandCount");
        Assert.AreEqual(you == 0 ? 12 : 7, count.Invoke(null, new[] { state, (object)(1 - you) }));
        type.GetField("Spectator").SetValue(state, true);
        Assert.AreEqual(7, count.Invoke(null, new[] { state, (object)0 }));
        Assert.AreEqual(12, count.Invoke(null, new[] { state, (object)1 }));
        type.GetField("HandCounts").SetValue(state, null);
        type.GetField("OpponentHandCount").SetValue(state, 9);
        Assert.AreEqual(9, count.Invoke(null, new[] { state, (object)(1 - you) }));
    }

    [Test] public void PlayerHudHasAuthoredHandCountersAndPhaseControls()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleInteractionUI.prefab");
        var hud = prefab.GetComponent(RuntimeType("Mishi.Networking.BattlePlayerHud"));
        var fields = new UnityEditor.SerializedObject(hud);
        foreach (var name in new[] { "phase", "nextPhase", "opponentHandCount", "spectatorHandCount" })
            Assert.NotNull(fields.FindProperty(name).objectReferenceValue, name);
    }

    [Test] public void CardCanReturnToCachedPoseAfterDirectDragChangesItsTransform()
    {
        var card = Create("moving card").AddComponent(RuntimeType("BattleCard"));
        var move = card.GetType().GetMethod("MoveTo");
        move.Invoke(card, new object[] { Vector3.one, Quaternion.identity, null, true });
        card.transform.position = Vector3.one * 5;
        move.Invoke(card, new object[] { Vector3.one, Quaternion.identity, null, false });
        Assert.AreEqual(Vector3.one, card.transform.position);
    }

    [Test] public void PowerLabelCanDisplayAnEmptyPileCountWithoutEnablingRaycasts()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Card/Card.prefab");
        var instance = Object.Instantiate(prefab); objects.Add(instance);
        var label = instance.GetComponent(RuntimeType("BattleCardPowerLabel"));
        label.GetType().GetMethod("ApplyCount").Invoke(label, new object[] { 0, true, Vector3.zero });
        var fields = new UnityEditor.SerializedObject(label);
        var panel = (RectTransform)fields.FindProperty("panel").objectReferenceValue;
        Assert.IsTrue(panel.gameObject.activeSelf);
        foreach (var graphic in panel.GetComponentsInChildren<UnityEngine.UI.Graphic>()) Assert.IsFalse(graphic.raycastTarget);
        label.GetType().GetMethod("ApplyCount").Invoke(label, new object[] { 0, false, Vector3.zero });
        Assert.IsFalse(panel.gameObject.activeSelf);
    }

    [Test] public void PhaseAdvanceOnlyAllowsUnblockedActivePlayerInMainOrCombat()
    {
        var snapshotType = RuntimeType("Mishi.Networking.NetworkSnapshot");
        var method = RuntimeType("Mishi.Networking.BattlePlayerHud").GetMethod("CanAdvance");
        object state = Activator.CreateInstance(snapshotType);
        void Set(string field, object value) => snapshotType.GetField(field).SetValue(state, value);
        foreach (string field in new[] { "Winner", "ChoicePlayer", "ResponsePlayer", "OccupationNode" }) Set(field, -1);
        for (int phase = 0; phase < 7; phase++)
        {
            Set("Phase", phase);
            Assert.AreEqual(phase == 4 || phase == 5, method.Invoke(null, new[] { state, (object)false }));
        }
        Set("Phase", 4);
        Assert.AreEqual(false, method.Invoke(null, new[] { state, (object)true }));
        foreach (string field in new[] { "Winner", "ChoicePlayer", "ResponsePlayer", "OccupationNode" })
        {
            Set(field, 0);
            Assert.AreEqual(false, method.Invoke(null, new[] { state, (object)false }), field);
            Set(field, -1);
        }
        Set("ActivePlayer", 1);
        Assert.AreEqual(false, method.Invoke(null, new[] { state, (object)false }));
        Set("ActivePlayer", 0); Set("Spectator", true);
        Assert.AreEqual(false, method.Invoke(null, new[] { state, (object)false }));
    }

    [Test] public void RecordingSaveDialogHasAuthoredReferencesAndCanOpenWithoutCreatingControls()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleInteractionUI.prefab");
        var instance = Object.Instantiate(prefab); objects.Add(instance);
        var panel = instance.GetComponent(RuntimeType("Mishi.Networking.BattleRecordingPanel"));
        Assert.NotNull(panel);
        var fields = new UnityEditor.SerializedObject(panel);
        foreach (string field in new[] { "panel", "recordingName", "description", "save", "discard" })
            Assert.NotNull(fields.FindProperty(field).objectReferenceValue, field);
        int before = instance.GetComponentsInChildren<RectTransform>(true).Length;
        panel.GetType().GetMethod("Show").Invoke(panel, new object[] { "测试结束" });
        Assert.IsTrue((bool)panel.GetType().GetProperty("IsOpen").GetValue(panel));
        Assert.AreEqual(before, instance.GetComponentsInChildren<RectTransform>(true).Length);
        panel.GetType().GetMethod("Hide").Invoke(panel, null);
        Assert.IsFalse((bool)panel.GetType().GetProperty("IsOpen").GetValue(panel));
    }
    [TestCase(1)] [TestCase(5)] [TestCase(18)] [TestCase(50)] [TestCase(100)]
    public void HandFanKeepsRotatedAndRaisedCardsInsideAvailableWidth(int total)
    {
        var layout = RuntimeType("Mishi.Networking.HandFanLayout");
        foreach (float width in new[] { 240f, 640f, 1100f })
        {
            var area = new Rect(380, 8, width, 720);
            float height = (float)layout.GetMethod("CardHeight").Invoke(null, new object[] { area });
            int capacity = (int)layout.GetMethod("Capacity").Invoke(null, new object[] { area });
            int count = Math.Min(total, capacity);
            Assert.GreaterOrEqual(capacity, 1); Assert.LessOrEqual(capacity, 18);
            for (int i = 0; i < count; i++)
            {
                var center = (Vector2)layout.GetMethod("Center").Invoke(null, new object[] { area, i, count });
                float angle = (float)layout.GetMethod("Angle").Invoke(null, new object[] { i, count }) * Mathf.Deg2Rad;
                float halfWidth = Mathf.Max(height * .7154f * 1.16f * .5f,
                    height * (.7154f * Mathf.Abs(Mathf.Cos(angle)) + Mathf.Abs(Mathf.Sin(angle))) * .5f);
                Assert.GreaterOrEqual(center.x - halfWidth, area.xMin);
                Assert.LessOrEqual(center.x + halfWidth, area.xMax);
            }
        }
    }
    [Test] public void HandHitSelectionUsesStableColumnsRatherThanLiftedGeometry()
    {
        var camera = Create("hand camera").AddComponent<Camera>();
        var type = RuntimeType("BattleCardPointer");
        var first = Create("hand first").AddComponent(type);
        var second = Create("hand second").AddComponent(type);
        type.GetMethod("SetHandArea").Invoke(first, new object[] { camera, new Rect(400, 0, 140, 220) });
        type.GetMethod("SetHandArea").Invoke(second, new object[] { camera, new Rect(440, 0, 140, 220) });
        var resolve = type.GetMethod("HandAt");
        Assert.AreSame(first, resolve.Invoke(null, new object[] { camera, new Vector2(470, 100) }));
        first.transform.position = new Vector3(100, 200, 300);
        Assert.AreSame(first, resolve.Invoke(null, new object[] { camera, new Vector2(470, 100) }));
        Assert.AreSame(second, resolve.Invoke(null, new object[] { camera, new Vector2(510, 100) }));
        type.GetMethod("ClearHandArea").Invoke(first, null);
        type.GetMethod("ClearHandArea").Invoke(second, null);
        Assert.IsNull(resolve.Invoke(null, new object[] { camera, new Vector2(470, 100) }));
    }
    [Test] public void RestingHandDoesNotClaimThePlayerAreaAndRaisedHoverUsesActualSurface()
    {
        var camera = Create("hand bounds camera").AddComponent<Camera>();
        camera.pixelRect = new Rect(0, 0, 1920, 1080);
        var type = RuntimeType("BattleCardPointer");
        var card = Create("resting hand");
        var pointer = card.AddComponent(type);
        var box = card.AddComponent<BoxCollider>(); box.size = new Vector3(.6195f, .866f, .01f);
        var layout = RuntimeType("Mishi.Networking.HandFanLayout");
        var area = new Rect(16, 8, 1888, 1072);
        var hit = (Rect)layout.GetMethod("HitRect").Invoke(null, new object[] { area, 0, 1 });
        var center = (Vector2)layout.GetMethod("Center").Invoke(null, new object[] { area, 0, 1 });
        float height = (float)layout.GetMethod("CardHeight").Invoke(null, new object[] { area });
        type.GetMethod("SetHandArea").Invoke(pointer, new object[] { camera, hit });
        type.GetMethod("SetHandShape").Invoke(pointer, new object[] { center, new Vector2(height * .7154f, height), 0f });
        var resolve = type.GetMethod("HandAt");
        Assert.AreSame(pointer, resolve.Invoke(null, new object[] { camera, center + Vector2.up * 20 }));
        Vector2 playerPosition = new Vector2(center.x, 250);
        Assert.IsNull(resolve.Invoke(null, new object[] { camera, playerPosition }), "A collapsed hand must not intercept the player's card.");
        card.transform.SetPositionAndRotation(camera.ScreenToWorldPoint(new Vector3(playerPosition.x, playerPosition.y, 3)), camera.transform.rotation);
        Assert.IsNull(resolve.Invoke(null, new object[] { camera, playerPosition }), "Only the hovered hand card gets an extended hit surface.");
        type.GetMethod("SetHandRaised").Invoke(pointer, new object[] { true });
        Assert.AreSame(pointer, resolve.Invoke(null, new object[] { camera, playerPosition }));
        Assert.IsNull(resolve.Invoke(null, new object[] { camera, playerPosition + Vector2.right * 500 }));
        type.GetMethod("ClearHandArea").Invoke(pointer, null);
    }
    [Test] public void ActionConfirmationSubmitsOnlyAfterConfirmAndCancelDiscardsIt()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleInteractionUI.prefab");
        var instance = Object.Instantiate(prefab); objects.Add(instance);
        var menu = instance.GetComponent(RuntimeType("Mishi.Networking.BattleActionMenu"));
        var type = menu.GetType();
        // Drive handlers directly because regular MonoBehaviour Awake is not guaranteed in EditMode.
        var choose = type.GetMethod("Choose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var cancel = type.GetMethod("Cancel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        int submitted = 0;
        Action submit = () => submitted++;
        type.GetMethod("ConfirmAction").Invoke(menu, new object[] { "确认登场？", submit });
        Assert.AreEqual(0, submitted);
        cancel.Invoke(menu, null);
        Assert.AreEqual(0, submitted);
        choose.Invoke(menu, new object[] { Mishi.Battle.TestCommandKind.Summon });
        Assert.AreEqual(0, submitted, "Cancelled confirmation must not survive.");
        type.GetMethod("ConfirmAction").Invoke(menu, new object[] { "确认攻击？", submit });
        choose.Invoke(menu, new object[] { Mishi.Battle.TestCommandKind.Summon });
        Assert.AreEqual(1, submitted);
        choose.Invoke(menu, new object[] { Mishi.Battle.TestCommandKind.Summon });
        Assert.AreEqual(1, submitted, "Confirm must execute only once.");
    }
    [Test] public void DedicatedEffectLabIsAuthoredWithoutEnablingLabOnNetworkScene()
    {
        string lab = System.IO.File.ReadAllText("Assets/Scenes/EffectTestLab.unity");
        string battle = System.IO.File.ReadAllText("Assets/Scenes/CardTestGym.unity");
        Assert.IsTrue(lab.Contains("effectTestMode: 1"));
        Assert.IsTrue(lab.Contains("defaultEffectTest: Tests/01-summon-combo.json"));
        Assert.IsFalse(battle.Contains("effectTestMode: 1"));
    }
    [Test] public void PreviewTabsAreAuthoredAndLogSurvivesPreviewClear()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PreviewPanel.prefab");
        var instance = Object.Instantiate(prefab); objects.Add(instance);
        var preview = instance.GetComponentInChildren(RuntimeType("CardPreview"));
        var fields = new UnityEditor.SerializedObject(preview);
        foreach (string field in new[] { "battleTabs", "effectTab", "logTab", "detailsText", "detailsScroll" })
            Assert.NotNull(fields.FindProperty(field).objectReferenceValue, field);
        preview.GetType().GetMethod("SetBattleLog").Invoke(preview, new object[] { "test", new[] { "first", "second" } });
        preview.GetType().GetMethod("SelectPage").Invoke(preview, new object[] { true });
        preview.GetType().GetMethod("Clear").Invoke(preview, null);
        var text = fields.FindProperty("detailsText").objectReferenceValue;
        Assert.AreEqual("second\n\nfirst", text.GetType().GetProperty("text").GetValue(text));
        preview.GetType().GetMethod("SelectPage").Invoke(preview, new object[] { false });
        Assert.AreEqual("暂无卡片。", text.GetType().GetProperty("text").GetValue(text));
    }
    [Test] public void SelectingCurrentPreviewTabDoesNotRequestAnotherLayoutRebuild()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PreviewPanel.prefab");
        var instance = Object.Instantiate(prefab); objects.Add(instance);
        var preview = instance.GetComponentInChildren(RuntimeType("CardPreview"));
        var type = preview.GetType();
        var pending = type.GetField("layoutPending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        pending.SetValue(preview, false);
        type.GetMethod("SelectPage").Invoke(preview, new object[] { false });
        Assert.IsFalse((bool)pending.GetValue(preview), "Overlay must not rebuild the same effect page again after Show.");
        type.GetMethod("SelectPage").Invoke(preview, new object[] { true });
        Assert.IsTrue((bool)pending.GetValue(preview));
        pending.SetValue(preview, false);
        type.GetMethod("SelectPage").Invoke(preview, new object[] { true });
        Assert.IsFalse((bool)pending.GetValue(preview));
    }
    [Test] public void AttachedPlayerCardsReserveSpaceBelowUnitsOnOrdinaryNodes()
    {
        var go = Create("attachment node"); go.SetActive(false);
        var node = go.AddComponent(RuntimeType("BoardNodeView"));
        var type = node.GetType();
        var baseline = (Pose)type.GetMethod("GetPlacementPose").Invoke(node, new object[] { 0 });
        type.GetMethod("SetPlayerCardsPresent").Invoke(node, new object[] { true });
        var abovePlayers = (Pose)type.GetMethod("GetPlacementPose").Invoke(node, new object[] { 0 });
        Assert.Less(abovePlayers.position.z, -.024f, "Both player faces must remain below the unit.");
        type.GetMethod("SetPlayerCardsPresent").Invoke(node, new object[] { false });
        var restored = (Pose)type.GetMethod("GetPlacementPose").Invoke(node, new object[] { 0 });
        Assert.AreEqual(baseline.position, restored.position);
    }
    private readonly List<GameObject> objects = new List<GameObject>();
    private EventSystem oldEvents;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private GameObject Create(string name)
    { var go = new GameObject(name); objects.Add(go); return go; }
    [SetUp] public void Setup() { oldEvents = EventSystem.current; }
    [TearDown] public void Cleanup()
    {
        foreach (var go in objects) Object.DestroyImmediate(go);
        objects.Clear(); EventSystem.current = oldEvents;
    }
    [Test] public void PowerAndPresentationAreAuthoredWithBoundReferences()
    {
        var card = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Card/Card.prefab");
        var power = card.GetComponent(RuntimeType("BattleCardPowerLabel"));
        Assert.NotNull(power);
        var powerFields = new UnityEditor.SerializedObject(power);
        Assert.NotNull(powerFields.FindProperty("panel").objectReferenceValue);
        Assert.NotNull(powerFields.FindProperty("value").objectReferenceValue);
        var ui = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleInteractionUI.prefab");
        var display = ui.GetComponent(RuntimeType("Mishi.Networking.BattleCardDisplay"));
        Assert.NotNull(display);
        var fields = new UnityEditor.SerializedObject(display);
        foreach (string name in new[] { "opacity", "cardPanel", "artwork", "caption" })
            Assert.NotNull(fields.FindProperty(name).objectReferenceValue, name);
        var opacity = (CanvasGroup)fields.FindProperty("opacity").objectReferenceValue;
        Assert.AreEqual(0f, opacity.alpha);
        Assert.IsFalse(opacity.blocksRaycasts);
        Assert.AreEqual(1f, fields.FindProperty("duration").floatValue);
        var zoneWindow = new UnityEditor.SerializedObject(ui.GetComponent(RuntimeType("Mishi.Networking.BattleZoneWindow")));
        foreach (string name in new[] { "scryControls", "scryTop", "scryBottom", "scryConfirm", "declarationSearch" })
            Assert.NotNull(zoneWindow.FindProperty(name).objectReferenceValue, name);
    }
    [TestCase(0, 0)] [TestCase(180, 0)] [TestCase(0, 90)] [TestCase(180, 90)]
    public void PowerBillboardStaysEntirelyInFrontOfTheCard(float cameraYaw, float tap)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Card/Card.prefab");
        var card = Object.Instantiate(prefab); objects.Add(card);
        card.transform.rotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, tap);
        var label = card.GetComponent(RuntimeType("BattleCardPowerLabel"));
        var fields = new UnityEditor.SerializedObject(label);
        var panel = (RectTransform)fields.FindProperty("panel").objectReferenceValue;
        panel.gameObject.SetActive(true);
        var camera = Create("power label camera").AddComponent<Camera>();
        camera.transform.rotation = Quaternion.Euler(52.838f, cameraYaw, 0);
        label.GetType().GetMethod("PositionForCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(label, new object[] { camera });
        var corners = new Vector3[4]; panel.GetWorldCorners(corners);
        foreach (var corner in corners)
            Assert.GreaterOrEqual(Vector3.Dot(corner - card.transform.position, -card.transform.forward), .0299f);
    }
    [Test] public void LogToggleDoesNotChangeHandArea()
    {
        var root = Create("hand area session"); root.SetActive(false);
        var session = root.AddComponent(RuntimeType("Mishi.Networking.BattleNetworkSession"));
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleLogWindow.prefab");
        var instance = Object.Instantiate(prefab); objects.Add(instance);
        var log = instance.GetComponent(RuntimeType("Mishi.Networking.BattleLogWindow"));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        session.GetType().GetField("battleLog", flags).SetValue(session, log);
        session.GetType().GetField("handPreviews", flags).SetValue(session, Array.CreateInstance(RuntimeType("CardPreview"), 0));
        var camera = Create("hand area camera").AddComponent<Camera>();
        camera.pixelRect = new Rect(0, 0, 1920, 1080);
        var area = session.GetType().GetMethod("HandArea", flags);
        var before = (Rect)area.Invoke(session, new object[] { camera });
        log.GetType().GetMethod("Toggle").Invoke(log, null);
        Assert.AreEqual(before, (Rect)area.Invoke(session, new object[] { camera }));
        log.GetType().GetMethod("Hide").Invoke(log, null);
        Assert.AreEqual(before, (Rect)area.Invoke(session, new object[] { camera }));
    }
    [Test] public void EveryAuthoredBattleZoneHasAnEnabledCollider()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/CardTestGym.unity");
        try
        {
            var zoneType = RuntimeType("CardPlacementZoneView");
            var zones = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren(zoneType, true)).ToArray();
            Assert.AreEqual(21, zones.Length);
            foreach (int owner in new[] { 0, 1 })
                foreach (string kind in new[] { "Deck", "Discard", "Contract" })
                    Assert.AreEqual(1, zones.Count(z =>
                        z.GetType().GetProperty("OwnerId").GetValue(z).Equals(owner) &&
                        z.GetType().GetProperty("Kind").GetValue(z).ToString() == kind),
                        $"P{owner + 1} needs exactly one {kind} zone for snapshot placement.");
            var sharedOffField = zones.Where(z => z.GetType().GetProperty("Kind").GetValue(z).ToString() == "OffField").ToArray();
            Assert.IsNotEmpty(sharedOffField);
            foreach (var zone in sharedOffField)
                Assert.AreEqual(-1, zone.GetType().GetProperty("OwnerId").GetValue(zone), "Off-field areas must be shared.");
            foreach (var zone in zones)
            {
                var collider = zone.GetComponent<Collider>();
                Assert.NotNull(collider, zone.name + " cannot receive a drop without a collider.");
                Assert.IsTrue(collider.enabled, zone.name);
                var kind = zone.GetType().GetProperty("NodeKind");
                if (kind != null && kind.GetValue(zone).ToString() == "Player")
                {
                    var anchor = (Transform)zone.GetType().GetProperty("CardAnchor").GetValue(zone);
                    var pose = (Pose)zone.GetType().GetMethod("GetPlacementPose").Invoke(zone, new object[] { 0 });
                    Assert.Greater(Vector3.Dot(pose.position - anchor.position, anchor.rotation * Vector3.back), .039f,
                        "The unit slot must sit above the back-to-back player cards.");
                }
            }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
    [Test] public void BattleInteractionPrefabContainsWiredMenusAndSixAuthoredCardSlots()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleInteractionUI.prefab");
        Assert.NotNull(prefab);
        var menu = prefab.GetComponent(RuntimeType("Mishi.Networking.BattleActionMenu"));
        var window = prefab.GetComponent(RuntimeType("Mishi.Networking.BattleZoneWindow"));
        Assert.NotNull(menu); Assert.NotNull(window);
        var menuData = new UnityEditor.SerializedObject(menu);
        foreach (string field in new[] { "panel", "title", "summon", "overclock", "move", "attack", "decision", "inspect", "close", "targetPanel", "targetHint", "cancelTarget" })
            Assert.NotNull(menuData.FindProperty(field).objectReferenceValue, field);
        Assert.IsFalse(((GameObject)menuData.FindProperty("panel").objectReferenceValue).activeSelf);
        var windowData = new UnityEditor.SerializedObject(window);
        foreach (string field in new[] { "panel", "title", "pageLabel", "previous", "next", "close", "cardScroll" })
            Assert.NotNull(windowData.FindProperty(field).objectReferenceValue, field);
        Assert.IsFalse(((GameObject)windowData.FindProperty("panel").objectReferenceValue).activeSelf);
        var slots = windowData.FindProperty("slots"); Assert.AreEqual(6, slots.arraySize);
        for (int i = 0; i < slots.arraySize; i++)
        {
            var data = new UnityEditor.SerializedObject(slots.GetArrayElementAtIndex(i).objectReferenceValue);
            Assert.NotNull(data.FindProperty("artwork").objectReferenceValue);
            Assert.NotNull(data.FindProperty("label").objectReferenceValue);
        }
    }
    [Test] public void CenteredBattleCameraUsesWholeViewportAndRestoresOriginalView()
    {
        var camera = Create("centered camera").AddComponent<Camera>();
        var original = new Vector3(-1.7f, 7.15f, -5.93f);
        camera.transform.SetPositionAndRotation(original, Quaternion.Euler(52.838f, 0, 0));
        var pivot = new Vector3(0, .062f, .7f);
        var type = RuntimeType("Mishi.Networking.LocalBattlePerspective");
        var view = Activator.CreateInstance(type, camera, pivot);
        type.GetMethod("SetCentered").Invoke(view, new object[] { true });
        foreach (int player in new[] { 0, 1, 0 })
        {
            type.GetMethod("SetPlayer").Invoke(view, new object[] { player });
            Assert.AreEqual(.5f, camera.WorldToViewportPoint(pivot).x, .001f);
        }
        type.GetMethod("Restore").Invoke(view, null);
        Assert.Less(Vector3.Distance(original, camera.transform.position), .001f);
    }
    [TestCase("CardTestGym")] [TestCase("EffectTestLab")]
    public void BattleScenesUseHiddenLeftPreviewAndAuthoredLogButton(string name)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
        try
        {
            var hud = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren(RuntimeType("Mishi.Networking.BattleClockHud"), true)).Single();
            var fields = new UnityEditor.SerializedObject(hud);
            var toggle = (UnityEngine.UI.Button)fields.FindProperty("logWindow").objectReferenceValue;
            Assert.NotNull(toggle);
            Assert.AreEqual(Vector2.zero, ((RectTransform)toggle.transform).anchorMin);
            Assert.AreEqual(Vector2.zero, ((RectTransform)toggle.transform).anchorMax);
            var overlay = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren(RuntimeType("Mishi.Networking.BattleCardPreviewOverlay"), true)).Single();
            var overlayFields = new UnityEditor.SerializedObject(overlay);
            foreach (string field in new[] { "preview", "panel", "opacity" })
                Assert.NotNull(overlayFields.FindProperty(field).objectReferenceValue, field);
            var group = (CanvasGroup)overlayFields.FindProperty("opacity").objectReferenceValue;
            Assert.AreEqual(0, group.alpha);
            Assert.IsFalse(group.blocksRaycasts);
            var rect = (RectTransform)overlay.transform;
            Assert.LessOrEqual(rect.anchorMax.x, .3f, "Preview must remain on the left.");
            Assert.GreaterOrEqual(rect.anchorMin.y, .43f, "Reserve the bottom for raised hand cards.");
            var log = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren(RuntimeType("Mishi.Networking.BattleLogWindow"), true)).Single();
            var logFields = new UnityEditor.SerializedObject(log);
            var logPanel = (GameObject)logFields.FindProperty("panel").objectReferenceValue;
            Assert.IsFalse(logPanel.activeSelf);
            Assert.Less(((RectTransform)logPanel.transform).anchorMax.y, rect.anchorMin.y, "Clicked enemy card preview belongs above the log on the screen Y axis.");
            Assert.IsFalse(scene.GetRootGameObjects().Any(go => go.name == "Battle Preview Window"));
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        foreach (string other in new[] { "MainMenu", "DeckBuilder" })
            Assert.IsFalse(System.IO.File.ReadAllText("Assets/Scenes/" + other + ".unity").Contains("logWindow:"));
    }
    [Test] public void BattlePreviewSupportsHoverAndClosingReleasesAllRaycasts()
    {
        Assert.IsTrue(typeof(IPointerEnterHandler).IsAssignableFrom(RuntimeType("BattleCardPointer")));
        Assert.IsTrue(typeof(IPointerEnterHandler).IsAssignableFrom(RuntimeType("Mishi.Networking.BattleZoneCardItem")));
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleCardPreview.prefab");
        Assert.AreEqual(UnityEditor.PrefabAssetType.Variant, UnityEditor.PrefabUtility.GetPrefabAssetType(prefab));
        var instance = Object.Instantiate(prefab); objects.Add(instance);
        var overlay = instance.GetComponent(RuntimeType("Mishi.Networking.BattleCardPreviewOverlay"));
        var group = instance.GetComponent<CanvasGroup>();
        int controls = instance.GetComponentsInChildren<RectTransform>(true).Length;
        var show = overlay.GetType().GetMethod("ShowCard", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        show.Invoke(overlay, null);
        Assert.IsTrue((bool)overlay.GetType().GetProperty("IsOpen").GetValue(overlay));
        Assert.IsTrue(group.blocksRaycasts);
        overlay.GetType().GetMethod("Hide").Invoke(overlay, null);
        Assert.AreEqual(0, group.alpha);
        Assert.IsFalse(group.blocksRaycasts);
        Assert.IsFalse(group.interactable);
        show.Invoke(overlay, null);
        overlay.GetType().GetMethod("Clear").Invoke(overlay, null);
        Assert.IsFalse((bool)overlay.GetType().GetProperty("IsOpen").GetValue(overlay));
        Assert.AreEqual(controls, instance.GetComponentsInChildren<RectTransform>(true).Length);
        var embedded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PreviewPanel.prefab");
        Assert.IsNull(embedded.GetComponent<CanvasGroup>(), "Deck builder keeps the embedded preview.");
    }
    [Test] public void BattleLogCanOpenWithoutCardSelectionAndUpdatesWhileClosed()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleLogWindow.prefab");
        var instance = Object.Instantiate(prefab); objects.Add(instance);
        var log = instance.GetComponent(RuntimeType("Mishi.Networking.BattleLogWindow"));
        var type = log.GetType();
        var fields = new UnityEditor.SerializedObject(log);
        foreach (string name in new[] { "panel", "entries", "scroll", "close" })
            Assert.NotNull(fields.FindProperty(name).objectReferenceValue, name);
        int count = instance.GetComponentsInChildren<RectTransform>(true).Length;
        type.GetMethod("SetLog").Invoke(log, new object[] { "match", new[] { "first", "second" } });
        Assert.IsFalse((bool)type.GetProperty("IsOpen").GetValue(log));
        type.GetMethod("Toggle").Invoke(log, null);
        Assert.IsTrue((bool)type.GetProperty("IsOpen").GetValue(log));
        var text = fields.FindProperty("entries").objectReferenceValue;
        Assert.AreEqual("second\n\nfirst", text.GetType().GetProperty("text").GetValue(text));
        type.GetMethod("Toggle").Invoke(log, null);
        Assert.IsFalse((bool)type.GetProperty("IsOpen").GetValue(log));
        Assert.AreEqual(count, instance.GetComponentsInChildren<RectTransform>(true).Length);
        Assert.IsEmpty(instance.GetComponentsInChildren(RuntimeType("CardPreview"), true));
    }
    [TestCase(1280, 720)] [TestCase(1920, 1080)] [TestCase(1024, 768)]
    public void LeftPreviewClearsRaisedHandWithoutReducingItsWidth(int width, int height)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleCardPreview.prefab");
        float bottom = ((RectTransform)prefab.transform).anchorMin.y * height;
        var layout = RuntimeType("Mishi.Networking.HandFanLayout");
        var area = new Rect(16, 8, width - 32, height - 8);
        float cardHeight = (float)layout.GetMethod("CardHeight").Invoke(null, new object[] { area });
        var center = (Vector2)layout.GetMethod("RaisedCenter").Invoke(null, new object[] { area, 0, 1 });
        float handTop = center.y + cardHeight * 1.16f / 2;
        Assert.Less(handTop, bottom);
    }
    [Test] public void GuestCameraKeepsTheirStartOnSameScreenSideAsHost()
    {
        var camera = Create("camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(-1.7f, 7.15f, -5.93f), Quaternion.Euler(52.838f, 0, 0));
        var host = new Vector3(0, .062f, -1.3f); var guest = new Vector3(0, .062f, 2.7f);
        Vector3 expected = camera.WorldToViewportPoint(host);
        var type = RuntimeType("Mishi.Networking.LocalBattlePerspective");
        var view = Activator.CreateInstance(type, camera, (host + guest) * .5f);
        type.GetMethod("SetPlayer").Invoke(view, new object[] { 1 });
        Assert.Less(Vector3.Distance(expected, camera.WorldToViewportPoint(guest)), .001f);
        var once = camera.transform.position;
        type.GetMethod("SetPlayer").Invoke(view, new object[] { 1 });
        Assert.Less(Vector3.Distance(once, camera.transform.position), .001f, "Snapshots must not accumulate rotations.");
        type.GetMethod("Restore").Invoke(view, null);
        Assert.Less(Vector3.Distance(expected, camera.WorldToViewportPoint(host)), .001f);
    }
    [Test] public void TableColliderDoesNotStealCardHitAndDragRestoresColliders()
    {
        var camera = Create("camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0, 5, 0), Quaternion.Euler(90, 0, 0));
        camera.pixelRect = new Rect(0, 0, 800, 600);
        var raycaster = (BaseRaycaster)camera.gameObject.AddComponent(RuntimeType("BattleTableRaycaster"));
        var scenery = Create("non-interactive collider above card");
        scenery.transform.position = new Vector3(0, 1, 0);
        scenery.AddComponent<BoxCollider>();
        var card = Create("card");
        var cardComponent = card.AddComponent(RuntimeType("BattleCard"));
        cardComponent.GetType().GetMethod("Initialize").Invoke(cardComponent, new object[] { Guid.NewGuid() });
        var collider = card.AddComponent<BoxCollider>(); collider.size = new Vector3(1, .01f, 1);
        var pointer = card.AddComponent(RuntimeType("BattleCardPointer"));
        // Awake does not necessarily run in EditMode for a regular MonoBehaviour.
        pointer.GetType().GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(pointer, null);
        pointer.GetType().GetProperty("CanDrag").SetValue(pointer, true);
        var events = Create("events").AddComponent<EventSystem>(); EventSystem.current = events;
        var input = new PointerEventData(events) { position = camera.WorldToScreenPoint(Vector3.zero), button = PointerEventData.InputButton.Left };
        input.pressPosition = input.position;
        Physics.SyncTransforms();
        var results = new List<RaycastResult>(); raycaster.Raycast(input, results);
        Assert.IsNotEmpty(results); Assert.AreEqual(card, results[0].gameObject);
        input.pointerPressRaycast = results[0];
        ((IBeginDragHandler)pointer).OnBeginDrag(input);
        Assert.IsFalse(collider.enabled);
        input.position = camera.WorldToScreenPoint(new Vector3(1, 0, 0));
        ((IDragHandler)pointer).OnDrag(input);
        Assert.Greater(card.transform.position.x, .9f);
        pointer.GetType().GetMethod("CancelDrag").Invoke(pointer, null);
        Assert.IsTrue(collider.enabled); Assert.AreEqual(Vector3.zero, card.transform.position);
    }
}
