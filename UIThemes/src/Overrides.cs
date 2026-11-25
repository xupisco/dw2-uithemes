using System.Reflection;
using DistantWorlds.UI;
using HarmonyLib;
using Stride.Core.Mathematics;

namespace DW2UIThemes;

/// <summary>
/// Colors that live on a single control instead of ColorHelper, read through a "...Resolved"
/// property. A theme sets them as "Class.Color" (e.g. "SelectionPanel.SectionBackColor");
/// a postfix on the property returns the theme's color. The game's value is logged once.
/// Properties declared on a base class (DWControl) only apply to instances of the named class.
/// </summary>
internal static class Overrides
{
    /// <summary>(class the property is declared on, color name, class the key is for).</summary>
    private static readonly (Type Declaring, string Name, Type For)[] Known =
    {
        (typeof(SelectionPanel), "SectionBackColor", typeof(SelectionPanel)),
        (typeof(SelectionPanel), "HotspotBackColor", typeof(SelectionPanel)),
        (typeof(SelectionPanel), "HighlightColor", typeof(SelectionPanel)),
        (typeof(SelectionPanel), "GraphBackColor", typeof(SelectionPanel)),
        (typeof(DWControl), "BackColor", typeof(SelectionPanel)),
        (typeof(DWControl), "ShadowColor", typeof(SelectionPanel)),
        (typeof(DWControl), "DarkColor", typeof(SelectionPanel)),
        (typeof(DWControl), "BorderColor", typeof(SelectionPanel)),
        (typeof(DWControl), "ForeColor", typeof(SelectionPanel)),
        // Dividers between buttons in the button strips: textures drawn by the container, tinted with a
        // color it copied when it was created. Top-left section tabs and top-right game buttons: ForeColor
        // (game: ButtonColorText, #CFCFCF). Bottom-right map overlay buttons: DarkColor (game: #606060).
        (typeof(DWControl), "ForeColor", typeof(EmpireSectionList)),
        (typeof(DWControl), "ForeColor", typeof(AngledButtonStrip)),
        (typeof(DWControl), "DarkColor", typeof(MapOverlayButtons)),
    };

    private sealed class Entry
    {
        public string Key;
        public Type For;
    }

    /// <summary>Theme key → entry; getter → entries that read through it.</summary>
    private static readonly Dictionary<string, Entry> Keys = new(StringComparer.Ordinal);
    private static readonly Dictionary<MethodBase, List<Entry>> ByGetter = new();
    private static readonly HashSet<string> logged = new(StringComparer.Ordinal);

    /// <summary>Current theme's overrides, by key. Replaced whole on each theme change.</summary>
    private static Dictionary<string, string> active = new(StringComparer.Ordinal);

    public static bool IsKnown(string key) => Keys.ContainsKey(key);

    public static void Install(Harmony harmony)
    {
        var postfix = new HarmonyMethod(typeof(Overrides), nameof(ResolvedPostfix));
        foreach (var (declaring, name, forType) in Known)
        {
            var getter = AccessTools.PropertyGetter(declaring, name + "Resolved");
            if (getter == null)
            {
                Log.Warn($"Overrides: {declaring.Name}.{name}Resolved not found.");
                continue;
            }
            var entry = new Entry { Key = $"{forType.Name}.{name}", For = forType };
            Keys[entry.Key] = entry;
            if (!ByGetter.TryGetValue(getter, out var list))
            {
                ByGetter[getter] = list = new List<Entry>();
                harmony.Patch(getter, postfix: postfix);
            }
            list.Add(entry);
        }
        Log.Info($"Overrides: {Keys.Count} control colors patched.");
    }

    /// <summary>Called whenever the theme changes.</summary>
    public static void Use(Theme theme)
    {
        var next = new Dictionary<string, string>(StringComparer.Ordinal);
        if (theme != null)
        {
            foreach (var key in Keys.Keys)
            {
                if (theme.Colors.TryGetValue(key, out var hex))
                {
                    next[key] = hex;
                }
            }
        }
        active = next;
    }

    private static void ResolvedPostfix(object __instance, ref Color __result, MethodBase __originalMethod)
    {
        bool logging = logged.Count < Keys.Count;
        if (!logging && active.Count == 0)
        {
            return;
        }
        if (__instance == null || !ByGetter.TryGetValue(__originalMethod, out var entries))
        {
            return;
        }
        foreach (var entry in entries)
        {
            if (!entry.For.IsInstanceOfType(__instance))
            {
                continue;
            }
            if (logging && logged.Add(entry.Key))
            {
                Log.Info($"Game color {entry.Key}: #{__result.R:X2}{__result.G:X2}{__result.B:X2}{__result.A:X2}");
            }
            if (active.TryGetValue(entry.Key, out var hex) && Themes.TryParseColor(hex, __result.A, out var color))
            {
                __result = color;
            }
            return;
        }
    }
}
