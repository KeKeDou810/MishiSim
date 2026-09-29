using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardBrowserFilters : MonoBehaviour
{
    [Serializable] public sealed class Option { public string value; public Toggle toggle; }
    [SerializeField] private TMP_InputField search, minimumPower, maximumPower, minimumTime, maximumTime;
    [SerializeField] private Option[] nations, types;
    [SerializeField] private TMP_Text result, organizeLabel;
    [SerializeField] private Button clear, expand, sortType, sortTime, reverseOrder;
    [SerializeField] private GameObject dropdownPanel;
    public event Action Changed;
    public event Action OrganizationChanged;
    public string Query => search.text;
    public bool ByTime { get; private set; }
    public bool Reverse { get; private set; }
    private void Awake()
    {
        foreach (var input in new[] { search, minimumPower, maximumPower, minimumTime, maximumTime })
            input.onValueChanged.AddListener(_ => Notify());
        foreach (var option in nations.Concat(types)) option.toggle.onValueChanged.AddListener(_ => Notify());
        clear.onClick.AddListener(ResetFilters);
        expand.onClick.AddListener(() => dropdownPanel.SetActive(!dropdownPanel.activeSelf));
        sortType.onClick.AddListener(() => SetOrganization(false, Reverse));
        sortTime.onClick.AddListener(() => SetOrganization(true, Reverse));
        reverseOrder.onClick.AddListener(() => SetOrganization(ByTime, !Reverse));
        dropdownPanel.SetActive(false);
        UpdateOrganizationLabel();
    }
    private void OnDisable() { if (dropdownPanel != null) dropdownPanel.SetActive(false); }
    private void SetOrganization(bool byTime, bool reverse)
    {
        ByTime = byTime; Reverse = reverse;
        UpdateOrganizationLabel(); Notify(); OrganizationChanged?.Invoke();
    }
    private void UpdateOrganizationLabel() => organizeLabel.text = "排序：" + (ByTime ? "时间" : "种类") + (Reverse ? " · 逆序" : " · 正序");
    private void Notify() => Changed?.Invoke();
    public void ResetFilters()
    {
        foreach (var input in new[] { search, minimumPower, maximumPower, minimumTime, maximumTime }) input.SetTextWithoutNotify("");
        foreach (var option in nations.Concat(types)) option.toggle.SetIsOnWithoutNotify(false);
        Notify();
    }
    public bool TryGetRange(out int? min, out int? max) => CardSearch.TryPowerRange(minimumPower.text, maximumPower.text, out min, out max);
    public bool TryGetTimeRange(out int? min, out int? max) => CardSearch.TryPowerRange(minimumTime.text, maximumTime.text, out min, out max);
    public bool Matches(CardDefinition card) => MatchesOptions(card.Faction, nations) && MatchesOptions(card.Type, types);
    private static bool MatchesOptions(string value, Option[] options)
    {
        if (!options.Any(o => o.toggle.isOn)) return true;
        return options.Any(o => o.toggle.isOn && (o.value == value || o.value == "*" && !options.Any(known => known.value == value)));
    }
    public void ShowResult(int visible, int total, bool valid) => result.text = valid
        ? $"{visible} / {total} 张 · 筛选与排序可展开" : "力量或时间范围无效：非负整数，最小值 ≤ 最大值";
}
