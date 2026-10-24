# PowerToys Controller

An unofficial Macro Deck 3 plugin that controls Microsoft PowerToys from your deck without relying on its keyboard shortcuts. Not affiliated with or endorsed by Microsoft.

Tested with PowerToys 0.102.2933 on Windows 10 22H2 (earlier releases tested with 0.101.2362).

## What it does

| Action | What it does |
| --- | --- |
| Launch PowerToys tool | Color Picker, Text Extractor, Screen Ruler, Shortcut Guide, Command Palette, Always On Top, Crop And Lock (reparent, thumbnail, screenshot), Find My Mouse, Mouse Highlighter, Mouse Crosshairs, Mouse Jump |
| Awake | Off, keep awake indefinitely, for N minutes, or until a time. Optionally keeps the screen on. |
| Windows theme | Toggle, dark or light, through Light Switch |
| PowerToys module on/off | Turns a module on, off or toggles it. **PowerToys restarts to apply it**, which closes its open windows for a few seconds. If PowerToys isn't running, the change is saved and applies when it next starts. |
| Apply FancyZones layout | Any custom layout or template, on one monitor or on all monitors. Cycle mode steps through the layouts you tick, one per press |
| Open FancyZones editor | Opens the layout editor |
| Launch PowerToys Workspace | Opens a saved Workspace |

The tools are triggered by the same signals PowerToys' own Command Palette and Quick Access use, so they keep working if you change or remove the shortcuts.

## Icons

The plugin ships the "PowerToys Controller Icons" pack: 58 flat white icons, one for every action and state above, pure white on a transparent background so Macro Deck's icon colour setting can recolour them. Macro Deck adds the pack when you install the plugin and keeps it in step on updates. The icons were made with AI help and are covered by this repository's MIT licence. The pack file is `src/PowerToysController/icon-packs/icons.macroDeckIconPack`.

## Things to know

- **The module must be on.** A tool whose module is off in PowerToys reports "turned off in PowerToys"; the plugin never turns modules on by itself. Light Switch, Awake, Workspaces and the mouse utilities are off by default in PowerToys.
- **Always On Top and Crop And Lock act on the focused window.** From a phone or the web client, that is the window you are working in. Pressing the button in the Macro Deck desktop window itself targets Macro Deck.
- **PowerToys running as administrator** cannot be controlled from Macro Deck, because Windows blocks it. `powertoys_status` then reads `elevated`.
- **Monitors.** A layout button remembers its monitor by name and serial number, not by position, so it keeps targeting the same screen after monitors are renumbered. An unplugged monitor fails with "not found".

## What the plugin changes

The plugin changes nothing until you press a button. It never acts in the background, and it never turns a module on by itself.

- **Awake** writes the mode into Awake's own settings file, `%LOCALAPPDATA%\Microsoft\PowerToys\Awake\settings.json`. Awake picks the change up live.
- **Module on/off** changes one entry of the `enabled` list in `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json`, the same entry PowerToys Settings changes. PowerToys reads that list only at startup and accepts live changes only from its own Settings app. So the plugin ends the PowerToys processes in your Windows session, writes the entry, and starts PowerToys again. Apps that you opened from PowerToys keep running. If PowerToys wasn't running, the plugin only writes the entry.
- **FancyZones layouts** are applied with `FancyZonesCLI.exe`, which ships with PowerToys.
- **Workspaces** are opened with PowerToys' own `PowerToys.WorkspacesLauncher.exe`.
- **Tools, the theme and the FancyZones editor** are opened with the same signals PowerToys' Command Palette and Quick Access send.

PowerToys and the Workspaces launcher are started through Windows Management Instrumentation (`Win32_Process.Create`), not as children of the plugin. Macro Deck ends a plugin's child processes when it restarts or updates the plugin, and this would otherwise close PowerToys or your Workspace apps too.

The plugin sends nothing over the network.

## Variables

| Variable | Value |
| --- | --- |
| `powertoys_status` | `ready`, `not_running`, `not_installed`, `elevated`, `restarting` |
| `powertoys_version` | For example `0.102.2933` |
| `powertoys_awake_mode` | `off`, `indefinite`, `timed`, `until` |
| `powertoys_awake_display_on` | `true` / `false` |
| `powertoys_theme` | `dark` / `light` (apps theme) |
| `powertoys_<module>_enabled` | One per module, for example `powertoys_colorpicker_enabled` or `powertoys_measure_tool_enabled` |
| `powertoys_layout_monitor_1` … `_4` | Active FancyZones layout name per monitor |

## Troubleshooting by `powertoys_status`

- **`not_running`**: start PowerToys.
- **`not_installed`**: install PowerToys from Microsoft (Store, winget or GitHub).
- **`elevated`**: in PowerToys Settings → General, turn off "Always run as administrator", then restart PowerToys.
- **`restarting`**: a module toggle is in progress. Wait a few seconds.
- **A tool button says "turned off in PowerToys"**: enable that module in PowerToys Settings, or with the module on/off action.

## Development

```
dotnet build
dotnet test
dotnet run --project tools/ManualPowerToysCheck -- --state          # read-only
dotnet run --project tools/ManualPowerToysCheck -- --fire ColorPicker  # live, visible
```

`docs/superpowers/specs/` holds the design. `docs/superpowers/plans/` holds the implementation plan.
