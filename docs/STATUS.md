# Status and next steps

Last updated 2026-10-27. v1.0.2 adds live module toggles and the ZoomIt, Always On Top and Mouse Without Borders commands. Read this first when picking the project back up.

## v1.0.2 (2026-10-27, PowerToys 0.102.3001, user watching)

- **Live module toggle.** The runner's Settings and Quick Access pipes accept only Microsoft-signed clients (`requireMicrosoftSignature`), and `PowerToys.DSC.exe set` only writes the file. So `Modules/SettingsUi.cs` opens Settings on the module's page (`PowerToys.exe --open-settings=<page>`), finds the Enable switch by UI Automation id and toggles it. It closes the window if it was not open before. The table of page names and ids comes from `src/settings-ui` at v0.102.3001. If no switch is found, the toggle falls back to the old restart path.
- Live test: all 36 modules were turned on one at a time from all off. 33 applied live in 2 to 5 s with no restart. AltWindowCycle, CmdNotFound and PowerDisplay have no reachable switch and restarted, as designed.
- An earlier report that "toggle does nothing" was Peek: it has no visible effect, and the press worked.
- **New actions:** `zoomit` (8 commands), `always-on-top` (pin, opacity up and down), `mouse-without-borders` (Easy Mouse, reconnect). `launch-tool` gained Cursor Wrap, Advanced Paste, Hosts and Environment Variables (plus admin), Registry Preview, Keyboard Manager editor and Power Display. Every event name was checked against v0.102.3001 and its listener in the module source.
- Every new command was fired live. ZoomIt Snip text put text on the clipboard. Record stops on a second press. Cursor Wrap and Mouse Without Borders confirmed in their own logs.
- 117/117 tests, 0 warnings, `validate --level Publication` clean.
- Not yet tried: hiding the Settings window during a live toggle (it shows for about 2 s).

## Where it stood on 2026-10-24

- `main` at PR #2 (SDK and CLI 3.0.0-beta.18, the Store minimum on 2026-10-24). CI green. 95/95 tests, 0 warnings.
- GitHub release `v1.0.0` (commit `f1c71ad`) built, passed conformance in CI and was accepted by the Platform on 2026-10-21 (run 39678312411). That is the build to submit.
- A local rebuild of the same commit as 1.0.0 passes `validate --level Publication` with 0 errors and 0 warnings.
- Ledger with every ruling and deferred minor: `.superpowers/sdd/2026-10-16-powertoys-controller/progress.md` (git-ignored, local only).

## Retest 2026-10-24 (Macro Deck 3.0.0-beta.19, PowerToys 0.102.2933, user watching)

- The 1.0.0 package loads in beta.19 with no host errors and reports PowerToys `ready`.
- All 15 trigger event names match PowerToys v0.102.2933.0 `shared_constants.h`.
- Fired live: Color Picker, Text Extractor, Screen Ruler (a toggle: a second fire closes it), theme toggle and back, FancyZones editor; layout apply on monitor 2 and restore; Workspaces launcher; module toggle (Peek off and on, PowerToys restarted each time, enabled map identical afterwards).
- Not retested: Awake (module off on this PC).

## Live session results (2026-10-16, user watching, via `tools/ManualPowerToysCheck`)

- Worked: Color Picker, Text Extractor, Screen Ruler, Shortcut Guide, Always On Top, Command Palette, Mouse Highlighter, FancyZones editor; module toggle (Awake on/off with PowerToys restart); layout apply/restore on monitor 2.
- Fixed during the session: Awake ignored the plugin's writes (temp-file swap is invisible to its watcher, now written in place); the Awake action succeeded while the Awake module was off (now Unavailable).
- Added 2026-10-17: cycle mode on "Apply FancyZones layout" (ticked list, wraps, skips deleted layouts; all monitors follows monitor 1). Live tested in Macro Deck 2026-10-17 on monitor 2 (8 presses, Tall -> Short -> Stacked, wraps). An earlier install showed no reaction to presses; reinstalling the same code fixed it, root cause not found.
- Theme toggle live-tested 2026-10-17 after a fix: Light Switch takes about 10 s per toggle and drops a toggle sent before it has finished, so the plugin now waits for its override event. Light Switch and Workspaces modules are now on.
- Workspaces live-tested 2026-10-17: "Workspace 1" launched through the plugin; the PowerToys launcher started and arranged its apps (its Calculator entry failed inside PowerToys: a TileIconify shortcut path).

## Warnings for future sessions

- **Never run `macrodeck-plugin test` locally.** It executes the plugin's real actions on this PC. On 2026-10-16 it fired tools unannounced and set monitor 2 (AOC2402) to the layout "1 - Primary". Conformance runs in CI only. Locally use `macrodeck-plugin build` and `validate --level Publication`.
- Live tests only with the user watching, one step at a time, and announce each one.
- Monitor 2 is back on its original layout "Stacked" (`{7972E5A3-E9A8-4A13-8B77-602EFBB52ED4}`) after the 2026-10-17 cycle test. The user renamed "1 - Primary" to "Tall" in the editor.
- Don't touch the user's Default profile in Macro Deck; make a separate showcase profile for screenshots.

## Before the Store

1. Done: package tested in Macro Deck 3, real icon, screenshots, Portal project, GitHub connection, release v1.0.0.
2. To do (user): in the Creator Portal, Create release from the v1.0.0 build, Add to submission, Submit for Review, with the moderator note from docs/STORE-SUBMISSION.md.
3. To do (user): icon pack `com.keystonedigital.powertoys-icons` from the 58 white icons, exported from Macro Deck.
4. A fix after review ships as GitHub release v1.0.1. Bump nothing in the repository: the tag sets the version.
