using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mishi.Networking
{
    public sealed class BattleZoneWindow : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title, pageLabel;
        [SerializeField] private Button previous, next, close;
        [SerializeField] private GameObject scryControls;
        [SerializeField] private Button scryTop, scryBottom, scryConfirm;
        [SerializeField] private TMP_InputField declarationSearch;
        [SerializeField] private ScrollRect cardScroll;
        [SerializeField] private BattleZoneCardItem[] slots;
        [Header("Window layout")]
        [SerializeField] private float browseHeight = 370, controlsHeight = 430, searchHeight = 480;
        [SerializeField] private float cardTopInset = 60, searchRowHeight = 50;
        private readonly List<BattleZoneCardItem> items = new List<BattleZoneCardItem>();
        private NetworkCardInfo[] cards = Array.Empty<NetworkCardInfo>();
        private const float CardWidth = 172, Gap = 12;
        public bool IsOpen => panel.activeSelf;
        public event Action<string> CardSelected;
        public event Action<string, int> DeckViewResolved;
        public event Action<int> NameDeclared;
        public event Action<bool> ForesightChosen;
        public event Action TriggersDeclined;
        private bool choosingTriggers;
        private bool browsingExile, exileSpectator;
        private int exileViewer;
        private NetworkCardInfo[] exileCards = Array.Empty<NetworkCardInfo>();
        public event Action<int> NumberChosen;
        public event Action PaymentCancelled;
        private bool choosingNumber, choosingPaymentCards;
        private int numberMinimum, numberMaximum, chosenNumber;
        private bool foresightOffer, choosingMark;
        private bool declaringName;
        private string declarationPrompt;
        private NetworkCardInfo[] declarationCards = Array.Empty<NetworkCardInfo>();
        private int selectedName = -1;
        private BattleCardSnapshot shownDatabase;
        private bool choosingScry, chooseDestination;
        private string selectedScry;
        private int fixedDestination;
        private void Awake()
        {
            previous.onClick.AddListener(() => Scroll(-1));
            next.onClick.AddListener(() => Scroll(1));
            close.onClick.AddListener(() => { if (choosingNumber) NumberChosen?.Invoke(-1); else if (choosingPaymentCards) PaymentCancelled?.Invoke(); else Hide(); });
            scryTop.onClick.AddListener(() => ResolveDeckView(1));
            scryBottom.onClick.AddListener(() => ResolveDeckView(2));
            scryConfirm.onClick.AddListener(() => ResolveDeckView(fixedDestination));
            scryControls.SetActive(false);
            declarationSearch.onValueChanged.AddListener(_ => { if (declaringName) FilterNames(); });
            declarationSearch.gameObject.SetActive(false);
            cardScroll.onValueChanged.AddListener(_ => UpdateNavigation());
            foreach (var slot in slots) Register(slot);
            panel.SetActive(false);
        }
        private void Register(BattleZoneCardItem item)
        {
            items.Add(item);
            item.Selected += id => {
                if (declaringName)
                {
                    selectedName = int.Parse(id.Substring(5));
                    scryConfirm.interactable = true;
                    pageLabel.text = "将宣言：" + shownDatabase.Cards[cards.First(c => c.InstanceId == id).DefinitionId].Name;
                    return;
                }
                if (!choosingScry) { CardSelected?.Invoke(id); return; }
                selectedScry = id;
                scryTop.interactable = scryBottom.interactable = true;
                foreach (var card in cards) if (card.InstanceId == id)
                    pageLabel.text = "已选：" + shownDatabase.Cards[card.DefinitionId].Name;
            };
        }
        public void Show(string zoneName, NetworkCardInfo[] visibleCards, BattleCardSnapshot database)
        {
            browsingExile = false; exileCards = Array.Empty<NetworkCardInfo>();
            close.GetComponentInChildren<TMP_Text>(true).text = "关闭";
            choosingTriggers = false;
            choosingNumber = choosingPaymentCards = false;
            foresightOffer = choosingMark = false;
            declaringName = false; declarationSearch.gameObject.SetActive(false);
            choosingScry = false; selectedScry = null; shownDatabase = database;
            ApplyLayout(false);
            scryControls.SetActive(false); close.gameObject.SetActive(true);
            ShowCards(zoneName, visibleCards, database);
        }
        public void ShowExile(NetworkCardInfo[] publicPiles, BattleCardSnapshot database, int viewer, bool spectator)
        {
            Show("除外区", Array.Empty<NetworkCardInfo>(), database);
            exileViewer = spectator ? 0 : viewer;
            exileSpectator = spectator;
            exileCards = publicPiles.Where(c => c.Zone == (int)Mishi.Battle.TestCardZone.Exile).ToArray();
            browsingExile = true;
            ApplyLayout(true);
            scryControls.SetActive(true);
            scryTop.gameObject.SetActive(true); scryBottom.gameObject.SetActive(true); scryConfirm.gameObject.SetActive(false);
            scryTop.GetComponentInChildren<TMP_Text>().text = ExileName(exileViewer) + " (" + exileCards.Count(c => c.Owner == exileViewer) + ")";
            scryBottom.GetComponentInChildren<TMP_Text>().text = ExileName(1 - exileViewer) + " (" + exileCards.Count(c => c.Owner != exileViewer) + ")";
            SelectExile(exileViewer);
        }
        private string ExileName(int owner) => exileSpectator ? "P" + (owner + 1) + " 除外区" : owner == exileViewer ? "我方除外区" : "对方除外区";
        private void SelectExile(int owner)
        {
            ShowCards(ExileName(owner), exileCards.Where(c => c.Owner == owner).ToArray(), shownDatabase);
            scryTop.interactable = owner != exileViewer; scryBottom.interactable = owner == exileViewer;
            pageLabel.text = cards.Length == 0 ? ExileName(owner) + "暂无卡片" : "双方除外区独立 · 左右浏览当前一方的卡片";
        }
        public void ShowCardChoice(string prompt, NetworkCardInfo[] candidates, BattleCardSnapshot database)
        {
            Show(prompt, candidates, database);
            close.GetComponentInChildren<TMP_Text>(true).text = "暂时收起";
            pageLabel.text = "单击卡片即提交 · 收起后点「继续选择」恢复，倒计时继续";
        }
        private void ApplyLayout(bool withControls, bool withSearch = false)
        {
            var dialog = (RectTransform)scryControls.transform.parent;
            dialog.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                withSearch ? searchHeight : withControls ? controlsHeight : browseHeight);
            // The authored list is top-anchored; search gets its own row below the title.
            var list = (RectTransform)cardScroll.transform;
            list.anchoredPosition = new Vector2(list.anchoredPosition.x,
                -cardTopInset - (withSearch ? searchRowHeight : 0) - list.rect.height * .5f);
        }
        private void ShowCards(string zoneName, NetworkCardInfo[] visibleCards, BattleCardSnapshot database)
        {
            bool same = IsOpen && cards.Length == visibleCards.Length;
            for (int i = 0; same && i < cards.Length; i++) same = cards[i].InstanceId == visibleCards[i].InstanceId;
            float position = same ? cardScroll.horizontalNormalizedPosition : 0;
            cards = visibleCards;
            title.text = zoneName + " · " + cards.Length + " 张";
            panel.SetActive(true);
            // Reuse the authored card template; only repeated data items grow at runtime.
            while (items.Count < cards.Length) Register(Instantiate(slots[0], cardScroll.content));
            for (int i = 0; i < items.Count; i++)
            {
                if (i >= cards.Length) { items[i].Clear(); items[i].gameObject.SetActive(false); continue; }
                var rect = (RectTransform)items[i].transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
                rect.pivot = new Vector2(0, .5f);
                rect.sizeDelta = new Vector2(CardWidth, 228);
                rect.anchoredPosition = new Vector2(i * (CardWidth + Gap), 0);
                items[i].Bind(cards[i], cards[i].FaceDown ? null : database.Cards[cards[i].DefinitionId]);
            }
            cardScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Max(0, cards.Length * (CardWidth + Gap) - Gap));
            pageLabel.text = cards.Length == 0 ? "此区域暂无卡片" : "左右拖动或滚轮浏览 · 单击卡片选择";
            Canvas.ForceUpdateCanvases();
            cardScroll.StopMovement();
            cardScroll.horizontalNormalizedPosition = position;
            UpdateNavigation();
        }
        public void ShowTriggerOrder(string prompt, NetworkTriggerInfo[] triggers, BattleCardSnapshot database)
        {
            Show(prompt, triggers.Select(t => new NetworkCardInfo { InstanceId = t.Id, DefinitionId = t.DefinitionId, Owner = t.Owner }).ToArray(), database);
            close.gameObject.SetActive(false);
            choosingTriggers = true;
            ApplyLayout(true);
            scryControls.SetActive(true);
            scryTop.gameObject.SetActive(false); scryBottom.gameObject.SetActive(false); scryConfirm.gameObject.SetActive(true);
            scryConfirm.interactable = true;
            scryConfirm.GetComponentInChildren<TMP_Text>().text = "放弃剩余效果";
            title.text = "可选诱发效果 · 剩余 " + triggers.Length + " 个";
            pageLabel.text = prompt;
            for (int i = 0; i < triggers.Length; i++)
                items[i].SetCaption(database.Cards[triggers[i].DefinitionId].Name + "\n<size=80%>" + triggers[i].Label + "</size>");
        }
        public void ShowNameDeclaration(string prompt, string[] names, BattleCardSnapshot database)
        {
            Show(prompt, Array.Empty<NetworkCardInfo>(), database);
            declaringName = true; declarationPrompt = prompt;
            var byName = database.Cards.Values.GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Id, StringComparer.Ordinal).First());
            declarationCards = names.Select((name, index) => new NetworkCardInfo {
                InstanceId = "name:" + index, DefinitionId = byName[name].Id,
                Power = byName[name].Power, Time = byName[name].Level
            }).ToArray();
            ApplyLayout(true, true);
            close.gameObject.SetActive(false); scryControls.SetActive(true);
            scryTop.gameObject.SetActive(false); scryBottom.gameObject.SetActive(false); scryConfirm.gameObject.SetActive(true);
            scryConfirm.GetComponentInChildren<TMP_Text>().text = "确认宣言";
            declarationSearch.SetTextWithoutNotify(""); declarationSearch.gameObject.SetActive(true);
            FilterNames();
        }
        private void FilterNames()
        {
            selectedName = -1; scryConfirm.interactable = false;
            string query = declarationSearch.text.Trim();
            var filtered = declarationCards.Where(c => shownDatabase.Cards[c.DefinitionId].Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            ShowCards(declarationPrompt, filtered.Take(24).ToArray(), shownDatabase);
            pageLabel.text = filtered.Length > 24 ? $"匹配 {filtered.Length} 个卡名，显示前 24 个；输入卡名缩小范围" : "单击卡片后确认宣言；同名版本只显示一次";
        }
        public void ShowDeckView(string prompt, NetworkCardInfo[] viewed, int[] destinations, BattleCardSnapshot database)
        {
            Show(prompt, viewed, database);
            scryTop.GetComponentInChildren<TMP_Text>().text = "卡顶";
            scryBottom.GetComponentInChildren<TMP_Text>().text = "卡底";
            ApplyLayout(true);
            choosingScry = true; chooseDestination = destinations.Length > 1;
            fixedDestination = destinations[0];
            scryControls.SetActive(true); close.gameObject.SetActive(false);
            scryTop.gameObject.SetActive(chooseDestination); scryBottom.gameObject.SetActive(chooseDestination);
            scryConfirm.gameObject.SetActive(!chooseDestination);
            scryTop.interactable = scryBottom.interactable = false; scryConfirm.interactable = true;
            scryConfirm.GetComponentInChildren<TMP_Text>().text = fixedDestination == 1 ? "确认放回卡顶" : fixedDestination == 2 ? "确认放回卡底" : "确认";
            pageLabel.text = chooseDestination ? "先选一张卡，再选择卡顶或卡底；各组按点击顺序排列" : "查看完成后确认";
            choosingMark = destinations.Contains(3);
            if (choosingMark)
            {
                scryTop.interactable = scryBottom.interactable = true;
                scryTop.GetComponentInChildren<TMP_Text>().text = "发动印记";
                scryBottom.GetComponentInChildren<TMP_Text>().text = "不发动";
                pageLabel.text = "选择是否发动翻到的印记 · 超时默认不发动";
            }
        }
        public void ShowForesightOffer(string prompt, NetworkCardInfo[] attacker, BattleCardSnapshot database)
        {
            Show(prompt, attacker, database); foresightOffer = true;
            ApplyLayout(true);
            scryControls.SetActive(true); close.gameObject.SetActive(false);
            scryTop.gameObject.SetActive(true); scryBottom.gameObject.SetActive(true); scryConfirm.gameObject.SetActive(false);
            scryTop.interactable = scryBottom.interactable = true;
            scryTop.GetComponentInChildren<TMP_Text>().text = "进行未来视";
            scryBottom.GetComponentInChildren<TMP_Text>().text = "放弃剩余次数";
            pageLabel.text = "每次公开牌库顶一张卡，处理完成后再决定是否继续";
        }
        public void ShowPublicDeckView(string prompt, NetworkCardInfo[] viewed, BattleCardSnapshot database)
        {
            Show(prompt, viewed, database); choosingScry = true;
            close.gameObject.SetActive(false); pageLabel.text = "等待对方确认公开的卡片";
        }
        private void ResolveDeckView(int destination)
        {
            if (choosingMark)
            {
                if (!scryTop.interactable) return;
                scryTop.interactable = scryBottom.interactable = false;
                DeckViewResolved?.Invoke(null, destination == 1 ? 0 : 3); return;
            }
            if (browsingExile) { SelectExile(destination == 1 ? exileViewer : 1 - exileViewer); return; }
            if (choosingNumber)
            {
                if (destination == 0) { NumberChosen?.Invoke(chosenNumber); return; }
                chosenNumber = Mathf.Clamp(chosenNumber + (destination == 1 ? -1 : 1), numberMinimum, numberMaximum);
                UpdateNumberChoice(); return;
            }
            if (choosingTriggers) { scryConfirm.interactable = false; TriggersDeclined?.Invoke(); return; }
            if (foresightOffer)
            {
                if (!scryTop.interactable) return;
                scryTop.interactable = scryBottom.interactable = false;
                ForesightChosen?.Invoke(destination == 1); return;
            }
            if (declaringName)
            {
                if (selectedName < 0 || !scryConfirm.interactable) return;
                scryConfirm.interactable = false; NameDeclared?.Invoke(selectedName); return;
            }
            if (!choosingScry || chooseDestination && selectedScry == null) return;
            scryTop.interactable = scryBottom.interactable = scryConfirm.interactable = false;
            DeckViewResolved?.Invoke(chooseDestination ? selectedScry : null, destination);
        }
        public void AllowPaymentCancel() { choosingPaymentCards = true; close.gameObject.SetActive(true); close.GetComponentInChildren<TMP_Text>(true).text = "取消支付"; pageLabel.text = "选择费用卡片，最后统一确认支付 · 取消不扣费用"; }
        public void ShowNumberChoice(string prompt, int minimum, int maximum, BattleCardSnapshot database)
        {
            bool preserve = choosingNumber && numberMinimum == minimum && numberMaximum == maximum;
            int value = preserve ? chosenNumber : minimum;
            Show(prompt, Array.Empty<NetworkCardInfo>(), database);
            close.GetComponentInChildren<TMP_Text>(true).text = "取消支付";
            choosingNumber = true; numberMinimum = minimum; numberMaximum = maximum; chosenNumber = value; fixedDestination = 0;
            ApplyLayout(true);
            scryControls.SetActive(true); close.gameObject.SetActive(true);
            scryTop.gameObject.SetActive(minimum != maximum); scryBottom.gameObject.SetActive(minimum != maximum); scryConfirm.gameObject.SetActive(true);
            scryTop.GetComponentInChildren<TMP_Text>().text = "减 1"; scryBottom.GetComponentInChildren<TMP_Text>().text = "加 1";
            scryConfirm.GetComponentInChildren<TMP_Text>().text = "确认"; scryConfirm.interactable = true;
            title.text = prompt; UpdateNumberChoice();
        }
        private void UpdateNumberChoice()
        {
            pageLabel.text = numberMinimum == numberMaximum ? "关闭取消发动，确认后统一支付" : $"支付时间：{chosenNumber}（{numberMinimum}–{numberMaximum}） · 关闭取消";
            scryTop.interactable = chosenNumber > numberMinimum; scryBottom.interactable = chosenNumber < numberMaximum;
        }
        private void Scroll(int direction)
        {
            float overflow = cardScroll.content.rect.width - cardScroll.viewport.rect.width;
            if (overflow <= 0) return;
            cardScroll.StopMovement();
            cardScroll.horizontalNormalizedPosition = Mathf.Clamp01(cardScroll.horizontalNormalizedPosition + direction * (CardWidth + Gap) / overflow);
            UpdateNavigation();
        }
        private void UpdateNavigation()
        {
            bool overflow = cardScroll.content.rect.width > cardScroll.viewport.rect.width + 1;
            previous.interactable = overflow && cardScroll.horizontalNormalizedPosition > .001f;
            next.interactable = overflow && cardScroll.horizontalNormalizedPosition < .999f;
        }
        public void Hide()
        {
            browsingExile = false; exileCards = Array.Empty<NetworkCardInfo>();
            foresightOffer = choosingMark = false;
            declaringName = false; selectedName = -1;
            declarationSearch.gameObject.SetActive(false);
            choosingScry = false; selectedScry = null;
            panel.SetActive(false);
            cardScroll.StopMovement();
            foreach (var item in items) if (item != null) item.Clear();
            cards = Array.Empty<NetworkCardInfo>();
        }
    }
}
