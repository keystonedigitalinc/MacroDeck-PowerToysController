using KeystoneDigital.PowerToysController.Events;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests.Events;

internal sealed class FakeEventOpener : INamedEventOpener
{
	public Dictionary<string, TriggerResult> Results { get; } = new(StringComparer.Ordinal);

	public List<string> Opened { get; } = [];

	public TriggerResult TrySet(string name)
	{
		Opened.Add(name);
		return Results.TryGetValue(name, out var result) ? result : TriggerResult.Triggered;
	}
}

[TestFixture]
public sealed class EventTriggerTests
{
	[Test]
	public void Every_tool_has_a_Local_event_name()
	{
		foreach (var tool in Enum.GetValues<PowerToysTool>())
		{
			Assert.That(PowerToysEvents.NameOf(tool), Does.StartWith(@"Local\"), tool.ToString());
			Assert.That(PowerToysEvents.ModuleOf(tool), Is.Not.Empty, tool.ToString());
		}
	}

	[Test]
	public void Color_picker_uses_the_verified_name() =>
		Assert.That(PowerToysEvents.NameOf(PowerToysTool.ColorPicker), Is.EqualTo(@"Local\ShowColorPickerEvent-8c46be2a-3e05-4186-b56b-4ae986ef2525"));

	[Test]
	public void Screen_ruler_belongs_to_the_Measure_Tool_module() =>
		Assert.That(PowerToysEvents.ModuleOf(PowerToysTool.ScreenRuler), Is.EqualTo("Measure Tool"));

	[Test]
	public void Trigger_opens_the_event_on_every_press()
	{
		var opener = new FakeEventOpener();
		var trigger = new EventTrigger(opener);

		trigger.Trigger(PowerToysTool.TextExtractor);
		trigger.Trigger(PowerToysTool.TextExtractor);

		Assert.That(opener.Opened, Has.Count.EqualTo(2));
	}

	[Test]
	public void Missing_event_maps_to_NotRunning()
	{
		var opener = new FakeEventOpener();
		opener.Results[PowerToysEvents.NameOf(PowerToysTool.MouseJump)] = TriggerResult.NotRunning;

		Assert.That(new EventTrigger(opener).Trigger(PowerToysTool.MouseJump), Is.EqualTo(TriggerResult.NotRunning));
	}

	[Test]
	public void Access_denied_maps_to_Denied()
	{
		var opener = new FakeEventOpener();
		opener.Results[PowerToysEvents.NameOf(PowerToysTool.ColorPicker)] = TriggerResult.Denied;

		Assert.That(new EventTrigger(opener).Trigger(PowerToysTool.ColorPicker), Is.EqualTo(TriggerResult.Denied));
	}
}
