# DW2 UI Themes

A Distant Worlds 2 mod that recolors the game's interface with themes. Themes are plain JSON files, and the visual editor in `tools\` creates them.

- Findings about the game and the color map: [FINDINGS.md](FINDINGS.md) (development notes, in Portuguese)
- Status and next steps: [PLAN.md](PLAN.md) (in Portuguese)

## Install (development)

Requirements: .NET SDK (8 or newer) and the game installed.

```powershell
.\install.ps1
```

The script:
1. builds `UIThemes\` in Release;
2. copies the DLL, `mod.json`, `settings.schema.json`, `dw2modlauncher.json`, `changelog.txt`, `description.bbcode`, `preview.jpg`, `theme-editor.html` and the `themes\` folder to `<game>\mods\DW2UIThemes\`;
3. adds `mods/DW2UIThemes` to `mods.json` and to the launcher's `mods.<profile>.json`, keeping a `*.uithemes_backup` copy.

Options:
- `-NoRegister`: copy only, without touching the mod lists.
- `-GameDir "..."`: another game folder.

While the game is running the DLL is locked: close the game to install a new DLL. Theme JSON files can be copied to `<game>\mods\DW2UIThemes\themes\` at any time.

## Use

- **Pick the theme:** in the DW2 Mod Launcher, **UI theme** option (Off, Amethyst, Basalt, Graphite, Obsidian, Synthwave; Basalt by default). Without the launcher, use `options.json` in the mod's folder: `{"Theme": "Basalt"}`. The theme applies from the moment the game starts.
- **In game:**

  | Shortcut | Action |
  |---|---|
  | `Ctrl+Alt++` | next theme (Off included); the name shows on screen |
  | `Ctrl+Alt+-` | previous theme |
  | `Ctrl+Alt+0` | shows the current theme |

  A theme switched with the shortcut lasts for the session only. The `themes\` folder is read again on every switch, so an edited JSON shows up by switching away and back.
- **Themes and the game's scheme:** the theme goes on top of the color scheme chosen in the game's options. With Colorblind Assist, its warning and status colors stay in place.
- **Log:** `<game>\data\Logs\DW2UIThemes\DW2UIThemes.log`.

## Make a theme

The easiest way is the editor:

1. Open `tools\theme-editor.html` in a browser (it works offline).
2. Pick a starting point, adjust the colors on the mockup and click **Copy JSON**.
3. Save it as `UIThemes\themes\<name>.json` and add the name to `options` in `UIThemes\settings.schema.json`.
4. Run `.\tools\build-editor.ps1` so the editor includes the new theme as a preset.

### Format

```jsonc
{
  "name": "Basalt",          // name shown in game and used in the launcher
  "opaqueTooltips": true,    // optional: tooltips without the panel's transparency
  "colors": {
    "AccentColor": "#5076B4",                                      // ColorHelper field
    "BackColorLightTransparent": "#121212F0",                      // with alpha
    "UserInterfaceController.HoverTipBackColorDefault": "#141414FF", // static field of another class
    "SelectionPanel.HotspotBackColor": "#141414FF"                 // a control's own color
  }
}
```

- **Colors:** `#RRGGBB` keeps the field's original transparency, and `#RRGGBBAA` sets the transparency too. `//` comments and trailing commas are accepted.
- **Accepted keys:**
  - any `Color` field of `DistantWorlds.Types.ColorHelper`, by name (listed in the dumps in `reference\`);
  - `UserInterfaceController.<field>` and `EmpireMessage.<field>` (static colors);
  - `SelectionPanel.<color>`, where the color is `SectionBackColor`, `HotspotBackColor`, `HighlightColor`, `GraphBackColor`, `BackColor`, `ShadowColor`, `DarkColor`, `BorderColor` or `ForeColor`;
  - dividers in the button strips, which the game draws with a color copied when the strip is created: `EmpireSectionList.ForeColor` (top-left section tabs), `AngledButtonStrip.ForeColor` (top-right game buttons), `MapOverlayButtons.DarkColor` (bottom-right map buttons);
  - `SectionHeaderPanel.OutlineColor`: the outline around a section's sub-tabs (Exploration, Military...), which the game draws with `AccentColor`.
- **Accent shades:** if `AccentColorMid`, `AccentColorDark`, `AccentColorMidDark` and `AccentColorVeryDark` are missing, the mod derives them from `AccentColor` (× 0.7, × 0.5, × 0.25, × 0.125), as the game does.
- **Scheme fields:** set the 24 fields the game's schemes change (the editor always includes them), so the theme looks the same with any scheme. The log lists the missing ones.
- Unknown keys and invalid colors log a warning, once per file.

What each field paints is in the [screen map in FINDINGS.md](FINDINGS.md#mapa-da-tela-elemento--cor).

## Development tools

- **Probes:** `UIThemes\probes\*.json` paint suspect fields with loud colors, to find out what each one paints. To use them, copy them to the game's `themes\` folder. They are not in the package.
- **Flags:** in the log folder, creating `toggle.flag` turns loud test colors on or off, and `dump.flag` writes every current color to `current-*.json`. The mod checks once per second.
- **Border survey:** creating `borders.flag` in the log folder records for 3 seconds which controls draw borders, lines and textures, in which color, and which `ColorHelper` fields hold that color. The summary goes to the log, and the Stride UI elements (if any) to `stride-ui.log`. The drawing patches exist only while it records. This is how the dividers and the sub-tab outline were found.
- **Per-scheme dumps:** the hidden `DumpColors` option writes `scheme-<n>.json` on every scheme change.
- **Reference:** `reference\scheme-<n>-<name>.json` are the game's 7 schemes, recorded in game.
- **Workshop image:** `.\tools\build-thumbnail.ps1` renders `tools\thumbnail.html` with headless Edge and writes `UIThemes\preview.jpg` (2560×1440). The theme colors are copied into the HTML: when a theme changes, update the `THEMES` list there.
- **Workshop screenshots:** save the in-game captures as `ss_<theme>.png` in the root (same scene in all of them) and run `.\tools\build-screenshots.ps1`. It writes a JPG of each capture to `workshop\`, plus `compare.jpg` with the left panel of each theme side by side (area set with `-Crop x,y,width,height`). The `workshop\` folder is not in the package.
- **GitHub release:** `.\tools\build-release.ps1` builds and writes to `release\` the files the launcher reads (same layout as the Refreshed mods): `DW2UIThemes-v<version>.zip` (with the `DW2UIThemes/` folder inside), `.zip.sha256`, `.manifest.json` and `.preview.jpg`. Then:
  ```powershell
  gh release create DW2UIThemes-v<version> release\DW2UIThemes-v<version>.* --target master --title "UI Themes <version>" --notes-file <notes>
  ```
  First bump the version in `UIThemes.csproj` and `mod.json` and write the `changelog.txt` entry. Once `mod.json` has a `workshopId`, the manifest includes it.

## Code (`UIThemes\src\`)

| File | Role |
|---|---|
| `Entry.cs` | Mod entry points (`Mod` for the game's loader, `Entry` for the launcher), reading options and installing the patches |
| `Themes.cs` | Loads themes, applies them after each `SetColorScheme`, restores extra fields, shortcuts, on-screen message, tooltip background and `opaqueTooltips` |
| `LiveRefresh.cs` | Keeps the color history and, in the `...Resolved` properties of `DWButton` and `DWControl`, swaps old copies for the current colors (theme switch with the game running) |
| `Overrides.cs` | Controls' own colors (`SelectionPanel.*`) via postfix on the `...Resolved` properties; logs the game's values |
| `Outlines.cs` | Recolors single drawing calls while one panel draws itself: the sub-tab outline (`SectionHeaderPanel.OutlineColor`) and the side gradient of the selected object window (`OverlayTintColor`) |
| `Diagnostics.cs` | Dumps, test colors and flags |
| `BorderSurvey.cs` | `borders.flag`: records what draws borders and in which color |
| `Log.cs` | File log |

Patches are installed per feature, each with its own Harmony id. If one fails, the others keep working, and the failure goes to the log.

## Credits

Made by Leandro with the help of Claude, Anthropic's AI assistant, which wrote much of the code, the tools and the documentation.

## License

MIT, see [LICENSE](LICENSE).
