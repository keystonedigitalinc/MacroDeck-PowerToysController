using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace KeystoneDigital.PowerToysController.Locator;

internal interface IPowerToysProcesses
{
	bool IsRunnerRunning();

	bool IsRunnerElevated();

	IReadOnlyList<int> RunnerProcessIds();
}

/// <summary>Finds the install, reads the version and module map, and answers policy questions.</summary>
internal sealed class PowerToysLocator(IPowerToysProcesses processes, Func<PowerToysPaths?> findPaths, Func<string, string?> readFile)
{
	private const string PolicyKey = @"SOFTWARE\Policies\PowerToys";

	public PowerToysPaths? Paths => findPaths();

	public IPowerToysProcesses Processes => processes;

	public string? Version => Paths is { } paths && readFile(paths.GeneralSettings) is { } json ? EnableMap.Version(json) : null;

	public IReadOnlyDictionary<string, bool> Modules =>
		Paths is { } paths && readFile(paths.GeneralSettings) is { } json ? EnableMap.Read(json) : new Dictionary<string, bool>();

	/// <summary>True when a Windows policy forces the module on or off, which PowerToys obeys over its own map.</summary>
	public static bool IsPolicyForced(string module)
	{
		var name = "ConfigureEnabledUtility" + module.Replace(" ", "", StringComparison.Ordinal);
		using var machine = Registry.LocalMachine.OpenSubKey(PolicyKey);
		using var user = Registry.CurrentUser.OpenSubKey(PolicyKey);
		return machine?.GetValue(name) is not null || user?.GetValue(name) is not null;
	}

	/// <summary>The install root from the uninstall entries, else the per-user default.</summary>
	public static PowerToysPaths? FindInstalled()
	{
		foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
		{
			using var uninstall = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
			if (uninstall is null) continue;

			foreach (var name in uninstall.GetSubKeyNames())
			{
				using var entry = uninstall.OpenSubKey(name);
				if (entry?.GetValue("DisplayName") is not string display || !display.StartsWith("PowerToys", StringComparison.Ordinal)) continue;
				if (entry.GetValue("InstallLocation") is string location && File.Exists(Path.Combine(location, "PowerToys.exe")))
				{
					return new PowerToysPaths(location, PowerToysPaths.DefaultDataRoot);
				}
			}
		}

		var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerToys");
		return File.Exists(Path.Combine(fallback, "PowerToys.exe")) ? new PowerToysPaths(fallback, PowerToysPaths.DefaultDataRoot) : null;
	}
}

internal sealed partial class WindowsPowerToysProcesses : IPowerToysProcesses
{
	private const uint TokenQuery = 0x0008;
	private const int TokenElevation = 20;

	public bool IsRunnerRunning() => RunnerProcessIds().Count > 0;

	public IReadOnlyList<int> RunnerProcessIds()
	{
		var processes = Process.GetProcessesByName("PowerToys");
		try
		{
			return [.. processes.Select(process => process.Id)];
		}
		finally
		{
			foreach (var process in processes) process.Dispose();
		}
	}

	public bool IsRunnerElevated()
	{
		foreach (var id in RunnerProcessIds())
		{
			try
			{
				using var process = Process.GetProcessById(id);
				if (!OpenProcessToken(process.SafeHandle, TokenQuery, out var token)) continue;
				using (token)
				{
					if (GetTokenInformation(token, TokenElevation, out var elevated, sizeof(int), out _) && elevated != 0) return true;
				}
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
			{
				// An elevated process refuses even this query from a medium-integrity caller.
				return true;
			}
		}

		return false;
	}

	[LibraryImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

	[LibraryImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, out int information, int length, out int returned);
}
