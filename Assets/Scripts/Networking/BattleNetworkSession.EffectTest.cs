using System;
using System.IO;
using System.Linq;
using FishNet.Transporting;
using Mishi.Battle;
using Newtonsoft.Json;
using UnityEngine;

namespace Mishi.Networking
{
    public sealed partial class BattleNetworkSession
    {
        [Header("Offline effect lab only")]
        [SerializeField] private bool effectTestMode;
        [SerializeField] private string defaultEffectTest = "Tests/01-summon-combo.json";
        private int effectTestPlayer;
        public bool IsEffectTest => effectTestMode;
        public bool EffectTestPaused { get; set; } = true;
        public int EffectTestPlayer => effectTestPlayer;
        public EffectTestScenario CurrentEffectTest { get; private set; }
        public string EffectTestStatus => status;
        public int[] EffectTestNodes => nodes?.Keys.OrderBy(n => n).ToArray() ?? Array.Empty<int>();
        public void LoadEffectTest(string relativePath)
        {
            if (!effectTestMode) throw new InvalidOperationException("Not an effect test scene.");
            var service = CardDatabaseService.Instance;
            string path = Path.GetFullPath(Path.Combine(service.ContentRoot, relativePath));
            string root = Path.GetFullPath(Path.Combine(service.ContentRoot, "Tests")) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Test scenarios must be in Content/Tests.");
            BeginEffectTest(JsonConvert.DeserializeObject<EffectTestScenario>(File.ReadAllText(path)));
        }
        public void BeginEffectTest(EffectTestScenario scenario, bool reloadLua = false)
        {
            if (!effectTestMode || nodes == null) throw new InvalidOperationException("Start EffectTestLab in Play Mode first.");
            StopSession();
            try
            {
                database = CardDatabaseService.Instance;
                if (reloadLua && !database.ReloadDatabase()) throw new InvalidOperationException(database.LastError);
                database.EnsureLoaded();
                catalog = database.BeginBattle(); ownsDatabaseLock = true;
                effectCatalog = new LuaBattleEffects(catalog.Cards.Values);
                match = NetworkTestMatch.FromEffectTest(board, nodes.Keys, hostStart.NodeId, guestStart.NodeId, scenario, effectCatalog,
                    Enumerable.Range(0, sharedOffFieldZones.Length));
                foreach (int player in new[] { 0, 1 })
                {
                    var contract = scenario.Cards.Single(c => c.Owner == player && catalog.Cards[c.Id].IsContract);
                    var definition = catalog.Cards[contract.Id];
                    match.AddPlayerCards(player, player == 0 ? hostStart.NodeId : guestStart.NodeId,
                        new NetworkTestMatch.Card(Guid.NewGuid(), definition.PlayerCardId1, player, -1),
                        new NetworkTestMatch.Card(Guid.NewGuid(), definition.PlayerCardId2, player, -1));
                }
                CurrentEffectTest = JsonConvert.DeserializeObject<EffectTestScenario>(JsonConvert.SerializeObject(scenario));
                effectTestPlayer = scenario.ActivePlayer;
                sequence = 0; stopping = false; stopNextUpdate = false; sessionOpen = true; connectDeadline = 0;
                lastClockUpdate = Time.realtimeSinceStartupAsDouble;
                RefreshEffectTest();
            }
            catch (Exception e) { StopSession(); status = e.Message; throw; }
        }
        public void SwitchEffectTestPlayer(int player)
        {
            if (!effectTestMode || match == null || player < 0 || player > 1) return;
            effectTestPlayer = player; RefreshEffectTest();
        }
        public void StepEffectTestTime(double seconds)
        {
            if (!effectTestMode || match == null) return;
            match.AdvanceTime(seconds); RefreshEffectTest();
        }
        private void RefreshEffectTest()
        {
            if (match == null) return;
            pending = false;
            OnSnapshot(BuildSnapshot(effectTestPlayer), Channel.Reliable);
            foreach (var item in match.DrainPresentations())
                if (item.Audience < 0 || item.Audience == effectTestPlayer)
                OnCardPresentation(new NetworkCardPresentation { MatchId = match.MatchId.ToString("N"), Sequence = item.Sequence,
                    DefinitionId = item.DefinitionId, Owner = item.Owner, Kind = (int)item.Kind, DiceSides = item.DiceSides, DiceResult = item.DiceResult }, Channel.Reliable);
        }
    }
}
