using System;
using UnityEngine;

// Atom One Dark palette shared by the remaining IMGUI screens and authored UI.
public static class AtomOneTheme
{
    public static readonly Color Background = Hex(0x282C34);
    public static readonly Color Surface = Hex(0x21252B);
    public static readonly Color Raised = Hex(0x353B45);
    public static readonly Color Text = Hex(0xABB2BF);
    public static readonly Color Accent = Hex(0x61AFEF);
    private static GUISkin skin;
    private static Color Hex(int rgb) => new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f);
    private static Texture2D Solid(Color color)
    {
        var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color); texture.Apply(); return texture;
    }
    public static IDisposable Use()
    {
        var previous = GUI.skin;
        if (skin == null)
        {
            skin = UnityEngine.Object.Instantiate(previous);
            skin.hideFlags = HideFlags.HideAndDontSave;
            // IMGUI requires a Font rather than a TMP SDF asset; use the same asset's source font.
            var fontAsset = TMPro.TMP_Settings.defaultFontAsset;
            if (fontAsset != null && fontAsset.sourceFontFile != null)
            {
                skin.font = fontAsset.sourceFontFile;
                foreach (var style in new[] { skin.box, skin.button, skin.textField, skin.textArea, skin.window, skin.label, skin.toggle })
                    style.font = skin.font;
            }
            var surface = Solid(Surface); var raised = Solid(Raised); var hover = Solid(Hex(0x3E4451)); var active = Solid(Hex(0x40566E));
            foreach (var style in new[] { skin.box, skin.button, skin.textField, skin.textArea, skin.window })
            {
                style.border = new RectOffset(); style.padding = new RectOffset(12, 12, 7, 7);
                style.margin = new RectOffset(4, 4, 4, 4);
                style.normal.background = style.onNormal.background = style == skin.button ? raised : surface;
                style.hover.background = style.onHover.background = hover;
                style.active.background = style.onActive.background = active;
                style.focused.background = style.onFocused.background = hover;
                foreach (var state in new[] { style.normal, style.hover, style.active, style.focused, style.onNormal, style.onHover, style.onActive, style.onFocused }) state.textColor = Text;
                style.hover.textColor = style.focused.textColor = Accent;
            }
            foreach (var style in new[] { skin.label, skin.toggle })
                foreach (var state in new[] { style.normal, style.hover, style.active, style.focused, style.onNormal, style.onHover, style.onActive, style.onFocused }) state.textColor = Text;
            skin.settings.cursorColor = Accent;
            skin.settings.selectionColor = Hex(0x3E4451);
            foreach (var style in new[] { skin.verticalScrollbar, skin.horizontalScrollbar })
            { style.normal.background = surface; style.border = new RectOffset(); }
            foreach (var style in new[] { skin.verticalScrollbarThumb, skin.horizontalScrollbarThumb })
            { style.border = new RectOffset(); style.normal.background = raised; style.hover.background = hover; style.active.background = active; }
        }
        GUI.skin = skin;
        return new Scope(previous);
    }
    private sealed class Scope : IDisposable
    {
        private readonly GUISkin previous;
        public Scope(GUISkin previous) { this.previous = previous; }
        public void Dispose() { GUI.skin = previous; }
    }
}
