using System.Diagnostics;
using System.Runtime.InteropServices;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Processes;

namespace KeystoneDigital.PowerToysController.Modules;

internal enum SettingsUiOutcome
{
	Applied,
	NoChange,
	Unsupported,
	Failed,
}

/// <summary>
/// Flips a module's Enable switch in PowerToys' own Settings app. The runner accepts live module changes
/// only from its signed Settings app, so driving that app is the one way to change a module without a restart.
/// </summary>
internal interface ISettingsUi
{
	Task<SettingsUiOutcome> SetAsync(PowerToysPaths location, string module, bool target, CancellationToken ct);
}

/// <summary>
/// The Settings page (the runner's <c>--open-settings=</c> name) and the automation id of the Enable switch,
/// or of the card that holds it, for each key of the <c>enabled</c> map. From src/settings-ui at v0.102.3001.
/// Modules without an entry have no reachable switch and fall back to a restart.
/// </summary>
internal static class SettingsPages
{
	public static readonly IReadOnlyDictionary<string, (string Page, string AutomationId)> Switches =
		new Dictionary<string, (string, string)>(StringComparer.Ordinal)
		{
			["AdvancedPaste"] = ("AdvancedPaste", "AdvancedPasteEnableToggleControlHeaderText"),
			["AlwaysOnTop"] = ("AlwaysOnTop", "AlwaysOnTopEnableToggleControlHeaderText"),
			["AutoHideCursor"] = ("MouseUtils", "MouseUtilsEnableAutoHideCursor"),
			["Awake"] = ("Awake", "AwakeEnableSettingsCard"),
			["CmdPal"] = ("CmdPal", "CmdPalEnableCmdPal"),
			["ColorPicker"] = ("ColorPicker", "Toggle_ColorPicker"),
			["CropAndLock"] = ("CropAndLock", "CropAndLockEnableToggleControlHeaderText"),
			["CursorWrap"] = ("MouseUtils", "MouseUtilsEnableCursorWrap"),
			["EnvironmentVariables"] = ("EnvironmentVariables", "EnvironmentVariablesEnableToggleControlHeaderText"),
			["FancyZones"] = ("FancyZones", "FancyZonesEnableToggleControlHeaderText"),
			["File Locksmith"] = ("FileLocksmith", "FileLocksmithEnableFileLocksmith"),
			["FindMyMouse"] = ("MouseUtils", "MouseUtilsEnableFindMyMouse"),
			["GrabAndMove"] = ("GrabAndMove", "GrabAndMoveEnableToggleControlHeaderText"),
			["Hosts"] = ("Hosts", "HostsEnableToggleControlHeaderText"),
			["Image Resizer"] = ("ImageResizer", "ImageResizerEnableToggle"),
			["Keyboard Manager"] = ("KBM", "KeyboardManagerEnableToggle"),
			["LightSwitch"] = ("LightSwitch", "Toggle_LightSwitch"),
			["Measure Tool"] = ("MeasureTool", "MeasureToolEnableMeasureTool"),
			["MouseButtonLock"] = ("MouseUtils", "MouseUtilsEnableMouseButtonLock"),
			["MouseHighlighter"] = ("MouseUtils", "MouseUtilsEnableMouseHighlighter"),
			["MouseJump"] = ("MouseUtils", "MouseUtils_MouseJumpToggleId"),
			["MousePointerCrosshairs"] = ("MouseUtils", "MouseUtilsEnableMousePointerCrosshairs"),
			["MouseWithoutBorders"] = ("MouseWithoutBorders", "MouseWithoutBordersToggleEnable"),
			["NewPlus"] = ("NewPlus", "NewPlusEnableToggle"),
			["Peek"] = ("Peek", "PeekEnablePeek"),
			["PowerRename"] = ("PowerRename", "PowerRenameToggleEnable"),
			["PowerToys Run"] = ("PowerLauncher", "PowerLauncherEnablePowerLauncher"),
			["QuickAccent"] = ("PowerAccent", "QuickAccentEnableQuickAccent"),
			["RegistryPreview"] = ("RegistryPreview", "RegistryPreviewEnableRegistryPreview"),
			["Shortcut Guide"] = ("ShortcutGuide", "ShortcutGuideEnable"),
			["TextExtractor"] = ("PowerOcr", "TextExtractorEnableToggleControlHeaderText"),
			["Workspaces"] = ("Workspaces", "WorkspacesEnableToggleControlHeaderText"),
			["ZoomIt"] = ("ZoomIt", "ZoomItEnableToggleControlHeaderText"),
		};
}

/// <summary>
/// Opens Settings on the module's page through the runner, finds the Enable switch with UI Automation and
/// toggles it, then closes the window again if it was not open before.
/// </summary>
internal sealed class WindowsSettingsUi(IProcessStarter starter, TimeSpan? wait = null) : ISettingsUi
{
	private const int AutomationIdProperty = 30011;
	private const int TogglePattern = 10015;
	private const int ToggleOn = 1;
	private const uint WmClose = 0x0010;

	private static readonly Guid CUIAutomation = new("ff48dba4-60ef-4201-aa87-54103eef594e");
	private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(150);

	private readonly TimeSpan _wait = wait ?? TimeSpan.FromSeconds(10);

	public Task<SettingsUiOutcome> SetAsync(PowerToysPaths location, string module, bool target, CancellationToken ct) =>
		SettingsPages.Switches.TryGetValue(module, out var entry)
			? Task.Run(() => Set(location, entry.Page, entry.AutomationId, target, ct), CancellationToken.None)
			: Task.FromResult(SettingsUiOutcome.Unsupported);

	private SettingsUiOutcome Set(PowerToysPaths location, string page, string automationId, bool target, CancellationToken ct)
	{
		var wasOpen = SettingsWindow() != IntPtr.Zero;
		var window = IntPtr.Zero;

		try
		{
			if (!starter.Start($"\"{location.RunnerExe}\" --open-settings={page}", location.InstallRoot)) return SettingsUiOutcome.Failed;

			var automation = (IUIAutomation)Activator.CreateInstance(Type.GetTypeFromCLSID(CUIAutomation, throwOnError: true)!)!;
			var byId = automation.CreatePropertyCondition(AutomationIdProperty, automationId);
			var any = automation.CreateTrueCondition();

			var deadline = DateTime.UtcNow + _wait;
			IUIAutomationTogglePattern? toggle = null;
			while (toggle is null)
			{
				ct.ThrowIfCancellationRequested();
				window = SettingsWindow();
				if (window != IntPtr.Zero && automation.ElementFromHandle(window).FindFirst(TreeScope.Descendants, byId) is { } element)
				{
					toggle = FindToggle(element, any);
				}

				if (toggle is not null) break;
				if (DateTime.UtcNow >= deadline) return SettingsUiOutcome.Failed;
				Thread.Sleep(Poll);
			}

			if (toggle.GetCurrentToggleState() == ToggleOn == target) return SettingsUiOutcome.NoChange;

			toggle.Toggle();
			var settled = DateTime.UtcNow + TimeSpan.FromSeconds(2);
			while (toggle.GetCurrentToggleState() == ToggleOn != target)
			{
				if (DateTime.UtcNow >= settled) return SettingsUiOutcome.Failed;
				Thread.Sleep(Poll);
			}

			return SettingsUiOutcome.Applied;
		}
		catch (Exception ex) when (ex is COMException or InvalidCastException or System.Management.ManagementException or UnauthorizedAccessException)
		{
			return SettingsUiOutcome.Failed;
		}
		finally
		{
			if (!wasOpen && window != IntPtr.Zero) PostMessageW(window, WmClose, IntPtr.Zero, IntPtr.Zero);
		}
	}

	/// <summary>The switch itself, or the first switch inside the card that carries the id.</summary>
	private static IUIAutomationTogglePattern? FindToggle(IUIAutomationElement element, IUIAutomationCondition any)
	{
		if (element.GetCurrentPattern(TogglePattern) is IUIAutomationTogglePattern own) return own;

		var children = element.FindAll(TreeScope.Descendants, any);
		for (var i = 0; i < children.GetLength(); i++)
		{
			if (children.GetElement(i).GetCurrentPattern(TogglePattern) is IUIAutomationTogglePattern inner) return inner;
		}

		return null;
	}

	private static IntPtr SettingsWindow()
	{
		var session = Process.GetCurrentProcess().SessionId;
		var processes = Process.GetProcessesByName("PowerToys.Settings");
		try
		{
			return processes.Where(p => p.SessionId == session).Select(p => p.MainWindowHandle).FirstOrDefault(h => h != IntPtr.Zero);
		}
		finally
		{
			foreach (var process in processes) process.Dispose();
		}
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	private enum TreeScope
	{
		Descendants = 4,
	}

	// The slices of the UI Automation COM interfaces (UIAutomationClient.h) this class calls. Each interface
	// keeps the vtable order up to the last method used; the placeholders hold the slots of the rest.
	[ComImport]
	[Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IUIAutomation
	{
		void CompareElements();

		void CompareRuntimeIds();

		void GetRootElement();

		IUIAutomationElement ElementFromHandle(IntPtr hwnd);

		void ElementFromPoint();

		void GetFocusedElement();

		void GetRootElementBuildCache();

		void ElementFromHandleBuildCache();

		void ElementFromPointBuildCache();

		void GetFocusedElementBuildCache();

		void CreateTreeWalker();

		void ControlViewWalker();

		void ContentViewWalker();

		void RawViewWalker();

		void RawViewCondition();

		void ControlViewCondition();

		void ContentViewCondition();

		void CreateCacheRequest();

		IUIAutomationCondition CreateTrueCondition();

		void CreateFalseCondition();

		IUIAutomationCondition CreatePropertyCondition(int propertyId, [MarshalAs(UnmanagedType.Struct)] object value);
	}

	[ComImport]
	[Guid("352ffba8-0973-437c-a61f-f64cafd81df9")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IUIAutomationCondition
	{
	}

	[ComImport]
	[Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IUIAutomationElement
	{
		void SetFocus();

		void GetRuntimeId();

		IUIAutomationElement? FindFirst(TreeScope scope, IUIAutomationCondition condition);

		IUIAutomationElementArray FindAll(TreeScope scope, IUIAutomationCondition condition);

		void FindFirstBuildCache();

		void FindAllBuildCache();

		void BuildUpdatedCache();

		void GetCurrentPropertyValue();

		void GetCurrentPropertyValueEx();

		void GetCachedPropertyValue();

		void GetCachedPropertyValueEx();

		void GetCurrentPatternAs();

		void GetCachedPatternAs();

		[return: MarshalAs(UnmanagedType.IUnknown)]
		object? GetCurrentPattern(int patternId);
	}

	[ComImport]
	[Guid("14314595-b4bc-4055-95f2-58f2e42c9855")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IUIAutomationElementArray
	{
		int GetLength();

		IUIAutomationElement GetElement(int index);
	}

	[ComImport]
	[Guid("94cf8058-9b8d-4ab9-8bfd-4cd0a33c8c70")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IUIAutomationTogglePattern
	{
		void Toggle();

		int GetCurrentToggleState();
	}
}
