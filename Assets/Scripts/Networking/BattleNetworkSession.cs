using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using Mishi.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mishi.Networking
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Mishi.Networking", "Assembly-CSharp", "MvpNetworkSession")]
    public sealed partial class BattleNetworkSession : MonoBehaviour
    {
        private const int Protocol = RoomIdentity.Protocol;
        private BattleCardDisplay cardDisplay;
        private long lastPresentation;
        private CardPlacementZoneView[] sharedOffFieldZones;
        [SerializeField] private BattleCard cardPrefab;
        [SerializeField] private NetworkManager networkPrefab;
        [SerializeField] private NetworkManager steamNetworkPrefab;
        [SerializeField] private BoardNodeView hostStart;
        [SerializeField] private BoardNodeView guestStart;
        [SerializeField] private BattleClockHud clockHud;
        [SerializeField] private BattleActionMenu actionMenu;
        [SerializeField] private BattleZoneWindow zoneWindow;
        private TestCommandKind? targetCommand;
        private bool confirmingAction;
        private CardPlacementZoneView menuZone;
        private readonly List<NetworkCardInfo> playerCards = new List<NetworkCardInfo>();
        private NetworkManager network;
        private GameObject networkRoot;
        private readonly RoomAudience<NetworkConnection> audience = new RoomAudience<NetworkConnection>();
        private IReadOnlyDictionary<NetworkConnection, int> seats => audience.Players;
        private readonly Dictionary<Guid, BattleCard> visuals = new Dictionary<Guid, BattleCard>();
        private Dictionary<int, BoardNodeView> nodes;
        private CardPlacementZoneView[] zones;
        private BattleBoard board;
        private NetworkTestMatch match;
        private readonly NetworkTestMatch.Card[][] submittedDecks = new NetworkTestMatch.Card[2][];
        private float openingDeadline;
        private double lastClockUpdate, nextClockBroadcast;
        private long clockSequence, receivedClockSequence;
        private BattleCardSnapshot catalog;
        private CardDatabaseService database;
        private NetworkSnapshot? snapshot;
        private string hash;
        private long sequence;
        private bool host, sessionOpen, ownsDatabaseLock, stopping, stopNextUpdate, pending;
        private float connectDeadline;
        private ushort port;
        private string status = "Choose Host or Join in Main Menu.";
        private Guid selected;
        private LocalBattlePerspective perspective;
        private LuaBattleEffects effectCatalog;

        private void Start()
        {


            try
            {
                if (clockHud == null) throw new InvalidOperationException("请在场景中绑定 BattleClockHud。");
                clockHud.ResponsePassed += () => SendCommand(snapshot.HasValue && snapshot.Value.ChoicePlayer == snapshot.Value.You ? TestCommandKind.ChooseEffect : TestCommandKind.PassResponse, Guid.Empty, -1);
                clockHud.ExileRequested += ShowExile;
                clockHud.ChoiceRequested += ShowEffectChoice;
                clockHud.LogRequested += OpenBattleLog;
                if (actionMenu == null || zoneWindow == null) throw new InvalidOperationException("请绑定场景中的卡牌指令菜单和区域窗口。");
                cardDisplay = actionMenu.GetComponent<BattleCardDisplay>();
                if (cardDisplay == null) throw new InvalidOperationException("战斗 UI 预制体缺少中央卡片展示组件。");
                actionMenu.CommandSelected += ChooseCommand;
                actionMenu.InspectRequested += InspectZone;
                actionMenu.Cancelled += CancelInteraction;
                zoneWindow.NameDeclared += index => SendCommand(TestCommandKind.DeclareCardName, Guid.Empty, index);
                zoneWindow.ForesightChosen += accept => SendCommand(TestCommandKind.ChooseForesight, Guid.Empty, accept ? 1 : 0);
                zoneWindow.TriggersDeclined += () => SendCommand(TestCommandKind.OrderTrigger, Guid.Empty, -1);
                zoneWindow.NumberChosen += value => SendCommand(TestCommandKind.ChooseNumber, Guid.Empty, value);
                zoneWindow.PaymentCancelled += () => SendCommand(TestCommandKind.ChooseEffect, Guid.Empty, -1);
                zoneWindow.DeckViewResolved += (id, destination) => SendCommand(TestCommandKind.ResolveDeckView, string.IsNullOrEmpty(id) ? Guid.Empty : Guid.Parse(id), destination);
                zoneWindow.CardSelected += id => {
                    if (snapshot.HasValue && (snapshot.Value.ChoiceIsDeckView || snapshot.Value.ChoiceIsForesightOffer)) return;
                    if (snapshot.HasValue && !snapshot.Value.Spectator && snapshot.Value.ChoicePlayer == snapshot.Value.You)
                        SendCommand(snapshot.Value.ChoiceTriggers?.Length > 0 ? TestCommandKind.OrderTrigger : TestCommandKind.ChooseEffect, Guid.Parse(id), -1);
                    else if (visuals.TryGetValue(Guid.Parse(id), out var view)) OpenCardMenu(view);
                };
                zones = ZoneRegistry.InScene(gameObject.scene).ToArray();
                foreach (int player in new[] { 0, 1 })
                    foreach (var kind in new[] { CardZoneKind.Deck, CardZoneKind.Discard, CardZoneKind.Contract })
                        if (zones.Count(z => z.Kind == kind && z.OwnerId == player) != 1)
                            throw new InvalidOperationException($"场景需要且只能有一个 P{player + 1} 的 {kind} 区域，请检查 OwnerId。");
                if (!zones.Any(z => z.Kind == CardZoneKind.OffField && z.OwnerId == -1))
                    throw new InvalidOperationException("场景需要共享场外区（OwnerId = -1）。");
                sharedOffFieldZones = zones.Where(z => z.Kind == CardZoneKind.OffField && z.OwnerId == -1).OrderBy(z => z.ZoneId, StringComparer.Ordinal).ToArray();
                nodes = zones.OfType<BoardNodeView>().ToDictionary(n => n.NodeId);
                board = ZoneRegistry.BuildBattleBoard(gameObject.scene);
                if (cardPrefab == null || hostStart == null || guestStart == null)
                    throw new InvalidOperationException("Battle card prefab / starting nodes are missing.");
                if (Camera.main != null)
                    perspective = new LocalBattlePerspective(Camera.main, (hostStart.CardAnchor.position + guestStart.CardAnchor.position) * .5f);
                foreach (var zone in zones)
                {
                    zone.PlacementRequested += OnPlacement;
                    zone.Clicked += OnZoneClick;
                }
                BindRecordingPanel();
                BindPlayerHud();
                BindRoom();
                if (effectTestMode) LoadEffectTest(defaultEffectTest);
                else if (MainMenuController.TakeReplay(out string replayPath)) StartReplay(replayPath);
                else if (MainMenuController.TakeLaunch(out bool launchHost, out string address, out ushort launchPort, out bool watch))
                    Connect(launchHost, address, launchPort, watch);
            }
            catch (Exception e) { status = e.Message; Debug.LogException(e, this); }
        }

        private void CreateNetwork()
        {
            // AddComponent invokes OnValidate even on an inactive object in the Editor.
            // Instantiate a prefab whose collection is already serialized before any callbacks.
            var selectedPrefab = MainMenuController.UseSteam ? steamNetworkPrefab : networkPrefab;
            if (selectedPrefab == null || selectedPrefab.SpawnablePrefabs == null)
                throw new InvalidOperationException("Battle NetworkManager prefab or spawnable collection is missing.");
            network = Instantiate(selectedPrefab);
            networkRoot = network.gameObject;
            networkRoot.name = "Battle FishNet";
            if (!network.Initialized) throw new InvalidOperationException("FishNet initialization failed.");
            // Tugboat's inspector slider stops at 9999; its API accepts the connection ID limit.
            network.TransportManager.Transport.SetMaximumClients(NetworkConnection.MAXIMUM_CLIENTID_WITHOUT_SIMULATED_VALUE);
            network.ClientManager.RegisterBroadcast<NetworkPublicFrame>(OnPublicFrame);
            network.ClientManager.RegisterBroadcast<NetworkSnapshot>(OnSnapshot);
            network.ClientManager.RegisterBroadcast<NetworkCardPresentation>(OnCardPresentation);
            network.ClientManager.RegisterBroadcast<NetworkTurnTimer>(OnTurnTimer);
            network.ClientManager.RegisterBroadcast<NetworkNotice>(OnNotice);
            network.ServerManager.RegisterBroadcast<NetworkHello>(OnHello);
            network.ServerManager.RegisterBroadcast<NetworkCommand>(OnCommand);
            network.ClientManager.OnAuthenticated += RequestChallenge;
            network.ClientManager.OnClientConnectionState += OnClientState;
            network.ServerManager.OnServerConnectionState += OnServerState;
            network.ServerManager.OnRemoteConnectionState += OnRemoteState;
        }

        private void Connect(bool asHost, string address, ushort selectedPort, bool watch = false)
        {
            try
            {
                if (sessionOpen) return;
                spectator = watch;
                reconnectAddress = address;
                if (MainMenuController.UseSteam) SteamRoomService.Instance.EnsureInitialized();
                var service = database = CardDatabaseService.Instance;
                // Reload once before locking so edits made outside the deck editor are included.
                if (!service.ReloadDatabase()) throw new InvalidOperationException(service.LastError);
                catalog = service.BeginBattle(); ownsDatabaseLock = true;
                effectCatalog = new LuaBattleEffects(catalog.Cards.Values);

                hash = ComputeSessionHash();
                CreateNetwork();
                RegisterRoomNetwork();
                roomView?.ShowLobby();
                host = asHost; port = selectedPort; sessionOpen = true;
                connectDeadline = Time.unscaledTime + 20f;
                status = asHost ? "Starting host..." : $"Connecting to {address}:{port}...";
                bool started = asHost ? network.ServerManager.StartConnection(port) : network.ClientManager.StartConnection(address, port);
                if (!started) throw new InvalidOperationException("Could not start connection.");
            }
            catch (Exception e) { status = e.Message; Debug.LogException(e, this); StopSession(); roomView?.ShowFailure(status); }
        }

        private void OnServerState(ServerConnectionStateArgs e)
        {
            if (stopping) return;
            if (e.ConnectionState == LocalConnectionState.Started && host)
            {
                if (!network.ClientManager.StartConnection(MainMenuController.UseSteam ? SteamRoomService.Instance.HostId.ToString() : "127.0.0.1", port))
                { status = "Could not connect local host client."; stopNextUpdate = true; }
            }
            else if (e.ConnectionState == LocalConnectionState.Stopped && sessionOpen)
            { status = "Server stopped. Check whether the port is already in use."; stopNextUpdate = true; }
        }
        private void OnHello(NetworkConnection connection, NetworkHello message, Channel channel) => AcceptRoomMember(connection, message);
        private void RejectConnection(NetworkConnection connection, string reason)
        {
            network.ServerManager.Broadcast(connection, new NetworkNotice { Text = reason, Fatal = true });
            connection.Disconnect(false);
        }
        private void OnCommand(NetworkConnection connection, NetworkCommand request, Channel channel)
        {
            if (stopping || match == null || !audience.AuthorizeCommand(connection, out int player)) return;
            if (RoomPaused) { network.ServerManager.Broadcast(connection, new NetworkNotice { Text = "等待玩家重连，操作和计时已暂停。" }); return; }
            AdvanceMatchClock(); // Check expiry before accepting any arriving action.
            string error = "Malformed request.";
            bool accepted = Guid.TryParse(request.MatchId, out Guid matchId) &&
                Guid.TryParse(request.CardId, out Guid cardId) &&
                match.TryCommand(player, matchId, request.Sequence, request.Revision,
                    (TestCommandKind)request.Kind, cardId, request.TargetNode, out error);
            if (accepted) BroadcastSnapshots(request, player);
            else
            {
                SendSnapshot(connection, player);
                network.ServerManager.Broadcast(connection, new NetworkNotice { Text = error });
            }
        }
        private NetworkCardInfo ToInfo(NetworkTestMatch.Card card) => string.IsNullOrEmpty(card.DefinitionId)
            ? new NetworkCardInfo { InstanceId = card.Id.ToString("N"), Owner = card.Owner, Zone = (int)card.Zone, FaceDown = true, HiddenChoice = card.Zone == TestCardZone.Hand, NodeId = card.NodeId, StackOrder = card.StackOrder, Covered = card.Covered, Tapped = card.Tapped }
            : new NetworkCardInfo {
            InstanceId = card.Id.ToString("N"), DefinitionId = card.DefinitionId, Owner = card.Owner, NodeId = card.NodeId,
            AttachedTo = card.AttachedTo == Guid.Empty ? null : card.AttachedTo.ToString("N"),
            Power = card.Power, Time = card.Time,
            PlayCostAdjustment = match.EffectivePlayCost(card.Id) - card.Time, NameEffectsBlocked = match.NameEffectsBlocked(card.Id),
            ZoneEnteredTurn = card.ZoneEnteredTurn,
            AttackRange = card.AttackRange,
            EffectsSuppressed = card.SuppressedUntilTurn >= match.Turn,
            ActivationUses = match.EffectUseCount(card.Id, EffectEvent.Activated, false), NamedActivationUses = match.EffectUseCount(card.Id, EffectEvent.Activated, true),
            PlayUses = match.EffectUseCount(card.Id, EffectEvent.Played, false), NamedPlayUses = match.EffectUseCount(card.Id, EffectEvent.Played, true),
            Zone = (int)card.Zone, Tapped = card.Tapped, StackOrder = card.StackOrder, Covered = card.Zone != TestCardZone.Player && card.Covered, FaceDown = card.HiddenAttachment || card.Zone == TestCardZone.Player && card.Covered };
        private void BroadcastSnapshots(NetworkCommand? acceptedAction = null, int actor = -1)
        {
            if (effectTestMode) { RefreshEffectTest(); return; }
            foreach (var seat in seats) SendSnapshot(seat.Key, seat.Value);
            var presentations = match.DrainPresentations();
            foreach (var item in presentations)
                foreach (var seat in seats.Keys)
                    if (item.Audience < 0 || seats[seat] == item.Audience)
                        network.ServerManager.Broadcast(seat, EncodePresentation(item));
            var frame = CreatePublicFrame(++publicSequence, presentations.Where(p => p.Audience < 0).Select(EncodePresentation).ToArray());
            frame.LogDelta = match.ActionLogFor(-1).Where(line => PublicLogSequence(line) > sentPublicLogSequence).ToArray();
            if (frame.LogDelta.Length > 0) sentPublicLogSequence = PublicLogSequence(frame.LogDelta[frame.LogDelta.Length - 1]);
            frame.HasAction = acceptedAction.HasValue; frame.Actor = actor;
            frame.CommandKind = acceptedAction?.Kind ?? 0; frame.CommandSequence = acceptedAction?.Sequence ?? 0;
            BroadcastPublic(frame);
        }
        private void OnCardPresentation(NetworkCardPresentation message, Channel channel)
        {
            if (!snapshot.HasValue || message.MatchId != snapshot.Value.MatchId || message.Sequence <= lastPresentation || cardDisplay == null) return;
            lastPresentation = message.Sequence;
            if ((CardPresentationKind)message.Kind == CardPresentationKind.Dice)
            { cardDisplay.ShowDice(message.DiceSides, message.DiceResult, message.Owner); return; }
            if (catalog.Cards.TryGetValue(message.DefinitionId, out var definition))
                cardDisplay.Show(definition, database.ContentRoot, (CardPresentationKind)message.Kind, message.Owner);
        }
        private void SendSnapshot(NetworkConnection connection, int player)
            => network.ServerManager.Broadcast(connection, BuildSnapshot(player));
        private NetworkSnapshot BuildSnapshot(int player)
        {
            var view = player < 0 ? match.ForObserver() : match.ForPlayer(player);
            // Never broadcast a full authoritative state. Build a separate view for each recipient.
            return new NetworkSnapshot { MatchId = view.MatchId.ToString("N"),
                Variables = view.Variables.Select(NetworkVariables.Encode).ToArray(),
                You = Math.Max(0, player), Spectator = player < 0, HandCounts = view.HandCounts, Revision = view.Revision, ActivePlayer = view.ActivePlayer, Turn = view.Turn,
                DecisionsUsed = match.DecisionsUsed, ContractMoved = view.ContractMoved, Board = view.Board.Select(ToInfo).ToArray(),
                OwnHand = view.OwnHand.Select(ToInfo).ToArray(), OpponentHandCount = view.OpponentHandCount,
                PublicPiles = view.PublicPiles.Select(ToInfo).ToArray(), Phase = (int)view.Phase,
                OccupationNode = view.OccupationNode, LastAction = view.LastAction, DeckCounts = view.DeckCounts, ActionLog = match.ActionLogFor(player),
                CostPointers = view.CostPointers, DamagePointers = view.DamagePointers, ClockKinds = view.ClockKinds.Select(c => (int)c).ToArray(),
                ChoicePlayer = view.Choice?.Player ?? -1, ChoicePrompt = view.Choice?.Prompt, ChoiceOptional = view.Choice?.Optional ?? false,
                ChoiceZones = view.Choice?.ZoneCandidates ?? Array.Empty<int>(),
                ChoiceIsBoardPlacement = view.Choice?.IsBoardPlacement ?? false, ChoiceIsNumber = view.Choice?.IsNumber ?? false,
                ChoiceIsPayment = view.Choice?.IsPayment ?? false, ChoiceNumberMinimum = view.Choice?.NumberMinimum ?? 0, ChoiceNumberMaximum = view.Choice?.NumberMaximum ?? 0,
                ChoiceNames = view.Choice?.NameOptions ?? Array.Empty<string>(),
                ChoiceTriggers = view.Choice?.TriggerOptions.Select(t => new NetworkTriggerInfo { Id = t.Id.ToString("N"), DefinitionId = t.DefinitionId, Label = t.Label, Owner = t.Owner }).ToArray() ?? Array.Empty<NetworkTriggerInfo>(),
                ChoiceIsDeckView = view.Choice?.IsDeckView ?? false, DeckPositions = view.Choice?.DeckPositions ?? Array.Empty<int>(),
                ChoiceIsForesightOffer = view.Choice?.IsForesightOffer ?? false, ChoicePublicView = view.Choice?.PublicView ?? false,
                ViewedCards = view.Choice?.ViewedCards.Select(ToInfo).ToArray() ?? Array.Empty<NetworkCardInfo>(),
                ChoiceCards = view.Choice?.Candidates.Select(id => id.ToString("N")).ToArray() ?? Array.Empty<string>(), Winner = view.Winner, ResultReason = view.ResultReason,
                RemainingTurnSeconds = (float)view.RemainingTurnSeconds, TimerRunning = view.TimerRunning,
                ResponsePlayer = view.ResponsePlayer, RemainingResponseSeconds = (float)view.RemainingResponseSeconds,
                PlayerCards = match.PlayerCards.Select(ToInfo).Select(c => player < 0 && c.FaceDown ? HideCardIdentity(c) : c).ToArray() };
        }
        private void OnSnapshot(NetworkSnapshot state, Channel channel)
        {
            if (!sessionOpen || stopping) return;
            if (snapshot.HasValue && snapshot.Value.MatchId == state.MatchId && state.Revision < snapshot.Value.Revision) return;
            try
            {
                CancelInteraction();
                EnsureBattlePreview();
                if (snapshot.HasValue && snapshot.Value.You != state.You) ClearBattlePreview();
                perspective?.SetPlayer(state.You);
                // Restore any in-progress local drag before applying authoritative positions.
                foreach (var visual in visuals.Values)
                {
                    visual.GetComponent<BattleCardPointer>().CancelDrag();
                    if (visual.CurrentZone != null) visual.CurrentZone.RemoveView(visual);
                }
                var liveIds = new HashSet<Guid>();
                foreach (var node in nodes.Values) node.SetPlayerCardsPresent(state.PlayerCards.Any(c => c.NodeId == node.NodeId));
                foreach (var info in state.Board.OrderBy(c => c.StackOrder).Concat(state.OwnHand).Concat(state.PublicPiles.OrderBy(c => c.StackOrder)).Concat(state.PlayerCards))
                {
                    if ((TestCardZone)info.Zone == TestCardZone.Exile) continue; // Visible through the authored exile overlay, not a board slot.
                    var id = Guid.Parse(info.InstanceId); liveIds.Add(id);
                    if (!visuals.TryGetValue(id, out var card))
                    {
                        card = Instantiate(cardPrefab);
                        card.name = $"P{info.Owner + 1} {info.DefinitionId} ({info.InstanceId})";
                        card.Initialize(id);
                        var sourceDeck = zones.FirstOrDefault(z => z.Kind == CardZoneKind.Deck && z.OwnerId == info.Owner);
                        if (snapshot.HasValue && sourceDeck != null)
                            card.MoveTo(sourceDeck.CardAnchor.position, sourceDeck.CardAnchor.rotation, Vector3.one, immediate: true);
                        if (!string.IsNullOrEmpty(info.DefinitionId)) card.AssignDefinition(catalog.Cards[info.DefinitionId]);
                        card.GetComponent<BattleCardPointer>().Clicked += SelectCard;
                        card.GetComponent<BattleCardPointer>().DragDenied += OnDragDenied;
                        card.GetComponent<BattleCardPointer>().DragStarted += OnDragStarted;
                        card.GetComponent<BattleCardPointer>().HandScrolled += ScrollHand;
                        card.GetComponent<BattleCardPointer>().HandPanStarted += StartHandPan;
                        visuals.Add(id, card);
                    }
                    if (!string.IsNullOrEmpty(info.DefinitionId) && card.Definition?.Id != info.DefinitionId) card.AssignDefinition(catalog.Cards[info.DefinitionId]);
                    if (string.IsNullOrEmpty(info.DefinitionId)) card.ClearDefinition();
                    if ((TestCardZone)info.Zone != TestCardZone.Hand) { card.GetComponent<BattleCardPointer>().ClearHandArea(); card.gameObject.SetActive(true); }
                    card.EffectivePower = info.Power; card.EffectiveTime = info.Time;
                    bool field = (TestCardZone)info.Zone == TestCardZone.Board || (TestCardZone)info.Zone == TestCardZone.OffField;
                    bool top = (TestCardZone)info.Zone != TestCardZone.OffField || !state.PublicPiles.Any(c => c.Zone == info.Zone && c.NodeId == info.NodeId && c.StackOrder > info.StackOrder);
                    card.UpdatePowerLabel(field && top && !info.Covered && !info.FaceDown);
                    // Scale transitions together with the approved pose.
                    bool tapped = info.Tapped;
                    if (info.Covered) tapped = true;
                    card.PlacementRotationOffset = Quaternion.Euler(0, 0, (info.Owner == 1 ? 180 : 0) + (tapped ? 90 : 0)) * (info.FaceDown && (TestCardZone)info.Zone != TestCardZone.Player ? Quaternion.Euler(0, 180, 0) : Quaternion.identity);
                    foreach (var collider in card.GetComponentsInChildren<Collider>()) collider.enabled = !info.Covered && !info.FaceDown;
                    CardPlacementZoneView destination = null;
                    if ((TestCardZone)info.Zone == TestCardZone.Player)
                    {
                        var node = nodes[info.NodeId];
                        var pose = new Pose(node.CardAnchor.position, node.CardAnchor.rotation);
                        card.MoveTo(pose.position + node.CardAnchor.rotation * new Vector3(0, 0, info.FaceDown ? -.01f : -.024f),
                            pose.rotation * card.PlacementRotationOffset * (info.FaceDown ? Quaternion.Euler(0, 180, 0) : Quaternion.identity), Vector3.one);
                        card.CurrentZone = node; // Target routing only; player cards do not occupy a unit slot.
                        foreach (var collider in card.GetComponentsInChildren<Collider>()) collider.enabled = !info.FaceDown;
                    }
                    else if ((TestCardZone)info.Zone == TestCardZone.Board) destination = nodes[info.NodeId];
                    else if ((TestCardZone)info.Zone != TestCardZone.Hand)
                    {
                        var kind = (TestCardZone)info.Zone == TestCardZone.Contract ? CardZoneKind.Contract : (TestCardZone)info.Zone == TestCardZone.OffField ? CardZoneKind.OffField : CardZoneKind.Discard;
                        if (kind == CardZoneKind.OffField)
                        {
                            // Token position is chosen by its controller and stored in the authoritative snapshot.
                            int slot = info.NodeId;
                            destination = slot >= 0 && slot < sharedOffFieldZones.Length ? sharedOffFieldZones[slot] : null;
                        }
                        else destination = zones.FirstOrDefault(z => z.Kind == kind && z.OwnerId == info.Owner);
                        if (destination == null) throw new InvalidOperationException($"找不到 P{info.Owner + 1} 的 {kind} 区域，无法摆放 {info.DefinitionId}。");
                    }
                    if (destination != null && !destination.PlaceApproved(card, (TestCardZone)info.Zone == TestCardZone.Board))
                        throw new InvalidOperationException("Board view capacity/configuration rejected snapshot.");
                    var pointer = card.GetComponent<BattleCardPointer>();
                    pointer.CanPreview = !info.FaceDown;
                    pointer.CanDrag = CanDragCard(state, info);
                }
                foreach (var id in visuals.Keys.Where(id => !liveIds.Contains(id)).ToArray())
                { Destroy(visuals[id].gameObject); visuals.Remove(id); }
                foreach (var deckZone in zones.OfType<DeckZoneView>())
                    deckZone.SetHiddenCount(state.DeckCounts[deckZone.OwnerId], cardPrefab);
                if (snapshot.HasValue && snapshot.Value.ChoicePlayer >= 0 && state.ChoicePlayer < 0)
                { zoneWindow.Hide(); actionMenu.Hide(); }
                if (confirmingAction && (!snapshot.HasValue || snapshot.Value.MatchId != state.MatchId || snapshot.Value.Revision != state.Revision || snapshot.Value.You != state.You)) CancelInteraction();
                snapshot = state; pending = false; connectDeadline = 0;
                roomView?.HidePages();
                if (selected == Guid.Empty || !liveIds.Contains(selected))
                {
                    var own = state.Board.Concat(state.OwnHand).FirstOrDefault(c => c.Owner == state.You);
                    selected = string.IsNullOrEmpty(own.InstanceId) ? Guid.Empty : Guid.Parse(own.InstanceId);
                }
                clockHud.Apply(state);
                LayoutHand();
                status = state.LastAction;
                if (battleLog != null) battleLog.SetLog(state.MatchId, state.ActionLog);
                if (!state.Spectator && state.ChoicePlayer == state.You) ShowEffectChoice();
                else if (state.ChoicePublicView && state.ChoiceIsDeckView) zoneWindow.ShowPublicDeckView("对方未来视／公开查看", state.ViewedCards, catalog);
                else if (state.ChoicePlayer >= 0) zoneWindow.Hide();
            }
            catch (Exception e) { status = "Snapshot failed: " + e.Message; Debug.LogException(e, this); stopNextUpdate = true; }
        }
        private bool CanDragCard(NetworkSnapshot state, NetworkCardInfo info)
        {
            if (state.Spectator || state.ChoicePlayer >= 0 || state.ResponsePlayer >= 0 || info.Covered || state.Winner >= 0 || state.You != state.ActivePlayer || info.Owner != state.You || state.OccupationNode >= 0) return false;
            if ((TestCardZone)info.Zone == TestCardZone.Hand) return (TestTurnPhase)state.Phase == TestTurnPhase.Main && catalog.Cards[info.DefinitionId].Type == "通常时魔";
            if ((TestCardZone)info.Zone != TestCardZone.Board) return false;
            return (TestTurnPhase)state.Phase == TestTurnPhase.Main
                ? catalog.Cards[info.DefinitionId].IsContract && !state.ContractMoved
                : (TestTurnPhase)state.Phase == TestTurnPhase.Combat && !info.Tapped;
        }
        private void SelectCard(BattleCard card)
        {
            if (reconnecting || roomState.Reconnecting) return;
            if (openingView != null && openingView.IsOpen) return;
            if (!snapshot.HasValue) return;
            if (!snapshot.Value.Spectator && snapshot.Value.ChoicePlayer >= 0)
            {
                if (TryChooseEffectZone(card.CurrentZone)) return;
                if (snapshot.Value.ChoicePlayer == snapshot.Value.You && snapshot.Value.ChoiceCards.Contains(card.InstanceId.ToString("N"))) SendCommand(TestCommandKind.ChooseEffect, card.InstanceId, -1);
                return;
            }
            if (targetCommand.HasValue)
            {
                if (card.CurrentZone is BoardNodeView node) ExecuteTarget(node);
                else status = "请选择目标圆阵。";
                return;
            }
            if (!ShowPlayerUnitSelection(card.CurrentZone)) OpenCardMenu(card);
        }
        private void OpenCardMenu(BattleCard card)
        {
            if (reconnecting || roomState.Reconnecting) return;
            if (card.Definition == null) return;
            CancelInteraction();
            selected = card.InstanceId;
            menuZone = card.CurrentZone;
            var state = snapshot.Value;
            var info = VisibleCards(state).First(c => c.InstanceId == selected.ToString("N"));
            var actions = pending ? CardActions.None : BattleMenuPolicy.Evaluate(state, info, catalog.Cards, effectCatalog, board, nodes.Keys);
            actionMenu.Show(card.Definition.Name, (actions & CardActions.Summon) != 0,
                (actions & CardActions.Overclock) != 0, (actions & CardActions.Move) != 0,
                (actions & CardActions.Attack) != 0, (actions & CardActions.Decision) != 0,
                CanInspect(menuZone), (actions & CardActions.Activate) != 0);
        }
        private static IEnumerable<NetworkCardInfo> VisibleCards(NetworkSnapshot state) =>
            state.Board.Concat(state.OwnHand).Concat(state.PublicPiles).Concat(state.PlayerCards);
        private NetworkCardInfo[] CardsInZone(CardPlacementZoneView zone)
        {
            if (zone == null || zone.Kind == CardZoneKind.Deck || !snapshot.HasValue) return Array.Empty<NetworkCardInfo>();
            return VisibleCards(snapshot.Value).Where(c => visuals.TryGetValue(Guid.Parse(c.InstanceId), out var view) && view.CurrentZone == zone)
                .OrderByDescending(c => c.StackOrder).ToArray();
        }
        private bool CanInspect(CardPlacementZoneView zone) => CardsInZone(zone).Length > 1;
        private bool ShowPlayerUnitSelection(CardPlacementZoneView zone)
        {
            var contents = CardsInZone(zone);
            if (!contents.Any(c => (TestCardZone)c.Zone == TestCardZone.Player) || !contents.Any(c => (TestCardZone)c.Zone == TestCardZone.Board)) return false;
            CancelInteraction(); menuZone = zone;
            zoneWindow.Show("选择时魔或玩家卡进行操作", contents, catalog);
            return true;
        }
        private void ShowExile()
        {
            if (!snapshot.HasValue) return;
            if (!snapshot.Value.Spectator && snapshot.Value.ChoicePlayer >= 0) { ShowEffectChoice(); return; }
            actionMenu.Hide();
            zoneWindow.ShowExile(snapshot.Value.PublicPiles, catalog, snapshot.Value.You, snapshot.Value.Spectator);
        }
        private void ShowEffectChoice()
        {
            if (!snapshot.HasValue || snapshot.Value.Spectator || snapshot.Value.ChoicePlayer != snapshot.Value.You) return;
            var state = snapshot.Value;
            if (state.ChoiceZones != null && state.ChoiceZones.Length > 0)
            {
                zoneWindow.Hide(); actionMenu.PickEffectZone(state.ChoicePrompt); return;
            }
            actionMenu.Hide();
            if (state.ChoiceIsForesightOffer) { zoneWindow.ShowForesightOffer(state.ChoicePrompt, state.ViewedCards, catalog); return; }
            if (state.ChoiceIsNumber) { zoneWindow.ShowNumberChoice(state.ChoicePrompt, state.ChoiceNumberMinimum, state.ChoiceNumberMaximum, catalog); return; }
            if (state.ChoiceTriggers != null && state.ChoiceTriggers.Length > 0) { zoneWindow.ShowTriggerOrder(state.ChoicePrompt, state.ChoiceTriggers, catalog); return; }
            if (state.ChoiceNames != null && state.ChoiceNames.Length > 0) { zoneWindow.ShowNameDeclaration(state.ChoicePrompt, state.ChoiceNames, catalog); return; }
            if (state.ChoiceIsDeckView) { zoneWindow.ShowDeckView(state.ChoicePrompt, state.ViewedCards, state.DeckPositions, catalog); return; }
            zoneWindow.ShowCardChoice(state.ChoicePrompt, VisibleCards(state).Concat(state.ViewedCards ?? Array.Empty<NetworkCardInfo>()).Where(c => state.ChoiceCards.Contains(c.InstanceId)).GroupBy(c => c.InstanceId).Select(g => g.First()).ToArray(), catalog);
            if (state.ChoiceIsPayment) zoneWindow.AllowPaymentCancel();
        }
        private void InspectZone()
        {
            if (!CanInspect(menuZone)) return;
            zoneWindow.Show(menuZone.name, CardsInZone(menuZone), catalog);
        }
        private void ChooseCommand(TestCommandKind command)
        {
            if (!snapshot.HasValue || pending) return;
            if (command == TestCommandKind.PlayDecision || command == TestCommandKind.ActivateEffect) { SendCommand(command, selected, -1); return; }
            targetCommand = command;
            string name = command == TestCommandKind.Summon ? "登场" : command == TestCommandKind.Overclock ? "超频" : command == TestCommandKind.MoveContract ? "移动" : "攻击";
            actionMenu.PickTarget(name);
        }
        private void ExecuteTarget(BoardNodeView node)
        {
            var command = targetCommand.Value;
            if (command == TestCommandKind.Attack && board.PlayerAt(node.NodeId) >= 0 && !snapshot.Value.Board.Any(c => c.NodeId == node.NodeId))
                command = TestCommandKind.AttackPlayer;
            targetCommand = null; actionMenu.Hide();
            SendCommand(command, selected, node.NodeId);
        }
        private void OnDragStarted(BattleCard card)
        {
            CancelInteraction();
            if (battlePreview != null) battlePreview.Hide();
        }
        private void CancelInteraction()
        {
            confirmingAction = false; targetCommand = null; menuZone = null;
            if (actionMenu != null) actionMenu.Hide();
            if (zoneWindow != null) zoneWindow.Hide();
        }
        private void OnDragDenied(BattleCard card)
        {
            if (!snapshot.HasValue) return;
            var state = snapshot.Value;
            var info = state.Board.Concat(state.OwnHand).Concat(state.PublicPiles).Concat(state.PlayerCards).First(c => c.InstanceId == card.InstanceId.ToString("N"));
            status = info.Owner != state.You ? "You cannot drag the opponent's contract."
                : state.ActivePlayer != state.You ? "Wait for your turn."
                : "Action blocked: check phase, tap state or pending Occupy/Stay.";
        }
        private bool TryChooseEffectZone(CardPlacementZoneView zone)
        {
            if (!snapshot.HasValue || snapshot.Value.Spectator || snapshot.Value.ChoicePlayer != snapshot.Value.You || snapshot.Value.ChoiceZones == null || snapshot.Value.ChoiceZones.Length == 0) return false;
            int index = snapshot.Value.ChoiceIsBoardPlacement ? (zone is BoardNodeView node ? node.NodeId : -1) : Array.IndexOf(sharedOffFieldZones, zone);
            if (index >= 0 && snapshot.Value.ChoiceZones.Contains(index)) SendCommand(TestCommandKind.ChooseEffectZone, Guid.Empty, index);
            else status = snapshot.Value.ChoiceIsBoardPlacement ? "请点击合法空圆阵。" : "请点击共享场外区。";
            return true;
        }
        private void OnZoneClick(CardPlacementZoneView zone, UnityEngine.EventSystems.PointerEventData.InputButton button)
        {
            if (openingView != null && openingView.IsOpen) return;
            if (button != UnityEngine.EventSystems.PointerEventData.InputButton.Left || !snapshot.HasValue) return;
            if (TryChooseEffectZone(zone)) return;
            if (!snapshot.Value.Spectator && snapshot.Value.ChoicePlayer >= 0) { ShowEffectChoice(); return; }
            if (targetCommand.HasValue && zone is BoardNodeView node) { ExecuteTarget(node); return; }
            if (zone.Kind == CardZoneKind.Deck) return;
            if (ShowPlayerUnitSelection(zone)) return;
            var info = CardsInZone(zone).FirstOrDefault(c => !c.Covered && !c.FaceDown);
            if (!string.IsNullOrEmpty(info.InstanceId)) OpenCardMenu(visuals[Guid.Parse(info.InstanceId)]);
        }
        private void OnPlacement(CardPlacementZoneView zone, BattleCard card)
        {
            if (zone is BoardNodeView node) SendCardAction(card.InstanceId, node.NodeId);
            else status = "Choose a board node.";
        }
        private void SendCardAction(Guid card, int target)
        {
            if (!snapshot.HasValue) return;
            var state = snapshot.Value;
            bool inHand = state.OwnHand.Any(c => c.InstanceId == card.ToString("N"));
            var kind = inHand ? (state.Board.Any(c => c.NodeId == target && c.Owner == state.You)
                ? TestCommandKind.Overclock : TestCommandKind.Summon)
                : (TestTurnPhase)state.Phase == TestTurnPhase.Combat ? (board.PlayerAt(target) >= 0 && !state.Board.Any(c => c.NodeId == target) ? TestCommandKind.AttackPlayer : TestCommandKind.Attack) : TestCommandKind.MoveContract;
            SendCommand(kind, card, target);
        }
        private void SendCommand(TestCommandKind kind, Guid card, int target, bool confirmed = false)
        {
            if (reconnecting || roomState.Reconnecting || leavingBattle) return;
            if (!snapshot.HasValue || snapshot.Value.Spectator || replay != null || pending || !sessionOpen || stopping) return;
            var state = snapshot.Value;
            if (!confirmed && RequiresActionConfirmation(kind))
            {
                CancelInteraction();
                confirmingAction = true;
                actionMenu.ConfirmAction(ActionDescription(kind, card, target, state), () => {
                    confirmingAction = false;
                    if (!snapshot.HasValue || snapshot.Value.MatchId != state.MatchId || snapshot.Value.Revision != state.Revision || snapshot.Value.You != state.You)
                    { status = "局面已变化，请重新选择操作。"; return; }
                    SendCommand(kind, card, target, true);
                });
                return;
            }
            pending = true;
            if (effectTestMode)
            {
                bool accepted = match.TryCommand(effectTestPlayer, match.MatchId, ++sequence, state.Revision, kind, card, target, out var error);
                RefreshEffectTest();
                if (!accepted) status = error;
                return;
            }
            network.ClientManager.Broadcast(new NetworkCommand { MatchId = state.MatchId, Sequence = ++sequence,
                Revision = state.Revision, Kind = (int)kind, CardId = card.ToString("N"), TargetNode = target });
        }
        private static bool RequiresActionConfirmation(TestCommandKind kind) =>
            kind == TestCommandKind.Summon || kind == TestCommandKind.Overclock || kind == TestCommandKind.MoveContract ||
            kind == TestCommandKind.Attack || kind == TestCommandKind.AttackPlayer || kind == TestCommandKind.PlayDecision ||
            kind == TestCommandKind.ActivateEffect || kind == TestCommandKind.Occupy || kind == TestCommandKind.NextPhase || kind == TestCommandKind.EndTurn;
        private string ActionDescription(TestCommandKind kind, Guid card, int target, NetworkSnapshot state)
        {
            string action = kind == TestCommandKind.Summon ? "登场" : kind == TestCommandKind.Overclock ? "超频" :
                kind == TestCommandKind.MoveContract ? "移动" : kind == TestCommandKind.Attack ? "攻击" :
                kind == TestCommandKind.AttackPlayer ? "攻击玩家" : kind == TestCommandKind.PlayDecision ? "使用决策卡" :
                kind == TestCommandKind.ActivateEffect ? "发动效果" : kind == TestCommandKind.Occupy ? "侵占圆阵" : "结束当前阶段";
            var info = VisibleCards(state).FirstOrDefault(c => c.InstanceId == card.ToString("N"));
            string name = !string.IsNullOrEmpty(info.DefinitionId) && catalog.Cards.TryGetValue(info.DefinitionId, out var definition) ? definition.Name : "";
            string destination = target >= 0 ? " → 圆阵 " + target : kind == TestCommandKind.Occupy ? " → 圆阵 " + state.OccupationNode : "";
            return "确认" + action + "？\n" + name + destination;
        }
        private void OnNotice(NetworkNotice notice, Channel channel)
        {
            status = notice.Text; pending = false;
            if (notice.Fatal) { roomView?.ShowFailure(notice.Text); stopNextUpdate = true; }
            else if (!snapshot.HasValue && notice.Text != null) roomView?.ShowFailure(notice.Text);
            else if (!snapshot.HasValue) connectDeadline = 0; // accepted room handshake; wait for player
        }
        private void OnClientState(ClientConnectionStateArgs e)
        {
            if (!stopping && sessionOpen && e.ConnectionState == LocalConnectionState.Stopped)
            {
                if (!stopNextUpdate && !string.IsNullOrEmpty(roomState.RoomInstance)) { BeginReconnect(); return; }
                if (!stopNextUpdate) status = "连接失败或断开，请检查地址、Steam 状态及房间是否仍存在。";
                stopNextUpdate = true;
            }
        }
        private void OnRemoteState(NetworkConnection connection, RemoteConnectionStateArgs e)
        {
            if (stopping || e.ConnectionState != RemoteConnectionState.Stopped) return;
            if (ReserveDisconnected(connection)) return;
            var kind = audience.Remove(connection);
            roomNames.Remove(connection); chatSentAt.Remove(connection);
            if (match == null)
            {
                if (kind == RoomMemberKind.Player && OpeningActive) { ClearOpening(); roomReady[0] = roomReady[1] = false; BroadcastOpening(); }
                ResetVacantSeats(); BroadcastRoom(); return;
            }
            if (kind != RoomMemberKind.Player) { BroadcastRoom(); return; }
            match = null;
            BroadcastPublic(new NetworkNotice { Text = "Opponent left. Match ended; return to menu.", Fatal = true });
            status = "Player disconnected. Match ended.";
            // Stop on the next frame, allowing the final reliable notice to flush.
            stopNextUpdate = true;
        }
        private void Update()
        {
            UpdateReplay();
            UpdateAccess();
            AdvanceOpening();
            AdvanceMatchClock();
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (snapshot.HasValue && snapshot.Value.ChoicePlayer == snapshot.Value.You) ShowEffectChoice();
                else CancelInteraction();
            }
            if (openingDuel == null && match != null && match.OpeningPending && Time.unscaledTime >= openingDeadline)
            { match.DealOpeningHands(); BroadcastSnapshots(); }
            if (stopNextUpdate) { stopNextUpdate = false; roomView?.ShowFailure(status); StopSession(); }
            else if (sessionOpen && connectDeadline > 0 && Time.unscaledTime >= connectDeadline)
            { status = "连接超时，请检查 IP、端口及防火墙设置。"; roomView?.ShowFailure(status); StopSession(); }
            RefreshPlayerHud();
        }
        private void AdvanceMatchClock()
        {
            if (stopping || match == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            double elapsed = Math.Max(0, now - lastClockUpdate); lastClockUpdate = now;
            if (effectTestMode && EffectTestPaused) return;
            bool changed = !RoomPaused && match.AdvanceTime(elapsed);
            if (!RoomPaused) changed |= match.AdvanceAutomaticPhases();
            if (changed) BroadcastSnapshots();
            if (now < nextClockBroadcast) return;
            nextClockBroadcast = now + .25;
            var tick = new NetworkTurnTimer { MatchId = match.MatchId.ToString("N"), Turn = match.Turn, Revision = match.Revision,
                ElapsedSeconds = PublicElapsed,
                Sequence = ++clockSequence, RemainingSeconds = (float)match.RemainingTurnSeconds, Running = !RoomPaused && match.TimerRunning,
                ResponsePlayer = RoomPaused ? -1 : match.ChoicePlayer >= 0 ? match.ChoicePlayer : match.ResponsePlayer, RemainingResponseSeconds = (float)(match.ChoicePlayer >= 0 ? match.ChoiceSeconds : match.RemainingResponseSeconds) };
            if (effectTestMode) OnTurnTimer(tick, Channel.Reliable);
            else BroadcastPublic(tick);
        }
        private void OnTurnTimer(NetworkTurnTimer tick, Channel channel)
        {
            if (!snapshot.HasValue || !sessionOpen || stopping) return;
            var state = snapshot.Value;
            if (tick.MatchId != state.MatchId || tick.Turn != state.Turn || tick.Revision != state.Revision || tick.Sequence <= receivedClockSequence) return;
            receivedClockSequence = tick.Sequence;
            RecordTimer(tick);
            if (reconnecting || roomState.Reconnecting) return;
            clockHud.ApplyTimer(tick.RemainingSeconds, tick.Running, tick.ResponsePlayer, tick.RemainingResponseSeconds);
        }
        private void LateUpdate() { LayoutHand(); LayoutPlayerCards(); LayoutHiddenHands(); }
        private void LayoutPlayerCards()
        {
            if (!snapshot.HasValue) return;
            foreach (var info in snapshot.Value.PlayerCards)
            {
                if (!visuals.TryGetValue(Guid.Parse(info.InstanceId), out var view) || !nodes.TryGetValue(info.NodeId, out var node)) continue;
                var delta = Vector3.zero;
                if (!string.IsNullOrEmpty(info.AttachedTo) && visuals.TryGetValue(Guid.Parse(info.AttachedTo), out var carrier))
                    delta = carrier.GetComponent<BattleCardPointer>().DragDelta;
                view.MoveTo(node.CardAnchor.position + delta + node.CardAnchor.rotation * new Vector3(0, 0, info.FaceDown ? -.01f : -.024f),
                    node.CardAnchor.rotation * view.PlacementRotationOffset * (info.FaceDown ? Quaternion.Euler(0, 180, 0) : Quaternion.identity), Vector3.one);
            }
        }
        private void LayoutHand() => LayoutHandFan();
        private void StopSession()
        {
            ClearRoomNetwork();
            ClearHiddenHands();
            panningHand = false; handPanRemainder = 0;
            FinishRecording("对局连接已结束");
            CancelInteraction();
            handWindowStart = 0; hoveredHand = Guid.Empty; handPreviews = null;
            ClearBattlePreview();
            if (battleLog != null) battleLog.Clear();
            clockHud?.ClearTimer();
            clockSequence = receivedClockSequence = 0; lastPresentation = 0;
            if (cardDisplay != null) cardDisplay.Clear();
            perspective?.Restore();
            stopping = true; sessionOpen = false; pending = false;
            if (network != null && network.Initialized)
            {
                network.ClientManager.OnAuthenticated -= RequestChallenge;
                network.ClientManager.OnClientConnectionState -= OnClientState;
                network.ServerManager.OnServerConnectionState -= OnServerState;
                network.ServerManager.OnRemoteConnectionState -= OnRemoteState;
                network.ClientManager.UnregisterBroadcast<NetworkSnapshot>(OnSnapshot);
                network.ClientManager.UnregisterBroadcast<NetworkCardPresentation>(OnCardPresentation);
                network.ClientManager.UnregisterBroadcast<NetworkTurnTimer>(OnTurnTimer);
                network.ClientManager.UnregisterBroadcast<NetworkNotice>(OnNotice);
                network.ClientManager.UnregisterBroadcast<NetworkPublicFrame>(OnPublicFrame);
                network.ServerManager.UnregisterBroadcast<NetworkHello>(OnHello);
                network.ServerManager.UnregisterBroadcast<NetworkCommand>(OnCommand);
                network.ClientManager.StopConnection();
                network.ServerManager.StopConnection(true);
            }
            if (MainMenuController.UseSteam) SteamRoomService.Existing?.Leave();
            foreach (var card in visuals.Values)
            {
                if (card == null) continue;
                if (card.CurrentZone != null) card.CurrentZone.RemoveView(card);
                Destroy(card.gameObject);
            }
            if (zones != null) foreach (var zone in zones.OfType<DeckZoneView>()) zone.SetHiddenCount(0, cardPrefab);
            submittedDecks[0] = submittedDecks[1] = null;
            playerCards.Clear();
            visuals.Clear(); audience.Clear(); snapshot = null; match = null;
            effectCatalog = null; catalog = null;
            if (networkRoot != null) Destroy(networkRoot);
            network = null;
            if (ownsDatabaseLock) { if (database != null) database.EndBattle(); ownsDatabaseLock = false; }
        }
        private void OnDestroy()
        {
            if (zones != null) foreach (var zone in zones)
            {
                if (zone == null) continue;
                zone.PlacementRequested -= OnPlacement; zone.Clicked -= OnZoneClick;
            }
            try { StopSession(); }
            finally { DiscardRecording(); }
        }
        private bool InteractionOpen => reconnecting || roomState.Reconnecting || (openingView != null && openingView.IsOpen) || (roomView != null && (roomView.InLobby || roomView.failure.activeSelf || roomView.chatPanel.activeSelf)) || (battleLog != null && battleLog.ChatFocused) || (recordingPanel != null && recordingPanel.IsOpen) || (actionMenu != null && actionMenu.IsOpen) || (zoneWindow != null && zoneWindow.IsOpen);
    }
}


