# PowerToys Controller

An unofficial Macro Deck 3 plugin that controls Microsoft PowerToys from your deck without relying on its keyboard shortcuts. Not affiliated with or endorsed by Microsoft.

Tested with PowerToys 0.102.3001 on Windows 10 22H2 (earlier releases tested with 0.101.2362 and 0.102.2933).

## What it does

| Action | What it does |
| --- | --- |
| Launch PowerToys tool | Color Picker, Text Extractor, Screen Ruler, Shortcut Guide, Command Palette, Always On Top, Crop And Lock (reparent, thumbnail, screenshot), Find My Mouse, Mouse Highlighter, Mouse Crosshairs, Mouse Jump, Cursor Wrap (on/off), Advanced Paste, Hosts File Editor, Environment Variables (both also as administrator), Registry Preview, Keyboard Manager editor, Power Display |
| ZoomIt | Zoom, Draw, Live Zoom, Snip, Snip text (OCR), Snip with dictation, Record screen (a second press stops it), Break timer |
| Always On Top | Pins or unpins the focused window, or raises or lowers the opacity of a pinned window |
| Mouse Without Borders | Toggles Easy Mouse, or reconnects to the other computers |
| Awake | Off, keep awake indefinitely, for N minutes, or until a time. Optionally keeps the screen on. |
| Windows theme | Toggle, dark or light, through Light Switch |
| PowerToys module on/off | Turns a module on, off or toggles it, live, without restarting PowerToys. PowerToys Settings shows for about 2 seconds while the plugin flips the module's switch. Alt Window Cycle, Command Not Found and Power Display have no switch the plugin can reach, so PowerToys restarts for those. If PowerToys isn't running, the change is saved and applies when it next starts. |
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
- **Module on/off** goes through PowerToys' own Settings app, because PowerToys accepts live module changes only from it. The plugin opens Settings on the module's page (`PowerToys.exe --open-settings=<page>`), flips the module's Enable switch with Windows UI Automation, and closes the window again unless it was already open. If the switch can't be found, for example after a PowerToys update moved it, the plugin falls back to a restart. It ends the PowerToys processes in your Windows session, changes the module's entry in the `enabled` list of `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json`, and starts PowerToys again. Apps that you opened from PowerToys keep running. If PowerToys wasn't running, the plugin only writes the entry.
- **FancyZones layouts** are applied with `FancyZonesCLI.exe`, which ships with PowerToys.
- **Workspaces** are opened with PowerToys' own `PowerToys.WorkspacesLauncher.exe`.
- **Tools, ZoomIt, Always On Top, Mouse Without Borders, the theme and the FancyZones editor** are triggered with the same signals PowerToys' Command Palette and Quick Access send.

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
- **`restarting`**: a module toggle that needs a PowerToys restart is in progress. Wait a few seconds.
- **A tool button says "turned off in PowerToys"**: enable that module in PowerToys Settings, or with the module on/off action.

## Development

```
dotnet build
dotnet test
dotnet run --project tools/ManualPowerToysCheck -- --state          # read-only
dotnet run --project tools/ManualPowerToysCheck -- --fire ColorPicker  # live, visible
```

`docs/superpowers/specs/` holds the design. `docs/superpowers/plans/` holds the implementation plan.
