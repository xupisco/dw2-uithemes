using DistantWorlds.Types;
using DistantWorlds.UI;
using HarmonyLib;
using Stride.Core.Mathematics;

namespace DW2UIThemes;

/// <summary>
/// The outline around a section's sub-tabs (e.g. Exploration: Ships, Known Orbs, Ruins...) is drawn by
/// SectionHeaderPanel with AccentColor itself, read live, so a theme could only change it together with
/// every other accent. The key "SectionHeaderPanel.OutlineColor" gives it its own color: while a
/// SectionHeaderPanel draws itself (not its child controls), lines and fills (1x1 texture) whose RGB is
/// the current AccentColor get the theme's color instead, keeping the panel's fade transparency.
/// Outside that, the cost is one check per sprite drawn.
///
/// The line under a SectionHeaderPanel's title is drawn the same way with ForeColorDark (1x1 lines and
/// fills) and gets "SectionHeaderPanel.TitleLineColor". Matching is by RGB, so a theme whose
/// ForeColorDark equals one of the header's fill colors would recolor that fill too.
///
/// "TableFrameColor" is global: the frame and grid of the game's boxed tables (the summary under a section
/// title, the boxes in the empire relation window...) are drawn everywhere in a fixed #C0C0C0 at 40%, as
/// rounded border textures, lines and fills; those pieces get the theme's color, keeping the fade.
///
/// Same mechanism for the side gradient of the selected object window: SelectionPanel draws the panel
/// gradient texture (1024x2) tinted plain white, while other panels tint it with OverlayTintColor. While
/// SelectionPanel draws itself, that texture gets the theme's OverlayTintColor (alpha 0 = no gradient).
/// </summary>
internal static class Outlines
{
    public const string Key = "SectionHeaderPanel.OutlineColor";
    public const string TitleLineKey = "SectionHeaderPanel.TitleLineColor";
    public const string FrameKey = "TableFrameColor";
    private const string GradientKey = "OverlayTintColor";

    // The game's table frame: #C0C0C0 at 40% (drawn as #C0C0C060 once the panel's fade is applied).
    private const byte FrameGray = 0xC0;
    private const float FrameAlpha = 0.4f;

    [ThreadStatic] private static DWControl header;
    [ThreadStatic] private static bool headerIsSelection;
    [ThreadStatic] private static int childDepth;
    private static Color? color;
    private static Color? titleLine;
    private static Color? frame;
    private static Color? gradient;

    public static bool IsKnown(string key) => key is Key or TitleLineKey or FrameKey;

    public static void Install(Harmony h)
    {
        foreach (var m in AccessTools.GetDeclaredMethods(typeof(SectionHeaderPanel)).Where(m => m.Name == "Render")
            .Concat(AccessTools.GetDeclaredMethods(typeof(SelectionPanel)).Where(m => m.Name is "Render" or "RenderToTexture")))
        {
            h.Patch(m, prefix: new HarmonyMethod(typeof(Outlines), nameof(HeaderPrefix)),
                finalizer: new HarmonyMethod(typeof(Outlines), nameof(HeaderFinalizer)));
        }
        // Child controls render through OnRender; their drawing keeps its own colors.
        foreach (var m in AccessTools.GetDeclaredMethods(typeof(DWControl)).Where(m => m.Name == "OnRender"))
        {
            h.Patch(m, prefix: new HarmonyMethod(typeof(Outlines), nameof(ChildPrefix)),
                finalizer: new HarmonyMethod(typeof(Outlines), nameof(ChildFinalizer)));
        }
        h.Patch(AccessTools.Method(typeof(Stride.Graphics.SpriteBatch), "DrawSprite"),
            prefix: new HarmonyMethod(typeof(Outlines), nameof(DrawSpritePrefix)));
    }

    /// <summary>Called whenever the theme changes.</summary>
    public static void Use(Theme theme)
    {
        color = Read(theme, Key);
        titleLine = Read(theme, TitleLineKey);
        frame = Read(theme, FrameKey);
        gradient = theme != null && theme.Colors.TryGetValue(GradientKey, out var g) && Themes.TryParseColor(g, 0, out var gc) ? gc : null;
    }

    private static Color? Read(Theme theme, string key) =>
        theme != null && theme.Colors.TryGetValue(key, out var hex) && Themes.TryParseColor(hex, 255, out var c) ? c : null;

    /// <summary>The scopes nest (SelectionPanel.Render calls RenderToTexture), so the previous one is restored.</summary>
    private static void HeaderPrefix(DWControl __instance, out (DWControl header, bool selection, int depth) __state)
    {
        __state = (header, headerIsSelection, childDepth);
        header = __instance;
        headerIsSelection = __instance is SelectionPanel;
        childDepth = 0;
    }

    private static Exception HeaderFinalizer(Exception __exception, (DWControl header, bool selection, int depth) __state)
    {
        (header, headerIsSelection, childDepth) = __state;
        return __exception;
    }

    private static void ChildPrefix(DWControl __instance)
    {
        if (header != null && !ReferenceEquals(__instance, header))
        {
            childDepth++;
        }
    }

    private static Exception ChildFinalizer(DWControl __instance, Exception __exception)
    {
        if (header != null && !ReferenceEquals(__instance, header) && childDepth > 0)
        {
            childDepth--;
        }
        return __exception;
    }

    private static void DrawSpritePrefix(Stride.Graphics.Texture texture, ref Color4 color)
    {
        if (texture == null)
        {
            return;
        }
        if (frame is { } f && IsFramePiece(texture, color))
        {
            color = Recolor(f, Math.Min(1f, color.A / FrameAlpha));
            return;
        }
        if (header == null || childDepth > 0)
        {
            return;
        }
        if (headerIsSelection)
        {
            // The panel gradient texture, drawn in plain white by this window only.
            if (gradient is { } tint && texture.Width == 1024 && texture.Height == 2)
            {
                color = new Color4(tint.R / 255f, tint.G / 255f, tint.B / 255f, tint.A / 255f);
            }
            return;
        }
        var drawn = (Color)color;
        if (texture.Width != 1 || texture.Height != 1)
        {
            return;
        }
        if (Outlines.color is { } outline && SameRgb(drawn, ColorHelper.AccentColor))
        {
            color = Recolor(outline, color.A);
        }
        else if (titleLine is { } line && SameRgb(drawn, ColorHelper.ForeColorDark))
        {
            color = Recolor(line, color.A);
        }
    }

    /// <summary>
    /// A piece of the game's fixed table frame: #C0C0C0 at 40% or less (the panel's fade lowers it), drawn
    /// as a line or fill (1x1) or as the rounded border textures (2x44, 44x44, 44x596). Text and icons in
    /// the same gray are opaque and use other textures, so they are left alone.
    /// </summary>
    private static bool IsFramePiece(Stride.Graphics.Texture texture, Color4 color)
    {
        if (color.A > FrameAlpha + 0.01f)
        {
            return false;
        }
        var c = (Color)color;
        if (c.R != FrameGray || c.G != FrameGray || c.B != FrameGray)
        {
            return false;
        }
        int w = texture.Width, h = texture.Height;
        return (w == 1 && h == 1) || w == 44 || h == 44;
    }

    private static bool SameRgb(Color a, Color b) => a.R == b.R && a.G == b.G && a.B == b.B;

    /// <summary>The theme color, keeping the panel's fade (the opacity it was drawing with).</summary>
    private static Color4 Recolor(Color c, float fade) =>
        new(c.R / 255f, c.G / 255f, c.B / 255f, fade * (c.A / 255f));
}
