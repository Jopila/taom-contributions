"""Painter-side script (run through painter_run.py): tiling texture sets for an Angmar
fortress, painted on the flat planes of tools/blender/make_tiling_planes.py (one texture set
per material, named carn_dum_stone, carn_dum_paving, carn_dum_iron, carn_dum_dark). Painter's
procedurals repeat across the 0..1 plane, so the export tiles on a mesh with world-scale UVs
(tools/oneoff/blender_build_carn_dum_icon.py: 3 m per repeat for stone and paving, 1.5 m for
iron). Idempotent: it deletes its own layers by name and rebuilds them. Needs an OCIO project
(painter_new_project.py without a high).

  stone   dark charcoal-blue ashlar (bricks_01 relief), dark mortar in the joints, lighter
          stone variation, grime, a little frost
  paving  grey-blue flagstones (tile_random_2 relief), per-stone variation, frost patches
  iron    wrought iron: mid-dark metal, fine scratches in the roughness, dark patina, a
          little rust, a low hammered relief (v6: the v5 iron, black with stone grunge relief,
          read as rock)
  dark    flat near-black, for window and gate openings
  roof    dark slate shingles (bricks_01 in small courses), variation, light frost (only when
          the project has a carn_dum_roof set)

ARGS (all optional): stone, mortar, paving, frost, iron, rust, dark (RGB 0..1, sRGB),
blocks ([columns, rows] per repeat, default [3, 6]), flags (flagstones per side, 5),
frost_stone (0.18), frost_paving (0.6), variation (0.35), rust_amount (0.06), roof.

First pass on Carn Dum (2026-10-16): rust at 0.35 turned the thin horns brown instead of
black, and stone variation at 0.55 read as mottled concrete; both are now arguments.
"""
import substance_painter.colormanagement as cm
import substance_painter.layerstack as ls
import substance_painter.resource as res
import substance_painter.textureset as ts

A = ARGS  # noqa: F821  (injected by painter_run.py)
CT = ts.ChannelType
PREFIX = "TAOM "
SETS = {t.name(): t for t in ts.all_texture_sets()}


def rgb(c):
    # a fill takes raw numbers as linear whatever the tag: see painter_stack_angmar_fortress.py
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return cm.Color(*lin, cm.GenericColorSpace.Working)


def gray(v):
    return cm.Color(v, v, v, cm.GenericColorSpace.Raw)


def find(name):
    for kw in ("brick", "stone", "pave", "grunge", "tile", "noise", name.split("/")[0]):
        for r in res.search(kw):
            if r.identifier().name == name:
                return r.identifier()
    raise LookupError(name)


def clear(stack):
    for n in list(ls.get_root_layer_nodes(stack)):
        if n.get_name().startswith(PREFIX):
            ls.delete_node(n)


def fill(stack, name, channels, color=None, rough=None, metal=None, height=None):
    f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack))
    f.set_name(PREFIX + name)
    f.active_channels = set(channels)
    if color is not None:
        f.set_source(CT.BaseColor, rgb(color))
    if rough is not None:
        f.set_source(CT.Roughness, gray(rough))
    if metal is not None:
        f.set_source(CT.Metallic, gray(metal))
    if height is not None:
        f.set_source(CT.Height, gray(height))
    return f


def procedural_source(node, channel, name, params):
    node.set_source(channel, find(name))
    node.get_source(channel).set_parameters(params)


def mask_with(layer, name, params, opacity=None, channels=()):
    layer.add_mask(ls.MaskBackground.Black)
    m = ls.insert_fill(ls.InsertPosition.inside_node(layer, ls.NodeStack.Mask))
    m.set_source(None, find(name))
    m.get_source().set_parameters(params)
    if opacity is not None:
        for ch in channels:
            layer.set_opacity(opacity, ch)


blocks = tuple(A.get("blocks", [3, 6]))
brick = {"number": blocks, "gap": (0.02, 0.045), "bevel": (0.12, 0.12), "hardness": 0.8}
done = {}

with ls.ScopedModification("TAOM Angmar tiling sets"):
    st = SETS["carn_dum_stone"].get_stack()
    clear(st)
    base = fill(st, "stone blocks", [CT.BaseColor, CT.Roughness, CT.Height],
                color=A.get("stone", [0.13, 0.135, 0.15]), rough=0.82)
    procedural_source(base, CT.Height, "bricks_01/bricks_01", brick)
    var = fill(st, "stone variation", [CT.BaseColor], color=[0.21, 0.215, 0.235])
    mask_with(var, "grunge_stone_details/grunge_stone_details", {"balance": 0.45, "contrast": 0.4},
              float(A.get("variation", 0.35)), [CT.BaseColor])
    mortar = fill(st, "mortar", [CT.BaseColor, CT.Roughness], color=A.get("mortar", [0.045, 0.045, 0.05]),
                  rough=0.95)
    mask_with(mortar, "bricks_01/bricks_01", dict(brick, invert=1))
    grime = fill(st, "grime", [CT.BaseColor, CT.Roughness], color=[0.06, 0.06, 0.065], rough=0.9)
    mask_with(grime, "grunge_concrete_dirty/grunge_concrete_dirty", {}, 0.45, [CT.BaseColor, CT.Roughness])
    frost = fill(st, "frost", [CT.BaseColor, CT.Roughness], color=A.get("frost", [0.78, 0.82, 0.88]), rough=0.7)
    mask_with(frost, "grunge_concrete_dusty/grunge_concrete_dusty", {},
              float(A.get("frost_stone", 0.18)), [CT.BaseColor, CT.Roughness])
    done["carn_dum_stone"] = [n.get_name() for n in ls.get_root_layer_nodes(st)]

    pv = SETS["carn_dum_paving"].get_stack()
    clear(pv)
    flags = int(A.get("flags", 5))
    tile = {"amount_x": flags, "amount_y": flags, "interstice_thickness": 0.05, "bevel_distance": 0.15,
            "random_size": 0.6, "luminance_random": 0.0}
    pbase = fill(pv, "flagstones", [CT.BaseColor, CT.Roughness, CT.Height],
                 color=A.get("paving", [0.3, 0.31, 0.34]), rough=0.86)
    procedural_source(pbase, CT.Height, "tile_random_2/tile_random_2", tile)
    pvar = fill(pv, "flag variation", [CT.BaseColor], color=[0.2, 0.205, 0.22])
    mask_with(pvar, "tile_random_2/tile_random_2", dict(tile, luminance_random=1.0), 0.6, [CT.BaseColor])
    pjoint = fill(pv, "joints", [CT.BaseColor, CT.Roughness], color=[0.07, 0.07, 0.075], rough=0.95)
    mask_with(pjoint, "tile_random_2/tile_random_2", dict(tile, invert=1))
    pfrost = fill(pv, "frost", [CT.BaseColor, CT.Roughness], color=A.get("frost", [0.78, 0.82, 0.88]), rough=0.7)
    mask_with(pfrost, "grunge_concrete_dusty/grunge_concrete_dusty", {},
              float(A.get("frost_paving", 0.6)), [CT.BaseColor, CT.Roughness])
    done["carn_dum_paving"] = [n.get_name() for n in ls.get_root_layer_nodes(pv)]

    ir = SETS["carn_dum_iron"].get_stack()
    clear(ir)
    ibase = fill(ir, "wrought iron", [CT.BaseColor, CT.Roughness, CT.Metallic, CT.Height],
                 color=A.get("iron", [0.2, 0.2, 0.21]), rough=0.38, metal=1.0)
    procedural_source(ibase, CT.Height, "fractal_sum_base_2/fractal_sum_base_2", {})
    for ch in (CT.Height,):
        ibase.set_opacity(0.35, ch)
    scratches = fill(ir, "scratches", [CT.Roughness], rough=0.62)
    mask_with(scratches, "grunge_brushed/grunge_brushed", {}, 0.55, [CT.Roughness])
    patina = fill(ir, "patina", [CT.BaseColor, CT.Roughness], color=[0.09, 0.09, 0.1], rough=0.55)
    mask_with(patina, "grunge_concrete_old/grunge_concrete_old", {}, 0.45, [CT.BaseColor, CT.Roughness])
    rust = fill(ir, "rust", [CT.BaseColor, CT.Roughness, CT.Metallic], color=A.get("rust", [0.24, 0.11, 0.05]),
                rough=0.9, metal=0.0)
    mask_with(rust, "grunge_dirt/grunge_dirt", {}, float(A.get("rust_amount", 0.06)),
              [CT.BaseColor, CT.Roughness, CT.Metallic])
    done["carn_dum_iron"] = [n.get_name() for n in ls.get_root_layer_nodes(ir)]

    if "carn_dum_roof" in SETS:
        rf = SETS["carn_dum_roof"].get_stack()
        clear(rf)
        shingle = {"number": (5, 14), "gap": (0.015, 0.03), "bevel": (0.2, 0.35), "hardness": 0.75}
        rbase = fill(rf, "slate", [CT.BaseColor, CT.Roughness, CT.Height],
                     color=A.get("roof", [0.12, 0.125, 0.14]), rough=0.72)
        procedural_source(rbase, CT.Height, "bricks_01/bricks_01", shingle)
        rvar = fill(rf, "slate variation", [CT.BaseColor], color=[0.2, 0.205, 0.225])
        mask_with(rvar, "grunge_stone_details/grunge_stone_details", {"balance": 0.4}, 0.35, [CT.BaseColor])
        rgap = fill(rf, "shingle gaps", [CT.BaseColor], color=[0.04, 0.04, 0.045])
        mask_with(rgap, "bricks_01/bricks_01", dict(shingle, invert=1))
        rfrost = fill(rf, "frost", [CT.BaseColor, CT.Roughness], color=A.get("frost", [0.78, 0.82, 0.88]),
                      rough=0.7)
        mask_with(rfrost, "grunge_concrete_dusty/grunge_concrete_dusty", {}, 0.25, [CT.BaseColor, CT.Roughness])
        done["carn_dum_roof"] = [n.get_name() for n in ls.get_root_layer_nodes(rf)]

    dk = SETS["carn_dum_dark"].get_stack()
    clear(dk)
    fill(dk, "opening", [CT.BaseColor, CT.Roughness], color=A.get("dark", [0.015, 0.015, 0.02]), rough=0.95)
    done["carn_dum_dark"] = [n.get_name() for n in ls.get_root_layer_nodes(dk)]

RESULT = {"layers_top_first": done}
