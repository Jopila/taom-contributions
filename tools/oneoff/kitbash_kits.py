"""
Shared kit table and naming logic for the kitbash tools (Erebor, Gondor).

Pure Python, no bpy, so the Blender scripts and the plain scripts import the
same code. The catalog, catalog-to-md, mine, prefab-render and join-test tools
take --kit erebor|gondor (default erebor) and read their paths from KITS; the
Gondor-only tools (assembler, frame probe, z-fight attribution) read KITS["gondor"].

Two grouping schemes (KITS[kit]["group_by"]):

    object   Erebor. One FBX object is one piece. `X`, `X.lodN` / `X_lodN` and
             `bo_X` fold into the piece X (key = X lowercased).
    prefix   Gondor. A piece is a PART FAMILY: objects named `<prefix>` or
             `<prefix>.<sub>`, LODs per sub-object (`<prefix>.<sub>.lodN`), one
             `bo_<prefix>` body per prefix. In Bannerlord a meta-mesh is every
             object sharing the prefix, so the prefab meta_mesh_component name
             is the prefix. `.decal*` sub-objects hang far below the part and
             are listed, never measured or placed. A bare trailing `.lod` with
             no base sibling is the only copy of that sub-object and becomes
             its base (the engine reads a bare `.lod` as LOD level 0). Blender's
             `.NNN` duplicate suffix is stripped before the first-dot split, and
             a `.NNN` copy is dropped whenever its plain name is present.
"""

import os
import re

_SCENES = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes"

EREBOR_FAMILIES = [
    ("Ground tiles and floor trim", r"^SM_DW_Ground_"),
    ("Pillars", r"^SM_DW_Pillar_"),
    ("Platforms (terrace blocks)", r"^SM_DW_Platform_"),
    ("Roof", r"^SM_DW_Roof_"),
    ("Wall overlays (beams, columns, capitals)", r"^SM_DW_Wall_(Beam|Clmn)"),
    ("Walls (3 m modules)", r"^SM_DW_Wall_3m_"),
    ("Decoration", r"^SM_DW_Pile_"),
    ("Castle", r"^SM_DW_Castle_(?!.*Mrln)"),
    ("Castle merlons", r"^SM_DW_Castle_.*Mrln"),
    ("Props (unit size, unplaced)", r"^(kys_|dw_pillar|SD_DW)"),
]

# Gondor: matched against "<folder>/<file stem>"; the kit's FBX files are already its families.
GONDOR_FAMILIES = [
    ("Wall panels (3 m and 6 m, straight)", r"^meshes/gondor_wall_[36]m_straight"),
    ("Wall panels (curved)", r"^meshes/gondor_wall_[36]m_curved"),
    ("Trims (string courses, cornices)", r"^meshes/gondor_trims_"),
    ("Roofs", r"^meshes/gondor_roof"),
    ("Pillars", r"^meshes/gondor_pillars"),
    ("Buttress", r"^meshes/gondor_buttres"),
    ("Stairs", r"^meshes/gondor_stairs"),
    ("Ground and paving", r"^meshes/gondor_ground"),
    ("Railing", r"^meshes/gondor_railing"),
    ("Bridge", r"^meshes/gondor_bridge"),
    ("Castle tower and wall (meshes)", r"^meshes/gondor_castle"),
    ("Bricks, rubble and broken stone", r"^meshes/gondor_(brick|rubble|stone)"),
    ("Meshes, other", r"^meshes/"),
    ("Castle walls and towers", r"^walls/"),
    ("Statues", r"^statues/"),
    ("Comprehend", r"^Comprehend/"),
    ("Ships", r"^ships/"),
    ("Buildings (props and references)", r"^buildings/"),
]

KITS = {
    "erebor": {
        "src": os.path.join(_SCENES, "erebor"),
        "folders": ["meshes", "walls", "statues"],
        "kit_folders": ["meshes", "walls", "statues"],
        "exclude_files": [],
        "catalog_dir": r"E:\LOTRAOMAssets\_export\erebor\catalog",
        "catalog_json": r"E:\LOTRAOMAssets\_export\erebor\catalog\erebor_catalog.json",
        "export_dir": os.path.join(_SCENES, "erebor", "buildings"),
        "group_by": "object",
        "kit_re": re.compile(r"^(sm_dw_|kys_dw_|dw_pillar_|sd_dw_)", re.IGNORECASE),
        "families": EREBOR_FAMILIES,
        "family_on": "name",
    },
    "gondor": {
        "src": os.path.join(_SCENES, "Gondor"),
        "folders": ["meshes", "walls", "statues", "Comprehend", "ships", "buildings"],
        "kit_folders": ["meshes", "walls"],
        "exclude_files": [r"meshes\gate bak.fbx", r"meshes\VEG_Grass_Tussock_A.fbx"],
        "catalog_dir": r"E:\LOTRAOMAssets\_export\gondor\catalog",
        "catalog_json": r"E:\LOTRAOMAssets\_export\gondor\catalog\gondor_catalog.json",
        "export_dir": os.path.join(_SCENES, "Gondor", "buildings_taom"),
        "group_by": "prefix",
        "kit_re": None,
        "families": GONDOR_FAMILIES,
        "family_on": "folder_file",
    },
}

LOD_RE = re.compile(r"^(?P<base>.+?)[._]lod(?P<n>\d*)$", re.IGNORECASE)
_DUP_RE = re.compile(r"\.\d{3}$")


# The FBX export settings whose output the Modding Kit is known to import correctly
# (lond-cirion-buildings.md; frame proof in docs/kitbash/gondor/design-patterns.md). Never change
# bake_space_transform or the axes: the 180 degree frame rule is the Kit's own Y-up conversion.
FBX_EXPORT = {"use_selection": True, "object_types": {"MESH"}, "use_mesh_modifiers": True,
              "bake_space_transform": True, "add_leaf_bones": False, "path_mode": "AUTO"}


def get_kit(kit):
    kit = kit.lower()
    if kit not in KITS:
        raise ValueError(f"unknown kit {kit!r}; expected one of {sorted(KITS)}")
    return KITS[kit]


def catalog_name(kit_cfg, kit, folder, only):
    """The catalog JSON a run writes: the kit's catalog_json for a whole-kit run, else tagged."""
    if not only and not folder:
        return os.path.basename(kit_cfg["catalog_json"])
    name = f"{kit}_catalog"
    if folder:
        name += f"_{folder}"
    if only:
        name += f"_only_{only}"
    return name + ".json"


def report_prefix(kit_cfg, kit, folder, only):
    """Prefix of a run's DONE and log files: none for a whole-kit run (the Erebor names), else the
    catalog stem, so per-folder runs never share or clobber one DONE.txt."""
    if not only and not folder:
        return ""
    return catalog_name(kit_cfg, kit, folder, only)[:-len(".json")] + "_"


def pick_structural(subs):
    """The structural sub of a measured part: `wall`, else the bare prefix object, else the largest
    bbox volume (Lond Cirion rule 7: grip the structural body)."""
    for want in ("wall", ""):
        for s in subs:
            if s["sub"] == want:
                return s

    def volume(s):
        d = [s["bbox"]["max"][k] - s["bbox"]["min"][k] for k in range(3)]
        return d[0] * d[1] * d[2]

    return max(subs, key=volume)


def is_excluded(kit_cfg, folder, fname):
    """True when `folder\\fname` is listed in the kit's exclude_files (case-insensitive)."""
    key = f"{folder}\\{fname}".lower()
    return any(key == e.lower() for e in kit_cfg["exclude_files"])


# --------------------------------------------------------------- naming

def strip_dup(name):
    """Drop Blender's trailing .NNN duplicate suffix."""
    return _DUP_RE.sub("", name)


def split_name(name):
    """-> (kind, base, lod) with kind in base|lod|bo."""
    if name.lower().startswith("bo_"):
        return "bo", name[3:], None
    m = LOD_RE.match(name)
    if m:
        return "lod", m.group("base"), m.group("n")  # "" = bare .lod
    return "base", name, None


def is_decal(name):
    return ".decal" in name.lower()


def _prefix_parts(name):
    """-> (kind, prefix, sub, lod) for the prefix scheme; sub is lowercase, "" = the prefix itself."""
    s = strip_dup(name)
    kind, base, n = split_name(s)
    prefix, _, sub = base.partition(".")
    return kind, prefix.lower(), sub.lower(), n


def part_key(name, group_by):
    """The lowercase key of the part a name belongs to (bo_ and LOD names resolve to their part)."""
    if group_by == "object":
        return split_name(name)[1].lower()
    kind, prefix, _sub, _n = _prefix_parts(name)
    return prefix


# --------------------------------------------------------------- grouping

def _new_part():
    return {"subs": {}, "bo": None, "decals": [], "bare_lod": [], "warnings": []}


def _new_sub():
    return {"base": None, "lods": {}, "lod_pairs": []}


def group_parts(names, group_by):
    """Group object names into parts.

    -> {key: {"subs": {sub: {"base": name|None, "lods": {n: name}, "lod_pairs": [(n, name)]}},
              "bo": name|None, "decals": [name], "bare_lod": [name], "warnings": [str]}}

    object: key = base.lower(), one sub ("") per part, the old measure_file behaviour: the last
    base wins, and a part with LODs but no base promotes its first LOD (sorted by `n or "0"`).
    A part with only a bo_ has base None (an orphan body). `lod_pairs` keeps every LOD in
    encounter order, which a `lods` dict cannot when two names carry the same number.

    prefix: key = lowercase prefix, subs keyed by the lowercase text after the first dot. A `.NNN`
    copy is dropped (with a warning) whenever its plain name is present, for every slot: base,
    LOD and bo_. Two bases of one sub keep the first and warn.
    """
    parts = {}
    dropped = []
    if group_by == "prefix":
        names = sorted(names)  # Blender's native FBX importer creates objects in a varying order
        present = set(names)
        dropped = [n for n in names if strip_dup(n) != n and strip_dup(n) in present]
        names = [n for n in names if n not in set(dropped)]
    for name in names:
        if group_by == "object":
            kind, base, n = split_name(name)
            key, sub = base.lower(), ""
        else:
            if is_decal(name):
                kind, key, sub, n = "decal", part_key(name, "prefix"), "", None
            else:
                kind, key, sub, n = _prefix_parts(name)
        part = parts.setdefault(key, _new_part())
        if group_by == "object":
            part["subs"].setdefault("", _new_sub())  # a bo_-only part keeps its base None
        if kind == "decal":
            part["decals"].append(name)
            continue
        if kind == "bo":
            part["bo"] = name
            continue
        s = part["subs"].setdefault(sub, _new_sub())
        if kind == "lod":
            s["lod_pairs"].append((n, name))
            s["lods"][n] = name
            if n == "":
                part["bare_lod"].append(name)
            continue
        old = s["base"]
        if old is not None and group_by == "prefix":
            part["warnings"].append(f"duplicate base for {key}.{sub}: kept {old}, ignored {name}")
            continue
        s["base"] = name
    for n in dropped:
        parts.setdefault(part_key(n, "prefix"), _new_part())["warnings"].append(
            f"{n} ignored: {strip_dup(n)} is present")
    for key, part in parts.items():
        _promote_missing_bases(key, part, group_by)
        if group_by != "prefix":
            continue
        if part["subs"] and part["bo"] is None:
            part["warnings"].append(f"{key} has no bo_ body")
        for sub, s in part["subs"].items():
            if s["base"] is not None and "" in s["lods"]:
                part["warnings"].append(f"{s['lods']['']} beside {s['base']}: the engine reads a bare "
                                        f".lod as level 0, so both draw at LOD0")
    return parts


def base_names(names, group_by):
    """The object names a loader places for each part. An object kit keeps the old loader rule
    (plain names only); a prefix kit takes every sub-object's base from group_parts, so a bare
    `.lod` or a LOD-only sub-object is kept and decals, bodies and `.NNN` copies are not."""
    if group_by == "object":
        return {n for n in names if split_name(n)[0] == "base"}
    return {s["base"] for g in group_parts(names, "prefix").values()
            for s in g["subs"].values() if s["base"]}


def _promote_missing_bases(key, part, group_by):
    if group_by == "object":
        s = part["subs"].get("")
        if s is None or s["base"] is not None or not s["lod_pairs"]:
            return
        s["lod_pairs"].sort(key=lambda t: t[0] or "0")
        n0, name0 = s["lod_pairs"].pop(0)
        s["base"] = name0
        s["lods"] = {n: nm for n, nm in s["lod_pairs"]}
        part["warnings"].append(f"{key} has no base object; using {name0}")
        return
    for sub, s in part["subs"].items():
        if s["base"] is not None or not s["lod_pairs"]:
            continue
        label = f"{key}.{sub}" if sub else key
        if "" in s["lods"]:
            name0 = s["lods"].pop("")
            s["lod_pairs"] = [t for t in s["lod_pairs"] if t[0] != ""]
        else:
            s["lod_pairs"].sort(key=lambda t: int(t[0]))
            n0, name0 = s["lod_pairs"].pop(0)
            s["lods"].pop(n0, None)
            part["warnings"].append(f"{label} has no base object; using {name0}")
        s["base"] = name0


# --------------------------------------------------------------- kit membership

def kit_part_names(kit):
    """Lowercase part prefixes of every non-bo, non-decal mesh in the kit folders (prefix kits)."""
    import sys
    tools = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    if tools not in sys.path:
        sys.path.insert(0, tools)
    from audit_fbx_lods import read_fbx_meshes

    cfg = get_kit(kit)
    names = set()
    for folder in cfg["kit_folders"]:
        d = os.path.join(cfg["src"], folder)
        if not os.path.isdir(d):
            continue
        for fn in sorted(os.listdir(d)):
            if not fn.lower().endswith(".fbx") or is_excluded(cfg, folder, fn):
                continue
            for m in read_fbx_meshes(os.path.join(d, fn)):
                if m.name.lower().startswith("bo_") or is_decal(m.name):
                    continue
                names.add(part_key(m.name, "prefix"))
    return names


def kit_matcher(kit):
    """A predicate on a lowercase meta_mesh_component name: kit_re when the kit has one, else the
    part prefixes found in the kit's FBX files."""
    cfg = get_kit(kit)
    if cfg["kit_re"] is not None:
        rx = cfg["kit_re"]
        return lambda name: bool(rx.match(name))
    parts = kit_part_names(kit)
    return lambda name: name.lower() in parts
