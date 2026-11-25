using System.Diagnostics;
using System.Reflection;
using DistantWorlds.Types;
using DistantWorlds.UI;
using HarmonyLib;
using Stride.Core.Mathematics;

namespace DW2UIThemes;

/// <summary>
/// Developer tool: borders.flag in the output folder records for 3 seconds
/// - every control whose border color is read while drawing (BorderColorResolved): type, parent, name,
///   border width, final color, position;
/// - every line, rectangle or border DrawingHelper draws while a button renders: button name and type,
///   drawing method, color, and the ColorHelper fields that currently hold that color.
/// Then it writes a summary to the log. Finds borders whose color does not come from the theme.
/// The drawing patches exist only while recording; idle, the tool costs one check per border read.
/// </summary>
internal static class BorderSurvey
{
    private const double Seconds = 3;

    private static readonly Stopwatch running = new();
    private static readonly Dictionary<string, (int count, string where)> seen = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> drawn = new(StringComparer.Ordinal);
    private static volatile bool active;
    [ThreadStatic] private static List<DWControl> rendering;
    private static readonly HashSet<ToggleButtonGroup> groupsLogged = new();
    private static Harmony drawHarmony;

    private static readonly FieldInfo[] HelperColors = typeof(ColorHelper)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(Color))
        .ToArray();

    public static void Install(Harmony h) =>
        h.Patch(AccessTools.PropertyGetter(typeof(DWControl), nameof(DWControl.BorderColorResolved)),
            // Last, so it sees the color after the theme's own postfixes.
            postfix: new HarmonyMethod(typeof(BorderSurvey), nameof(Postfix)) { priority = Priority.Last });

    public static void Start()
    {
        lock (seen)
        {
            seen.Clear();
            drawn.Clear();
            groupsLogged.Clear();
            outsideCallers.Clear();
        }
        lock (fieldsCache)
        {
            fieldsCache.Clear(); // The theme may have changed since the last survey.
        }
        PatchDrawing();
        DumpStrideControls();
        running.Restart();
        active = true;
        Log.Info($"Border survey: recording for {Seconds} s.");
    }

    // ---- Stride UI elements --------------------------------------------------------------------
    // Some parts of the UI (the top bar buttons, apparently) are Stride UI elements kept in
    // UserInterfaceController.StrideControls, not DWControls. Their colors are properties of the
    // elements; this writes the tree with every color-like property to stride-ui.log.

    private const int StrideMaxLines = 1500;

    private static void DumpStrideControls()
    {
        try
        {
            var controls = UserInterfaceController.StrideControls;
            if (controls == null)
            {
                return;
            }
            var lines = new List<string>();
            List<KeyValuePair<string, Stride.UI.UIElement>> roots;
            lock (UserInterfaceController.CustomControlLock ?? new object())
            {
                roots = controls.ToList();
            }
            foreach (var (key, element) in roots)
            {
                lines.Add($"[{key}]");
                DumpElement(element, 1, lines, new HashSet<object>());
            }
            File.WriteAllLines(Path.Combine(Core.OutDir, "stride-ui.log"), lines.Take(StrideMaxLines));
            Log.Info($"Border survey: {roots.Count} Stride UI roots, {lines.Count} lines written to stride-ui.log.");
        }
        catch (Exception e)
        {
            Log.Warn("Border survey: Stride UI dump failed: " + e.Message);
        }
    }

    private static void DumpElement(object element, int depth, List<string> lines, HashSet<object> visited)
    {
        if (element == null || depth > 10 || lines.Count > StrideMaxLines || !visited.Add(element))
        {
            return;
        }
        var type = element.GetType();
        var parts = new List<string>();
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0)
            {
                continue;
            }
            bool interesting = p.PropertyType == typeof(Color) || p.Name.Contains("Border") || p.Name is "Name" or "Visibility" or "Opacity";
            if (!interesting)
            {
                continue;
            }
            try
            {
                object v = p.GetValue(element);
                parts.Add($"{p.Name}={(v is Color c ? $"{Hex(c)} ({FieldsWith(c)})" : v)}");
            }
            catch
            {
                // Property not readable now.
            }
        }
        foreach (var name in new[] { "Source", "Sprite", "SpriteProvider", "NotPressedImage", "PressedImage", "MouseOverImage" })
        {
            var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null)
            {
                try
                {
                    parts.Add($"{name}={p.GetValue(element)?.GetType().Name ?? "null"}");
                }
                catch
                {
                    // Skip.
                }
            }
        }
        lines.Add($"{new string(' ', depth * 2)}{type.Name}: {string.Join(", ", parts)}");

        // Children: Panel.Children, ContentControl.Content, and the internal VisualChildren as a fallback.
        var children = new List<object>();
        if (type.GetProperty("Children", BindingFlags.Public | BindingFlags.Instance)?.GetValue(element) is System.Collections.IEnumerable list)
        {
            children.AddRange(list.Cast<object>());
        }
        if (type.GetProperty("Content", BindingFlags.Public | BindingFlags.Instance)?.GetValue(element) is { } content)
        {
            children.Add(content);
        }
        if (children.Count == 0 && type.GetProperty("VisualChildren", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.GetValue(element) is System.Collections.IEnumerable visual)
        {
            children.AddRange(visual.Cast<object>());
        }
        foreach (var child in children)
        {
            DumpElement(child, depth + 1, lines, visited);
        }
    }

    /// <summary>Called once a second with the other flags; writes the summary when the time is up.</summary>
    public static void Check()
    {
        if (!active || running.Elapsed.TotalSeconds < Seconds)
        {
            return;
        }
        active = false;
        UnpatchDrawing();
        List<KeyValuePair<string, (int count, string where)>> rows;
        List<KeyValuePair<string, int>> lines;
        lock (seen)
        {
            rows = seen.OrderBy(r => r.Key, StringComparer.Ordinal).ToList();
            lines = drawn.OrderBy(r => r.Key, StringComparer.Ordinal).ToList();
        }
        Log.Info($"Border survey: {rows.Count} kinds of bordered controls (color | width | type < parent | name | reads | sample position):");
        foreach (var (key, (count, where)) in rows)
        {
            Log.Info($"  {key} | {count} | {where}");
        }
        Log.Info($"Border survey: {lines.Count} kinds of drawing calls inside controls (control < parent | method [texture] | color = ColorHelper fields | calls):");
        foreach (var (key, count) in lines)
        {
            Log.Info($"  {key} | {count}");
        }
    }

    private static void Postfix(DWControl __instance, Color __result)
    {
        if (!active || __instance == null || __instance.BorderWidth <= 0)
        {
            return;
        }
        try
        {
            string parent = __instance.Parent?.GetType().Name ?? "-";
            string group = __instance.Parent is ToggleButtonGroup g ? $" (group ButtonBorderColor {Hex(g.ButtonBorderColor)})" : "";
            string key = $"{Hex(__result)} | {__instance.BorderWidth:0.##} | {__instance.GetType().Name} < {parent} | {__instance.Name ?? "-"}{group}";
            lock (seen)
            {
                seen.TryGetValue(key, out var row);
                var r = __instance.DisplayRectangle;
                seen[key] = (row.count + 1, row.where ?? $"{r.X:0},{r.Y:0} {r.Width:0}x{r.Height:0}");
            }
        }
        catch
        {
            // A control in an odd state; skip it.
        }
    }

    // ---- Lines drawn inside buttons ------------------------------------------------------------

    private static void PatchDrawing()
    {
        try
        {
            drawHarmony ??= new Harmony("dw2uithemes.border-survey-drawing");
            // Controls render through DWControl.OnRender, and some buttons are drawn by calling their Render
            // directly; keep a stack of the ones being drawn.
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(DWControl)).Where(m => m.Name == "OnRender")
                .Concat(AccessTools.GetDeclaredMethods(typeof(DWButton)).Where(m => m.Name == "Render")))
            {
                drawHarmony.Patch(m, prefix: new HarmonyMethod(typeof(BorderSurvey), nameof(RenderPrefix)),
                    finalizer: new HarmonyMethod(typeof(BorderSurvey), nameof(RenderFinalizer)));
            }
            var draw = new HarmonyMethod(typeof(BorderSurvey), nameof(DrawPrefix));
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(DrawingHelper)))
            {
                bool isShape = m.Name is "DrawLine" or "DrawRectangle" or "DrawBorder" or "DrawLineCustomTexture"
                    or "FillLopsidedRectangle" or "FillRectangle" or "FillRectangleGradient";
                if (isShape && m.GetParameters().Any(p => p.Name == "color" && p.ParameterType == typeof(Color)))
                {
                    drawHarmony.Patch(m, prefix: draw);
                }
            }
            // Which "...Resolved" color each control reads while it draws.
            var resolved = new HarmonyMethod(typeof(BorderSurvey), nameof(ResolvedPostfix)) { priority = Priority.Last };
            foreach (var type in new[] { typeof(DWControl), typeof(DWButton) })
            {
                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(p => p.PropertyType == typeof(Color) && p.Name.EndsWith("Resolved") && p.GetMethod != null))
                {
                    drawHarmony.Patch(p.GetMethod, postfix: resolved);
                }
            }
            // Textured frames: every SpriteBatch.Draw overload ends in the internal DrawSprite (tint is a Color4).
            var drawSprite = AccessTools.Method(typeof(Stride.Graphics.SpriteBatch), "DrawSprite");
            if (drawSprite != null)
            {
                drawHarmony.Patch(drawSprite, prefix: new HarmonyMethod(typeof(BorderSurvey), nameof(SpritePrefix)));
            }
            else
            {
                Log.Warn("Border survey: SpriteBatch.DrawSprite not found.");
            }
        }
        catch (Exception e)
        {
            Log.Warn("Border survey: drawing patches failed: " + e.Message);
        }
    }

    private static void UnpatchDrawing()
    {
        try
        {
            drawHarmony?.UnpatchAll(drawHarmony.Id);
        }
        catch (Exception e)
        {
            Log.Warn("Border survey: could not remove drawing patches: " + e.Message);
        }
    }

    private static void RenderPrefix(DWControl __instance)
    {
        (rendering ??= new List<DWControl>()).Add(__instance);
        if (active && __instance is ToggleButtonGroup g)
        {
            LogGroup(g);
        }
    }

    private static Exception RenderFinalizer(Exception __exception)
    {
        if (rendering is { Count: > 0 })
        {
            rendering.RemoveAt(rendering.Count - 1);
        }
        return __exception;
    }

    private static void DrawPrefix(MethodBase __originalMethod, Color color) => Record(__originalMethod.Name, color);

    /// <summary>Only reads made by the control being drawn, on itself (not on its children).</summary>
    private static void ResolvedPostfix(DWControl __instance, Color __result, MethodBase __originalMethod)
    {
        if (active && rendering is { Count: > 0 } && ReferenceEquals(rendering[^1], __instance))
        {
            Record("reads " + __originalMethod.Name.Replace("get_", ""), __result);
        }
    }

    private static void SpritePrefix(Stride.Graphics.Texture texture, Stride.Core.Mathematics.Color4 color) =>
        Record($"DrawSprite [{texture?.Name ?? "?"} {texture?.Width}x{texture?.Height}]", (Color)color);

    /// <summary>(what, color) drawn outside any control → the game methods that called it (first time only).</summary>
    private static readonly Dictionary<string, string> outsideCallers = new(StringComparer.Ordinal);

    private static void Record(string what, Color color)
    {
        if (!active)
        {
            return;
        }
        try
        {
            string key;
            if (rendering is { Count: > 0 })
            {
                var c = rendering[^1];
                string parent = c.Parent == null ? "-" : $"{c.Parent.GetType().Name}:{c.Parent.Name ?? "-"}";
                key = $"{c.GetType().Name}:{c.Name ?? "-"} < {parent} | {what} | {Hex(color)} = {FieldsWith(color)}";
            }
            else
            {
                // Drawn outside any control (e.g. by the renderer itself): name the caller instead.
                string bare = $"{what} | {Hex(color)}";
                string caller;
                lock (seen)
                {
                    outsideCallers.TryGetValue(bare, out caller);
                }
                if (caller == null)
                {
                    caller = Caller();
                    lock (seen)
                    {
                        outsideCallers[bare] = caller;
                    }
                }
                key = $"(outside controls) {caller} | {what} | {Hex(color)} = {FieldsWith(color)}";
            }
            lock (seen)
            {
                drawn.TryGetValue(key, out int n);
                drawn[key] = n + 1;
            }
        }
        catch
        {
            // Skip.
        }
    }

    private static string Caller()
    {
        var frames = new StackTrace(3, false).GetFrames() ?? Array.Empty<StackFrame>();
        return string.Join(" < ", frames
            .Select(f => f.GetMethod())
            .Where(m => m?.DeclaringType != null && !m.DeclaringType.FullName.StartsWith("HarmonyLib")
                && !m.DeclaringType.FullName.StartsWith("DW2UIThemes") && !m.DeclaringType.FullName.StartsWith("Stride.Graphics"))
            .Take(3)
            .Select(m => $"{m.DeclaringType.Name}.{m.Name}".Replace("_Patch", "~")));
    }

    /// <summary>Each group's own color fields (copied when it was created) and the ColorHelper fields they match.</summary>
    private static void LogGroup(ToggleButtonGroup g)
    {
        lock (seen)
        {
            if (!groupsLogged.Add(g))
            {
                return;
            }
        }
        var fields = typeof(ToggleButtonGroup).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.FieldType == typeof(Color));
        var parts = fields.Select(f => { var c = (Color)f.GetValue(g); return $"{f.Name} {Hex(c)} = {FieldsWith(c)}"; });
        Log.Info($"Border survey: group {g.Name ?? "-"} < {g.Parent?.GetType().Name ?? "-"}: {string.Join("; ", parts)}");
    }

    private static readonly Dictionary<uint, string> fieldsCache = new();

    /// <summary>Cached per color while recording (DrawSprite runs thousands of times a frame).</summary>
    private static string FieldsWith(Color color)
    {
        uint packed = ((uint)color.R << 24) | ((uint)color.G << 16) | ((uint)color.B << 8) | color.A;
        lock (fieldsCache)
        {
            if (fieldsCache.TryGetValue(packed, out var cached))
            {
                return cached;
            }
        }
        string result = FieldsWithUncached(color);
        lock (fieldsCache)
        {
            fieldsCache[packed] = result;
        }
        return result;
    }

    private static string FieldsWithUncached(Color color)
    {
        var names = HelperColors.Where(f => (Color)f.GetValue(null) == color).Select(f => f.Name).ToList();
        if (names.Count == 0)
        {
            // Same color with another alpha is a common pattern; list those separately.
            names = HelperColors.Where(f => { var c = (Color)f.GetValue(null); return c.R == color.R && c.G == color.G && c.B == color.B; })
                .Select(f => f.Name + "(rgb)").ToList();
        }
        return names.Count == 0 ? "none" : string.Join(", ", names.Take(6));
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";
}
