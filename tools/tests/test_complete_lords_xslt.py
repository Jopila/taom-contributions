"""complete_lords_xslt.py regenerates lords.xslt from scratch, so the #758 vanilla-wanderer retag and the #957
minor-clan troop retag and leader clones must come from the generator itself, or a regeneration silently puts
Calradian wanderers back in the spawn pool and the Grey Company's troops back on Dunland's culture."""
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import complete_lords_xslt  # noqa: E402

RETAG_MATCH = "NPCCharacter[@is_template='true' and @occupation='Wanderer']/@occupation"
XSL = "{http://www.w3.org/1999/XSL/Transform}"
MINOR_CLAN_TROOPS = {
    "legion_of_the_betrayed_tier_": "arthedain",
    "company_of_the_boar_tier_": "umbar",
    "eleftheroi_tier_": "sturgia",
    "brotherhood_of_woods_tier_": "empire",
}


class GenerateXsltTests(unittest.TestCase):
    def test_generate_xslt_emits_the_vanilla_wanderer_retag(self):
        root = ET.fromstring(complete_lords_xslt.generate_xslt({}, []).encode("utf-8"))

        retag = [t for t in root.iter(f"{XSL}template") if t.get("match") == RETAG_MATCH]

        self.assertEqual(1, len(retag), "regenerating lords.xslt would drop the #758 retag")
        attribute = retag[0].find(f"{XSL}attribute")
        self.assertEqual("occupation", attribute.get("name"))
        self.assertEqual("NotAssigned", attribute.text)

    def test_shipped_lords_xslt_carries_the_generators_retag_verbatim(self):
        shipped = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData" / "lords.xslt"
        text = shipped.read_bytes().decode("utf-8-sig").replace("\r\n", "\n")

        self.assertIn("\n".join(complete_lords_xslt.VANILLA_WANDERER_RETAG), text,
                      "lords.xslt and the generator's retag block have drifted apart")

    def test_generate_xslt_emits_the_minor_clan_troop_retags_and_leader_clone(self):
        root = ET.fromstring(complete_lords_xslt.generate_xslt({}, []).encode("utf-8"))
        templates = {t.get("match"): t for t in root.iter(f"{XSL}template")}

        for prefix, culture in MINOR_CLAN_TROOPS.items():
            match = f"NPCCharacter[starts-with(@id,'{prefix}')]/@culture"
            self.assertIn(match, templates, f"regenerating lords.xslt would drop the {prefix} retag (#957)")
            attribute = templates[match].find(f"{XSL}attribute")
            self.assertEqual("culture", attribute.get("name"))
            self.assertEqual(f"Culture.{culture}", attribute.text)

        clone = [t for m, t in templates.items() if m and m.startswith("NPCCharacter[starts-with(@id,'spc_legion_of_the_betrayed_leader_')")]
        self.assertEqual(1, len(clone), "regenerating lords.xslt would drop the leader clone template (#957)")
        copies = clone[0].findall(f"{XSL}copy")
        self.assertEqual(2, len(copies), "the original is copied, then the clone beside it")
        self.assertEqual("taom_", copies[1].find(f"{XSL}attribute[@name='id']").text.split("<")[0])

    def test_shipped_lords_xslt_carries_the_generators_minor_clan_block_verbatim(self):
        shipped = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData" / "lords.xslt"
        text = shipped.read_bytes().decode("utf-8-sig").replace("\r\n", "\n")

        self.assertIn("\n".join(complete_lords_xslt.MINOR_CLAN_CULTURES), text,
                      "lords.xslt and the generator's minor-clan block have drifted apart")

    def test_generate_xslt_places_the_minor_clan_block_after_the_wanderer_retag_and_before_the_lords(self):
        lord = {
            "attrs": {"id": "lord_1_1"},
            "face_xml": "<face/>",
            "skills_xml": "",
            "traits_xml": "<Traits/>",
            "is_new": False,
            "is_dead": False,
        }

        out = complete_lords_xslt.generate_xslt({"lord_1_1": lord}, ["lord_1_1"])

        retag = out.index("\n".join(complete_lords_xslt.VANILLA_WANDERER_RETAG))
        minor = out.index("\n".join(complete_lords_xslt.MINOR_CLAN_CULTURES))
        first_lord = out.index("NPCCharacter[@id='lord_1_1']")
        self.assertLess(retag, minor)
        self.assertLess(minor, first_lord, "the minor-clan block must precede the lord templates")


if __name__ == "__main__":
    unittest.main()
