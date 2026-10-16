using System.Text.Json;
using KeystoneDigital.PowerToysController.IO;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Processes;

namespace KeystoneDigital.PowerToysController.Workspaces;

internal sealed record WorkspaceInfo(string Id, string Name);

internal enum WorkspaceOutcome
{
	Launched,
	NotFound,
	Failed,
}

/// <summary>
/// Lists saved Workspaces and launches one the way a Workspace desktop shortcut does:
/// <c>PowerToys.WorkspacesLauncher.exe &lt;id&gt; 1</c>, where 1 means "invoked by shortcut". It starts through WMI
/// so the apps it opens are not tied to Macro Deck's plugin job.
/// </summary>
internal sealed class WorkspacesControl(IFileStore files, PowerToysPaths paths, IProcessStarter starter)
{
	public IReadOnlyList<WorkspaceInfo> List()
	{
		string? json;
		try
		{
			json = files.Read(paths.WorkspacesFile);
		}
		catch (IOException)
		{
			return [];
		}

		if (string.IsNullOrWhiteSpace(json)) return [];

		try
		{
			using var document = JsonDocument.Parse(json);
			if (!document.RootElement.TryGetProperty("workspaces", out var list) || list.ValueKind != JsonValueKind.Array) return [];

			return [.. list.EnumerateArray()
				.Where(w => w.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
				.Select(w => new WorkspaceInfo(
					w.GetProperty("id").GetString()!,
					w.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString()! : w.GetProperty("id").GetString()!))];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	public WorkspaceOutcome Launch(string id)
	{
		if (List().All(w => w.Id != id)) return WorkspaceOutcome.NotFound;

		return starter.Start($"\"{paths.WorkspacesLauncher}\" {id} 1", paths.InstallRoot) ? WorkspaceOutcome.Launched : WorkspaceOutcome.Failed;
	}
}
