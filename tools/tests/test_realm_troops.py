#!/usr/bin/env python3
"""Design tests for the Cardolan and Rhudaur troop generators (#978), which share tools/realm_troops.py
with Arthedain's (tools/tests/test_generate_arthedain_troops.py covers that one).

Run:  python -m unittest tools.tests.test_realm_troops

Pure stdlib; each generator builds its tree in memory. The same invariants the validator would
otherwise report only after a write: the self-check passes, no upgrade lowers a skill, the militia
follow the TAOM rule, the tavern mercenary is a leaf copy, every bow troop carries its ranged-ladder
cell, the party templates name only defined troops and the lord template sits at the men's target.
"""
import dataclasses
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_cardolan_troops as gct  # noqa: E402
import generate_rhudaur_troops as grt  # noqa: E402
import ranged_ladder as rl  # noqa: E402
import realm_troops as rt  # noqa: E402

GENERATORS = (gct, grt)


def built(gen):
    return gen.add_mercenary(gen.clamp_skills(gen.build()))


class RealmDesignTests(unittest.TestCase):
    def test_each_design_passes_its_self_check(self):
        for gen in GENERATORS:
            with self.subTest(culture=gen.CULTURE):
                self.assertEqual(gen.self_check(built(gen)), [])

    def _probe_errors(self, weapons):
        """Self-check errors for the first Cardolan troop re-armed with `weapons` in both rosters."""
        probe = dataclasses.replace(built(gct)[0], weapons=(list(weapons), list(weapons)), upgrades=[])
        return rt.self_check(lambda: [], [probe])

    def test_a_shield_with_a_two_handed_axe_fails_the_self_check(self):
        # Dunland's two-handed axes are named dunland_caerdh_axe_2h_*, with no greatsword or poleaxe in the id.
        errors = self._probe_errors(["dunland_caerdh_axe_2h_b", "sk_shield_round_a"])
        self.assertTrue(any("shield and a two-handed" in e for e in errors), errors)

    def test_a_shield_with_a_greatsword_fails_the_self_check(self):
        errors = self._probe_errors(["gondor_greatsword_a", "sk_shield_round_a"])
        self.assertTrue(any("shield and a two-handed" in e for e in errors), errors)

    def test_a_shield_with_a_one_handed_weapon_passes_that_rule(self):
        errors = self._probe_errors(["gondor_sword_a", "sk_shield_round_a"])
        self.assertFalse(any("shield and a two-handed" in e for e in errors), errors)

    def test_no_upgrade_lowers_any_skill(self):
        for gen in GENERATORS:
            troops = built(gen)
            by_id = {t.id: t for t in troops}
            for t in troops:
                for u in t.upgrades:
                    for k in rt.SKILLS:
                        self.assertGreaterEqual(by_id[u].sk[k], t.sk[k], f"{t.id} -> {u} {k}")

    def test_the_elite_militia_is_the_basic_plus_the_bonus(self):
        for gen in GENERATORS:
            by_id = {t.id: t for t in built(gen)}
            for basic_id, vet_id in gen.MILITIA.items():
                basic, vet = by_id[basic_id], by_id[vet_id]
                self.assertEqual(vet.level, 16, vet_id)
                for k in rt.SKILLS:
                    if k == "Bow" and basic.role == "ranged":
                        continue  # the ladder owns a laddered archer's Bow
                    self.assertEqual(vet.sk[k], basic.sk[k] + rt.MILITIA_ELITE_BONUS, f"{vet_id} {k}")

    def test_the_mercenary_is_a_leaf_copy_of_the_rarest_pool_entry(self):
        for gen in GENERATORS:
            by_id = {t.id: t for t in built(gen)}
            merc = by_id[gen.MERC_SOURCE + "_merc"]
            self.assertEqual(merc.occupation, "Mercenary")
            self.assertEqual(merc.upgrades, [])
            self.assertEqual(merc.sk, by_id[gen.MERC_SOURCE].sk)

    def test_every_ranged_troop_carries_its_ladder_cell(self):
        spec = rl.load_spec()
        for gen in GENERATORS:
            for t in built(gen):
                if t.role != "ranged":
                    continue
                tier = rl.engine_tier(t.level)
                self.assertEqual(t.sk["Bow"], rl.skill_cell(gen.CULTURE, tier, spec), t.id)
                for roster in t.weapons:
                    self.assertEqual(roster[0], rl.ladder_id(gen.CULTURE, "Bow", tier), t.id)

    def test_every_party_template_names_only_defined_troops(self):
        for gen in GENERATORS:
            ids = {t.id for t in built(gen)}
            for tid, stacks in gen.party_templates():
                for troop, lo, hi in stacks:
                    self.assertIn(troop, ids, tid)
                    self.assertLessEqual(lo, hi, tid)

    def test_the_lord_template_is_this_generators_alone_at_the_mens_target(self):
        import rebalance_party_template_maxes as rb
        for gen in GENERATORS:
            tid = f"kingdom_hero_party_{gen.CULTURE}_template"
            self.assertEqual(rb.culture_for(tid), (None, None), f"second writer on {gen.REALM.pt_marker}")
            stacks = dict(gen.party_templates())[tid]
            self.assertEqual(sum(hi for _troop, _lo, hi in stacks), rb.CULTURE_TARGETS["gondor"], tid)

    def test_every_troop_wears_its_own_realms_label_and_culture(self):
        for gen in GENERATORS:
            root = ET.fromstring(gen.troops_xml(built(gen), "\r\n").encode("utf-8"))
            for npc in root.findall("NPCCharacter"):
                self.assertTrue(npc.get("id").startswith(gen.CULTURE + "_"), npc.get("id"))
                self.assertIn(gen.REALM.label, npc.get("name"))
                self.assertEqual(npc.get("culture"), f"Culture.{gen.CULTURE}")


class SharedModuleTests(unittest.TestCase):
    def test_a_full_item_id_takes_no_prefix(self):
        realm = rt.Realm("x", "[X]", "x.py", "X-PT", {}, "x_y", "Y")
        a, b = rt.armour(realm, ("=sk_gd_cape_a", "cape_b"), ("chest_a", "chest_b"), None, None, None)
        self.assertEqual(a["Head"], "sk_gd_cape_a")
        self.assertEqual(b["Head"], "sk_ar_art_cape_b")
        self.assertEqual(a["Body"], "sk_ar_art_chest_a")
        self.assertNotIn("Leg", a)


if __name__ == "__main__":
    unittest.main()
