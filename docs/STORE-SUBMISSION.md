# Macro Deck Store submission

Follow the same path the Razer plugin took: `keystonedigitalinc/MacroDeck-RazerController`, `docs/STORE-SUBMISSION.md`. Re-read the Creator Guidelines and the SDK policy before each submission.

## Status

| Item | State |
| --- | --- |
| Public repo and MIT licence | Done: https://github.com/keystonedigitalinc/MacroDeck-PowerToysController |
| SDK at or above the Store minimum | Done: 3.0.0-beta.17 |
| ID and name | Done: `com.keystonedigital.powertoys`, "PowerToys Control (unofficial)". No Microsoft branding. |
| Publisher | Keystone Digital (existing Portal organization) |
| AI declaration | `generatedAssets: true`. The icon is AI-generated (neon toolbox, 512x512). |
| Permissions | None declared |
| Publication validation | 0 errors |
| Conformance | 33 passed, 0 failed, 16 skipped |
| Release workflow | `cli-version: 3.0.0-beta.17`, no `build-per-platform` |
| Live tests by the publisher | Pending: the session with the user |
| Icon | Done 2026-10-17 |
| Screenshots, Portal project | To do (the user) |

## Store description (draft)

> Control Microsoft PowerToys from Macro Deck without its keyboard shortcuts.
>
> - Launch Color Picker, Text Extractor, Screen Ruler, Shortcut Guide, Command Palette, Always On Top, Crop And Lock and the mouse utilities.
> - Keep the PC awake (indefinitely, for a while, or until a time).
> - Switch between light and dark theme.
> - Turn PowerToys modules on or off.
> - Apply FancyZones layouts to a chosen monitor.
> - Launch saved Workspaces.
>
> Variables show PowerToys' status, Awake, the theme, which modules are on, and the active layout per monitor.
>
> **Requires:** Windows, Microsoft PowerToys (tested with 0.101), Macro Deck 3. PowerToys must not run as administrator.
>
> **Good to know:**
> - A tool works only while its module is on in PowerToys.
> - Always On Top and Crop And Lock act on the focused window.
> - Turning a module on or off restarts PowerToys for a few seconds.
>
> Not affiliated with or endorsed by Microsoft.

## Risks

- **Module toggling.** It edits PowerToys' settings file and restarts PowerToys. This is disclosed in the action description, the README and the listing.
- **Trigger events.** They are PowerToys internals, although PowerToys' own Command Palette and Quick Access rely on them. A PowerToys update could rename one. The table lives in `Events/PowerToysTool.cs`.
