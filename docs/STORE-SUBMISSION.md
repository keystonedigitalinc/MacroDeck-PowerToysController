# Macro Deck Store submission

Checked against Creator Guidelines **version 6** and the SDK policy on 2026-10-17. Re-read both before every submission, because they change:

- https://api.macro-deck.app/api/v1/public/creator-guidelines (the `X-Creator-Guidelines-Version` header gives the version)
- https://api.macro-deck.app/api/v1/public/dependency-policy/sdk (minimum SDK and CLI at that date: `3.0.0-beta.17`)
- https://api.macro-deck.app/api/v1/public/dependency-policy/blocked-packages (empty at that date)

## Status

| Item | State |
| --- | --- |
| Public GitHub repository, MIT `LICENSE` | Done: https://github.com/keystonedigitalinc/MacroDeck-PowerToysController |
| SDK at or above the Store minimum | Done: `3.0.0-beta.17` |
| Package ID names the developer, not Microsoft | Done: `com.keystonedigital.powertoys` |
| Display name implies no official relationship | Done: "PowerToys Controller" (describes what it controls; section 5 allows a product name used descriptively). "Unofficial" leads both descriptions |
| `publisher.name` equals the Creator Portal owner | Done: `Keystone Digital`, the existing Portal organization |
| AI declaration (section 9) | Done: `generatedAssets: true` (icon and text written with AI help), no runtime AI. **You:** declare the same in the Portal |
| Only needed permissions (section 8) | Done: `process:spawn` only. It covers `FancyZonesCLI.exe`, restarting PowerToys for a module toggle, and the Workspaces launcher |
| Nothing happens without the user (lesson from the Razer review) | Done: every write, restart and launch comes from a button press. The background state check only reads files and the registry |
| What the plugin changes is disclosed (lesson from the Razer review) | Done: README "What the plugin changes", the module action's description, and the Store description |
| CI on every push | Done: `.github/workflows/ci.yml`, Windows runner |
| Store release workflow | Done: `.github/workflows/release.yml`, `cli-version: 3.0.0-beta.17`, no `build-per-platform` |
| Publication validation | Done: 0 errors, 0 warnings (`macrodeck-plugin validate --artifact ... --level Publication`) |
| Conformance | Done in CI: 33 passed, 0 failed, 16 skipped |
| Tested by the publisher (section 6) | Done. Done: all tools, Awake, module toggle, layout apply and cycle, FancyZones editor, inside Macro Deck too. Theme toggle and Workspaces done 2026-10-17. |
| Icon (section 10) | Done: neon toolbox with a zone grid, colour swatches, gear, magnifier and wrench. No Microsoft or Macro Deck logo. Source in `docs/branding/icon-source.png`; the package ships a 512 px copy |
| Screenshots of real use (section 10) | To do |
| Creator Portal project and GitHub connection | To do |
| Release `v1.0.0` | To do |

## Your steps in the Creator Portal

1. In the **Keystone Digital** organization, create a Project of type **Plugin / Integration** with Package ID `com.keystonedigital.powertoys`.
2. Under **Builds**, select **Connect GitHub** and choose `keystonedigitalinc/MacroDeck-PowerToysController`. The Portal should find `.github/workflows/release.yml` on `main`, so PR #1 must be merged first.
3. Fill in the Store listing: the brief description and the description below, 2 or 3 screenshots, and the AI declaration to match the manifest.
4. Tell me. I then publish GitHub release `v1.0.0`, which builds, tests, runs conformance and uploads the build. The tag sets the version, so the manifest's `0.1.0` stays.
5. Under **Builds**, select **Create release**, then **Add to submission**, then **Submit for Review**. Leave the moderator the note below.

Screenshots: real captures only, cropped to Macro Deck. Useful ones are:

- a deck page with tool, Awake, theme and layout buttons showing live variable values
- the Apply FancyZones layout editor in cycle mode with ticked layouts
- the module on/off action editor showing its description

## Brief description

For the Portal's short description field:

```text
Unofficial. Open PowerToys tools without shortcuts, set Awake, switch the theme, turn modules on or off, apply FancyZones layouts and launch Workspaces from Macro Deck.
```

## Store description

Paste this into the Portal as is.

```text
Unofficial. Control Microsoft PowerToys from Macro Deck without its keyboard shortcuts.

WHAT IT DOES
- Open Color Picker, Text Extractor, Screen Ruler, Shortcut Guide, Command Palette, Always On Top, Crop And Lock and the mouse utilities
- Keep the PC awake: indefinitely, for a number of minutes, or until a time, optionally with the screen on
- Switch the Windows theme between light and dark through Light Switch
- Turn PowerToys modules on or off
- Apply a FancyZones layout to one monitor or all monitors, or cycle through the layouts you tick
- Open the FancyZones editor and launch saved Workspaces
- Variables for PowerToys' status and version, Awake, the theme, which modules are on, and the active layout per monitor

REQUIRES
- Windows, Microsoft PowerToys (tested with 0.101) and Macro Deck 3
- PowerToys must not run as administrator. Windows blocks Macro Deck from reaching it, and powertoys_status shows elevated
- A tool works only while its module is on in PowerToys. Light Switch, Awake, Workspaces and the mouse utilities are off by default

WHAT THE PLUGIN CHANGES
Nothing happens until you press a button. The plugin never acts in the background and never turns a module on by itself.
- Awake: writes the mode into Awake's own settings file, which Awake picks up live.
- Module on/off: PowerToys reads its module list only at startup, so the plugin closes PowerToys, changes that one module in PowerToys' settings file and starts PowerToys again. Its open windows close for a few seconds. Apps you opened from PowerToys keep running.
- FancyZones layouts use FancyZonesCLI, and Workspaces use the Workspaces launcher, both shipped with PowerToys.
- The plugin sends nothing over the network.

GOOD TO KNOW
- Always On Top and Crop And Lock act on the focused window. From a phone or the web client, that is the window you are working in.
- A layout button remembers its monitor by name and serial number, so renumbered monitors keep working. An unplugged monitor fails with "not found".
- The tool triggers are PowerToys internals. A PowerToys update can change them, and the plugin then needs an update.

Not affiliated with or endorsed by Microsoft.
```

## Note for the moderator

> This plugin drives Microsoft PowerToys only through interfaces PowerToys itself ships or uses: the named events its Command Palette and Quick Access fire to open tools, Awake's settings file (which Awake watches), FancyZonesCLI.exe, and PowerToys.WorkspacesLauncher.exe. Every change comes from a button press; the background check only reads PowerToys' files and the registry.
>
> The one disruptive action is turning a module on or off. PowerToys reads its module list only at startup and its settings pipe accepts only its own signed Settings app, so the plugin ends the PowerToys processes in the user's session (never their process trees), edits that one entry of settings.json and starts PowerToys again. The action's description, the README and this listing say so.
>
> PowerToys and the Workspaces launcher are started with Win32_Process.Create so that they don't belong to the plugin's job object. Otherwise Macro Deck would close PowerToys and the user's Workspace apps whenever it restarts or updates the plugin. The manifest declares process:spawn for this and for FancyZonesCLI. The plugin makes no network connections.

## Risks that remain

- **Process restart and WMI start.** The same pattern is in the Razer plugin, which is under review for v1.0.1. If the moderator objects there, change both plugins the same way.
- **Trigger events.** They are PowerToys internals. A PowerToys update could rename one. The table lives in `Events/PowerToysTool.cs`.
- **AI-assisted code (section 6).** It is allowed. You, as publisher, must be able to explain, debug and maintain the code. `docs/superpowers/` holds the design and plan, and every part has tests.

## Release workflow lessons (from the Razer plugin)

- **Single platform:** do not set `build-per-platform: true` while the manifest declares only one platform.
- **CLI version:** pin `cli-version` to the Platform's minimum CLI. The workflow's default CLI is older, and the Platform refuses it.
- **Next version:** for a fix after review, bump nothing in the repository. Publish the next GitHub release, because the tag sets the version.
