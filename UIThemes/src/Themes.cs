using System.Globalization;
using System.Reflection;
using System.Text.Json;
using DistantWorlds.Types;
using DistantWorlds2;
using Stride.Core.Mathematics;
using Stride.Input;

namespace DW2UIThemes;

internal sealed class Theme
{
    public string Name;
    public string File;
    public Dictionary<string, string> Colors = new(StringComparer.Ordinal);

    /// <summary>"opaqueTooltips": draw tooltips at full opacity instead of the owning panel's.</summary>
    public bool OpaqueTooltips;
}

/// <summary>
/// Loads themes\*.json and writes their colors into ColorHelper every time the game applies
/// a color scheme. A theme sits on top of whatever scheme is chosen in the game's options.
/// </summary>
internal static class Themes
{
    /// <summary>The 24 UI fields the game's own schemes 0-5 change (see reference\).</summary>
    public static readonly string[] SchemeFields =
    {
        "DialogBackColor", "MainViewHeaderBackColor",
        "BackColorLightTransparent", "BackColorLightTransparentLess", "BackColorLightTransparentMore",
        "HeaderBackColor", "HeaderForeColor", "HeaderDecorationColor",
        "AccentColor", "AccentColorLight", "AccentColorMid", "AccentColorDark", "AccentColorVeryDark",
        "AccentColorMainViewBright",
        "ButtonColorToggle", "ButtonColorToggleHover",
        "ButtonColorText", "ButtonColorTextHover", "ButtonColorTextDisabled",
        "ButtonColorDropDownSelector", "ButtonColorDropDownSelectorHover",
        "PopupListCurrentItemColor", "PopupListCurrentItemTextColor", "PopupListHighlightColor",
    };

    /// <summary>Accent shades derived from AccentColor when a theme leaves them out (same factors in every game scheme).</summary>
    private static readonly (string Field, float Factor)[] AccentShades =
    {
        ("AccentColorMid", 0.7f), ("AccentColorDark", 0.5f), ("AccentColorMidDark", 0.25f), ("AccentColorVeryDark", 0.125f),
    };

    /// <summary>
    /// Static colors outside ColorHelper, written as "Class.Field" in a theme,
    /// e.g. "UserInterfaceController.HoverTipBackColorDefault" (tooltip background).
    /// </summary>
    private static readonly Type[] OtherColorHolders =
    {
        typeof(DistantWorlds.UI.UserInterfaceController),
        typeof(EmpireMessage),
    };

    /// <summary>ColorHelper fields by bare name, the others as "Class.Field".</summary>
    private static readonly Dictionary<string, FieldInfo> Fields = BuildFields();

    private static Dictionary<string, FieldInfo> BuildFields()
    {
        static IEnumerable<FieldInfo> ColorsOf(Type type) => type
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Color) && !f.IsLiteral && !f.IsInitOnly);

        var fields = ColorsOf(typeof(ColorHelper)).ToDictionary(f => f.Name, StringComparer.Ordinal);
        foreach (var type in OtherColorHolders)
        {
            foreach (var field in ColorsOf(type))
            {
                fields[$"{type.Name}.{field.Name}"] = field;
            }
        }
        return fields;
    }

    /// <summary>Load warnings already written; themes are re-read on every switch, so each warning shows once.</summary>
    private static readonly HashSet<string> warned = new(StringComparer.Ordinal);

    private static void WarnOnce(string text)
    {
        if (warned.Add(text))
        {
            Log.Warn(text);
        }
    }

    private static readonly HashSet<string> SchemeFieldSet = new(SchemeFields, StringComparer.Ordinal);

    /// <summary>
    /// Game value of every non-scheme field the mod has written, taken before the first write.
    /// The game's SetColorScheme only resets the scheme fields, so these are put back by hand
    /// before each theme is applied (otherwise Off would keep the last theme's extras).
    /// </summary>
    private static readonly Dictionary<string, Color> originals = new(StringComparer.Ordinal);

    private static List<Theme> loaded = new();
    private static Theme current;
    private static string wantedName = "Off";
    private static int lastScheme = -1;
    private static bool reapplying;

    public static string Folder { get; set; }

    public static string CurrentName => current?.Name ?? "Off";

    /// <summary>Sets the theme by name (from the launcher or options.json) and applies it now if the game is running.</summary>
    public static void Select(string name, string source)
    {
        wantedName = string.IsNullOrWhiteSpace(name) ? "Off" : name.Trim();
        Reload();
        current = Find(wantedName);
        if (current == null && !IsOff(wantedName))
        {
            Log.Warn($"Theme \"{wantedName}\" not found in {Folder}; using Off.");
        }
        Log.Info($"Theme: {CurrentName} ({source}).");
        Reapply();
    }

    public static void SetColorSchemePostfix(int schemeSelection)
    {
        if (!reapplying)
        {
            lastScheme = schemeSelection;
        }
        foreach (var (name, color) in originals)
        {
            Fields[name].SetValue(null, color);
        }
        Overrides.Use(current);
        Outlines.Use(current);
        LiveRefresh.Record(); // the game's own colors
        if (current != null)
        {
            Apply(current);
            LiveRefresh.Record();
        }
        LiveRefresh.Rebuild();
    }

    private const string TooltipBackKey = "UserInterfaceController.HoverTipBackColorDefault";
    private static readonly HashSet<uint> tooltipBacksSeen = new();

    /// <summary>
    /// Some tooltips set their own gray background instead of using HoverTipBackColorDefault.
    /// While one is drawn, a neutral gray background is swapped for the theme's tooltip background.
    /// </summary>
    public static void DrawHoverTipPrefix(DistantWorlds.UI.DWControl __instance, HoverTip hoverTip, out (Color Back, float Opacity) __state)
    {
        __state = (default, -1f);
        if (hoverTip == null)
        {
            return;
        }
        __state.Back = hoverTip.BackColor;
        if (current != null && current.OpaqueTooltips && __instance != null)
        {
            float opacity = __instance.Opacity;
            if (opacity < 1f)
            {
                __state.Opacity = opacity;
                __instance.Opacity = 1f;
            }
        }
        var back = hoverTip.BackColor;
        uint packed = ((uint)back.R << 24) | ((uint)back.G << 16) | ((uint)back.B << 8) | back.A;
        if (back.A != 0 && tooltipBacksSeen.Count < 20 && tooltipBacksSeen.Add(packed))
        {
            Log.Info($"Tooltip with its own background: {Hex(back)}");
        }
        if (back.A != 0 && back.R == back.G && back.G == back.B
            && current != null && current.Colors.TryGetValue(TooltipBackKey, out var hex)
            && TryParse(hex, back.A, out var themed))
        {
            hoverTip.BackColor = themed;
        }
    }

    public static void DrawHoverTipPostfix(DistantWorlds.UI.DWControl __instance, HoverTip hoverTip, (Color Back, float Opacity) __state)
    {
        if (hoverTip != null)
        {
            hoverTip.BackColor = __state.Back;
        }
        if (__state.Opacity >= 0f && __instance != null)
        {
            __instance.Opacity = __state.Opacity;
        }
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";

    /// <summary>Writes a color field, remembering the game's value of non-scheme fields first.</summary>
    private static void Write(string name, Color color)
    {
        var field = Fields[name];
        if (!SchemeFieldSet.Contains(name) && !originals.ContainsKey(name))
        {
            originals[name] = (Color)field.GetValue(null);
        }
        field.SetValue(null, color);
    }

    public static void Hotkeys(InputManager input)
    {
        bool ctrl = input.IsKeyDown(Keys.LeftCtrl) || input.IsKeyDown(Keys.RightCtrl);
        bool alt = input.IsKeyDown(Keys.LeftAlt) || input.IsKeyDown(Keys.RightAlt);
        if (!ctrl || !alt)
        {
            return;
        }
        if (input.IsKeyPressed(Keys.OemPlus) || input.IsKeyPressed(Keys.Add))
        {
            Cycle(+1);
        }
        else if (input.IsKeyPressed(Keys.OemMinus) || input.IsKeyPressed(Keys.Subtract))
        {
            Cycle(-1);
        }
        else if (input.IsKeyPressed(Keys.D0) || input.IsKeyPressed(Keys.NumPad0))
        {
            Announce();
        }
    }

    /// <summary>Shows the current theme in the game's brief on-screen message.</summary>
    private static void Announce()
    {
        try
        {
            if ((Galaxy.Game as DWGame)?.Renderer is ScaledRenderer renderer)
            {
                renderer.SetBriefMessage($"UI theme: {CurrentName}", 2.5f);
            }
        }
        catch (Exception e)
        {
            Log.Warn("Could not show the theme on screen: " + e.Message);
        }
    }

    /// <summary>Next/previous theme, Off included. Re-reads the folder so edited JSON shows up right away.</summary>
    private static void Cycle(int step)
    {
        Reload();
        var names = new List<string> { "Off" };
        names.AddRange(loaded.Select(t => t.Name));
        int index = names.FindIndex(n => string.Equals(n, CurrentName, StringComparison.OrdinalIgnoreCase));
        index = ((index < 0 ? 0 : index) + step + names.Count) % names.Count;
        current = Find(names[index]);
        Log.Info($"Theme: {CurrentName} (hotkey).");
        Reapply();
        Announce();
    }

    /// <summary>Runs the game's SetColorScheme again so fields the theme leaves out go back to the game's values.</summary>
    private static void Reapply()
    {
        if (lastScheme < 0)
        {
            return; // The game has not applied a scheme yet; the postfix will apply the theme when it does.
        }
        try
        {
            reapplying = true;
            ColorHelper.SetColorScheme(lastScheme);
        }
        catch (Exception e)
        {
            Log.Error("Re-applying the color scheme failed: " + e);
        }
        finally
        {
            reapplying = false;
        }
    }

    private static void Apply(Theme theme)
    {
        int set = 0;
        foreach (var (name, hex) in theme.Colors)
        {
            if (!Fields.TryGetValue(name, out var field))
            {
                continue; // Reported once at load.
            }
            var color = (Color)field.GetValue(null);
            if (TryParse(hex, color.A, out var parsed))
            {
                Write(name, parsed);
                set++;
            }
        }

        if (theme.Colors.TryGetValue("AccentColor", out var accentHex)
            && TryParse(accentHex, 255, out var accent))
        {
            foreach (var (name, factor) in AccentShades)
            {
                if (!theme.Colors.ContainsKey(name) && Fields.TryGetValue(name, out var field))
                {
                    var shade = new Color((byte)(accent.R * factor), (byte)(accent.G * factor), (byte)(accent.B * factor), ((Color)field.GetValue(null)).A);
                    Write(name, shade);
                    set++;
                }
            }
        }
        Log.Info($"Applied theme {theme.Name}: {set} colors (scheme {lastScheme}).");
    }

    private static void Reload()
    {
        var themes = new List<Theme>();
        if (Folder == null || !Directory.Exists(Folder))
        {
            Log.Warn($"Themes folder not found: {Folder}");
            loaded = themes;
            return;
        }
        foreach (var path in Directory.GetFiles(Folder, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                themes.Add(Load(path));
            }
            catch (Exception e)
            {
                WarnOnce($"Skipped {Path.GetFileName(path)}: {e.Message}");
            }
        }
        loaded = themes;
        if (current != null)
        {
            current = Find(current.Name); // Pick up edits to the active theme.
        }
    }

    private static Theme Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        var theme = new Theme
        {
            File = Path.GetFileName(path),
            Name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : Path.GetFileNameWithoutExtension(path),
            OpaqueTooltips = root.TryGetProperty("opaqueTooltips", out var o) && o.ValueKind == JsonValueKind.True,
        };
        if (root.TryGetProperty("colors", out var colors) && colors.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in colors.EnumerateObject())
            {
                string hex = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                if (!Fields.ContainsKey(prop.Name) && !Overrides.IsKnown(prop.Name) && !Outlines.IsKnown(prop.Name))
                {
                    WarnOnce($"{theme.File}: unknown color \"{prop.Name}\" ignored.");
                }
                else if (!TryParse(hex, 255, out _))
                {
                    WarnOnce($"{theme.File}: \"{prop.Name}\" has an invalid color \"{hex}\" (use #RRGGBB or #RRGGBBAA).");
                }
                else
                {
                    theme.Colors[prop.Name] = hex;
                }
            }
        }
        var missing = SchemeFields.Where(f => !theme.Colors.ContainsKey(f) && !AccentShades.Any(s => s.Field == f && theme.Colors.ContainsKey("AccentColor"))).ToList();
        if (missing.Count > 0)
        {
            WarnOnce($"{theme.File}: leaves {missing.Count} scheme colors to the game's scheme: {string.Join(", ", missing)}");
        }
        return theme;
    }

    private static Theme Find(string name) =>
        IsOff(name) ? null : loaded.FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileNameWithoutExtension(t.File), name, StringComparison.OrdinalIgnoreCase));

    private static bool IsOff(string name) => string.Equals(name, "Off", StringComparison.OrdinalIgnoreCase);

    public static bool TryParseColor(string hex, byte alpha, out Color color) => TryParse(hex, alpha, out color);

    /// <summary>#RRGGBB keeps the given alpha (the field's own transparency); #RRGGBBAA sets it.</summary>
    private static bool TryParse(string hex, byte alpha, out Color color)
    {
        color = default;
        if (string.IsNullOrEmpty(hex))
        {
            return false;
        }
        string s = hex.TrimStart('#');
        if ((s.Length != 6 && s.Length != 8) || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
        {
            return false;
        }
        color = s.Length == 6
            ? new Color((byte)(v >> 16), (byte)(v >> 8), (byte)v, alpha)
            : new Color((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }
}
