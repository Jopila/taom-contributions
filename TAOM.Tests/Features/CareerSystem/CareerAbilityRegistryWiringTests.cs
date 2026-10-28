using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Abilities.Executors;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// The archetype map feeds two consumers: the starting-kit roster id and the ability executor registry.
/// A new CareerArchetype value that the registry's switch does not name leaves its careers on the
/// registry's no-op executor (the toast, sound and cooldown fire and no buff applies), and no map or
/// kit test sees it (#1045: TwoHanded). This builds the registry the way production does.
/// </summary>
[TestClass]
public class CareerAbilityRegistryWiringTests
{
    // BuildAbilityEffectRegistry is the private static method the IoC registration delegate calls;
    // reaching it by reflection avoids resolving the whole feature graph for one switch.
    private static CareerAbilityEffectRegistry BuildProductionRegistry()
    {
        var method = typeof(CareerSystemIoC).GetMethod("BuildAbilityEffectRegistry", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "CareerSystemIoC.BuildAbilityEffectRegistry was renamed or moved; update this test");
        return (CareerAbilityEffectRegistry)method!.Invoke(null, new object[] { Substitute.For<ICareerConfigProvider>() })!;
    }

    [TestMethod]
    public void Registry_EveryMappedCareer_GetsItsOwnExecutorNotTheNoOp()
    {
        var registry = BuildProductionRegistry();
        var map = CareerSystemIoC.GetCareerArchetypeMap();

        var noOp = map.Keys.Where(id => registry.GetExecutor(id).CareerId != id).ToList();

        Assert.IsTrue(map.Count > 50, $"read only {map.Count} mapped careers");
        Assert.AreEqual(0, noOp.Count,
            $"Careers on the no-op executor (is their archetype named in BuildAbilityEffectRegistry?): {string.Join(", ", noOp)}");
    }

    [TestMethod]
    public void Registry_EveryArchetype_HasAnExecutorFamily()
    {
        var registry = BuildProductionRegistry();
        var families = new Dictionary<CareerArchetype, string>();
        foreach (var pair in CareerSystemIoC.GetCareerArchetypeMap())
            families[pair.Value] = registry.GetExecutor(pair.Key).GetType().Name;

        foreach (CareerArchetype archetype in System.Enum.GetValues(typeof(CareerArchetype)))
            Assert.IsTrue(families.ContainsKey(archetype), $"no mapped career uses {archetype}");
        Assert.AreEqual("InfantryAbilityExecutor", families[CareerArchetype.Infantry]);
        Assert.AreEqual("RangedAbilityExecutor", families[CareerArchetype.Ranged]);
        Assert.AreEqual("CavalryAbilityExecutor", families[CareerArchetype.Cavalry]);
    }

    [TestMethod]
    public void Registry_HewerOfBelegost_RunsTheInfantryExecutor()
    {
        var executor = BuildProductionRegistry().GetExecutor("hewer_of_belegost");

        Assert.IsInstanceOfType(executor, typeof(InfantryAbilityExecutor));
        Assert.AreEqual("hewer_of_belegost", executor.CareerId);
    }
}
