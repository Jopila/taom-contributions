#!/usr/bin/env python3
"""Contract tests for tools/build_ds_skeleton_pack.py.

Run:  python -m pytest tools/tests/test_build_ds_skeleton_pack.py -q

Every tpac below is built byte by byte in the test (layout of tools/tpac_clone_metamesh.py's
docstring), so the tests do not depend on the game install or on the tool's own writer.
"""
import contextlib
import io
import os
import struct
import sys
import tempfile
import unittest
from pathlib import Path

import xxhash

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import build_ds_skeleton_pack as bds  # noqa: E402
import tpac_skeleton_dump as dump  # noqa: E402

SKELETON = bytes.fromhex("d5a335c6bbeadd45883eaa57e4196113")
SKELDEF = bytes.fromhex("377dd01120e76b40ab67c846f96a8771")
SKELUSR = bytes.fromhex("6dc06a9b46a5af40a55540d301ab4b2f")
OTHER_TYPE = bytes.fromhex("978b8fa07c19ea4bb95b53846cae834e")   # a metamesh, not a skeleton
PKG_A = bytes.fromhex("11" * 16)


def _sized(s: str) -> bytes:
    b = s.encode("ascii")
    return struct.pack("<i", len(b)) + b


def skeleton_payload(name: str, bones: int = 1) -> bytes:
    """A SkeletonDefinition segment payload tpac_skeleton_dump can parse."""
    out = _sized(name) + struct.pack("<i", bones)
    for i in range(bones):
        out += _sized("bone%d" % i) + struct.pack("<i", i - 1) + struct.pack("<16f", *([float(i)] * 16))
    return out


def build_item(name: str, item_guid: bytes, payloads, type_guid: bytes = SKELETON, seg_types=(SKELDEF, SKELUSR)):
    """(toc bytes with offsets 0, [blobs]); offsets are patched when the file is laid out."""
    meta = b"\x01\x02\x03" + name.encode("ascii")
    checksum = struct.pack("<Q", xxhash.xxh64(struct.pack("<q", len(meta)) + meta, seed=0).intdigest())
    toc = type_guid + item_guid + struct.pack("<I", 0) + _sized(name) + struct.pack("<q", len(meta)) + meta + checksum
    toc += struct.pack("<i", len(payloads))
    for i, p in enumerate(payloads):
        toc += struct.pack("<QQQ", 0, len(p), len(p)) + bytes([i + 1]) * 16 + seg_types[i % len(seg_types)]
        toc += struct.pack("<Q", xxhash.xxh64(p, seed=0).intdigest()) + b"\0" * 4 + b"\x00"
    toc += struct.pack("<i", 0)
    return toc, list(payloads)


def write_pack(path: Path, items, pkg_guid: bytes = PKG_A) -> None:
    """items: [(name, [payloads])] or [(name, [payloads], type_guid)]. Skeleton items by default."""
    built = []
    for i, spec in enumerate(items):
        name, payloads = spec[0], spec[1]
        type_guid = spec[2] if len(spec) > 2 else SKELETON
        guid = struct.pack("<I", i + 1) * 4
        built.append(build_item(name, guid, payloads, type_guid))
    toc_size = sum(len(t) for t, _ in built)
    cur = 36 + toc_size
    tocs, blobs = [], []
    for toc, pls in built:
        toc = bytearray(toc)
        # segment entries start after the seg-count field; find it from the tail: userdata count(4) + n*69
        n = len(pls)
        start = len(toc) - 4 - n * 69
        for k, p in enumerate(pls):
            struct.pack_into("<Q", toc, start + k * 69, cur)
            cur += len(p)
            blobs.append(p)
        tocs.append(bytes(toc))
    header = b"TPAC" + struct.pack("<I", 2) + pkg_guid + struct.pack("<I", len(built)) + struct.pack("<Q", toc_size)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(header + b"".join(tocs) + b"".join(blobs))


class Workspace:
    """A fake game dir (Armory with a loose tree), a server pack and an output path."""

    def __init__(self, root: Path):
        self.root = root
        self.game = root / "game"
        self.armory = self.game / "Modules" / "LOTRLOME_Armory"
        self.moduledata = self.armory / "ModuleData"
        self.assets = self.armory / "Assets"
        self.server = root / "server" / "skeletons.tpac"
        self.out = root / "out" / "taom_creature_skeletons.tpac"
        self.moduledata.mkdir(parents=True)

    def action_sets(self, *names):
        rows = "\n".join('  <action_set id="as_%d" skeleton="%s" movement_system="x"/>' % (i, n) for i, n in enumerate(names))
        (self.moduledata / "action_sets.xml").write_text("<action_sets>\n%s\n</action_sets>\n" % rows, encoding="utf-8")

    def argv(self, *extra):
        return ["--game-dir", str(self.game), "--server-pack", str(self.server), "--out", str(self.out), *extra]


def run(argv):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        code = bds.main(argv)
    return code, out.getvalue(), err.getvalue()


class NameCollectionTests(unittest.TestCase):
    def test_names_come_from_action_sets_monsters_and_the_rein_and_owner_attributes(self):
        with tempfile.TemporaryDirectory() as d:
            md = Path(d)
            (md / "action_sets.xml").write_text(
                '<action_sets>\n'
                '  <action_set id="a" skeleton="troll_skeleton_a" movement_system="x"/>\n'
                '  <action_set\n      id="b"\n      skeleton="spider_skeleton"\n      movement_system="x"/>\n'
                '  <action_set id="c" skeleton="troll_skeleton_a"/>\n'
                '</action_sets>', encoding="utf-8")
            (md / "monsters.xml").write_text(
                '<monsters><monster id="m" action_set="a" skeleton="elephant_skeleton"/></monsters>', encoding="utf-8")
            (md / "Monsters" / "LOTR").mkdir(parents=True)
            (md / "Monsters" / "LOTR" / "chariot.xml").write_text(
                '<Monsters><Monster id="c" skeleton="chariot_skeleton" owner_skeleton="dwarf_skeleton_a"'
                ' rein_skeleton="horse_harness_rein_skel"/></Monsters>', encoding="utf-8")
            self.assertEqual(
                bds.collect_skeleton_names(md),
                {"troll_skeleton_a", "spider_skeleton", "elephant_skeleton", "chariot_skeleton",
                 "dwarf_skeleton_a", "horse_harness_rein_skel"})

    def test_an_unreadable_xml_is_an_input_error(self):
        with tempfile.TemporaryDirectory() as d:
            (Path(d) / "action_sets.xml").write_text("<action_sets><oops", encoding="utf-8")
            with self.assertRaises(bds.PackError):
                bds.collect_skeleton_names(Path(d))

    def test_a_missing_action_sets_file_is_an_input_error(self):
        with tempfile.TemporaryDirectory() as d:
            with self.assertRaises(bds.PackError):
                bds.collect_skeleton_names(Path(d))


class BuildTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.ws = Workspace(Path(self._tmp.name))
        self.troll = skeleton_payload("troll_skeleton_a", 3)
        self.spider = skeleton_payload("spider_skeleton", 2)
        write_pack(self.ws.assets / "creature" / "troll.tpac", [("troll_skeleton_a", [self.troll, b"userdata-troll"])])
        write_pack(self.ws.assets / "creature" / "spider.tpac",
                   [("spider_skeleton", [self.spider, b"userdata-spider"]), ("sk_spider_mesh", [b"mesh"], OTHER_TYPE)])
        write_pack(self.ws.server, [("human_skeleton", [skeleton_payload("human_skeleton"), b"u"])])
        self.ws.action_sets("human_skeleton", "troll_skeleton_a", "spider_skeleton")

    def test_names_the_server_already_has_are_skipped(self):
        code, out, _ = run(self.ws.argv())
        self.assertEqual(code, 0)
        self.assertIn("troll_skeleton_a", out)
        self.assertIn("spider_skeleton", out)
        self.assertNotIn("human_skeleton", out)
        self.assertFalse(self.ws.out.exists(), "a dry run writes nothing")

    def test_a_declared_name_that_is_not_a_skeleton_item_counts_as_missing(self):
        self.ws.action_sets("troll_skeleton_a", "sk_spider_mesh")
        code, _, err = run(self.ws.argv())
        self.assertEqual(code, 2)
        self.assertIn("sk_spider_mesh", err)

    def test_a_skeleton_in_two_packages_fails_naming_both(self):
        write_pack(self.ws.assets / "other" / "troll_copy.tpac", [("troll_skeleton_a", [self.troll, b"userdata-other"])])
        code, _, err = run(self.ws.argv("--apply"))
        self.assertEqual(code, 2)
        self.assertIn("troll_skeleton_a", err)
        self.assertIn("troll.tpac", err)
        self.assertIn("troll_copy.tpac", err)
        self.assertFalse(self.ws.out.exists())

    def test_a_declared_skeleton_found_nowhere_fails(self):
        self.ws.action_sets("troll_skeleton_a", "ghost_skeleton")
        code, _, err = run(self.ws.argv("--apply"))
        self.assertEqual(code, 2)
        self.assertIn("ghost_skeleton", err)
        self.assertFalse(self.ws.out.exists())

    def test_the_loose_tree_wins_over_the_cooked_packs(self):
        write_pack(self.ws.armory / "AssetPackages" / "pack0.tpac", [("troll_skeleton_a", [self.troll, b"stale-cooked"])])
        code, _, _ = run(self.ws.argv("--apply"))
        self.assertEqual(code, 0)
        back = bds.read_package(self.ws.out.read_bytes())
        troll = next(i for i in back.items if i.name == "troll_skeleton_a")
        self.assertEqual(troll.blobs[1], b"userdata-troll")

    def test_items_are_copied_byte_for_byte(self):
        src = bds.read_package((self.ws.assets / "creature" / "troll.tpac").read_bytes()).items[0]
        code, _, _ = run(self.ws.argv("--apply"))
        self.assertEqual(code, 0)
        back = bds.read_package(self.ws.out.read_bytes())
        got = next(i for i in back.items if i.name == "troll_skeleton_a")
        self.assertEqual(got.blobs, src.blobs)
        self.assertEqual(got.item_guid, src.item_guid)
        self.assertEqual(got.checksum, src.checksum)
        # the TOC differs only in the two data offsets
        a, b = bytearray(got.toc), bytearray(src.toc)
        for it, buf in ((got, a), (src, b)):
            for s in it.segments:
                buf[s.entry_pos:s.entry_pos + 8] = b"\0" * 8
        self.assertEqual(bytes(a), bytes(b))

    def test_the_header_carries_the_real_toc_size_and_the_constant_guid(self):
        run(self.ws.argv("--apply"))
        data = self.ws.out.read_bytes()
        self.assertEqual(data[:8], b"TPAC" + struct.pack("<I", 2))
        self.assertEqual(data[8:24], bds.PACKAGE_GUID)
        count = struct.unpack_from("<I", data, 24)[0]
        toc_size = struct.unpack_from("<Q", data, 28)[0]
        self.assertEqual(count, 2)
        self.assertGreater(toc_size, 0)
        pkg = bds.read_package(data)
        self.assertEqual(toc_size, sum(len(i.toc) for i in pkg.items))
        self.assertEqual(pkg.items[0].segments[0].offset, 36 + toc_size)

    def test_every_item_passes_its_checksum(self):
        run(self.ws.argv("--apply"))
        pkg = bds.read_package(self.ws.out.read_bytes())
        for it in pkg.items:
            self.assertEqual(bds.tcm.expected_checksum(it), it.checksum, it.name)

    def test_a_source_item_with_a_bad_checksum_is_refused(self):
        path = self.ws.assets / "creature" / "troll.tpac"
        data = bytearray(path.read_bytes())
        item = bds.read_package(bytes(data)).items[0]
        start, end = bds.tcm._metadata_span(item)
        data[36 + end] ^= 0xFF
        path.write_bytes(bytes(data))
        code, _, err = run(self.ws.argv("--apply"))
        self.assertEqual(code, 2)
        self.assertIn("checksum", err)

    def test_output_is_sorted_by_name_and_deterministic(self):
        run(self.ws.argv("--apply"))
        first = self.ws.out.read_bytes()
        names = [i.name for i in bds.read_package(first).items]
        self.assertEqual(names, sorted(names))
        # a different directory order and mtime must not change the bytes
        write_pack(self.ws.assets / "aaa" / "zz.tpac", [("zzz_unrelated", [b"x", b"y"])])
        self.ws.out.unlink()
        run(self.ws.argv("--apply"))
        self.assertEqual(first, self.ws.out.read_bytes())

    def test_round_trip_through_the_skeleton_dump_parser(self):
        run(self.ws.argv("--apply"))
        for name, payload in (("troll_skeleton_a", self.troll), ("spider_skeleton", self.spider)):
            _, defdata, userdata = dump.find_skeleton(str(self.ws.out), name)
            self.assertIsNotNone(defdata, name)
            self.assertIsNotNone(userdata, name)
            raw = dump.read_segment_data(str(self.ws.out), defdata)
            self.assertEqual(raw, payload)
            parsed = dump.parse_skeleton_definition(raw)
            self.assertEqual(parsed["name"], name)

    def test_apply_refuses_when_nothing_is_needed(self):
        self.ws.action_sets("human_skeleton")
        code, out, _ = run(self.ws.argv("--apply"))
        self.assertEqual(code, 0)
        self.assertIn("nothing to build", out)
        self.assertFalse(self.ws.out.exists())


class CheckTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.ws = Workspace(Path(self._tmp.name))
        self.troll = skeleton_payload("troll_skeleton_a", 3)
        write_pack(self.ws.assets / "t.tpac", [("troll_skeleton_a", [self.troll, b"u1"])])
        write_pack(self.ws.server, [("human_skeleton", [skeleton_payload("human_skeleton"), b"u"])])
        self.ws.action_sets("human_skeleton", "troll_skeleton_a")

    def test_check_exits_0_when_the_committed_file_matches(self):
        run(self.ws.argv("--apply"))
        code, out, _ = run(self.ws.argv("--check"))
        self.assertEqual(code, 0)
        self.assertIn("up to date", out)

    def test_check_exits_1_when_the_armory_art_drifted(self):
        run(self.ws.argv("--apply"))
        write_pack(self.ws.assets / "t.tpac", [("troll_skeleton_a", [self.troll, b"u2-changed"])])
        code, _, err = run(self.ws.argv("--check"))
        self.assertEqual(code, 1)
        self.assertIn("differs", err)

    def test_check_exits_1_when_the_committed_file_is_absent(self):
        code, _, _ = run(self.ws.argv("--check"))
        self.assertEqual(code, 1)

    def test_check_exits_2_when_the_inputs_are_unreadable(self):
        run(self.ws.argv("--apply"))
        self.ws.server.unlink()
        code, _, err = run(self.ws.argv("--check"))
        self.assertEqual(code, 2)
        self.assertIn("server", err.lower())

    def test_check_exits_2_when_an_armory_package_is_unreadable(self):
        # The unreadable package might hold a second copy of a skeleton, so "up to date" is unproven.
        run(self.ws.argv("--apply"))
        (self.ws.assets / "broken.tpac").write_bytes(b"not a tpac")
        code, out, err = run(self.ws.argv("--check"))
        self.assertEqual(code, 2)
        self.assertNotIn("up to date", out)
        self.assertIn("unreadable", err)

    def test_check_never_writes(self):
        before = self.ws.out.exists()
        run(self.ws.argv("--check"))
        self.assertEqual(before, self.ws.out.exists())


if __name__ == "__main__":
    unittest.main()
