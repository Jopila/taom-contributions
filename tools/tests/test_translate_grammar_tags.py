"""The translator guard counts grammar tags such as {.NS}, {.FN} and {.MS}.

Vanilla's Portuguese and Italian names carry a leading tag that the game's text processors read
(por_br_functions.xml GENDER, ItalianTextProcessor). A translation that drops or adds one
changes the article or preposition the game prints, so placeholders_match must reject it.
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import translate_with_claude as twc  # noqa: E402


class GrammarTagCountTests(unittest.TestCase):
    def test_extract_placeholders_CountsGrammarTags(self):
        counts = twc.extract_placeholders("{.NS}Erebor and {.FN}Gondor and {.NS}Rohan")
        self.assertEqual(counts["{.NS}"], 2)
        self.assertEqual(counts["{.FN}"], 1)

    def test_extract_placeholders_PlainAndVariableTextHasNoTagKeys(self):
        counts = twc.extract_placeholders("Hello {RULER.NAME}")
        self.assertFalse([k for k in counts if k.startswith("{.")])

    def test_placeholders_match_DroppedLeadingTag_IsRejected(self):
        ok, why = twc.placeholders_match("{.NS}Erebor", "Erebor")
        self.assertFalse(ok)
        self.assertIn("{.NS}", why)

    def test_placeholders_match_AddedTag_IsRejected(self):
        ok, why = twc.placeholders_match("Erebor", "{.NS}Erebor")
        self.assertFalse(ok)
        self.assertIn("{.NS}", why)

    def test_placeholders_match_ChangedTag_IsRejected(self):
        ok, _ = twc.placeholders_match("{.NS}Erebor", "{.MS}Erebor")
        self.assertFalse(ok)

    def test_placeholders_match_KeptLeadingTag_Passes(self):
        ok, why = twc.placeholders_match("{.FS}House of Bard", "{.FS}Casa di Bard")
        self.assertTrue(ok, why)

    def test_placeholders_match_TextWithoutTags_StillPasses(self):
        ok, why = twc.placeholders_match("Kingdom of {NAME}", "Regno di {NAME}")
        self.assertTrue(ok, why)


class GrammarTagPromptTests(unittest.TestCase):
    def test_SystemPrompt_StillFormatsWithTargetLanguage(self):
        text = twc.SYSTEM_PROMPT.format(target_language="Italian")
        self.assertIn("Italian", text)


if __name__ == "__main__":
    unittest.main()
