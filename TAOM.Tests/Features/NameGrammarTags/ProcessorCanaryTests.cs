using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NameGrammarTags;

namespace TAOM.Tests.Features.NameGrammarTags;

[TestClass]
public class ProcessorCanaryTests
{
    [TestMethod]
    public void For_Italian_BindsATaggedProperNounBehindAPreposition()
    {
        var spec = ProcessorCanary.For("IT")!;
        StringAssert.Contains(spec.Text, "{.di}");
        Assert.AreEqual("{.FN}Roma", spec.TaggedValue);
        Assert.AreEqual("di Roma", spec.Expected);
    }

    [TestMethod]
    public void For_Brazilian_ChecksTheGenderFunctionPicksTheOptionOfTheTag()
    {
        var spec = ProcessorCanary.For("BR")!;
        StringAssert.Contains(spec.Text, "GENDER(");
        Assert.AreEqual("{.FS}Roma", spec.TaggedValue);
        Assert.AreEqual("FS", spec.Expected);
    }

    [TestMethod]
    public void For_OtherLanguage_HasNoCanary()
    {
        Assert.IsNull(ProcessorCanary.For("DE"));
    }

    [TestMethod]
    public void Evaluate_RenderMatchesTheExpectedText_Passes()
    {
        var result = ProcessorCanary.Evaluate("IT", (text, variable, value) => "di Roma");
        Assert.IsTrue(result.Applicable);
        Assert.IsTrue(result.Passed);
    }

    [TestMethod]
    public void Evaluate_RenderPrintsNoPreposition_FailsAndSaysWhatItPrinted()
    {
        var result = ProcessorCanary.Evaluate("IT", (text, variable, value) => "Roma");
        Assert.IsFalse(result.Passed);
        StringAssert.Contains(result.Description, "Roma");
    }

    [TestMethod]
    public void Evaluate_RenderThrows_Fails()
    {
        var result = ProcessorCanary.Evaluate("IT", (text, variable, value) => throw new System.InvalidOperationException("no engine"));
        Assert.IsFalse(result.Passed);
        StringAssert.Contains(result.Description, "no engine");
    }

    [TestMethod]
    public void Evaluate_LanguageWithoutACanary_IsNotApplicable()
    {
        var result = ProcessorCanary.Evaluate("DE", (text, variable, value) => "x");
        Assert.IsFalse(result.Applicable);
    }

    [TestMethod]
    public void Evaluate_ItalianSpec_PassesTheTaggedValueToTheRenderer()
    {
        string? seenValue = null;
        ProcessorCanary.Evaluate("IT", (text, variable, value) => { seenValue = value; return "di Roma"; });
        Assert.AreEqual("{.FN}Roma", seenValue);
    }
}
