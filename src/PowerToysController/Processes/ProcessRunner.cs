using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeystoneDigital.PowerToysController.Processes;

internal interface IProcessRunner
{
	Task<(int Exit, string Output)> RunAsync(string exe, string args, TimeSpan timeout, CancellationToken ct);
}

/// <summary>Runs a short-lived command-line tool and waits for it. It is meant to die with the plugin.</summary>
internal sealed class ProcessRunner : IProcessRunner
{
	public async Task<(int Exit, string Output)> RunAsync(string exe, string args, TimeSpan timeout, CancellationToken ct)
	{
		using var process = new System.Diagnostics.Process
		{
			StartInfo = new ProcessStartInfo(exe, args)
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
			},
		};

		try
		{
			process.Start();
		}
		catch (System.ComponentModel.Win32Exception ex)
		{
			return (-1, ex.Message);
		}

		var output = process.StandardOutput.ReadToEndAsync(ct);
		var error = process.StandardError.ReadToEndAsync(ct);

		using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
		limit.CancelAfter(timeout);
		try
		{
			await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			process.Kill(entireProcessTree: true);
			return (-1, "Timed out after " + timeout);
		}

		return (process.ExitCode, (await output.ConfigureAwait(false)) + (await error.ConfigureAwait(false)));
	}
}

internal interface IFancyZonesSignals
{
	void RequestEditorParametersSave();
}

/// <summary>The broadcast FancyZonesCLI sends so FancyZones re-saves editor-parameters.json with the current monitors.</summary>
internal sealed partial class FancyZonesSignals : IFancyZonesSignals
{
	private static readonly IntPtr Broadcast = new(0xffff);

	public void RequestEditorParametersSave()
	{
		var message = RegisterWindowMessage("{d8f9c0e3-5d77-4e83-8a4f-7c704c2bfb4a}");
		if (message != 0) PostMessage(Broadcast, message, IntPtr.Zero, IntPtr.Zero);
	}

	[LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
	private static partial uint RegisterWindowMessage(string name);

	[LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
