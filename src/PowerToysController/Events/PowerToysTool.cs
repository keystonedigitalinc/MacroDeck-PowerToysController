namespace KeystoneDigital.PowerToysController.Events;

internal enum PowerToysTool
{
	ColorPicker,
	TextExtractor,
	ScreenRuler,
	ShortcutGuide,
	AlwaysOnTop,
	CommandPalette,
	CropAndLockReparent,
	CropAndLockThumbnail,
	CropAndLockScreenshot,
	FindMyMouse,
	MouseHighlighter,
	MouseCrosshairs,
	MouseJump,
	LightSwitchToggle,
	FancyZonesEditor,
}

/// <summary>
/// The named events PowerToys' own Command Palette, Quick Access and Run plugin set to trigger a tool,
/// from src/common/interop/shared_constants.h at v0.101.2362.0. They do not depend on the user's
/// hotkeys. The module key is the one in the general settings <c>enabled</c> map.
/// </summary>
internal static class PowerToysEvents
{
	public static string NameOf(PowerToysTool tool) => tool switch
	{
		PowerToysTool.ColorPicker => @"Local\ShowColorPickerEvent-8c46be2a-3e05-4186-b56b-4ae986ef2525",
		PowerToysTool.TextExtractor => @"Local\PowerOCREvent-dc864e06-e1af-4ecc-9078-f98bee745e3a",
		PowerToysTool.ScreenRuler => @"Local\MeasureToolEvent-3d46745f-09b3-4671-a577-236be7abd199",
		PowerToysTool.ShortcutGuide => @"Local\ShortcutGuide-TriggerEvent-d4275ad3-2531-4d19-9252-c0becbd9b496",
		PowerToysTool.AlwaysOnTop => @"Local\AlwaysOnTopPinEvent-892e0aa2-cfa8-4cc4-b196-ddeb32314ce8",
		PowerToysTool.CommandPalette => @"Local\PowerToysCmdPal-ShowEvent-62336fcd-8611-4023-9b30-091a6af4cc5a",
		PowerToysTool.CropAndLockReparent => @"Local\PowerToysCropAndLockReparentEvent-6060860a-76a1-44e8-8d0e-6355785e9c36",
		PowerToysTool.CropAndLockThumbnail => @"Local\PowerToysCropAndLockThumbnailEvent-1637be50-da72-46b2-9220-b32b206b2434",
		PowerToysTool.CropAndLockScreenshot => @"Local\PowerToysCropAndLockScreenshotEvent-ff077ab2-8360-4bd1-864a-637389d35593",
		PowerToysTool.FindMyMouse => @"Local\FindMyMouseTriggerEvent-5a9dc5f4-1c74-4f2f-a66f-1b9b6a2f9b23",
		PowerToysTool.MouseHighlighter => @"Local\MouseHighlighterTriggerEvent-1e3c9c3d-3fdf-4f9a-9a52-31c9b3c3a8f4",
		PowerToysTool.MouseCrosshairs => @"Local\MouseCrosshairsTriggerEvent-0d4c7f92-0a5c-4f5c-b64b-8a2a2f7e0b21",
		PowerToysTool.MouseJump => @"Local\MouseJumpEvent-aa0be051-3396-4976-b7ba-1a9cc7d236a5",
		PowerToysTool.LightSwitchToggle => @"Local\PowerToys-LightSwitch-ToggleEvent-d8dc2f29-8c94-4ca1-8c5f-3e2b1e3c4f5a",
		PowerToysTool.FancyZonesEditor => @"Local\FancyZones-ToggleEditorEvent-1e174338-06a3-472b-874d-073b21c62f14",
		_ => throw new ArgumentOutOfRangeException(nameof(tool)),
	};

	public static string ModuleOf(PowerToysTool tool) => tool switch
	{
		PowerToysTool.ColorPicker => "ColorPicker",
		PowerToysTool.TextExtractor => "TextExtractor",
		PowerToysTool.ScreenRuler => "Measure Tool",
		PowerToysTool.ShortcutGuide => "Shortcut Guide",
		PowerToysTool.AlwaysOnTop => "AlwaysOnTop",
		PowerToysTool.CommandPalette => "CmdPal",
		PowerToysTool.CropAndLockReparent or PowerToysTool.CropAndLockThumbnail or PowerToysTool.CropAndLockScreenshot => "CropAndLock",
		PowerToysTool.FindMyMouse => "FindMyMouse",
		PowerToysTool.MouseHighlighter => "MouseHighlighter",
		PowerToysTool.MouseCrosshairs => "MousePointerCrosshairs",
		PowerToysTool.MouseJump => "MouseJump",
		PowerToysTool.LightSwitchToggle => "LightSwitch",
		PowerToysTool.FancyZonesEditor => "FancyZones",
		_ => throw new ArgumentOutOfRangeException(nameof(tool)),
	};
}
