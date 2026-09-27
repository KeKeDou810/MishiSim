using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mishi.Networking
{
    // Small development menu. The deck builder remains the existing scene and database service.
    public sealed class MvpMenu : MonoBehaviour
    {
        public const string MenuScene = "MainMenu";
        public const string BattleScene = "CardTestGym";
        public const string DeckScene = "DeckBuilder";
        private static MvpMenu instance;
        private static bool launchPending, launchHost;
        private static string launchAddress;
        private static ushort launchPort;
        private string address = "127.0.0.1", port = "7770", error = "";
        private MvpGuiBlocker guiBlocker;
        private CardDatabaseService service;
        private Vector2 menuScroll;
        private void Start()
        {
            try { service = CardDatabaseService.Instance; service.EnsureLoaded(); }
            catch (Exception e) { error = e.Message; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; launchPending = false; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureMenu()
        {
            if (instance == null) new GameObject("MVP Menu Navigation").AddComponent<MvpMenu>();
        }
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            guiBlocker = MvpGuiBlocker.Create(transform);
        }
        private void Update()
        {
            bool deck = SceneManager.GetActiveScene().name == DeckScene;
            guiBlocker.gameObject.SetActive(deck);
            if (deck) guiBlocker.SetArea(new Rect(Screen.width - 160, 8, 150, 32));
        }
        public static bool TakeLaunch(out bool host, out string address, out ushort port)
        {
            host = launchHost; address = launchAddress; port = launchPort;
            bool pending = launchPending; launchPending = false; return pending;
        }
        private void Launch(bool host)
        {
            if (!ushort.TryParse(port, out var parsed) || parsed == 0)
            { error = "Port must be between 1 and 65535."; return; }
            if (!host && string.IsNullOrWhiteSpace(address)) { error = "Enter the host address."; return; }
            try {
                service.EnsureLoaded();
                if (!service.SelectedDeck.ValidateComplete(out string deckError)) { error = deckError; return; }
            } catch (Exception e) { error = e.Message; return; }
            launchHost = host; launchAddress = address.Trim(); launchPort = parsed; launchPending = true;
            Load(BattleScene);
        }
        private void Load(string scene)
        {
            try { error = ""; SceneManager.LoadScene(scene); }
            catch (Exception e) { launchPending = false; error = e.Message; }
        }
        private void OnGUI()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene == DeckScene)
            {
                if (GUI.Button(new Rect(Screen.width - 160, 8, 150, 32), "Main Menu")) Load(MenuScene);
                return;
            }
            if (scene != MenuScene) return;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            float width = Mathf.Min(380, Screen.width - 24);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, Mathf.Max(12, (Screen.height - 600) / 2), width, Mathf.Min(600, Screen.height - 24)), GUI.skin.box);
            menuScroll = GUILayout.BeginScrollView(menuScroll);
            GUILayout.Space(16);
            GUILayout.Label("MISHI", new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter });
            GUILayout.Label("Two-player networking MVP", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(20);
            if (service != null && service.IsLoaded)
            {
                GUILayout.Label("Game mode: " + service.ActiveMode.Name);
                foreach (var mode in service.Modes)
                    if (GUILayout.Button(mode.Name))
                        try { service.SelectMode(mode.Id); error = ""; } catch (Exception e) { error = e.Message; }
                GUILayout.Label($"Deck: {service.SelectedDeck.Count}/{service.ActiveMode.DeckSize} | Copies: {service.ActiveMode.MaxCopies}");
                if (service.ActiveMode.ExampleDeck != null && GUILayout.Button("Load mode example deck (replace current)"))
                    try { service.LoadExampleDeck(); error = ""; } catch (Exception e) { error = e.Message; }
            }
            if (GUILayout.Button("Reload database / game modes"))
                try { service = CardDatabaseService.Instance; error = service.ReloadDatabase() ? "Reloaded." : service.LastError; }
                catch (Exception e) { error = e.Message; }
            GUILayout.Label("Host address (Join)");
            address = GUILayout.TextField(address, 255);
            GUILayout.Label("Port (UDP)");
            port = GUILayout.TextField(port, 5);
            GUILayout.Space(12);
            if (GUILayout.Button("Host", GUILayout.Height(36))) Launch(true);
            if (GUILayout.Button("Join", GUILayout.Height(36))) Launch(false);
            if (GUILayout.Button("Deck Build", GUILayout.Height(36))) Load(DeckScene);
            GUILayout.Space(10);
            GUILayout.Label("Host / Join uses the selected mode and deck. Both players must select the same mode.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Label(error, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        private void OnDestroy() { if (instance == this) instance = null; }
    }
}

