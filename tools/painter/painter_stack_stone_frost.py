"""Painter-side script (run through painter_run.py): the "cold dark stone"
layer stack, first used on the Carn Dum map icon (2026-10-16). Idempotent:
it deletes its own layers by name and rebuilds them. Needs an OCIO project
(painter_new_project.py) with the mesh maps baked.

Bottom to top:
  Stone base      stone_slate material, TRIPLANAR (a Tripo re-UV has hundreds
                  of islands: UV projection shows a seam at every one)
  Cavity grime    dark fill, mask = mg_dirt generator (triplanar)
  Edge wear       lighter fill, mask = mg_metal_edge_wear generator (triplanar)
  Frost and snow  pale fill on upward faces, mask = mg_world_space_normals

ARGS (all optional): stone, grime, edge, frost (RGB lists 0..1, sRGB),
triplanar_scale (8), frost_balance (0.8), frost_contrast (0.85),
frost_opacity (0.85), dirt_level (0.8), wear_level (0.5), skip_frost (false),
stone_relief (false: the slate gives color and roughness only).

Tuning notes from the first run:
- The world-space-normal mask maps normal.y [-1, 1] to [0, 1], so walls sit at
  0.5, and ws_balance is the threshold: 0.4 frosted every wall, 0.8 keeps it to
  faces more than ~50 deg upward.
- Generator grunge runs in UV space unless Use_Triplanar=1: blotches that
  break at every island.
- Triplanar scale 20 read as noise at map distance; 8 reads as blocks.
- Layer.set_opacity needs a channel outside a mask stack.
- The slate's own normal and height buried the windows and arches the baked
  normal carries: in the Kit it read as melted wax. stone_relief stays off
  unless the mesh has no architecture to show.
"""
import substance_painter.colormanagement as cm
import substance_painter.layerstack as ls
import substance_painter.resource as res
import substance_painter.textureset as ts

A = ARGS  # noqa: F821  (injected by painter_run.py)
STONE = A.get("stone", [0.14, 0.145, 0.16])
GRIME = A.get("grime", [0.03, 0.03, 0.035])
EDGE = A.get("edge", [0.28, 0.29, 0.32])
FROST = A.get("frost", [0.78, 0.82, 0.88])
LAYERS = ["Stone base", "Cavity grime", "Edge wear", "Frost and snow"]

tset = ts.all_texture_sets()[0]
stack = tset.get_stack()
CT = ts.ChannelType


def rgb(c):
    # a fill takes raw numbers as linear whatever the tag: see painter_stack_angmar_fortress.py
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return cm.Color(*lin, cm.GenericColorSpace.Working)


def find(name, usage):
    # resource.search("stone_slate") finds nothing: list the usage and filter
    for r in res.search(usage):
        if r.identifier().name.split("/")[0] == name:
            return r.identifier()
    raise LookupError(f"{usage} {name}")


def color_fill(name, color, rough):
    fill = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack))
    fill.set_name(name)
    fill.active_channels = {CT.BaseColor, CT.Roughness}
    fill.set_source(CT.BaseColor, rgb(color))
    fill.set_source(CT.Roughness, cm.Color(rough, rough, rough, cm.GenericColorSpace.Raw))
    return fill


def masked_by(fill, generator, params):
    fill.add_mask(ls.MaskBackground.Black)
    gen = ls.insert_generator_effect(
        ls.InsertPosition.inside_node(fill, ls.NodeStack.Mask), find(generator, "u:generator"))
    gen.get_source().set_parameters(params)


with ls.ScopedModification("TAOM stone and frost stack"):
    for n in list(ls.get_root_layer_nodes(stack)):
        if n.get_name() in LAYERS:
            ls.delete_node(n)

    base = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack))
    base.set_name("Stone base")
    base.set_material_source(find("stone_slate", "u:material"))
    if not A.get("stone_relief", False):
        base.active_channels = {CT.BaseColor, CT.Roughness}
    base.get_material_source().set_parameters({
        "stone_color": rgb(STONE), "stone_roughness": 0.78,
        "color_variation": 0.25, "slate_density": 0.45})
    base.set_projection_mode(ls.ProjectionMode.Triplanar)
    proj_params = base.get_projection_parameters()
    scale = float(A.get("triplanar_scale", 8.0))
    proj_params.uv_transformation.scale = [scale, scale]
    base.set_projection_parameters(proj_params)

    masked_by(color_fill("Cavity grime", GRIME, 0.92), "mg_dirt",
              {"Use_Triplanar": 1, "dirt_level": float(A.get("dirt_level", 0.8))})
    masked_by(color_fill("Edge wear", EDGE, 0.7), "mg_metal_edge_wear",
              {"Use_Triplanar": 1, "Wear_Level": float(A.get("wear_level", 0.5))})
    if not A.get("skip_frost", False):
        frost = color_fill("Frost and snow", FROST, 0.85)
        for ch in (CT.BaseColor, CT.Roughness):
            frost.set_opacity(float(A.get("frost_opacity", 0.85)), ch)
        masked_by(frost, "mg_world_space_normals", {
            "ws_balance": float(A.get("frost_balance", 0.8)),
            "ws_contrast": float(A.get("frost_contrast", 0.85))})

RESULT = {"layers_top_first": [n.get_name() for n in ls.get_root_layer_nodes(stack)]}
