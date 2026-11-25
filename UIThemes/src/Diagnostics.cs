using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using DistantWorlds.Types;
using DistantWorlds2;
using Stride.Core.Mathematics;

namespace DW2UIThemes;

/// <summary>
/// Developer tools: dumps ColorHelper colors to JSON and toggles loud test colors.
/// Triggered by flag files in the output folder (checked once a second):
/// dump.flag writes the current colors, toggle.flag turns the test colors on/off,
/// borders.flag records which controls draw borders and in what color (BorderSurvey).
/// With the DumpColors option, every scheme change is dumped to scheme-n.json.
/// </summary>
internal static class Diagnostics
{
    // Declaration order (metadata token) keeps related fields together in the dumps.
    private static readonly FieldInfo[] ColorFields = typeof(ColorHelper)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(Color) && !f.IsLiteral && !f.IsInitOnly)
        .OrderBy(f => f.MetadataToken)
        .ToArray();

    private static Dictionary<FieldInfo, Color> saved;
    private static readonly Stopwatch flagTimer = Stopwatch.StartNew();

    private static bool TestActive => saved != null;

    /// <summary>Runs right after the game sets its scheme colors, before the theme.</summary>
    public static void OnScheme(int schemeSelection)
    {
        // The call stack is only useful while mapping the game, so it goes with the developer option.
        Log.Info(Core.DumpColors
            ? $"SetColorScheme({schemeSelection}), called from {Caller()}"
            : $"SetColorScheme({schemeSelection})");
        if (Core.DumpColors)
        {
            Dump("scheme", schemeSelection);
        }
    }

    /// <summary>Runs after the theme, so the test colors stay on top while they are active.</summary>
    public static void AfterTheme()
    {
        if (TestActive)
        {
            ApplyTestColors();
        }
    }

    public static void CheckFlags()
    {
        if (flagTimer.ElapsedMilliseconds < 1000)
        {
            return;
        }
        flagTimer.Restart();
        if (TakeFlag("toggle.flag"))
        {
            ToggleTestColors("toggle.flag");
        }
        if (TakeFlag("dump.flag"))
        {
            Dump("dump.flag");
        }
        if (TakeFlag("borders.flag"))
        {
            BorderSurvey.Start();
        }
        BorderSurvey.Check();
    }

    public static void Dump(string reason, int? scheme = null)
    {
        try
        {
            int? setting = (Galaxy.Game as DWGame)?.DWSettings?.ColorScheme;
            string name = scheme.HasValue
                ? $"scheme-{scheme.Value}.json"
                : $"current-{reason}-{DateTime.Now:HHmmss}.json";
            string path = Path.Combine(Core.OutDir, name);

            using (var stream = File.Create(path))
            using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                json.WriteStartObject();
                json.WriteString("reason", reason);
                if (scheme.HasValue)
                {
                    json.WriteNumber("schemeSelection", scheme.Value);
                }
                if (setting.HasValue)
                {
                    json.WriteNumber("settingsColorScheme", setting.Value);
                }
                json.WriteString("theme", Themes.CurrentName);
                json.WriteBoolean("testColorsActive", TestActive);
                json.WriteString("time", DateTime.Now.ToString("s"));
                json.WriteStartObject("colors");
                foreach (var field in ColorFields)
                {
                    json.WriteString(field.Name, Hex((Color)field.GetValue(null)));
                }
                json.WriteEndObject();
                json.WriteEndObject();
            }
            Log.Info($"Dumped {ColorFields.Length} colors to {name}.");
        }
        catch (Exception e)
        {
            Log.Error($"Dump ({reason}) failed: {e}");
        }
    }

    private static void ToggleTestColors(string source)
    {
        if (TestActive)
        {
            foreach (var (field, color) in saved)
            {
                field.SetValue(null, color);
            }
            saved = null;
            Log.Info($"Test colors OFF ({source}): previous colors restored.");
        }
        else
        {
            ApplyTestColors();
            Log.Info($"Test colors ON ({source}).");
        }
    }

    private static void ApplyTestColors()
    {
        saved = new Dictionary<FieldInfo, Color>();
        foreach (var field in ColorFields)
        {
            var original = (Color)field.GetValue(null);
            var test = TestColorFor(field.Name, original);
            if (test.HasValue)
            {
                saved[field] = original;
                field.SetValue(null, test.Value);
            }
        }
    }

    /// <summary>One loud color per family, keeping the original alpha so transparency stays the same.</summary>
    private static Color? TestColorFor(string name, Color original)
    {
        Color rgb;
        if (name.StartsWith("Accent") || name == "SelectionColor")
        {
            rgb = new Color(255, 0, 255);   // magenta
        }
        else if (name.StartsWith("Button"))
        {
            rgb = new Color(255, 140, 0);   // orange
        }
        else if (name.StartsWith("Border") || name.StartsWith("Header") || name.StartsWith("PopupList"))
        {
            rgb = new Color(0, 255, 255);   // cyan
        }
        else if (name.StartsWith("Back") || name.EndsWith("BackColor"))
        {
            rgb = new Color(0, 110, 0);     // dark green
        }
        else
        {
            return null;
        }
        rgb.A = original.A;
        return rgb;
    }

    private static bool TakeFlag(string file)
    {
        string path = Path.Combine(Core.OutDir, file);
        if (!File.Exists(path))
        {
            return false;
        }
        File.Delete(path);
        return true;
    }

    private static string Caller()
    {
        var frames = new StackTrace(2, false).GetFrames()
            .Select(f => f.GetMethod())
            .Where(m => m != null && m.DeclaringType != null
                && !m.DeclaringType.FullName.StartsWith("HarmonyLib")
                && !m.DeclaringType.FullName.StartsWith("DW2UIThemes"))
            .Take(6)
            .Select(m => $"{m.DeclaringType.Name}.{m.Name}");
        return string.Join(" <- ", frames);
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";
}
