# PowerToys Control (Macro Deck 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A sideloadable Macro Deck 3 plugin that triggers PowerToys tools by their named events and sets Awake, theme and module on/off (phase 1), plus FancyZones layouts and Workspaces (phase 2), with state variables.

**Architecture:** One out-of-process MD3 plugin, built like the Razer Controller at `E:\Projects\Macro Deck Applications\Razer Controller`.
- Small single-purpose controls (events, Awake file, theme, module map, FancyZones, Workspaces) sit behind fakeable interfaces.
- A hosted `StateService` caches a snapshot every 2 s.
- `PowerToysIntegration` exposes the actions and variables.

**Tech Stack:** .NET 10, MacroDeck.* 3.0.0-beta.17, System.Management 10.0.13, NUnit 4, NUnit3TestAdapter 5.0.0.

**Spec:** `docs/superpowers/specs/2026-10-16-powertoys-controller-design.md`

## Spec amendments (decided while planning)

1. **FancyZones state comes from PowerToys' own JSON files, not CLI output.** `FancyZonesCLI` prints human text with no JSON mode, so parsing it would be fragile.
   - **Monitors** come from `editor-parameters.json` (`monitors[]`: `monitor`, `monitor-number`, `monitor-serial-number`, `virtual-desktop`).
   - **Applied layouts** come from `applied-layouts.json`, matched to monitors the CLI's way: same `monitor`, same `virtual-desktop` (or the default all-zeros GUID), and the same serial when both have one.
   - **Layout names** come from `custom-layouts.json` and `layout-templates.json`.
   - **Before reading monitors,** post the FancyZones save message, then wait up to 500 ms for the file timestamp to change. This mirrors `EditorParametersRefresh`. The message is `RegisterWindowMessage("{d8f9c0e3-5d77-4e83-8a4f-7c704c2bfb4a}")` broadcast with `PostMessage(HWND_BROADCAST)`.
   - **The CLI is used only to apply:** `FancyZonesCLI.exe set-layout <uuid|template> --monitor <n>` or `--all`, where `<n>` is the 1-based index into `editor-parameters.monitors`.
2. **Module toggle when PowerToys is not running:** write the map and do not start PowerToys. A closed PowerToys stays closed. The change applies on its next start.

## Global Constraints

- **Package versions.** `MacroDeckSdkVersion` is pinned to `3.0.0-beta.17`. NUnit3TestAdapter stays at `5.0.0`.
- **Target framework.** `net10.0`, plus `[assembly: SupportedOSPlatform("windows")]` in both the plugin and the test assembly. Never `net10.0-windows`.
- **Identity.** Plugin id `com.keystonedigital.powertoys`. Name `PowerToys Control (unofficial)`. Publisher `Keystone Digital`. License `MIT`. Repository `https://github.com/keystonedigitalinc/MacroDeck-PowerToysController`.
- **Namespace and assembly.** Root namespace `KeystoneDigital.PowerToysController`, AssemblyName `PowerToysController`.
- **Entrypoint and permissions.** `win-x64`, `runtimes/win-x64/PowerToysController.dll`, FrameworkDependent 10.0. Permissions: none. AI declaration: `{interaction:false, generatedContent:false, generatedAssets:true}`.
- **Release workflow.** `cli-version: 3.0.0-beta.17`, and no `build-per-platform`.
- **Paths.** The data root is `%LOCALAPPDATA%\Microsoft\PowerToys`. The install root comes from the uninstall registry `InstallLocation` for `DisplayName` matching `^PowerToys`, or falls back to `%LOCALAPPDATA%\PowerToys`.
- **Named events.** Open with `EventWaitHandle.TryOpenExisting`, never with the create-or-open constructor. Never cache handles across presses.
- **Process launches.** Any process the plugin starts and must outlive Macro Deck (`PowerToys.exe`, `WorkspacesLauncher.exe`) starts through WMI `Win32_Process.Create`. The CLI runs as a normal child process and is waited for.
- **Code style.** Tabs. No em dashes. Every user-facing string lives in `Localization/Strings.resx`. Async rules as in the Razer plugin. Every commit carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` trailer.
- **No live PowerToys in tests.** No automated test touches the real PowerToys, registry or events. Live checks run only from `tools/ManualPowerToysCheck`, with the user watching.

## Review Focus

1. **PowerToys restarted or updated while the plugin runs.** The next press must work without a plugin restart. Events are opened per press, never cached. Test (Task 3): `Trigger_opens_the_event_on_every_press`.
2. **Awake's `settings.json` being written by PowerToys at the same moment.** A sharing violation or partial JSON must be retried, not crash or wipe fields. Test (Task 4): `Set_retries_when_the_file_is_locked_or_half_written`.
3. **A monitor unplugged or renumbered since the button was saved.** Apply must fail with NotFound, never apply to another monitor. Test (Task 9): `Apply_fails_for_a_monitor_that_is_gone`.
4. **PowerToys running elevated.** Every event-based action must report PermissionDenied, and status must be `elevated`, not `ready`. Test (Task 3): `Access_denied_maps_to_Denied`. Test (Task 7): `Status_is_elevated_when_the_runner_is_elevated`.
5. **Two quick presses of Awake, Theme toggle or Module toggle.** Each must act once, in order, never on stale state. Tests (Tasks 5 and 6): `Two_quick_theme_toggles_end_where_they_started`, `Two_quick_module_toggles_end_where_they_started`.

---

### Task 1: Scaffold, CI and release workflow

**Files:**
- Create: `Directory.Build.props`, `Directory.Packages.props`, `NuGet.config`, `.gitignore`, `LICENSE`, `PowerToysController.slnx`, `.github/workflows/ci.yml`, `.github/workflows/release.yml`
- Create: `src/PowerToysController/` with `PowerToysController.csproj`, `Program.cs`, `AssemblyInfo.cs`, `manifest.json`, `macrodeck-build.json`, `Properties/launchSettings.json`, `Localization/Strings.resx` (key `Integration.Name`), `PowerToysIntegration.cs` (empty actions), `Services.cs` (`AddPowerToysServices()`), and a placeholder `Assets/icon.png` (the Razer icon until the user supplies one)
- Create: `test/PowerToysController.Tests/` with the csproj, `AssemblyInfo.cs` and `IntegrationTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class PowerToysIntegration : IPluginIntegration` with an internal constructor and `public static PowerToysIntegration Create(IServiceProvider)`
  - `internal static class Services` with `AddPowerToysServices(this IServiceCollection)` and a test overload that takes pre-built fakes, extended in later tasks
  - `IntegrationTests.CreateHarness(...)`, reused by later tasks

- [ ] **Step 1: Copy the Razer project's files** (`Directory.*.props`, `NuGet.config`, `.gitignore`, `LICENSE`, CI and release workflows, csproj shapes) and rename them for this project.
  - Remove HidSharp.
  - Keep System.Management.
  - Keep `AllowUnsafeBlocks`, which LibraryImport needs for `RegisterWindowMessage` and `PostMessage`.
  - Set `release.yml` to `source: src/PowerToysController`.
- [ ] **Step 2: Write `IntegrationTests`:**
  - `The_plugin_builds_and_initializes`
  - `The_catalog_is_scoped_to_the_plugin_id`, asserting `"plugin:com.keystonedigital.powertoys"`
  - `Every_resx_key_resolves`
- [ ] **Step 3: Build and test.**

  Run: `dotnet test`

  Expected: 3 pass, 0 warnings.
- [ ] **Step 4: Create the GitHub repository, push and confirm CI.**

  Run:
  ```
  gh repo create keystonedigitalinc/MacroDeck-PowerToysController --public --source . --remote origin
  git push -u origin main
  ```

  Expected: CI is green on `windows-latest`.
- [ ] **Step 5: Commit** with `feat: scaffold PowerToys Control MD3 plugin`, then push.

---

### Task 2: Locating PowerToys and reading the module map

**Files:**
- Create: `src/PowerToysController/Locator/PowerToysPaths.cs`, `PowerToysLocator.cs`, `EnableMap.cs`
- Create: `test/.../Fixtures/general-settings.json`, captured read-only from `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json`. Keep only `enabled`, `run_elevated` and `powertoys_version`.
- Test: `test/.../Locator/LocatorTests.cs`

**Interfaces:**
- Produces:
  - `record PowerToysPaths(string InstallRoot, string DataRoot)`, with properties `GeneralSettings`, `AwakeSettings`, `FancyZonesDir`, `WorkspacesFile`, `RunnerExe`, `FancyZonesCli`, `WorkspacesLauncher`
  - `interface IPowerToysProcesses { bool IsRunnerRunning(); bool IsRunnerElevated(); IReadOnlyList<int> RunnerProcessIds(); }`, implemented by `WindowsPowerToysProcesses` (elevation through `OpenProcessToken` and `TokenElevation`)
  - `static class EnableMap` with `IReadOnlyDictionary<string,bool> Read(string generalSettingsJson)` and `string WithModule(string json, string module, bool enabled)` (keeps every other field)
  - `class PowerToysLocator(IPowerToysProcesses, Func<PowerToysPaths?>)` with `PowerToysPaths? Paths`, `string? Version` and `bool IsPolicyForced(string module)`. That last method reads `HKLM\SOFTWARE\Policies\PowerToys`, value `ConfigureEnabledUtility<Module>`.

- [ ] **Step 1: Write the tests:**
  - `Reads_every_module_from_the_fixture`: `ColorPicker` true, `FancyZones` true, `Awake` present
  - `WithModule_flips_one_key_and_keeps_the_rest`: the rest of the JSON is deep-equal apart from that key
  - `Malformed_json_reads_as_empty`: no throw
- [ ] **Step 2: Run them.** They fail.
- [ ] **Step 3: Implement** with `System.Text.Json.Nodes` and the order-preserving `JsonObject`.
- [ ] **Step 4: Run them.** They pass.
- [ ] **Step 5: Commit** with `feat: locate PowerToys and read its module map`.

---

### Task 3: Event trigger

**Files:**
- Create: `src/PowerToysController/Events/PowerToysTool.cs`, `EventTrigger.cs`
- Test: `test/.../Events/EventTriggerTests.cs`

**Interfaces:**
- Produces:
  - `enum PowerToysTool { ColorPicker, TextExtractor, ScreenRuler, ShortcutGuide, AlwaysOnTop, CommandPalette, CropAndLockReparent, CropAndLockThumbnail, CropAndLockScreenshot, FindMyMouse, MouseHighlighter, MouseCrosshairs, MouseJump, LightSwitchToggle, FancyZonesEditor }`
  - `static class PowerToysEvents` with `string NameOf(PowerToysTool)` (the spec's table 4.1, verbatim) and `string ModuleOf(PowerToysTool)` (the enable-map key)
  - `enum TriggerResult { Triggered, NotRunning, Denied }`
  - `interface INamedEventOpener { TriggerResult TrySet(string name); }`, implemented by `WindowsNamedEventOpener` (`TryOpenExisting`, `UnauthorizedAccessException` gives Denied)
  - `class EventTrigger(INamedEventOpener)` with `TriggerResult Trigger(PowerToysTool tool)`

- [ ] **Step 1: Write the tests:**
  - `Every_tool_has_a_Local_event_name`: each name starts with `Local\`
  - `Color_picker_uses_the_verified_name`: equals `Local\ShowColorPickerEvent-8c46be2a-3e05-4186-b56b-4ae986ef2525`
  - `Trigger_opens_the_event_on_every_press`: two triggers call the fake twice
  - `Missing_event_maps_to_NotRunning`
  - `Access_denied_maps_to_Denied`
- [ ] **Step 2: Run them.** They fail. **Step 3:** Implement. **Step 4:** Run them; they pass.
- [ ] **Step 5: Commit** with `feat: trigger PowerToys tools by their named events`.

---

### Task 4: Awake

**Files:**
- Create: `src/PowerToysController/Awake/AwakeState.cs`, `AwakeControl.cs`
- Create: `test/.../Fixtures/awake-settings.json`, captured read-only
- Test: `test/.../Awake/AwakeTests.cs`

**Interfaces:**
- Produces:
  - `enum AwakeMode { Off = 0, Indefinite = 1, Timed = 2, Until = 3 }`
  - `record AwakeState(AwakeMode Mode, bool KeepDisplayOn, int Hours, int Minutes, DateTimeOffset? Until)`
  - `interface IFileStore { string? Read(string path); void Write(string path, string content); }`, implemented by `DiskFileStore` with an atomic temp-file-plus-replace write
  - `class AwakeControl(IFileStore, PowerToysPaths, TimeProvider)` with:
    - `AwakeState? Read()`
    - `Task<bool> SetAsync(AwakeMode mode, bool keepDisplayOn, int minutes, DateTimeOffset? until, CancellationToken ct)`, which returns true once a read-back matches
  - Same-target presses are serialized with a `SemaphoreSlim`.

- [ ] **Step 1: Write the tests:**
  - `Timed_30_minutes_writes_mode_2_hours_0_minutes_30`
  - `Indefinite_keeps_unknown_fields`: a `customTrayTimes` field survives
  - `Until_writes_an_iso_offset_timestamp`
  - `Set_retries_when_the_file_is_locked_or_half_written`: the fake throws `IOException` once, then returns partial JSON once, then succeeds; the result is true
  - `Read_of_missing_file_is_null`
- [ ] **Step 2: Run them.** They fail. **Step 3:** Implement, with retries every 50 ms for up to 1 s. **Step 4:** Run them; they pass.
- [ ] **Step 5: Commit** with `feat: Awake control through its watched settings file`.

---

### Task 5: Theme

**Files:**
- Create: `src/PowerToysController/Theme/ThemeControl.cs`
- Test: `test/.../Theme/ThemeTests.cs`

**Interfaces:**
- Consumes: `EventTrigger.Trigger(PowerToysTool.LightSwitchToggle)`
- Produces:
  - `enum Theme { Light, Dark }`
  - `interface IThemeReader { Theme? Current(); }`, implemented by `RegistryThemeReader` (HKCU `...\Themes\Personalize\AppsUseLightTheme`)
  - `enum ThemeOutcome { Applied, NoChange, NotRunning, Denied, Timeout }`
  - `class ThemeControl(EventTrigger, IThemeReader, TimeProvider)` with `Task<ThemeOutcome> ApplyAsync(string mode /* toggle|dark|light */, CancellationToken ct)`. It confirms the flip within 3 s, polling every 100 ms, and serializes presses.

- [ ] **Step 1: Write the tests:**
  - `Dark_when_already_dark_is_NoChange_and_fires_nothing`
  - `Light_when_dark_fires_one_toggle_and_confirms`
  - `Two_quick_theme_toggles_end_where_they_started`
  - `No_flip_within_3s_is_Timeout`
- [ ] **Step 2: Run them.** They fail. **Step 3:** Implement. **Step 4:** Run them; they pass.
- [ ] **Step 5: Commit** with `feat: theme toggle and force through Light Switch`.

---

### Task 6: Module toggle

**Files:**
- Create: `src/PowerToysController/Modules/ModuleToggle.cs`, `RunnerControl.cs`
- Test: `test/.../Modules/ModuleToggleTests.cs`

**Interfaces:**
- Consumes: `EnableMap`, `IFileStore`, `PowerToysLocator`, `IPowerToysProcesses`
- Produces:
  - `interface IRunnerControl { Task StopAsync(CancellationToken ct); void Start(string runnerExe); }`
    - `WindowsRunnerControl.StopAsync` kills `PowerToys` and every `PowerToys.*` process and waits up to 5 s.
    - `Start` launches through WMI `Win32_Process.Create`.
  - `enum ModuleOutcome { Applied, NoChange, PolicyForced, NotInstalled, Timeout }`
  - `class ModuleToggle(...)` with `Task<ModuleOutcome> SetAsync(string module, string state /* on|off|toggle */, CancellationToken ct)`. If the runner was running, it restarts it and confirms it is running again within 15 s. If not, it writes only (amendment 2). It serializes presses.

- [ ] **Step 1: Write the tests:**
  - `Off_writes_the_map_and_restarts_a_running_PowerToys`
  - `Toggle_while_PowerToys_is_closed_writes_only`
  - `Already_in_state_is_NoChange_without_restart`
  - `Policy_forced_module_is_rejected_without_writing`
  - `Runner_not_back_in_15s_is_Timeout`
  - `Two_quick_module_toggles_end_where_they_started`
- [ ] **Step 2: Run them.** They fail. **Step 3:** Implement. **Step 4:** Run them; they pass.
- [ ] **Step 5: Commit** with `feat: module on/off by enable map and PowerToys restart`.

---

### Task 7: State service and variables

**Files:**
- Create: `src/PowerToysController/State/PowerToysSnapshot.cs`, `StateService.cs`
- Create: `src/PowerToysController/Variables/PowerToysVariables.cs`
- Modify: `PowerToysIntegration.cs` (implements `IVariableProvider`), `Services.cs`, `Strings.resx`
- Test: `test/.../State/StateTests.cs`, `test/.../VariablesTests.cs`

**Interfaces:**
- Produces:
  - `record PowerToysSnapshot(string Status, string? Version, AwakeState? Awake, Theme? Theme, IReadOnlyDictionary<string,bool> Enabled, IReadOnlyList<MonitorLayout> Monitors /* empty until Task 9 */)`
  - `StateService : BackgroundService` with `PowerToysSnapshot Current` and `Task RefreshAsync(CancellationToken)`, refreshing every 2 s
  - Status values: `ready`, `not_running`, `not_installed`, `elevated`, `restarting`. `ModuleToggle` sets `restarting` while the runner is down.
  - Variables (spec section 7), with local ids `status`, `version`, `awake-mode`, `awake-display-on` and `theme`.
  - Modules: one variable per key in this fixed list. The name is `powertoys_<slug>_enabled`, and the slug is lowercase with non-alphanumerics replaced by `_`.

    `AlwaysOnTop, Awake, CmdPal, ColorPicker, CropAndLock, FancyZones, FindMyMouse, LightSwitch, Measure Tool, MouseHighlighter, MouseJump, MousePointerCrosshairs, Peek, Shortcut Guide, TextExtractor, Workspaces`
  - Monitors: `layout-monitor-1` to `layout-monitor-4`, filled in Task 9.

- [ ] **Step 1: Write the tests:**
  - `Status_is_not_installed_without_paths`
  - `Status_is_not_running_when_runner_absent`
  - `Status_is_elevated_when_the_runner_is_elevated`
  - `Variables_read_live_values`: through the harness, with the tagged-union value helper copied from the Razer tests
  - `Module_slug_for_Measure_Tool_is_measure_tool`
- [ ] **Step 2: Run them.** They fail. **Step 3:** Implement. **Step 4:** Run them; they pass.
- [ ] **Step 5: Commit** with `feat: PowerToys state snapshot and variables`.

---

### Task 8: Phase 1 actions

**Files:**
- Create: `src/PowerToysController/Actions/ParameterReader.cs` (copy the Razer version, including the range fix), `LaunchToolAction.cs`, `AwakeAction.cs`, `ThemeAction.cs`, `ModuleAction.cs`, `Results.cs`
- Modify: `PowerToysIntegration.cs`, `Strings.resx`
- Test: `test/.../Actions/Phase1ActionTests.cs`

**Interfaces:**
- Consumes: Tasks 3 to 7.
- Produces these actions:

  | Id | Parameters |
  | --- | --- |
  | `launch-tool` | `tool`: Choice, every `PowerToysTool` except `LightSwitchToggle` and `FancyZonesEditor` |
  | `awake` | `mode`: Choice off/indefinite/timed/until; `minutes`: Number 1–1440, default 30, OnlyWhen timed; `until`: Text `HH:mm`, OnlyWhen until; `display-on`: Toggle |
  | `theme` | `mode`: Choice toggle/dark/light |
  | `module` | `module`: DynamicChoice of the snapshot's enabled keys; `state`: Choice on/off/toggle. The description says it restarts PowerToys. |

  `Results` maps outcomes to `ActionResult` codes as in spec section 8.

  Before triggering, the launch action checks status (not installed, not running, elevated) and whether the tool's module is enabled. A disabled module gives `Unavailable`, "`<Tool>` is turned off in PowerToys".

- [ ] **Step 1: Write the tests** (all through the harness):
  - `Launch_color_picker_sets_its_event`
  - `Launch_with_module_off_is_Unavailable_and_sets_nothing`
  - `Launch_when_elevated_is_PermissionDenied`
  - `Awake_timed_45_confirms`
  - `Theme_dark_when_light_succeeds`
  - `Module_off_reports_restart_outcome`
  - `Every_action_id_is_a_valid_local_id`
- [ ] **Step 2: Run them.** They fail. **Step 3:** Implement. **Step 4:** Run them; they pass. Then run `dotnet test`: all pass, 0 warnings.
- [ ] **Step 5: Commit** with `feat: phase 1 actions (tools, Awake, theme, modules)`, then push. CI is green.

---

### Task 9: FancyZones (phase 2)

**Files:**
- Create: `src/PowerToysController/FancyZones/FancyZonesData.cs`, `MonitorLayout.cs`, `FancyZonesControl.cs`, `Process/ProcessRunner.cs`, `Native/BroadcastMessages.cs`
- Create: `src/PowerToysController/Actions/ApplyLayoutAction.cs`, `OpenZoneEditorAction.cs`
- Create fixtures (read-only copies): `editor-parameters.json`, `applied-layouts.json`, `custom-layouts.json`, `layout-templates.json`
- Test: `test/.../FancyZones/FancyZonesTests.cs`

**Interfaces:**
- Produces:
  - `record LayoutInfo(string Id /* uuid or template type */, string Name, bool IsTemplate)`
  - `record MonitorLayout(int Index /* 1-based, CLI order */, string Monitor, string? Serial, string VirtualDesktop, string? ActiveLayoutName)`
  - `static class FancyZonesData`:
    - `IReadOnlyList<LayoutInfo> Layouts(string customJson, string templatesJson)`
    - `IReadOnlyList<MonitorLayout> Monitors(string editorParamsJson, string appliedJson, IReadOnlyList<LayoutInfo> layouts)`, using amendment 1's matching
  - `interface IProcessRunner { Task<(int Exit, string Output)> RunAsync(string exe, string args, TimeSpan timeout, CancellationToken ct); }`
  - `interface IFancyZonesSignals { void RequestEditorParametersSave(); }`, implemented with `RegisterWindowMessage` and `PostMessage(HWND_BROADCAST)` through LibraryImport
  - `class FancyZonesControl(...)`:
    - `Task<IReadOnlyList<MonitorLayout>> MonitorsAsync(CancellationToken ct)`, which saves, then waits up to 500 ms
    - `IReadOnlyList<LayoutInfo> Layouts()`
    - `Task<LayoutOutcome> ApplyAsync(string layoutId, int? monitorIndex /* null = all */, CancellationToken ct)`, which runs `set-layout` and then confirms through `MonitorsAsync`
  - `enum LayoutOutcome { Applied, NotFound, NotRunning, CliFailed, Timeout }`
  - Action `apply-layout` (`layout`: DynamicChoice; `monitor`: DynamicChoice of `all` plus each `Index: Monitor` label, value = `Monitor` name plus serial, so it survives renumbering)
  - Action `open-zone-editor`
  - `StateService` fills `Monitors`, and the variables `layout-monitor-1` to `layout-monitor-4` go live.

- [ ] **Step 1: Write the tests:**
  - `Reads_four_custom_layouts_with_names`: includes `1 - Primary`
  - `Monitor_matching_uses_name_desktop_and_serial`: the fixture gives AOC2702 at index 1 with its active layout name
  - `Default_virtual_desktop_record_matches_any_desktop`
  - `Apply_runs_set_layout_with_the_monitor_index`: the fake runner sees the args `set-layout {E2D55CC1-5F19-4C36-BB72-12C519ACAEA3} --monitor 1`
  - `Apply_fails_for_a_monitor_that_is_gone`: NotFound, and the CLI is not run
  - `Cli_nonzero_exit_is_CliFailed_with_its_output`
- [ ] **Step 2: Run them.** They fail. **Step 3:** Implement. **Step 4:** Run them; they pass.
- [ ] **Step 5: Commit** with `feat: FancyZones layouts per monitor`, then push. CI is green.

---

### Task 10: Workspaces (phase 2)

**Files:**
- Create: `src/PowerToysController/Workspaces/WorkspacesControl.cs`, `Actions/LaunchWorkspaceAction.cs`
- Test: `test/.../Workspaces/WorkspacesTests.cs`, with an inline JSON fixture (the user has none saved)

**Interfaces:**
- Produces:
  - `record WorkspaceInfo(string Id, string Name)`
  - `class WorkspacesControl(IFileStore, PowerToysPaths, IProcessStarter)`:
    - `IReadOnlyList<WorkspaceInfo> List()`
    - `bool Launch(string id)`, which starts `"<WorkspacesLauncher>" <id> 1` through WMI
  - `interface IProcessStarter { bool Start(string commandLine, string workingDirectory); }`, implemented by `WmiProcessStarter`, which `RunnerControl.Start` also uses
  - Action `launch-workspace` (`workspace`: DynamicChoice)

- [ ] **Step 1: Write the tests:**
  - `Lists_workspaces_by_name`
  - `Missing_file_is_empty_list`
  - `Launch_quotes_the_launcher_and_passes_id_and_1`
  - `Launch_unknown_id_is_NotFound`
- [ ] **Step 2: Run them.** They fail. **Step 3:** Implement. **Step 4:** Run them; they pass.
- [ ] **Step 5: Commit** with `feat: launch PowerToys Workspaces`.

---

### Task 11: Package, manual check tool and live session

**Files:**
- Create: `tools/ManualPowerToysCheck/` (console app). Commands:
  - `--state`: read-only, prints the snapshot
  - `--fire <tool>`
  - `--awake-roundtrip`
  - `--theme-roundtrip`
  - `--layout-roundtrip <monitor>`
  - `--module-roundtrip <module>`
- Create: `README.md` (setup, the foreground-window caveat, elevation, module restart, troubleshooting by `powertoys_status`) and `docs/STORE-SUBMISSION.md`, following the Razer version.

- [ ] **Step 1: Validate and package.**

  Run: `macrodeck-plugin build --source src/PowerToysController --rid win-x64 --output ./artifacts`, then `validate --level Publication`, then `macrodeck-plugin test --artifact ...`

  Expected: 0 errors; conformance with no failed Required check.
- [ ] **Step 2: Read the live state** (read-only, no user needed).

  Run: `dotnet run --project tools/ManualPowerToysCheck -- --state`

  Expected: status `ready`, version `0.101.2362.0`, both monitors with layout names, Awake off.
- [ ] **Step 3: Live session with the user watching.** Ask first, then run each command and confirm with the user what appeared:
  1. `--fire` for each tool
  2. `--awake-roundtrip`
  3. `--theme-roundtrip`
  4. `--layout-roundtrip 2` (restores the original)
  5. `--module-roundtrip MouseJump` (restarts PowerToys)

  Note any event that reports NotRunning while its module is on. This confirms the Crop And Lock names and the mouse-utility listeners.
- [ ] **Step 4: The user installs the package in MD3** and builds a test page. Fix any issue through superpowers:systematic-debugging.
- [ ] **Step 5: Commit and push** with `docs: README, Store plan and live check tool`. CI is green.
