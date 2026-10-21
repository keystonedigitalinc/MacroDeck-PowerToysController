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

	/// <summary>
	/// Writes the file in place, the way PowerToys Settings does. Awake watches for in-place writes only;
	/// replacing the file through a temp file went unnoticed. Readers already retry half-written files.
	/// </summary>
	public void Write(string path, string content)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete);
		using var writer = new StreamWriter(stream);
		writer.Write(content);
	}
}
