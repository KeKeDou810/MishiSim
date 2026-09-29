using System;
using System.IO;
using Mishi.Battle.Effects;
using UnityEditor;
using UnityEngine;

namespace Mishi.EditorTools
{
    public sealed class KernelEffectEditorWindow : EditorWindow
    {
        [SerializeField] private string selectedPath, originalSource, editedSource;
        [SerializeField] private string search = "", newId = "";
        [SerializeField] private int template;
        private string status, selectedId, description, luaExample;
        private Type[] types = Array.Empty<Type>();
        private Vector2 listScroll, codeScroll, detailScroll;
        private bool Dirty => !string.IsNullOrEmpty(selectedPath) && editedSource != originalSource;

        [MenuItem("Mishi/Kernel Effects/Effect Editor")]
        public static void Open() => GetWindow<KernelEffectEditorWindow>("内核效果");
        private void OnEnable() { minSize = new Vector2(850, 680); Refresh(); }
        private void Refresh() { types = KernelEffectAuthoring.EffectTypes(); Repaint(); }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("内核效果编辑器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("一项效果一个 C# 类。Lua 使用 op ID 调用；校验和执行在同一文件维护。保存后由 Unity 编译并更新注册表。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField("搜索 ID / 类名", search);
                if (GUILayout.Button("刷新", GUILayout.Width(65))) Refresh();
                using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("重建注册表", GUILayout.Width(110))) Run(KernelEffectAuthoring.RegenerateCatalog);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(210)))
                {
                    listScroll = EditorGUILayout.BeginScrollView(listScroll);
                    foreach (var type in types)
                    {
                        KernelEffect effect;
                        try { effect = (KernelEffect)Activator.CreateInstance(type); }
                        catch (Exception e) { EditorGUILayout.HelpBox(type.Name + ": " + e.Message, MessageType.Error); continue; }
                        if (effect.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && type.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (GUILayout.Button(effect.Id, GUILayout.Height(28))) Select(type, effect);
                    }
                    EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.Height(132));
                    EditorGUILayout.LabelField(selectedId ?? "选择效果或创建一个新效果", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(description ?? "", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.SelectableLabel(luaExample ?? "", EditorStyles.textArea, GUILayout.Height(42));
                    EditorGUILayout.LabelField(selectedPath ?? "", EditorStyles.miniLabel);
                    EditorGUILayout.EndScrollView();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedPath)))
                        {
                            if (GUILayout.Button("在代码编辑器打开")) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(selectedPath));
                            if (GUILayout.Button("复制 Lua 示例")) EditorGUIUtility.systemCopyBuffer = luaExample ?? "";
                            if (GUILayout.Button("放弃编辑并重载")) Run(() => { originalSource = editedSource = File.ReadAllText(selectedPath); });
                        }
                        using (new EditorGUI.DisabledScope(!Dirty || string.IsNullOrEmpty(selectedPath) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                            if (GUILayout.Button("保存源码")) Run(() => {
                                KernelEffectAuthoring.SaveSource(selectedPath, originalSource, editedSource);
                                originalSource = editedSource; status = "已保存，等待 Unity 编译。若有语法错误，请查看 Console 并修正。";
                            });
                    }
                    codeScroll = EditorGUILayout.BeginScrollView(codeScroll);
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedPath)))
                        editedSource = EditorGUILayout.TextArea(editedSource ?? "", new GUIStyle(EditorStyles.textArea) { wordWrap = false }, GUILayout.ExpandHeight(true), GUILayout.MinHeight(260));
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("新增效果", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                newId = EditorGUILayout.TextField("效果 ID", newId);
                template = EditorGUILayout.Popup(template, new[] { "通用效果", "逐卡效果", "玩家效果" }, GUILayout.Width(135));
                using (new EditorGUI.DisabledScope(Dirty || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("创建独立文件", GUILayout.Width(120))) Run(() => {
                        selectedPath = KernelEffectAuthoring.Create(newId.Trim(), template);
                        originalSource = editedSource = File.ReadAllText(selectedPath);
                        selectedId = newId.Trim(); description = "模板尚未实现：完成 Validate 和 Execute / Apply 后使用。"; luaExample = "";
                        status = "已创建。编译成功后自动注册；模板会拒绝执行，避免未实现效果空放。";
                    });
            }
            if (Dirty) EditorGUILayout.HelpBox("有尚未保存的源码修改。请先保存或放弃，再切换效果。", MessageType.Warning);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
        }
        private void Select(Type type, KernelEffect effect)
        {
            if (Dirty) { status = "请先保存或放弃当前修改。"; return; }
            Run(() => {
                var path = KernelEffectAuthoring.SourcePath(type);
                if (path == null) throw new IOException("找不到该类的 MonoScript，请检查文件名与类名是否一致。");
                selectedPath = path; originalSource = editedSource = File.ReadAllText(path);
                selectedId = effect.Id; description = effect.Description; luaExample = effect.LuaExample; status = "";
            });
        }
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception e) { status = e.Message; }
        }
    }
}
