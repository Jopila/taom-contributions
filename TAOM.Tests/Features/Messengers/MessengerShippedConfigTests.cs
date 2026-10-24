using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using TAOM.Features.Messengers;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Messengers;

/// <summary>
/// Pins the SHIPPED <c>messenger_config.json</c> against the compiled defaults. Newtonsoft ignores an unknown key and
/// leaves the property at its default, so a misspelled key reads in game as "the setting does nothing" with no error.
/// </summary>
[TestClass]
public class MessengerShippedConfigTests
{
    private const string ShippedPath = "Main/_Module/ModuleData/messengers/messenger_config.json";

    [TestMethod]
    public void ShippedConfig_ParsesAndMatchesTheCompiledDefaults()
    {
        var parsed = JsonConvert.DeserializeObject<MessengerConfig>(RepoPaths.ReadSource(ShippedPath));
        Assert.IsNotNull(parsed);

        var defaults = new MessengerConfig();
        Assert.AreEqual(defaults.AccidentChancePerHour, parsed!.AccidentChancePerHour, 0.00001f, nameof(defaults.AccidentChancePerHour));
        Assert.AreEqual(defaults.TravelSpeedMultiplier, parsed.TravelSpeedMultiplier, 0.00001f, nameof(defaults.TravelSpeedMultiplier));
        Assert.AreEqual(defaults.GoldPerTravelDay, parsed.GoldPerTravelDay, 0.00001f, nameof(defaults.GoldPerTravelDay));
        Assert.AreEqual(defaults.AtWarMultiplier, parsed.AtWarMultiplier, 0.00001f, nameof(defaults.AtWarMultiplier));
        Assert.AreEqual(defaults.NeverMetMultiplier, parsed.NeverMetMultiplier, 0.00001f, nameof(defaults.NeverMetMultiplier));
        Assert.AreEqual(defaults.BesiegedMultiplier, parsed.BesiegedMultiplier, 0.00001f, nameof(defaults.BesiegedMultiplier));
    }

    [TestMethod]
    public void ShippedConfig_NamesEveryConfigProperty_AndNothingElse()
    {
        var json = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(RepoPaths.ReadSource(ShippedPath));
        Assert.IsNotNull(json);

        var declared = typeof(MessengerConfig).GetProperties();
        foreach (var property in declared)
            Assert.IsTrue(json!.Keys.Any(k => string.Equals(k, property.Name, StringComparison.OrdinalIgnoreCase)),
                "Shipped config has no key for '" + property.Name + "'.");
        foreach (var key in json!.Keys)
            Assert.IsTrue(declared.Any(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)),
                "Shipped config carries '" + key + "', which matches no property on MessengerConfig.");
    }
}
