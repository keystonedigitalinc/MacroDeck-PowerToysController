namespace KeystoneDigital.PowerToysController.IO;

internal interface IFileStore
{
	/// <summary>The file's text, or null when it does not exist. May throw IOException while another process writes it.</summary>
	string? Read(string path);

	void Write(string path, string content);
}

internal sealed class DiskFileStore : IFileStore
{
	public string? Read(string path)
	{
		if (!File.Exists(path)) return null;

		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}

	/// <summary>Writes beside the target, then swaps it in, so a watcher never reads half a file.</summary>
	public void Write(string path, string content)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		var temp = path + ".tmp-" + Environment.ProcessId;
		try
		{
			File.WriteAllText(temp, content);
			File.Move(temp, path, overwrite: true);
		}
		catch
		{
			File.Delete(temp);
			throw;
		}
	}
}
