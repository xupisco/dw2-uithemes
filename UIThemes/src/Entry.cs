using System.Text.Json;
using DistantWorlds.Types;
using DistantWorlds2;
using HarmonyLib;

namespace DW2UIThemes;

/// <summary>Entry point for the game's own mod loader (mod.json mainClass). Options come from options.json.</summary>
public class Mod
{
    public Mod(DWGame game)
    {
        Core.Init("game mod loader");
        string path = Path.Combine(Core.ModDir, "options.json");
        Core.ApplyOptions(File.Exists(path) ? File.ReadAllText(path) : null, "options.json");
    }
}

/// <summary>Entry point for the DW2 Mod Launcher, which passes the settings.schema.json values.</summary>
public static class Entry
{
    public static void InitWithOptions(string json)
    {
        Core.Init("mod launcher");
        Core.ApplyOptions(json, "launcher settings");
    }

    public static void Init() => Core.Init("mod launcher");
}

internal static class Core
{
    private static bool initialized;

    public static string GameDir { get; private set; }

    public static string ModDir { get; private set; }

    public static string OutDir { get; private set; }

    public static bool DumpColors { get; private set; }

    public static void Init(string via)
    {
        if (initialized)
        {
            return;
        }
        initialized = true;

        GameDir = Path.GetDirectoryName(typeof(DWGame).Assembly.Location) ?? AppContext.BaseDirectory;
        string location = typeof(Core).Assembly.Location;
        ModDir = string.IsNullOrEmpty(location) ? Path.Combine(GameDir, "mods", "DW2UIThemes") : Path.GetDirectoryName(location);
        OutDir = Path.Combine(GameDir, "data", "Logs", "DW2UIThemes");
        Directory.CreateDirectory(OutDir);
        Log.Start(Path.Combine(OutDir, "DW2UIThemes.log"));
        Log.Info($"DW2 UI Themes {typeof(Core).Assembly.GetName().Version} starting (via {via}).");
        Log.Info($"Mod folder: {ModDir}");
        Themes.Folder = Path.Combine(ModDir, "themes");

        Install("Color scheme", h => h.Patch(
            AccessTools.Method(typeof(ColorHelper), nameof(ColorHelper.SetColorScheme)),
            postfix: new HarmonyMethod(typeof(Core), nameof(SetColorSchemePostfix))));

        Install("Tooltip backgrounds", h => h.Patch(
            AccessTools.Method(typeof(DistantWorlds.UI.DWControl), "DrawHoverTip"),
            prefix: new HarmonyMethod(typeof(Themes), nameof(Themes.DrawHoverTipPrefix)),
            postfix: new HarmonyMethod(typeof(Themes), nameof(Themes.DrawHoverTipPostfix))));

        Install("Live refresh", LiveRefresh.Install);

        Install("Control overrides", Overrides.Install);

        Install("Hotkeys", h => h.Patch(
            AccessTools.Method(typeof(DWGame), "Update"),
            postfix: new HarmonyMethod(typeof(Core), nameof(UpdatePostfix))));
    }

    /// <summary>Safe to call more than once (both loaders may run); the last call wins.</summary>
    public static void ApplyOptions(string json, string source)
    {
        string theme = "Basalt"; // Same default as settings.schema.json.
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var root = doc.RootElement;
            if (root.TryGetProperty("Theme", out var t) && t.ValueKind == JsonValueKind.String)
            {
                theme = t.GetString();
            }
            DumpColors = root.TryGetProperty("DumpColors", out var d) && d.ValueKind == JsonValueKind.True;
        }
        catch (JsonException e)
        {
            Log.Warn($"Options from {source} could not be read, using defaults: {e.Message}");
        }
        Themes.Select(theme, source);
    }

    private static void SetColorSchemePostfix(int schemeSelection)
    {
        try
        {
            Diagnostics.OnScheme(schemeSelection);
            Themes.SetColorSchemePostfix(schemeSelection);
            Diagnostics.AfterTheme();
        }
        catch (Exception e)
        {
            Log.Error("SetColorScheme postfix: " + e);
        }
    }

    private static void UpdatePostfix(DWGame __instance)
    {
        try
        {
            var input = __instance.Input;
            if (input != null && input.HasKeyboard)
            {
                Themes.Hotkeys(input);
            }
            Diagnostics.CheckFlags();
        }
        catch (Exception e)
        {
            Log.Error("Update postfix: " + e);
        }
    }

    private static void Install(string feature, Action<Harmony> patch)
    {
        try
        {
            patch(new Harmony("dw2uithemes." + feature.Replace(' ', '-').ToLowerInvariant()));
            Log.Info("Installed: " + feature);
        }
        catch (Exception e)
        {
            Log.Error($"{feature} failed to install: {e}");
        }
    }
}
