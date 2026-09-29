using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DeckStoragePanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField deckName;
    [SerializeField] private Button save;
    [SerializeField] private TMP_Dropdown deckSelector;
    [SerializeField] private TMP_Text message;
    private string overwriteName;
    private string[] names = Array.Empty<string>();
    private int appliedIndex;

    private void Awake()
    {
        save.onClick.AddListener(Save);
        deckSelector.onValueChanged.AddListener(SelectDeck);
        deckName.onValueChanged.AddListener(_ => overwriteName = null);
    }
    private void Start()
    {
        try
        {
            var service = CardDatabaseService.Instance;
            service.EnsureLoaded();
            if (service.SelectedDeckName != "未保存卡组") deckName.SetTextWithoutNotify(service.SelectedDeckName);
            RefreshOptions();
        }
        catch (Exception e) { message.text = e.Message; }
    }
    private void RefreshOptions()
    {
        var service = CardDatabaseService.Instance;
        names = service.SavedDeckNames();
        var options = new List<string> { names.Length == 0 ? "暂无存档" : "选择卡组" };
        options.AddRange(names);
        deckSelector.ClearOptions();
        deckSelector.AddOptions(options);
        appliedIndex = Array.IndexOf(names, service.SelectedDeckName) + 1;
        deckSelector.SetValueWithoutNotify(appliedIndex);
        deckSelector.interactable = names.Length > 0;
    }
    private void Save()
    {
        try
        {
            var service = CardDatabaseService.Instance;
            string name = deckName.text.Trim();
            if (Array.IndexOf(service.SavedDeckNames(), name) >= 0 && overwriteName != name)
            { overwriteName = name; message.text = "同名存档已存在，再点保存确认覆盖。"; return; }
            service.SaveDeck(name); overwriteName = null;
            RefreshOptions();
            message.text = "已保存：" + name + "（不足张数可作为草稿）";
        }
        catch (Exception e) { message.text = e.Message; }
    }
    private void SelectDeck(int index)
    {
        if (index <= 0 || index > names.Length)
        { deckSelector.SetValueWithoutNotify(appliedIndex); return; }
        try
        {
            string name = names[index - 1];
            CardDatabaseService.Instance.LoadDeck(name);
            appliedIndex = index;
            deckName.SetTextWithoutNotify(name); overwriteName = null;
            message.text = "已应用：" + name;
        }
        catch (Exception e)
        {
            deckSelector.SetValueWithoutNotify(appliedIndex);
            message.text = "切换失败，保留当前卡组：" + e.Message;
        }
    }
}
