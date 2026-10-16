using KeystoneDigital.PowerToysController.Processes;

namespace KeystoneDigital.PowerToysController.Modules;

internal interface IRunnerControl
{
	Task StopAsync(CancellationToken ct);

	bool Start(string runnerExe);
}

/// <summary>Stops PowerToys and every module process it hosts, and starts it again outside Macro Deck's job.</summary>
internal sealed class WindowsRunnerControl(IProcessStarter starter) : IRunnerControl
{
	private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(5);

	public async Task StopAsync(CancellationToken ct)
	{
		foreach (var process in PowerToysProcesses())
		{
			using (process)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
				{
					// Already exiting with the runner killed earlier in this loop.
				}
			}
		}

		var deadline = DateTime.UtcNow + StopWait;
		while (DateTime.UtcNow < deadline)
		{
			var remaining = PowerToysProcesses();
			var count = remaining.Count;
			foreach (var process in remaining) process.Dispose();
			if (count == 0) return;

			await Task.Delay(100, ct).ConfigureAwait(false);
		}
	}

	public bool Start(string runnerExe) => starter.Start($"\"{runnerExe}\"", Path.GetDirectoryName(runnerExe) ?? "");

	private static List<System.Diagnostics.Process> PowerToysProcesses() =>
		[.. System.Diagnostics.Process.GetProcesses().Where(process =>
		{
			var keep = process.ProcessName == "PowerToys" || process.ProcessName.StartsWith("PowerToys.", StringComparison.Ordinal);
			if (!keep) process.Dispose();
			return keep;
		})];
}
