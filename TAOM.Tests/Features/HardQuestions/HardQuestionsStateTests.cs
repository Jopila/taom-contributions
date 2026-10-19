using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HardQuestions;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Tests.Features.HardQuestions;

[TestClass]
public class HardQuestionsStateTests
{
    [TestMethod]
    public void MarkAsked_IsPerTopicAndPerHero()
    {
        var state = new HardQuestionsState();

        state.MarkAsked(HardQuestionTopic.Clan, "a");

        Assert.IsTrue(state.IsAsked(HardQuestionTopic.Clan, "a"));
        Assert.IsFalse(state.IsAsked(HardQuestionTopic.Kingdom, "a"));
        Assert.IsFalse(state.IsAsked(HardQuestionTopic.Clan, "b"));
    }

    [TestMethod]
    public void TheThreeUsesOfOneHeroAreIndependent()
    {
        var state = new HardQuestionsState();

        state.MarkStripped("a");
        state.MarkAsked(HardQuestionTopic.Kingdom, "a");

        Assert.IsTrue(state.IsStripped("a"));
        Assert.IsTrue(state.IsAsked(HardQuestionTopic.Kingdom, "a"));
        Assert.IsFalse(state.IsAsked(HardQuestionTopic.Clan, "a"));
    }

    [TestMethod]
    public void ClearHero_ForgetsEveryTopicOfThatHeroOnly()
    {
        var state = new HardQuestionsState();
        state.MarkAsked(HardQuestionTopic.Clan, "a");
        state.MarkAsked(HardQuestionTopic.Kingdom, "a");
        state.MarkStripped("a");
        state.MarkStripped("b");

        state.ClearHero("a");

        Assert.IsFalse(state.IsAsked(HardQuestionTopic.Clan, "a"));
        Assert.IsFalse(state.IsAsked(HardQuestionTopic.Kingdom, "a"));
        Assert.IsFalse(state.IsStripped("a"));
        Assert.IsTrue(state.IsStripped("b"));
    }

    [TestMethod]
    public void Reset_EmptiesTheState()
    {
        var state = new HardQuestionsState();
        state.MarkAsked(HardQuestionTopic.Clan, "a");
        state.MarkStripped("a");

        state.Reset();

        Assert.IsFalse(state.IsAsked(HardQuestionTopic.Clan, "a"));
        Assert.IsFalse(state.IsStripped("a"));
    }

    [TestMethod]
    public void Encode_WritesTheVersionAndOneBitFieldPerHero()
    {
        var state = new HardQuestionsState();
        state.MarkAsked(HardQuestionTopic.Clan, "a");
        state.MarkAsked(HardQuestionTopic.Kingdom, "b");
        state.MarkStripped("c");
        state.MarkAsked(HardQuestionTopic.Clan, "d");
        state.MarkAsked(HardQuestionTopic.Kingdom, "d");
        state.MarkStripped("d");

        var data = state.Encode();

        Assert.AreEqual("1", data["v"]);
        Assert.AreEqual("1", data["used|a"]);
        Assert.AreEqual("2", data["used|b"]);
        Assert.AreEqual("4", data["used|c"]);
        Assert.AreEqual("7", data["used|d"]);
        Assert.AreEqual(5, data.Count);
    }

    [TestMethod]
    public void Decode_RoundTripsAnEncodedState()
    {
        var source = new HardQuestionsState();
        source.MarkAsked(HardQuestionTopic.Clan, "a");
        source.MarkAsked(HardQuestionTopic.Kingdom, "b");
        source.MarkStripped("lord|with|bars");
        var target = new HardQuestionsState();
        target.MarkStripped("stale");

        var skipped = target.Decode(source.Encode());

        Assert.AreEqual(0, skipped);
        Assert.IsTrue(target.IsAsked(HardQuestionTopic.Clan, "a"));
        Assert.IsTrue(target.IsAsked(HardQuestionTopic.Kingdom, "b"));
        Assert.IsTrue(target.IsStripped("lord|with|bars"));
        Assert.IsFalse(target.IsStripped("stale"));
    }

    [TestMethod]
    public void Decode_Null_YieldsAnEmptyState()
    {
        var state = new HardQuestionsState();
        state.MarkStripped("a");

        Assert.AreEqual(0, state.Decode(null));

        Assert.IsFalse(state.IsStripped("a"));
    }

    [TestMethod]
    public void Decode_DamagedEntries_AreSkippedAndCounted()
    {
        var state = new HardQuestionsState();
        var data = new Dictionary<string, string>
        {
            ["v"] = "1",
            ["used|good"] = "5",
            ["used|zero"] = "0",
            ["used|eight"] = "8",
            ["used|negative"] = "-1",
            ["used|text"] = "x",
            ["used|"] = "1",
            ["used"] = "1",
            ["mystery|x"] = "1",
            ["used|ok"] = "4",
        };

        var skipped = state.Decode(data);

        Assert.AreEqual(7, skipped);
        Assert.IsTrue(state.IsAsked(HardQuestionTopic.Clan, "good"));
        Assert.IsFalse(state.IsAsked(HardQuestionTopic.Kingdom, "good"));
        Assert.IsTrue(state.IsStripped("good"));
        Assert.IsTrue(state.IsStripped("ok"));
        Assert.IsFalse(state.IsStripped("zero"));
        Assert.IsFalse(state.IsStripped("eight"));
    }
}
