#!/usr/bin/env python3
"""Unit tests for tools/generate_enlistment_rosters.py's creature-rider skip, on synthetic data.

Run:  python -m unittest tools.tests.test_generate_enlistment_rosters

A rider of a mount-locked creature (the giant spider, the war elephant, the Mumakil) is never an enlistment donor
(Mike 2026-09-29): the generator emits no mounts, so a spider rider's kit would hand a goblin "cavalry" enlistee a
spider rider's gear on foot. The traps: dropping a horse or war-ram rider or a troop with no mount, reading the mount
from a civilian roster the troop never rides into battle, reading it from the first battle roster only (the engine
draws each slot from any battle set), missing a Horse written directly under <Equipments> (the engine writes it over
that slot in every set), letting the mount into the emitted slots, and matching a Monster by substring (the
spider_statue row). Record: docs/reviews/rca-keyforce-art-wiring-2026-09-29.md, Codex pass C1 and fix review R1.
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_enlistment_rosters as ger  # noqa: E402

HORSES = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    '<Items>\n'
    '  <Item id="spider_mount_mountain_a1" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.spider" body_length="100" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="taom_war_elephant" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.taom_war_elephant" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="taom_mumakil" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.taom_mumakil" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="war_horse_a" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.horse" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="taom_war_ram_a" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.taom_war_ram" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="spider_statue_mount" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.spider_statue" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="spider_saddle_like" mesh="m" Type="HorseHarness">\n'
    '    <ItemComponent><Armor family_type="1" /></ItemComponent>\n'
    '  </Item>\n'
    '</Items>\n'
)


class CreatureMountItemIdsTests(unittest.TestCase):
    def test_reads_the_mount_locked_creatures_and_nothing_else(self):
        with tempfile.TemporaryDirectory() as items_dir:
            with open(os.path.join(items_dir, 'LOTRAOM_horses.xml'), 'w', encoding='utf-8') as f:
                f.write(HORSES)
            ids = ger.creature_mount_item_ids(items_dir)
        self.assertEqual({'spider_mount_mountain_a1', 'taom_war_elephant', 'taom_mumakil'}, ids)

    def test_a_missing_items_folder_is_an_error_not_an_empty_set(self):
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(SystemExit):
                ger.creature_mount_item_ids(os.path.join(root, 'absent'))


class DropCreatureRidersTests(unittest.TestCase):
    def test_drops_only_riders_of_a_creature_mount(self):
        by_culture = {
            'goblin': [
                {'id': 'goblin_lurker', 'level': 16, 'group': 'Ranged', 'slots': {}, 'mounts': []},
                {'id': 'goblin_spider_rider', 'level': 21, 'group': 'Cavalry', 'slots': {},
                 'mounts': ['spider_mount_mountain_a1']},
            ],
            'aserai': [
                {'id': 'harad_elephant_rider', 'level': 51, 'group': 'HorseArcher', 'slots': {},
                 'mounts': ['taom_war_elephant']},
                {'id': 'harad_horseman', 'level': 16, 'group': 'Cavalry', 'slots': {}, 'mounts': ['war_horse_a']},
            ],
            'erebor': [
                {'id': 'ironpass_ram_breaker', 'level': 26, 'group': 'Cavalry', 'slots': {},
                 'mounts': ['taom_war_ram_a']},
            ],
        }
        kept, dropped = ger.drop_creature_riders(by_culture, {'spider_mount_mountain_a1', 'taom_war_elephant'})
        self.assertEqual(['goblin_lurker'], [t['id'] for t in kept['goblin']])
        self.assertEqual(['harad_horseman'], [t['id'] for t in kept['aserai']])
        self.assertEqual(['ironpass_ram_breaker'], [t['id'] for t in kept['erebor']])
        self.assertEqual(['goblin_spider_rider', 'harad_elephant_rider'], sorted(dropped))


TROOPS = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    '<NPCCharacters>\n'
    '  <NPCCharacter id="goblin_spider_rider" level="21" default_group="Cavalry" occupation="Soldier"\n'
    '                culture="Culture.goblin">\n'
    '    <Equipments>\n'
    '      <EquipmentRoster civilian="true">\n'
    '        <equipment slot="Horse" id="Item.war_horse_a" />\n'
    '      </EquipmentRoster>\n'
    '      <EquipmentRoster>\n'
    '        <equipment slot="Item0" id="Item.wm_gundabad_axe_a01" />\n'
    '        <equipment slot="Body" id="Item.sk_md_orc_inf_chest_med_b" />\n'
    '        <equipment slot="Horse" id="Item.spider_mount_mountain_a1" />\n'
    '      </EquipmentRoster>\n'
    '    </Equipments>\n'
    '  </NPCCharacter>\n'
    '</NPCCharacters>\n'
)


def goblin(troop_id, equipments):
    """One goblin Cavalry NPCCharacter whose <Equipments> body is `equipments`."""
    return (f'  <NPCCharacter id="{troop_id}" level="21" default_group="Cavalry" occupation="Soldier"\n'
            f'                culture="Culture.goblin">\n'
            f'    <Equipments>\n{equipments}    </Equipments>\n'
            f'  </NPCCharacter>\n')


AXE = '        <equipment slot="Item0" id="Item.wm_gundabad_axe_a01" />\n'
SPIDER = '        <equipment slot="Horse" id="Item.spider_mount_mountain_a1" />\n'
HORSE = '        <equipment slot="Horse" id="Item.war_horse_a" />\n'


def roster(body, attrs=''):
    return f'      <EquipmentRoster{attrs}>\n{body}      </EquipmentRoster>\n'


def override(item_id):
    """An <equipment slot="Horse"> directly under <Equipments>: the engine writes it over that slot in every set."""
    return f'      <equipment slot="Horse" id="{item_id}" />\n'


# One goblin troop per rule of how the engine assembles a troop's battle sets, each named for its case.
MIXED_ROSTERS = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    '<NPCCharacters>\n'
    + goblin('spider_in_second_set', roster(AXE)
             + roster(AXE + '        <equipment slot="Body" id="Item.sk_md_orc_inf_chest_med_b" />\n' + SPIDER))
    + goblin('horse_then_spider', roster(HORSE) + roster(SPIDER))
    + goblin('spider_then_horse', roster(SPIDER) + roster(HORSE))
    + goblin('spider_as_override', roster(AXE) + override('Item.spider_mount_mountain_a1'))
    + goblin('horse_overrides_spider', roster(AXE + SPIDER) + override('Item.war_horse_a'))
    + goblin('override_clears_spider', roster(AXE + SPIDER) + override(''))
    + goblin('civilian_typed_spider', roster(SPIDER, ' equipmentType="Civilian"') + roster(AXE))
    + goblin('goblin_horseman', roster(HORSE) + roster(HORSE))
    + '</NPCCharacters>\n'
)


def parse_troops_from(xml):
    """parse_troops() over one troops_goblin.xml holding `xml`."""
    with tempfile.TemporaryDirectory() as troops_dir:
        with open(os.path.join(troops_dir, 'troops_goblin.xml'), 'w', encoding='utf-8') as f:
            f.write(xml)
        with mock.patch.object(ger, 'TROOPS_DIR', troops_dir):
            return ger.parse_troops()


class ParseTroopsMountTests(unittest.TestCase):
    def test_the_mounts_come_from_the_battle_rosters_and_are_never_a_slot(self):
        troop = parse_troops_from(TROOPS)['goblin'][0]
        self.assertEqual(['spider_mount_mountain_a1'], troop['mounts'],
                         'the civilian roster is not what the troop rides')
        self.assertNotIn('Horse', troop['slots'], 'a mount must never reach the emitted slots')

    def test_a_creature_the_engine_mounts_in_any_battle_set_drops_the_troop(self):
        kept, dropped = ger.drop_creature_riders(parse_troops_from(MIXED_ROSTERS), {'spider_mount_mountain_a1'})
        self.assertEqual(['horse_then_spider', 'spider_as_override', 'spider_in_second_set', 'spider_then_horse'],
                         sorted(dropped))
        self.assertEqual(['civilian_typed_spider', 'goblin_horseman', 'horse_overrides_spider', 'override_clears_spider'],
                         sorted(t['id'] for t in kept['goblin']))

    def test_the_kit_comes_from_the_first_battle_set(self):
        troops = {t['id']: t for t in parse_troops_from(MIXED_ROSTERS)['goblin']}
        self.assertEqual({'Item0': 'wm_gundabad_axe_a01'}, troops['spider_in_second_set']['slots'])
        self.assertEqual({'Item0': 'wm_gundabad_axe_a01'}, troops['civilian_typed_spider']['slots'],
                         'an equipmentType="Civilian" roster is not a battle set')


class SeedMarkerTests(unittest.TestCase):
    def test_the_seed_marker_is_a_legal_xml_comment(self):
        # XML forbids "--" inside a comment, and the marker once named the flag, so every
        # --seed-missing run refused its own output as malformed (2026-10-15).
        import xml.etree.ElementTree as ET
        ET.fromstring(f'<EquipmentRosters>\n    {ger.SEED_MARKER}\n</EquipmentRosters>')


CLASSES = {'composite_bow': 'Bow', 'ladder_harad_bow_x': 'Bow', 'arrows_a': 'Arrows', 'sword_a': 'OneHanded',
           'ladder_other_bow': 'Bow'}


class IssueLauncherTests(unittest.TestCase):
    def test_a_listed_cultures_archer_cell_swaps_only_the_bow(self):
        slots = {'Item0': 'ladder_harad_bow_x', 'Item1': 'arrows_a', 'Item2': 'sword_a', 'Body': 'b'}
        out = ger.apply_issue_launcher('aserai', 'archer', 'soldier', slots, CLASSES)
        self.assertEqual({'Item0': 'steppe_heavy_bow', 'Item1': 'arrows_a', 'Item2': 'sword_a', 'Body': 'b'}, out)
        self.assertEqual('ladder_harad_bow_x', slots['Item0'], 'the input is not mutated')

    def test_each_rank_has_its_own_bow_for_each_harad_culture(self):
        for culture in ('aserai', 'abanissa', 'shaghana'):
            got = [ger.apply_issue_launcher(culture, 'archer', r, {'Item0': 'ladder_harad_bow_x'}, CLASSES)['Item0']
                   for r in ger.RANKS]
            self.assertEqual(['composite_bow', 'steppe_heavy_bow', 'composite_steppe_bow', 'steppe_war_bow'], got)

    def test_other_assignments_and_cultures_are_untouched(self):
        slots = {'Item0': 'ladder_other_bow', 'Item1': 'arrows_a'}
        self.assertEqual(slots, ger.apply_issue_launcher('aserai', 'cavalry', 'soldier', slots, CLASSES))
        self.assertEqual(slots, ger.apply_issue_launcher('khuzait', 'archer', 'soldier', slots, CLASSES))

    def test_a_listed_archer_cell_without_a_bow_is_left_alone(self):
        slots = {'Item0': 'sword_a'}
        self.assertEqual(slots, ger.apply_issue_launcher('aserai', 'archer', 'recruit', slots, CLASSES))


def rosters_xml(*ids, comment_before=None):
    parts = ['<?xml version="1.0"?>\r\n<EquipmentRosters>\r\n']
    for rid in ids:
        if comment_before == rid:
            parts.append('    <!-- Hand-tuned bows (2026-10-15): keep -->\r\n')
        parts.append(f'    <EquipmentRoster id="{rid}" culture="Culture.x">\r\n        <EquipmentSet>\r\n'
                     f'            <Equipment slot="Item0" id="Item.a" />\r\n        </EquipmentSet>\r\n'
                     f'    </EquipmentRoster>\r\n')
    parts.append('</EquipmentRosters>')
    return ''.join(parts)


class ReseedTests(unittest.TestCase):
    def test_strip_removes_only_the_exact_culture_and_its_hand_tuned_comment(self):
        text = rosters_xml('enlist_aserai_archer_recruit', 'enlist_aserai2_archer_recruit',
                           'enlist_aserai_archer_soldier', 'enlist_khuzait_archer_recruit',
                           'enlist_default_archer_recruit', comment_before='enlist_aserai_archer_soldier')
        out, removed, _where = ger.strip_culture_rosters(text, 'aserai')
        self.assertEqual(2, removed)
        self.assertNotIn('"enlist_aserai_archer_recruit"', out)
        self.assertNotIn('"enlist_aserai_archer_soldier"', out)
        self.assertNotIn('Hand-tuned', out)
        for keep in ('enlist_aserai2_archer_recruit', 'enlist_khuzait_archer_recruit', 'enlist_default_archer_recruit'):
            self.assertIn(f'"{keep}"', out)
        ET.fromstring(out)

    def test_a_comment_above_a_kept_roster_survives(self):
        text = rosters_xml('enlist_aserai_archer_recruit', 'enlist_khuzait_archer_recruit',
                           comment_before='enlist_khuzait_archer_recruit')
        out, _, _where = ger.strip_culture_rosters(text, 'aserai')
        self.assertIn('Hand-tuned', out)

    def test_strip_reports_where_the_first_removed_roster_stood(self):
        text = rosters_xml('enlist_khuzait_archer_recruit', 'enlist_aserai_archer_recruit',
                           'enlist_aserai_archer_soldier')
        out, removed, where = ger.strip_culture_rosters(text, 'aserai')
        self.assertEqual(2, removed)
        self.assertEqual(text.index('    <EquipmentRoster id="enlist_aserai_archer_recruit"'), where)
        self.assertTrue(out[where:].startswith('</EquipmentRosters>'))

    def test_strip_reports_no_position_when_nothing_matched(self):
        text = rosters_xml('enlist_khuzait_archer_recruit')
        out, removed, where = ger.strip_culture_rosters(text, 'aserai')
        self.assertEqual((text, 0, None), (out, removed, where))

    def test_a_self_closing_roster_does_not_swallow_its_neighbour(self):
        text = ('<EquipmentRosters>\r\n'
                '    <EquipmentRoster id="enlist_aserai_archer_recruit" culture="Culture.x" />\r\n'
                '    <EquipmentRoster id="enlist_khuzait_archer_recruit" culture="Culture.x">\r\n'
                '        <EquipmentSet />\r\n'
                '    </EquipmentRoster>\r\n'
                '</EquipmentRosters>')
        out, removed, _where = ger.strip_culture_rosters(text, 'aserai')
        self.assertEqual(1, removed)
        self.assertIn('"enlist_khuzait_archer_recruit"', out)
        self.assertNotIn('enlist_aserai_archer_recruit', out)
        ET.fromstring(out)

    def run_main(self, argv):
        with mock.patch.object(sys, 'argv', ['gen'] + argv), \
                mock.patch.object(ger, 'build_indexes', side_effect=AssertionError('indexes built')):
            return ger.main()

    def test_reseed_refuses_without_culture_or_apply(self):
        for argv in (['--reseed'], ['--reseed', '--apply'], ['--reseed', '--culture', 'aserai']):
            with self.assertRaises(SystemExit) as cm:
                self.run_main(argv)
            self.assertIn('--reseed', str(cm.exception.code))


class EverythingExists:
    """A stand-in index that contains every id."""

    def __contains__(self, _item):
        return True


PIPE_CLASSES = {'bow_x': 'Bow', 'arrows_a': 'Arrows', 'composite_bow': 'Bow', 'steppe_heavy_bow': 'Bow',
                'composite_steppe_bow': 'Bow', 'steppe_war_bow': 'Bow'}
ARCHER = {'id': 'archer_a', 'level': 10, 'group': 'Ranged', 'mounts': [],
          'slots': {'Body': 'body_a', 'Item0': 'bow_x', 'Item1': 'arrows_a'}}

OLD_FILE = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n<EquipmentRosters>\r\n'
    '\r\n    <!-- KHUZAIT -->\r\n\r\n'
    '    <EquipmentRoster id="enlist_khuzait_archer_recruit" culture="Culture.khuzait">\r\n'
    '        <EquipmentSet>\r\n            <Equipment slot="Item0" id="Item.k" />\r\n        </EquipmentSet>\r\n'
    '    </EquipmentRoster>\r\n'
    '\r\n    <!-- SHAGHANA -->\r\n\r\n'
    '    <EquipmentRoster id="enlist_shaghana_archer_recruit" culture="Culture.shaghana">\r\n'
    '        <EquipmentSet>\r\n            <Equipment slot="Item0" id="Item.old" />\r\n        </EquipmentSet>\r\n'
    '    </EquipmentRoster>\r\n'
    '\r\n    <!-- NEXT -->\r\n\r\n'
    '    <EquipmentRoster id="enlist_next_archer_recruit" culture="Culture.next">\r\n'
    '        <EquipmentSet>\r\n            <Equipment slot="Item0" id="Item.n" />\r\n        </EquipmentSet>\r\n'
    '    </EquipmentRoster>\r\n'
    '\r\n</EquipmentRosters>\r\n'
)


class PipelineTests(unittest.TestCase):
    def run_main(self, argv, troops=None):
        troops = {'shaghana': [dict(ARCHER)]} if troops is None else troops
        buf = io.StringIO()
        with mock.patch.object(sys, 'argv', ['gen'] + argv), \
                mock.patch.object(ger, 'build_indexes',
                                  return_value=(EverythingExists(), EverythingExists(), PIPE_CLASSES, {})), \
                mock.patch.object(ger, 'parse_troops', return_value=troops), \
                mock.patch.object(ger, 'game_modules', return_value='nowhere'), \
                mock.patch.object(ger, 'creature_mount_item_ids', return_value=set()), \
                contextlib.redirect_stdout(buf):
            ger.main()
        return buf.getvalue()

    def test_the_launcher_swap_precedes_duplicate_suppression(self):
        # One donor serves every rank, so the donor kits are identical; only the issued launchers differ.
        out = self.run_main(['--culture', 'shaghana'])
        rows = [ln for ln in out.splitlines() if ln.strip().startswith('shaghana') and ' archer ' in ln]
        self.assertEqual(4, len(rows))
        self.assertFalse(any('SUPPRESSED' in ln for ln in rows), rows)
        soldier = [ln for ln in rows if ' soldier ' in ln][0]
        self.assertIn('Item0=steppe_heavy_bow', soldier)

    def reseed(self, path):
        return self.run_main(['--culture', 'shaghana', '--apply', '--reseed', '--out', path])

    def test_reseed_replaces_in_place_and_is_idempotent(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'rosters.xml')
            with open(path, 'wb') as fh:
                fh.write(OLD_FILE.encode('utf-8'))
            self.reseed(path)
            first = open(path, 'rb').read()
            self.reseed(path)
            second = open(path, 'rb').read()
        self.assertEqual(first, second)
        text = first.decode('utf-8')
        self.assertNotIn('seed-missing', text)
        self.assertNotIn('Item.old', text)
        self.assertNotIn('enlist_default_', text, 'reseed does not run the defaults loop')
        shaghana = text.index('id="enlist_shaghana_archer_recruit"')
        self.assertGreater(shaghana, text.index('<!-- SHAGHANA -->'))
        self.assertLess(shaghana, text.index('<!-- NEXT -->'))
        self.assertIn('id="enlist_shaghana_archer_soldier"', text)
        ET.fromstring(text)

    def test_reseed_of_a_culture_with_no_rosters_falls_back_to_the_marker_append(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'rosters.xml')
            bare = '<?xml version="1.0"?>\r\n<EquipmentRosters>\r\n</EquipmentRosters>\r\n'
            with open(path, 'wb') as fh:
                fh.write(bare.encode('utf-8'))
            self.reseed(path)
            text = open(path, 'rb').read().decode('utf-8')
        self.assertIn(ger.SEED_MARKER, text)
        self.assertIn('id="enlist_shaghana_archer_soldier"', text)
        self.assertNotIn('enlist_default_', text)


class LauncherIdCheckTests(unittest.TestCase):
    def test_a_launcher_id_missing_from_the_universe_fails_closed(self):
        universe = {i for ranks in ger.ISSUE_LAUNCHER.values() for i in ranks.values()}
        universe.discard('steppe_war_bow')
        with self.assertRaises(SystemExit) as cm:
            ger.check_issue_launchers(universe)
        self.assertIn('steppe_war_bow', str(cm.exception.code))

    def test_every_launcher_present_passes(self):
        ger.check_issue_launchers({i for ranks in ger.ISSUE_LAUNCHER.values() for i in ranks.values()})


class FileHeaderTests(unittest.TestCase):
    def test_header_carries_the_abanissa_and_launcher_sentences(self):
        header = ger.file_header()
        self.assertIn('ISSUE_LAUNCHER', header)
        self.assertIn('abanissa binds', header)
        self.assertIn('builds their rosters from those trees', header)
        ET.fromstring(header + '</EquipmentRosters>')


if __name__ == '__main__':
    unittest.main()
