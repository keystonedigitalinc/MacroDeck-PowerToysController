using System.Text.Json.Nodes;
using KeystoneDigital.PowerToysController.Awake;
using KeystoneDigital.PowerToysController.IO;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Tests.Locator;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests.Awake;

internal sealed class FakeFileStore : IFileStore
{
	public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

	public Queue<Func<string?>> ReadOverrides { get; } = new();

	public int Writes { get; private set; }

	public string? Read(string path)
	{
		if (ReadOverrides.Count > 0) return ReadOverrides.Dequeue()();
		return Files.TryGetValue(path, out var text) ? text : null;
	}

	public Exception? WriteError { get; set; }

	public void Write(string path, string content)
	{
		if (WriteError is not null) throw WriteError;
		Writes++;
		Files[path] = content;
	}
}

[TestFixture]
public sealed class AwakeTests
{
	private static readonly PowerToysPaths Paths = new(@"C:\PT", @"C:\Data");

	private static (AwakeControl Control, FakeFileStore Store) Create()
	{
		var store = new FakeFileStore();
		store.Files[Paths.AwakeSettings] = Fixture.Text("awake-settings.json");
		return (new AwakeControl(store, Paths, TimeProvider.System), store);
	}

	private static JsonObject Properties(FakeFileStore store) => JsonNode.Parse(store.Files[Paths.AwakeSettings])!["properties"]!.AsObject();

	[Test]
	public async Task Timed_30_minutes_writes_mode_2_hours_0_minutes_30()
	{
		var (control, store) = Create();

		var ok = await control.SetAsync(AwakeMode.Timed, keepDisplayOn: false, minutes: 30, until: null, default);

		Assert.That(ok, Is.True);
		var properties = Properties(store);
		Assert.That(properties["mode"]!.GetValue<int>(), Is.EqualTo(2));
		Assert.That(properties["intervalHours"]!.GetValue<int>(), Is.EqualTo(0));
		Assert.That(properties["intervalMinutes"]!.GetValue<int>(), Is.EqualTo(30));
	}

	[Test]
	public async Task Timed_90_minutes_splits_into_hours_and_minutes()
	{
		var (control, store) = Create();

		await control.SetAsync(AwakeMode.Timed, false, 90, null, default);

		Assert.That(Properties(store)["intervalHours"]!.GetValue<int>(), Is.EqualTo(1));
		Assert.That(Properties(store)["intervalMinutes"]!.GetValue<int>(), Is.EqualTo(30));
	}

	[Test]
	public async Task Indefinite_keeps_unknown_fields()
	{
		var (control, store) = Create();

		await control.SetAsync(AwakeMode.Indefinite, keepDisplayOn: true, minutes: 0, until: null, default);

		var root = JsonNode.Parse(store.Files[Paths.AwakeSettings])!.AsObject();
		Assert.That(root["properties"]!["customTrayTimes"], Is.Not.Null);
		Assert.That(root["name"]!.GetValue<string>(), Is.EqualTo("Awake"));
		Assert.That(root["properties"]!["keepDisplayOn"]!.GetValue<bool>(), Is.True);
		Assert.That(root["properties"]!["mode"]!.GetValue<int>(), Is.EqualTo(1));
	}

	[Test]
	public async Task Until_writes_an_iso_offset_timestamp()
	{
		var (control, store) = Create();
		var until = new DateTimeOffset(2026, 10, 16, 23, 30, 0, TimeSpan.FromHours(-4));

		await control.SetAsync(AwakeMode.Until, false, 0, until, default);

		var written = Properties(store)["expirationDateTime"]!.GetValue<string>();
		Assert.That(DateTimeOffset.Parse(written, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(until));
		Assert.That(Properties(store)["mode"]!.GetValue<int>(), Is.EqualTo(3));
	}

	[Test]
	public async Task Set_retries_when_the_file_is_locked_or_half_written()
	{
		var (control, store) = Create();
		store.ReadOverrides.Enqueue(() => throw new IOException("in use"));
		store.ReadOverrides.Enqueue(() => """{"properties":{"mode":""");

		var ok = await control.SetAsync(AwakeMode.Off, false, 0, null, default);

		Assert.That(ok, Is.True);
		Assert.That(Properties(store)["mode"]!.GetValue<int>(), Is.EqualTo(0));
		Assert.That(Properties(store)["customTrayTimes"], Is.Not.Null);
	}

	[Test]
	public void Read_reports_the_current_state()
	{
		var (control, _) = Create();

		Assert.That(control.Read(), Is.EqualTo(new AwakeState(AwakeMode.Off, false, 0, 1, DateTimeOffset.Parse("2026-09-14T14:47:13.4845494-04:00", System.Globalization.CultureInfo.InvariantCulture))));
	}

	[Test]
	public void Read_of_missing_file_is_null()
	{
		var control = new AwakeControl(new FakeFileStore(), Paths, TimeProvider.System);

		Assert.That(control.Read(), Is.Null);
	}
}
