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
/// </summary>
internal static class Outlines
{
    public const string Key = "SectionHeaderPanel.OutlineColor";

    [ThreadStatic] private static SectionHeaderPanel header;
    [ThreadStatic] private static int childDepth;
    private static Color? color;

    public static bool IsKnown(string key) => key == Key;

    public static void Install(Harmony h)
    {
        foreach (var m in AccessTools.GetDeclaredMethods(typeof(SectionHeaderPanel)).Where(m => m.Name == "Render"))
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
    }

    private static void HeaderPrefix(SectionHeaderPanel __instance)
    {
        header = __instance;
        childDepth = 0;
    }

    private static Exception HeaderFinalizer(Exception __exception)
    {
        header = null;
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
        if (header == null || childDepth > 0 || Outlines.color is not { } outline || texture == null || texture.Width != 1 || texture.Height != 1)
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
