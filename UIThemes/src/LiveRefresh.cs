using System.Reflection;
using DistantWorlds.Types;
using DistantWorlds.UI;
using HarmonyLib;
using Stride.Core.Mathematics;

namespace DW2UIThemes;

/// <summary>
/// Buttons and other controls copy ColorHelper colors into their own fields when they are created,
/// so a theme switched in game does not reach controls that already exist. They read those copies
/// through "...Resolved" properties; a postfix on each one swaps a color that belonged to an earlier
/// state of the matching ColorHelper field(s) for that field's current value. Colors the game chose
/// on purpose for a single control never matched a ColorHelper value, so they are left alone.
/// </summary>
internal static class LiveRefresh
{
    /// <summary>Resolved property → ColorHelper fields its copy may come from, in priority order.</summary>
    private static readonly (Type Type, string Property, string[] Fields)[] Getters =
    {
        (typeof(DWButton), "ButtonColorResolved", new[] { "ButtonColor" }),
        (typeof(DWButton), "ButtonColorHoverResolved", new[] { "ButtonColorHover" }),
        (typeof(DWButton), "ButtonColorDisabledResolved", new[] { "ButtonColorDisabled" }),
        (typeof(DWButton), "ButtonColorToggleResolved", new[] { "ButtonColorToggle" }),
        (typeof(DWButton), "ButtonColorToggleHoverResolved", new[] { "ButtonColorToggleHover" }),
        (typeof(DWButton), "ForeColorHoverResolved", new[] { "ButtonColorTextHover", "ForeColorHovered" }),
        (typeof(DWButton), "DropDownSelectorArrowColorResolved", new[] { "ButtonColorDropDownSelector" }),
        (typeof(DWButton), "DropDownSelectorArrowColorHoverResolved", new[] { "ButtonColorDropDownSelectorHover" }),
        (typeof(DWButton), "ImageTintColorResolved", new[] { "ButtonImageTintColor", "ButtonImageTintColorToggled" }),
        (typeof(DWButton), "ImageTintColorHoverResolved", new[] { "ButtonImageTintColorHover", "ButtonImageTintColorToggledHover" }),
        (typeof(DWButton), "ImageTintColorDisabledResolved", new[] { "ButtonImageTintColorDisabled" }),
        (typeof(DWControl), "BackColorResolved", new[] { "BackColor", "BackColorLightTransparent", "BackColorLightTransparentLess", "BackColorLightTransparentMore", "DialogBackColor", "HeaderBackColor", "ButtonColor", "PopupListBackColor", "HoverInfoBackColor" }),
        (typeof(DWControl), "ForeColorResolved", new[] { "ForeColor", "ButtonColorText", "HeaderForeColor" }),
        (typeof(DWControl), "BorderColorResolved", new[] { "BorderColor", "FaintBorderColor", "PopupListBorderColor", "HoverInfoBorderColor" }),
        (typeof(DWControl), "DarkColorResolved", new[] { "DarkColor" }),
        (typeof(DWControl), "ShadowColorResolved", new[] { "ShadowColor" }),
        (typeof(DWControl), "ListSelectionColorResolved", new[] { "ButtonColorToggle", "AccentColor", "AccentColorMid", "SelectionColor" }),
    };

    private static readonly Dictionary<string, FieldInfo> HelperFields = typeof(ColorHelper)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(Color))
        .ToDictionary(f => f.Name, StringComparer.Ordinal);

    /// <summary>Every value each ColorHelper field has had (game scheme and each theme applied).</summary>
    private static readonly Dictionary<string, HashSet<uint>> history = new(StringComparer.Ordinal);

    /// <summary>Getter → (old packed color → current color). Rebuilt after every theme application.</summary>
    private static Dictionary<MethodBase, Dictionary<uint, Color>> maps = new();

    private static volatile bool active;

    public static void Install(Harmony harmony)
    {
        var postfix = new HarmonyMethod(typeof(LiveRefresh), nameof(ResolvedPostfix));
        int patched = 0;
        foreach (var (type, property, _) in Getters)
        {
            var getter = AccessTools.PropertyGetter(type, property);
            if (getter == null)
            {
                Log.Warn($"Live refresh: {type.Name}.{property} not found.");
                continue;
            }
            harmony.Patch(getter, postfix: postfix);
            patched++;
        }
        Log.Info($"Live refresh: {patched} color properties patched.");
    }

    /// <summary>Called after the game's colors and again after the theme's, on every scheme application.</summary>
    public static void Record()
    {
        foreach (var (name, field) in HelperFields)
        {
            if (!history.TryGetValue(name, out var values))
            {
                history[name] = values = new HashSet<uint>();
            }
            values.Add(Pack((Color)field.GetValue(null)));
        }
    }

    /// <summary>Rebuilds the remap tables from the history and the current ColorHelper values.</summary>
    public static void Rebuild()
    {
        var next = new Dictionary<MethodBase, Dictionary<uint, Color>>();
        int entries = 0;
        foreach (var (type, property, fields) in Getters)
        {
            var getter = AccessTools.PropertyGetter(type, property);
            if (getter == null)
            {
                continue;
            }
            var map = new Dictionary<uint, Color>();
            foreach (var name in fields)
            {
                if (!HelperFields.TryGetValue(name, out var field) || !history.TryGetValue(name, out var values))
                {
                    continue;
                }
                var now = (Color)field.GetValue(null);
                uint nowPacked = Pack(now);
                foreach (uint old in values)
                {
                    // First field in the list wins when two fields shared an old value.
                    if (old != nowPacked && !map.ContainsKey(old))
                    {
                        map[old] = now;
                    }
                }
            }
            if (map.Count > 0)
            {
                next[getter] = map;
                entries += map.Count;
            }
        }
        maps = next;
        active = next.Count > 0;
        Log.Info($"Live refresh: {entries} old colors mapped to the current ones.");
    }

    private static void ResolvedPostfix(ref Color __result, MethodBase __originalMethod)
    {
        if (!active)
        {
            return;
        }
        if (maps.TryGetValue(__originalMethod, out var map) && map.TryGetValue(Pack(__result), out var current))
        {
            __result = current;
        }
    }

    private static uint Pack(Color c) => ((uint)c.R << 24) | ((uint)c.G << 16) | ((uint)c.B << 8) | c.A;
}
