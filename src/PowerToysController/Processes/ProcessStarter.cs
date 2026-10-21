using System.Globalization;
using System.Management;

namespace KeystoneDigital.PowerToysController.Processes;

internal interface IProcessStarter
{
	bool Start(string commandLine, string workingDirectory);
}

/// <summary>
/// Macro Deck runs each plugin in a job object that is closed with the plugin, so a child started with
/// Process.Start would die on every Macro Deck restart or plugin update. A process created through WMI
/// belongs to no job of ours and still runs in the user's desktop session.
/// </summary>
internal sealed class WmiProcessStarter : IProcessStarter
{
	public bool Start(string commandLine, string workingDirectory)
	{
		using var processClass = new ManagementClass("Win32_Process");
		using var arguments = processClass.GetMethodParameters("Create");
		arguments["CommandLine"] = commandLine;
		arguments["CurrentDirectory"] = workingDirectory;

		using var result = processClass.InvokeMethod("Create", arguments, null);
		return Convert.ToUInt32(result["ReturnValue"], CultureInfo.InvariantCulture) == 0;
	}
}
