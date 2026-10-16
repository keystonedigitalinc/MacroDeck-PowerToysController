# PowerToys Control for Macro Deck 3: Design Specification

Date: 2026-10-16
Status: Draft for review

## 1. Purpose

Control Microsoft PowerToys from Macro Deck 3 buttons, without depending on PowerToys hotkeys, and show PowerToys state on the deck. The plugin covers four areas:

- instant tools
- FancyZones layouts
- Awake and theme
- module on/off and Workspaces

## 2. Scope

### Phase 1

- **Launch tool.** Color Picker, Text Extractor, Screen Ruler, Shortcut Guide, Command Palette, Crop And Lock (reparent, thumbnail, screenshot), Find My Mouse, Mouse Highlighter, Mouse Crosshairs, Mouse Jump, and Always On Top.
- **Awake.** Off, keep awake indefinitely, keep awake for N minutes, or keep awake until a time. Optionally keep the display on.
- **Theme.** Toggle, force dark, or force light, through Light Switch.
- **Module on/off.** On, off, or toggle for any PowerToys module. This **restarts PowerToys**, and the action is labelled to say so.
- **Variables.** Status, Awake mode, theme, and enabled state per module.

### Phase 2

- **Apply FancyZones layout.** Applies a layout to one monitor or to all monitors.
- **Open FancyZones editor.**
- **Launch Workspace.**
- **Variables.** Active layout per monitor.

### Out of scope

- Peek. It needs a selected file in Explorer, which makes it a poor deck button.
- PowerToys Run, now superseded by Command Palette.
- Editing any module's settings other than Awake and the enabled map.
- PowerToys running elevated. The plugin detects this case and reports it, but does not work around it.

## 3. Environment

| Item | Value |
| --- | --- |
| PowerToys | (Preview) 0.101.2362.0, per-user install, `%LOCALAPPDATA%\PowerToys`, not elevated |
| PowerToys data | `%LOCALAPPDATA%\Microsoft\PowerToys` |
| Macro Deck | 3.0.0-beta.17 |
| SDK | `MacroDeck.*` 3.0.0-beta.17 (pinned), CLI 3.0.0-beta.17 |
| Target | `net10.0` + `[assembly: SupportedOSPlatform("windows")]`, framework-dependent `win-x64` |
| Plugin ID | `com.keystonedigital.powertoys` |
| Display name | PowerToys Control (unofficial) |
| Publisher | Keystone Digital |
| Repository | `https://github.com/keystonedigitalinc/MacroDeck-PowerToysController` |

The target framework rule comes from the Razer plugin. NUnit cannot load a test assembly built for `net10.0-windows`.

## 4. Proven foundations

These come from a source spike on 2026-10-16, which read tag `v0.101.2362.0` (commit 389b1a38). No live test has been run yet; section 10 covers that.

### 4.1 Trigger events

- **Origin.** PowerToys' own Command Palette extension, the PowerToys Run plugin and Quick Access trigger tools by setting fixed named events: `new EventWaitHandle(...).Set()`. The names are defined in `src/common/interop/shared_constants.h`.
- **Scope and naming.** Every name is `Local\` (per session) and carries a fixed GUID suffix.
- **Independent of hotkeys.** An event works no matter what hotkeys the user has set.
- **Opening.** The plugin opens events with `EventWaitHandle.TryOpenExisting`. If the event does not exist, the module or its listener is not running. The plugin then never creates a stray event.

| Tool | Event name |
| --- | --- |
| Color Picker | `Local\ShowColorPickerEvent-8c46be2a-3e05-4186-b56b-4ae986ef2525` |
| Text Extractor | `Local\PowerOCREvent-dc864e06-e1af-4ecc-9078-f98bee745e3a` |
| Screen Ruler | `Local\MeasureToolEvent-3d46745f-09b3-4671-a577-236be7abd199` |
| Shortcut Guide | `Local\ShortcutGuide-TriggerEvent-d4275ad3-2531-4d19-9252-c0becbd9b496` |
| Always On Top (pin foreground) | `Local\AlwaysOnTopPinEvent-892e0aa2-cfa8-4cc4-b196-ddeb32314ce8` |
| Command Palette | `Local\PowerToysCmdPal-ShowEvent-62336fcd-8611-4023-9b30-091a6af4cc5a` |
| Crop And Lock reparent | `Local\PowerToysCropAndLockReparentEvent-6060860a-76a1-44e8-8d0e-6355785e9c36` |
| Crop And Lock thumbnail | `Local\PowerToysCropAndLockThumbnailEvent-1637be50-da72-46b2-9220-b32b206b2434` |
| Crop And Lock screenshot | `Local\PowerToysCropAndLockScreenshotEvent-ff077ab2-8360-4bd1-864a-637389d35593` |
| Find My Mouse | `Local\FindMyMouseTriggerEvent-5a9dc5f4-1c74-4f2f-a66f-1b9b6a2f9b23` |
| Mouse Highlighter | `Local\MouseHighlighterTriggerEvent-1e3c9c3d-3fdf-4f9a-9a52-31c9b3c3a8f4` |
| Mouse Crosshairs | `Local\MouseCrosshairsTriggerEvent-0d4c7f92-0a5c-4f5c-b64b-8a2a2f7e0b21` |
| Mouse Jump | `Local\MouseJumpEvent-aa0be051-3396-4976-b7ba-1a9cc7d236a5` |
| Light Switch toggle | `Local\PowerToys-LightSwitch-ToggleEvent-d8dc2f29-8c94-4ca1-8c5f-3e2b1e3c4f5a` |
| FancyZones editor | `Local\FancyZones-ToggleEditorEvent-1e174338-06a3-472b-874d-073b21c62f14` |

The exact Crop And Lock names are confirmed during implementation, as shown in section 10. Its tools, Always On Top and the Crop And Lock actions act on the **foreground window**.

### 4.2 Awake

- **Live file.** Awake watches `%LOCALAPPDATA%\Microsoft\PowerToys\Awake\settings.json` and applies changes within about 25 ms. PowerToys' own Awake service sets modes by rewriting this file.
- **Fields.** Under `properties`:
  - `mode`: 0 off, 1 indefinite, 2 timed, 3 expirable
  - `keepDisplayOn`
  - `intervalHours`
  - `intervalMinutes`
  - `expirationDateTime`, an ISO date-time with offset
- **Writing.** The plugin reads, changes and writes the file back, and keeps every unknown field.

### 4.3 Module enable map

- **The map.** `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json` contains `enabled: { "<Module>": bool }`. The runner reads it **only at startup**.
- **No live route.** The runner's settings pipe accepts only the signed `PowerToys.Settings.exe`.
- **How a toggle works.**
  1. Write the map.
  2. Stop `PowerToys.exe` and its module processes.
  3. Start `PowerToys.exe` through WMI (`Win32_Process.Create`). The Razer plugin showed that a child process started by the plugin dies with Macro Deck's plugin job.
- **Key names** (from PowerToys' `ModuleEnablementService`): `AlwaysOnTop`, `Awake`, `CmdPal`, `ColorPicker`, `CropAndLock`, `FancyZones`, `FindMyMouse`, `LightSwitch`, `Measure Tool`, `MouseHighlighter`, `MouseJump`, `MousePointerCrosshairs`, `Peek`, `Shortcut Guide`, `TextExtractor`, `Workspaces`, and the others present in the map.
- **Policy.** A group policy can force a module on or off. The plugin reports this when it applies.

### 4.4 FancyZones

`FancyZonesCLI.exe` ships in the install root and is present on this machine. It needs the FancyZones process to be running. Commands:

| Command | Purpose |
| --- | --- |
| `get-monitors` | List monitors |
| `get-layouts` | List layouts |
| `get-active-layout` | Read the active layout |
| `set-layout <uuid\|template> --monitor N` or `--all` | Apply a layout |
| `open-editor` | Open the zones editor |

`set-layout` itself does three things:
1. It refreshes the editor parameters.
2. It merges the new layout into `applied-layouts.json`.
3. It broadcasts the reload message.

### 4.5 Workspaces

- **Launch.** Run `<install>\PowerToys.WorkspacesLauncher.exe <workspaceGuid> 1`. This is what a workspace desktop shortcut runs.
- **List.** Read `%LOCALAPPDATA%\Microsoft\PowerToys\Workspaces\workspaces.json`, which holds `workspaces[]` with `id`, `name` and more.
- **This machine** has no workspaces saved yet.

### 4.6 Theme

- **Light Switch.** It exposes a toggle event only.
- **Forcing a theme.** "Force dark" and "force light" read `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` (`AppsUseLightTheme`, `SystemUsesLightTheme`) and toggle only when the theme differs.

### 4.7 Elevation

If PowerToys runs elevated (`run_elevated`), Windows integrity rules most likely block the events and the FancyZones broadcasts from Macro Deck, which runs at medium integrity. The plugin detects an elevated `PowerToys.exe` and reports status `elevated`. This is inferred, not tested.

## 5. Architecture

### 5.1 Components

Each component has one responsibility.

| Component | Responsibility |
| --- | --- |
| `PowerToysLocator` | Finds the install path and version (uninstall registry entry, then the default path). Reports running and elevated state. Reads the enable map. |
| `EventTrigger` | Holds the event table (section 4.1) and the tool ids. `TryTrigger(tool)` returns one of: Triggered, ModuleNotRunning, Denied. |
| `AwakeControl` | Reads the Awake state. Sets a mode with a read-modify-write. Confirms by reading back. |
| `ThemeControl` | Reads the current theme. Toggles it, and forces dark or light. |
| `ModuleToggle` | Writes the enable map, restarts PowerToys through WMI, and confirms that the map holds and the runner is back. |
| `FancyZonesControl` | Runs `FancyZonesCLI.exe`, parses its output, and provides monitors, layouts, the active layout and apply. |
| `WorkspacesControl` | Lists workspaces and launches one through the launcher. |
| `ProcessRunner` | Runs a CLI with a timeout and captures its output. Fakeable in tests. |
| `StateService` | Hosted service. Every 2 s it rebuilds a snapshot: status, Awake, theme and enable map. Every 30 s, and after each apply, it reads layouts, monitors and workspaces. |
| `PowerToysIntegration` | Actions, variables and the variable read mapping. |

### 5.2 Rules carried over from the Razer plugin

- **Synchronisation.** One `SemaphoreSlim` per target serialises same-target presses: one for Awake, one for theme, one per monitor, and one for the module map.
- **Confirmation.** A confirmable action reports `Success` only after it is confirmed (section 6).
- **No live hardware in tests.** Tests never touch the real PowerToys. Fixtures are captured from this machine read-only, with personal data trimmed.
- **Process launches.** Any process the plugin must keep alive is started through WMI.

## 6. Actions

| Action | Parameters | Confirmation |
| --- | --- | --- |
| Launch tool | Tool (choice) | The event was set |
| Awake | Mode (off, indefinite, timed, until), minutes, time, keep display on | The file reads back with the new values |
| Theme | Mode (toggle, dark, light) | The registry reads back the expected theme within 3 s |
| Module on/off | Module (dynamic choice from the enable map), state (on, off, toggle). Labelled "Restarts PowerToys". | The map holds and `PowerToys.exe` is running again within 15 s |
| Apply FancyZones layout | Layout (dynamic: custom layouts and templates), monitor (dynamic: monitors plus "All") | `get-active-layout` shows the layout |
| Open FancyZones editor | (none) | The event was set |
| Launch Workspace | Workspace (dynamic) | The launcher started |

## 7. Variables

| Variable | Type | Value |
| --- | --- | --- |
| `powertoys_status` | Text | `ready`, `not_running`, `not_installed`, `elevated`, `restarting` |
| `powertoys_version` | Text | For example `0.101.2362.0` |
| `powertoys_awake_mode` | Text | `off`, `indefinite`, `timed`, `until` |
| `powertoys_awake_display_on` | Boolean | |
| `powertoys_theme` | Text | `dark`, `light` (apps theme) |
| `powertoys_<module>_enabled` | Boolean | One per module in a fixed list. The local id is a slug of the module key. |
| `powertoys_layout_monitor_<n>` | Text | Active layout name per monitor number (phase 2). Fixed at monitors 1 to 4. |

## 8. Error handling

| Situation | Code | Message |
| --- | --- | --- |
| PowerToys not installed | NotConfigured | "PowerToys is not installed." |
| PowerToys not running | NotConnected | "PowerToys isn't running." |
| PowerToys elevated | PermissionDenied | "PowerToys runs as administrator; Macro Deck can't reach it." |
| Module off (event missing) | Unavailable | "<Tool> is turned off in PowerToys." |
| Unknown PowerToys version, event missing | Unavailable | Same as above, with the version logged |
| CLI failure | ProviderError | The CLI's own message |
| Layout, monitor or workspace not found | NotFound | |
| Not confirmed in time | Timeout | |
| Group policy forces a module | ProviderRejected | "<Module> is set by a Windows policy." |

## 9. Distribution

- **Public repository** in `keystonedigitalinc`, under the MIT licence.
- **Workflows.** CI and the Store `release.yml` exist from the first commit, with `cli-version` pinned and no `build-per-platform`.
- **AI declaration.** `generatedAssets: true`, with the icon to come.
- **Store listing.** It states the prerequisites (PowerToys installed and running, not elevated), the foreground-window caveat, and that module on/off restarts PowerToys.

## 10. Testing

### 10.1 Automated

- **Parsers.** The enable map, Awake settings, `workspaces.json` and the CLI output formats, against fixtures captured read-only from this machine.
- **Event table.** Lookup per tool and its result mapping, with a fake opener.
- **Awake writes.** Every unknown field survives the read-modify-write.
- **Module toggle.** The flow with a fake process controller: write, restart, confirm, and the timeout path.
- **Theme.** Force logic: toggle only when the theme differs.
- **Error rows.** Every row of section 8, through fakes.
- **Harness and conformance.** Integration tests through the harness, and the conformance suite.

### 10.2 Live, with the user watching

Each step is reverted afterwards.

1. Fire each tool event once.
2. Set Awake on, then off.
3. Toggle the theme, then toggle it back.
4. Apply one FancyZones layout to one monitor, then restore the original.
5. Turn one module off and on. This restarts PowerToys.
6. Launch a workspace, once the user has saved one.

The first live session also confirms the Crop And Lock event names and the Mouse Highlighter and Crosshairs listeners.

## 11. Project layout

```
PowerToys Controller/
  docs/superpowers/specs/, docs/superpowers/plans/, docs/STORE-SUBMISSION.md
  src/PowerToysController/        plugin
    Locator/ Events/ Awake/ Theme/ Modules/ FancyZones/ Workspaces/ Process/ State/ Actions/ Variables/
  test/PowerToysController.Tests/ (Fixtures/ captured read-only)
  tools/ManualPowerToysCheck/      live checks, run only with the user watching
  .github/workflows/ci.yml, release.yml
```
