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
    public sealed class MvpNetworkSession : MonoBehaviour
    {
        private const int Protocol = 3;
        [SerializeField] private BattleCard cardPrefab;
        [SerializeField] private NetworkManager networkPrefab;
        [SerializeField] private BoardNodeView hostStart;
        [SerializeField] private BoardNodeView guestStart;
        private NetworkManager network;
        private GameObject networkRoot;
        private readonly Dictionary<NetworkConnection, int> seats = new Dictionary<NetworkConnection, int>();
        private readonly Dictionary<Guid, BattleCard> visuals = new Dictionary<Guid, BattleCard>();
        private Dictionary<int, BoardNodeView> nodes;
        private CardPlacementZoneView[] zones;
        private BattleBoard board;
        private NetworkTestMatch match;
        private readonly NetworkTestMatch.Card[][] submittedDecks = new NetworkTestMatch.Card[2][];
        private float openingDeadline;
        private BattleCardSnapshot catalog;
        private CardDatabaseService database;
        private MvpGuiBlocker guiBlocker;
        private MvpSnapshot? snapshot;
        private string hash;
        private long sequence;
        private bool host, sessionOpen, ownsDatabaseLock, stopping, stopNextUpdate, pending;
        private float connectDeadline;
        private ushort port;
        private string status = "Choose Host or Join in Main Menu.";
        private Guid selected;
        private Rect panel;
        private Vector2 scroll;
        private bool showDetails;
        private LocalBattlePerspective perspective;

        private void Start()
        {
            guiBlocker = MvpGuiBlocker.Create(transform);
            try
            {
                zones = ZoneRegistry.InScene(gameObject.scene).ToArray();
                nodes = zones.OfType<BoardNodeView>().ToDictionary(n => n.NodeId);
                board = ZoneRegistry.BuildBattleBoard(gameObject.scene);
                if (cardPrefab == null || hostStart == null || guestStart == null)
                    throw new InvalidOperationException("MVP card prefab / starting nodes are missing.");
                if (Camera.main != null)
                    perspective = new LocalBattlePerspective(Camera.main, (hostStart.CardAnchor.position + guestStart.CardAnchor.position) * .5f);
                foreach (var zone in zones)
                {
                    zone.PlacementRequested += OnPlacement;
                    zone.Clicked += OnZoneClick;
                }
                if (MvpMenu.TakeLaunch(out bool launchHost, out string address, out ushort launchPort))
                    Connect(launchHost, address, launchPort);
            }
            catch (Exception e) { status = e.Message; Debug.LogException(e, this); }
        }

        private void CreateNetwork()
        {
            // AddComponent invokes OnValidate even on an inactive object in the Editor.
            // Instantiate a prefab whose collection is already serialized before any callbacks.
            if (networkPrefab == null || networkPrefab.SpawnablePrefabs == null)
                throw new InvalidOperationException("MVP NetworkManager prefab or spawnable collection is missing.");
            network = Instantiate(networkPrefab);
            networkRoot = network.gameObject;
            networkRoot.name = "MVP FishNet";
            if (!network.Initialized) throw new InvalidOperationException("FishNet initialization failed.");
            network.ClientManager.RegisterBroadcast<MvpSnapshot>(OnSnapshot);
            network.ClientManager.RegisterBroadcast<MvpNotice>(OnNotice);
            network.ServerManager.RegisterBroadcast<MvpHello>(OnHello);
            network.ServerManager.RegisterBroadcast<MvpCommand>(OnCommand);
            network.ClientManager.OnAuthenticated += SendHello;
            network.ClientManager.OnClientConnectionState += OnClientState;
            network.ServerManager.OnServerConnectionState += OnServerState;
            network.ServerManager.OnRemoteConnectionState += OnRemoteState;
        }

        private void Connect(bool asHost, string address, ushort selectedPort)
        {
            try
            {
                if (sessionOpen) return;
                var service = database = CardDatabaseService.Instance;
                // Reload once before locking so edits made outside the deck editor are included.
                if (!service.ReloadDatabase()) throw new InvalidOperationException(service.LastError);
                catalog = service.BeginBattle(); ownsDatabaseLock = true;
                if (!service.SelectedDeck.ValidateComplete(out string deckError)) throw new InvalidOperationException(deckError);
                var map = string.Join(";", nodes.Values.OrderBy(n => n.NodeId).Select(n =>
                    $"{n.NodeId}:{n.ZoneId}:{n.NodeKind}:{n.OwnerId}:" +
                    string.Join(",", nodes.Keys.Where(id => board.AreAdjacent(n.NodeId, id)).OrderBy(id => id))));
                map += $"|{Protocol}|{hostStart.NodeId}|{guestStart.NodeId}|{service.ActiveMode.Id}";
                hash = MvpContentHash.Compute(service.ContentRoot, map);
                CreateNetwork();
                host = asHost; port = selectedPort; sessionOpen = true;
                connectDeadline = Time.unscaledTime + 20f;
                status = asHost ? "Starting host..." : $"Connecting to {address}:{port}...";
                bool started = asHost ? network.ServerManager.StartConnection(port) : network.ClientManager.StartConnection(address, port);
                if (!started) throw new InvalidOperationException("Could not start connection.");
            }
            catch (Exception e) { status = e.Message; Debug.LogException(e, this); StopSession(); }
        }

        private void OnServerState(ServerConnectionStateArgs e)
        {
            if (stopping) return;
            if (e.ConnectionState == LocalConnectionState.Started && host)
            {
                if (!network.ClientManager.StartConnection("127.0.0.1", port))
                { status = "Could not connect local host client."; stopNextUpdate = true; }
            }
            else if (e.ConnectionState == LocalConnectionState.Stopped && sessionOpen)
            { status = "Server stopped. Check whether the port is already in use."; stopNextUpdate = true; }
        }
        private void SendHello() => network.ClientManager.Broadcast(new MvpHello { Protocol = Protocol, ContentHash = hash, ModeId = database.ActiveMode.Id,
            Deck = catalog.Deck.SelectMany(e => Enumerable.Repeat(e.Key, e.Value)).ToArray() });

        private void OnHello(NetworkConnection connection, MvpHello message, Channel channel)
        {
            if (stopping || !sessionOpen) return;
            if (message.Protocol != Protocol || message.ContentHash != hash || message.ModeId != database.ActiveMode.Id)
            { RejectConnection(connection, "Content or board differs. Use the same build and Content folder."); return; }
            if (seats.ContainsKey(connection)) return;
            int seat = connection.IsLocalClient ? 0 : 1;
            if (seats.ContainsValue(seat) || match != null)
            { RejectConnection(connection, "Room full (two players only)."); return; }
            try
            {
                var deck = database.ValidateDeck(message.Deck);
                submittedDecks[seat] = deck.Entries.SelectMany(e => Enumerable.Repeat(catalog.Cards[e.Key], e.Value))
                    .Select(c => new NetworkTestMatch.Card(Guid.Empty, c.Id, seat, -1, c.Power, c.IsContract, TestCardZone.Deck, c.IsDecision)).ToArray();
            }
            catch (Exception e) { RejectConnection(connection, "Invalid deck: " + e.Message); return; }
            seats.Add(connection, seat);
            network.ServerManager.Broadcast(connection, new MvpNotice { Text = $"Player {seat + 1} ready. Waiting for opponent..." });
            if (seats.Count == 2)
            {
                match = NetworkTestMatch.FromDecks(board, nodes.Keys, hostStart.NodeId, guestStart.NodeId,
                    submittedDecks, database.ActiveMode.BattleRules(), new System.Random());
                openingDeadline = Time.unscaledTime + .8f;
                BroadcastSnapshots();
            }
        }
        private void RejectConnection(NetworkConnection connection, string reason)
        {
            network.ServerManager.Broadcast(connection, new MvpNotice { Text = reason, Fatal = true });
            connection.Disconnect(false);
        }
        private void OnCommand(NetworkConnection connection, MvpCommand request, Channel channel)
        {
            if (stopping || match == null || !seats.TryGetValue(connection, out int player)) return;
            string error = "Malformed request.";
            bool accepted = Guid.TryParse(request.MatchId, out Guid matchId) &&
                Guid.TryParse(request.CardId, out Guid cardId) &&
                match.TryCommand(player, matchId, request.Sequence, request.Revision,
                    (TestCommandKind)request.Kind, cardId, request.TargetNode, out error);
            if (accepted) BroadcastSnapshots();
            else
            {
                SendSnapshot(connection, player);
                network.ServerManager.Broadcast(connection, new MvpNotice { Text = error });
            }
        }
        private static MvpCardInfo ToInfo(NetworkTestMatch.Card card) => new MvpCardInfo {
            InstanceId = card.Id.ToString("N"), DefinitionId = card.DefinitionId, Owner = card.Owner, NodeId = card.NodeId,
            Zone = (int)card.Zone, Tapped = card.Tapped };
        private void BroadcastSnapshots()
        {
            foreach (var seat in seats) SendSnapshot(seat.Key, seat.Value);
        }
        private void SendSnapshot(NetworkConnection connection, int player)
        {
            var view = match.ForPlayer(player);
            // Never broadcast a full authoritative state. Build a separate view for each recipient.
            network.ServerManager.Broadcast(connection, new MvpSnapshot { MatchId = view.MatchId.ToString("N"),
                You = player, Revision = view.Revision, ActivePlayer = view.ActivePlayer, Turn = view.Turn,
                ContractMoved = view.ContractMoved, Board = view.Board.Select(ToInfo).ToArray(),
                OwnHand = view.OwnHand.Select(ToInfo).ToArray(), OpponentHandCount = view.OpponentHandCount,
                PublicPiles = view.PublicPiles.Select(ToInfo).ToArray(), Phase = (int)view.Phase,
                OccupationNode = view.OccupationNode, LastAction = view.LastAction, DeckCounts = view.DeckCounts });
        }
        private void OnSnapshot(MvpSnapshot state, Channel channel)
        {
            if (!sessionOpen || stopping) return;
            if (snapshot.HasValue && snapshot.Value.MatchId == state.MatchId && state.Revision < snapshot.Value.Revision) return;
            try
            {
                perspective?.SetPlayer(state.You);
                // Restore any in-progress local drag before applying authoritative positions.
                foreach (var visual in visuals.Values)
                {
                    visual.GetComponent<BattleCardPointer>().CancelDrag();
                    if (visual.CurrentZone != null) visual.CurrentZone.RemoveView(visual);
                }
                var liveIds = new HashSet<Guid>();
                foreach (var info in state.Board.Concat(state.OwnHand).Concat(state.PublicPiles))
                {
                    var id = Guid.Parse(info.InstanceId); liveIds.Add(id);
                    if (!visuals.TryGetValue(id, out var card))
                    {
                        card = Instantiate(cardPrefab);
                        card.name = $"P{info.Owner + 1} {info.DefinitionId} ({info.InstanceId})";
                        card.Initialize(id);
                        card.AssignDefinition(catalog.Cards[info.DefinitionId]);
                        card.GetComponent<BattleCardPointer>().Clicked += SelectCard;
                        card.GetComponent<BattleCardPointer>().DragDenied += OnDragDenied;
                        visuals.Add(id, card);
                    }
                    card.transform.localScale = Vector3.one;
                    card.PlacementRotationOffset = Quaternion.Euler(0, 0, (info.Owner == 1 ? 180 : 0) + (info.Tapped ? 90 : 0));
                    CardPlacementZoneView destination = null;
                    if ((TestCardZone)info.Zone == TestCardZone.Board) destination = nodes[info.NodeId];
                    else if ((TestCardZone)info.Zone != TestCardZone.Hand)
                    {
                        var kind = (TestCardZone)info.Zone == TestCardZone.Contract ? CardZoneKind.Contract : CardZoneKind.Discard;
                        destination = zones.FirstOrDefault(z => z.Kind == kind && z.OwnerId == info.Owner);
                        if (destination == null) throw new InvalidOperationException("Missing discard/contract zone for player.");
                    }
                    if (destination != null && !destination.PlaceApproved(card))
                        throw new InvalidOperationException("Board view capacity/configuration rejected snapshot.");
                    var pointer = card.GetComponent<BattleCardPointer>();
                    pointer.CanPreview = true;
                    pointer.CanDrag = CanDragCard(state, info);
                }
                foreach (var id in visuals.Keys.Where(id => !liveIds.Contains(id)).ToArray())
                { Destroy(visuals[id].gameObject); visuals.Remove(id); }
                foreach (var deckZone in zones.OfType<DeckZoneView>())
                    deckZone.SetHiddenCount(state.DeckCounts[deckZone.OwnerId], cardPrefab);
                snapshot = state; pending = false; connectDeadline = 0;
                if (selected == Guid.Empty || !liveIds.Contains(selected))
                {
                    var own = state.Board.Concat(state.OwnHand).FirstOrDefault(c => c.Owner == state.You);
                    selected = string.IsNullOrEmpty(own.InstanceId) ? Guid.Empty : Guid.Parse(own.InstanceId);
                }
                LayoutHand();
                status = state.LastAction;
            }
            catch (Exception e) { status = "Snapshot failed: " + e.Message; Debug.LogException(e, this); stopNextUpdate = true; }
        }
        private bool CanDragCard(MvpSnapshot state, MvpCardInfo info)
        {
            if (state.You != state.ActivePlayer || info.Owner != state.You || state.OccupationNode >= 0) return false;
            if ((TestCardZone)info.Zone == TestCardZone.Hand) return (TestTurnPhase)state.Phase == TestTurnPhase.Main && catalog.Cards[info.DefinitionId].Type == "通常时魔";
            if ((TestCardZone)info.Zone != TestCardZone.Board) return false;
            return (TestTurnPhase)state.Phase == TestTurnPhase.Main
                ? catalog.Cards[info.DefinitionId].IsContract && !state.ContractMoved
                : (TestTurnPhase)state.Phase == TestTurnPhase.Combat && !info.Tapped;
        }
        private void SelectCard(BattleCard card)
        {
            if (snapshot.HasValue)
            {
                var target = snapshot.Value.Board.FirstOrDefault(c => c.InstanceId == card.InstanceId.ToString("N"));
                if (!string.IsNullOrEmpty(target.InstanceId) && target.Owner != snapshot.Value.You &&
                    (TestTurnPhase)snapshot.Value.Phase == TestTurnPhase.Combat && selected != Guid.Empty)
                { SendCommand(TestCommandKind.Attack, selected, target.NodeId); return; }
            }
            selected = card.InstanceId;
        }
        private void OnDragDenied(BattleCard card)
        {
            if (!snapshot.HasValue) return;
            var state = snapshot.Value;
            var info = state.Board.Concat(state.OwnHand).Concat(state.PublicPiles).First(c => c.InstanceId == card.InstanceId.ToString("N"));
            status = info.Owner != state.You ? "You cannot drag the opponent's contract."
                : state.ActivePlayer != state.You ? "Wait for your turn."
                : "Action blocked: check phase, tap state or pending Occupy/Stay.";
        }
        private void OnZoneClick(CardPlacementZoneView zone, UnityEngine.EventSystems.PointerEventData.InputButton button)
        {
            if (button == UnityEngine.EventSystems.PointerEventData.InputButton.Left && selected != Guid.Empty && zone is BoardNodeView node)
                SendCardAction(selected, node.NodeId);
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
                : (TestTurnPhase)state.Phase == TestTurnPhase.Combat ? TestCommandKind.Attack : TestCommandKind.MoveContract;
            SendCommand(kind, card, target);
        }
        private void SendCommand(TestCommandKind kind, Guid card, int target)
        {
            if (!snapshot.HasValue || pending || !sessionOpen || stopping) return;
            var state = snapshot.Value;
            pending = true;
            network.ClientManager.Broadcast(new MvpCommand { MatchId = state.MatchId, Sequence = ++sequence,
                Revision = state.Revision, Kind = (int)kind, CardId = card.ToString("N"), TargetNode = target });
        }
        private void OnNotice(MvpNotice notice, Channel channel)
        {
            status = notice.Text; pending = false;
            if (notice.Fatal) stopNextUpdate = true;
            else if (!snapshot.HasValue) connectDeadline = 0; // accepted room handshake; wait for player
        }
        private void OnClientState(ClientConnectionStateArgs e)
        {
            if (!stopping && sessionOpen && e.ConnectionState == LocalConnectionState.Stopped)
            { if (!stopNextUpdate) status = "Disconnected. Return to menu to create or join a new room."; stopNextUpdate = true; }
        }
        private void OnRemoteState(NetworkConnection connection, RemoteConnectionStateArgs e)
        {
            if (stopping || e.ConnectionState != RemoteConnectionState.Stopped || !seats.Remove(connection)) return;
            match = null;
            foreach (var remaining in seats.Keys)
                network.ServerManager.Broadcast(remaining, new MvpNotice { Text = "Opponent left. Match ended; return to menu.", Fatal = true });
            status = "Player disconnected. Match ended.";
            // Stop on the next frame, allowing the final reliable notice to flush.
            stopNextUpdate = true;
        }
        private void Update()
        {
            if (match != null && match.OpeningPending && Time.unscaledTime >= openingDeadline)
            { match.DealOpeningHands(); BroadcastSnapshots(); }
            panel = GetPanelRect();
            if (guiBlocker != null) guiBlocker.SetArea(panel);
            if (stopNextUpdate) { stopNextUpdate = false; StopSession(); }
            else if (sessionOpen && connectDeadline > 0 && Time.unscaledTime >= connectDeadline)
            { status = "Connection timed out. Check address, port and firewall."; StopSession(); }
        }
        private void LateUpdate() { LayoutHand(); }
        private void LayoutHand()
        {
            if (!snapshot.HasValue || Camera.main == null) return;
            var hand = snapshot.Value.OwnHand;
            var camera = Camera.main;
            for (int i = 0; i < hand.Length; i++)
            {
                if (!visuals.TryGetValue(Guid.Parse(hand[i].InstanceId), out var card) || card.GetComponent<BattleCardPointer>().IsDragging) continue;
                float x = .5f + (i - (hand.Length - 1) * .5f) * Mathf.Min(.095f, .8f / Mathf.Max(1, hand.Length - 1));
                card.transform.SetPositionAndRotation(camera.ViewportToWorldPoint(new Vector3(x, .11f, 4)), camera.transform.rotation);
                card.transform.localScale = Vector3.one * .7f;
            }
        }
        private void StopSession()
        {
            perspective?.Restore();
            stopping = true; sessionOpen = false; pending = false;
            if (network != null && network.Initialized)
            {
                network.ClientManager.OnAuthenticated -= SendHello;
                network.ClientManager.OnClientConnectionState -= OnClientState;
                network.ServerManager.OnServerConnectionState -= OnServerState;
                network.ServerManager.OnRemoteConnectionState -= OnRemoteState;
                network.ClientManager.UnregisterBroadcast<MvpSnapshot>(OnSnapshot);
                network.ClientManager.UnregisterBroadcast<MvpNotice>(OnNotice);
                network.ServerManager.UnregisterBroadcast<MvpHello>(OnHello);
                network.ServerManager.UnregisterBroadcast<MvpCommand>(OnCommand);
                network.ClientManager.StopConnection();
                network.ServerManager.StopConnection(true);
            }
            foreach (var card in visuals.Values)
            {
                if (card == null) continue;
                if (card.CurrentZone != null) card.CurrentZone.RemoveView(card);
                Destroy(card.gameObject);
            }
            if (zones != null) foreach (var zone in zones.OfType<DeckZoneView>()) zone.SetHiddenCount(0, cardPrefab);
            submittedDecks[0] = submittedDecks[1] = null;
            visuals.Clear(); seats.Clear(); snapshot = null; match = null;
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
            StopSession();
        }
        private void OnGUI()
        {
            panel = GetPanelRect();
            GUILayout.BeginArea(panel, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("MISHI / ACTION TEST (no costs/effects)");
            GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
            if (snapshot.HasValue)
            {
                var state = snapshot.Value;
                var phase = (TestTurnPhase)state.Phase;
                GUILayout.Label($"Mode: {database.ActiveMode.Name} | Deck: {state.DeckCounts[state.You]} / Opponent: {state.DeckCounts[1 - state.You]}");
                GUILayout.Label($"You: P{state.You + 1} | Turn {state.Turn} | {phase}");
                GUILayout.Label(state.You == state.ActivePlayer ? "YOUR TURN" : "OPPONENT'S TURN");
                bool canAct = !pending && state.ActivePlayer == state.You;
                GUI.enabled = canAct;
                if (state.OccupationNode >= 0)
                {
                    if (GUILayout.Button("Occupy Node " + state.OccupationNode)) SendCommand(TestCommandKind.Occupy, Guid.Empty, -1);
                    if (GUILayout.Button("Stay in place")) SendCommand(TestCommandKind.Stay, Guid.Empty, -1);
                }
                else if (phase == TestTurnPhase.End)
                {
                    if (GUILayout.Button("End Turn")) SendCommand(TestCommandKind.EndTurn, Guid.Empty, -1);
                }
                else if (GUILayout.Button("Next Phase")) SendCommand(TestCommandKind.NextPhase, Guid.Empty, -1);
                GUI.enabled = true;
            }
            if (GUILayout.Button(showDetails ? "Hide test controls" : "Show test controls")) showDetails = !showDetails;
            if (snapshot.HasValue && showDetails)
            {
                var state = snapshot.Value;
                GUILayout.Label($"Opponent hand: {state.OpponentHandCount} | Revision: {state.Revision}");
                GUILayout.Label("Main: drag hand to summon; drag contract to move.");
                GUILayout.Label("Combat: select attacker then click/drag to enemy.");
                GUILayout.Label("Your hand:");
                foreach (var card in state.OwnHand)
                    if (GUILayout.Button($"{card.DefinitionId} [{card.InstanceId.Substring(0, 4)}] / Select"))
                    {
                        selected = Guid.Parse(card.InstanceId);
                        CardPreviewRequestEvent.Trigger(catalog.Cards[card.DefinitionId], CardDatabaseService.Instance.ContentRoot);
                    }
                GUILayout.Label("Board (T = tapped):");
                foreach (var card in state.Board)
                    if (GUILayout.Button($"P{card.Owner + 1} {card.DefinitionId} / Node {card.NodeId} {(card.Tapped ? "T" : "")}"))
                        selected = Guid.Parse(card.InstanceId);
                if (selected != Guid.Empty)
                {
                    GUILayout.Label("Selected: " + selected.ToString("N").Substring(0, 8));
                    GUI.enabled = !pending && state.ActivePlayer == state.You && state.OccupationNode < 0;
                    foreach (var node in nodes.Keys.OrderBy(n => n))
                        if (GUILayout.Button("Use selected on Node " + node)) SendCardAction(selected, node);
                    GUI.enabled = true;
                }
                GUILayout.Label("Public discard / contract zones:");
                foreach (var card in state.PublicPiles)
                    if (GUILayout.Button($"P{card.Owner + 1} {card.DefinitionId} / {(TestCardZone)card.Zone}"))
                        CardPreviewRequestEvent.Trigger(catalog.Cards[card.DefinitionId], CardDatabaseService.Instance.ContentRoot);
            }
            GUILayout.Space(8);
            if (GUILayout.Button("Disconnect / Main Menu"))
            { StopSession(); SceneManager.LoadScene(MvpMenu.MenuScene); }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private Rect GetPanelRect()
        {
            float width = showDetails ? 300 : 250;
            return new Rect(Mathf.Max(12, Screen.width - width - 12), 12, width,
                Mathf.Min(Screen.height - 24, showDetails ? 520 : 270));
        }
    }
}
