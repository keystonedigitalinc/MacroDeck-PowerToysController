using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests;

[TestFixture]
public sealed class IntegrationTests
{
	internal static PluginTestHarness CreateHarness(TestRig? rig = null) =>
		PluginTestHarness.Create(builder =>
		{
			builder.Services.AddPowerToysComponents((rig ?? new TestRig()).Components);
			builder.UseLocalization(Strings.LocalizationCatalog).RegisterIntegration(PowerToysIntegration.Create);
		});

	[Test]
	public async Task The_plugin_builds_and_initializes()
	{
		await using var harness = CreateHarness();

		Assert.DoesNotThrowAsync(harness.InitializeIntegrationsAsync);
	}

	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:com.keystonedigital.powertoys"));
	}

	[Test]
	public void Every_resx_key_resolves()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True, key);
			Assert.That(text, Is.Not.Empty, key);
		}
	}
}
