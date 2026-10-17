# Status and next steps

Last updated 2026-10-16. Read this first when picking the project back up.

## Where it stands

- Branch `feature/md3-plugin`, draft PR #1 on `keystonedigitalinc/MacroDeck-PowerToysController` (public). CI green.
- Plan fully executed, final review done, all critical and important findings fixed. 95/95 tests, 0 warnings.
- Ledger with every ruling and deferred minor: `.superpowers/sdd/2026-10-16-powertoys-controller/progress.md` (git-ignored, local only).
- Package built: `D:\com.keystonedigital.powertoys-0.1.0-win-x64.macroDeckPlugin` (also in `artifacts/`).

## Live session results (2026-10-16, user watching, via `tools/ManualPowerToysCheck`)

- Worked: Color Picker, Text Extractor, Screen Ruler, Shortcut Guide, Always On Top, Command Palette, Mouse Highlighter, FancyZones editor; module toggle (Awake on/off with PowerToys restart); layout apply/restore on monitor 2.
- Fixed during the session: Awake ignored the plugin's writes (temp-file swap is invisible to its watcher, now written in place); the Awake action succeeded while the Awake module was off (now Unavailable).
- Added 2026-10-17: cycle mode on "Apply FancyZones layout" (ticked list, wraps, skips deleted layouts; all monitors follows monitor 1). Live tested in Macro Deck 2026-10-17 on monitor 2 (8 presses, Tall -> Short -> Stacked, wraps). An earlier install showed no reaction to presses; reinstalling the same code fixed it, root cause not found.
- Not tested: theme toggle (needs Light Switch enabled), Workspaces (module off, none saved), and the plugin inside Macro Deck itself.

## Warnings for future sessions

- **Never run `macrodeck-plugin test` locally.** It executes the plugin's real actions on this PC. On 2026-10-16 it fired tools unannounced and set monitor 2 (AOC2402) to the layout "1 - Primary". Conformance runs in CI only. Locally use `macrodeck-plugin build` and `validate --level Publication`.
- Live tests only with the user watching, one step at a time, and announce each one.
- Monitor 2 is back on its original layout "Stacked" (`{7972E5A3-E9A8-4A13-8B77-602EFBB52ED4}`) after the 2026-10-17 cycle test. The user renamed "1 - Primary" to "Tall" in the editor.
- Don't touch the user's Default profile in Macro Deck; make a separate showcase profile for screenshots.

## Before the Store

1. User installs the package in Macro Deck 3 and tests real buttons (Color Picker, a layout, Awake with the module enabled).
2. Real icon: `src/PowerToysController/Assets/icon.png` is still the Razer placeholder. User generates one (prompt given in session: navy background, toolbox/wrench with a 3x3 button grid, no text, no Microsoft logos).
3. Bump `manifest.json` version 0.1.0 to 1.0.0.
4. Showcase profile and screenshots.
5. Merge PR #1, create the Creator Portal project, connect GitHub, release v1.0.0. Follow `docs/STORE-SUBMISSION.md` and the Razer project's lessons (CLI 3.0.0-beta.17, no `build-per-platform`).
