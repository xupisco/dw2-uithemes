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
/// Same mechanism for the side gradient of the selected object window: SelectionPanel draws the panel
/// gradient texture (1024x2) tinted plain white, while other panels tint it with OverlayTintColor. While
/// SelectionPanel draws itself, that texture gets the theme's OverlayTintColor (alpha 0 = no gradient).
/// </summary>
internal static class Outlines
{
    public const string Key = "SectionHeaderPanel.OutlineColor";
    private const string GradientKey = "OverlayTintColor";

    [ThreadStatic] private static DWControl header;
    [ThreadStatic] private static bool headerIsSelection;
    [ThreadStatic] private static int childDepth;
    private static Color? color;
    private static Color? gradient;

    public static bool IsKnown(string key) => key == Key;

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
        color = theme != null && theme.Colors.TryGetValue(Key, out var hex) && Themes.TryParseColor(hex, 255, out var c) ? c : null;
        gradient = theme != null && theme.Colors.TryGetValue(GradientKey, out var g) && Themes.TryParseColor(g, 0, out var gc) ? gc : null;
    }

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
        if (header == null || childDepth > 0 || texture == null)
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
        if (Outlines.color is not { } outline || texture.Width != 1 || texture.Height != 1)
        {
            return;
        }
        var accent = ColorHelper.AccentColor;
        var drawn = (Color)color;
        if (drawn.R != accent.R || drawn.G != accent.G || drawn.B != accent.B)
        {
            return;
        }
        // Keep the fade: the panel draws the accent with its own opacity in the alpha.
        float alpha = color.A * (outline.A / 255f);
        color = new Color4(outline.R / 255f, outline.G / 255f, outline.B / 255f, alpha);
    }
}
