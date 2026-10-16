"""Painter-side script (run through painter_run.py): the Angmar fortress layer
stacks for a uniquely unwrapped, baked mesh (tools/blender/unwrap_texture_sets.py,
then painter_new_project.py with low_as_high). First used on the Carn Dum map
icon (2026-10-16). Idempotent: it deletes its own layers (prefix "TAOM ") and
rebuilds them.

The look Mike approved is the stone colour alone: no snow, rime, worn edges, soot,
water streaks or stone courses ("they won't show at 1K"; round 13 had all of them).
Every pattern is TRIPLANAR (world space), because the packer rotates islands.

Recipes, bottom to top:
  stone   dark blue-grey fill, slate grain (Overlay), light and dark tone patches,
          a cold tint, the baked occlusion; the paving set too (walks and tops are
          stone, Mike on round 14)
  roof    dark slate fill, tone patches, occlusion
  iron    iron_forged (wrought iron, no rust: Mike on the tiling pass)
  dark    near-black openings, darker in their depth

ARGS (all optional): sets ({"set name": "recipe"}; default: by name, see
RECIPE_BY_NAME), scale (triplanar repeats across the mesh, 8), any colour as an
RGB list (stone, stone_light, stone_dark, tint, roof, iron, dark, occlusion).

Painter 12.1.5 traps met on the way (rounds 1 to 13; the pipeline doc has the detail):
- A fill takes the raw numbers as LINEAR whatever the Color's tag: rgb() converts.
  A Substance material parameter (iron_color) takes sRGB: rgb_param().
- set_material_source turns every output of the material back on: set
  active_channels after it (the slate's normal tilted 20% of the texels).
- Height becomes normal per texel: with a 55 m mesh in one 1K tile even 3% relief
  tilted a quarter of the texels and the lit preview went blotchy. No height here.
- A feature under 2 texels (about 18 cm at 11 px/m) vanishes at 1K.
- bricks_01 is white in its joints (invert=1 paints the blocks).
"""
import substance_painter.colormanagement as cm
import substance_painter.layerstack as ls
import substance_painter.resource as res
import substance_painter.textureset as ts

A = ARGS  # noqa: F821  (injected by painter_run.py)
CT = ls.ChannelType
BM = ls.BlendingMode
PREFIX = "TAOM "
RECIPE_BY_NAME = {"towers": "stone", "walls": "stone", "town": "stone", "paving": "stone", "roof": "roof",
                  "iron": "iron", "dark": "dark"}
C = {"stone": [0.14, 0.145, 0.16], "stone_light": [0.23, 0.235, 0.25], "stone_dark": [0.09, 0.09, 0.1],
     "tint": [0.2, 0.215, 0.26], "roof": [0.11, 0.115, 0.13],
     "iron": [0.17, 0.17, 0.18], "dark": [0.022, 0.022, 0.027], "occlusion": [0.0, 0.0, 0.0]}
C.update({k: v for k, v in A.items() if k in C})
SCALE = float(A.get("scale", 8.0))
_cache = {}
SKIPPED = []


def rgb(c):
    """An sRGB colour for a fill: Painter takes a fill's numbers as working-space (linear)
    values whatever the Color's tag (0.2 tagged sRGB, Raw or Working all export as 0.486)."""
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return cm.Color(*lin, cm.GenericColorSpace.Working)


def rgb_param(c):
    """An sRGB colour for a Substance material parameter, which takes sRGB (the linear
    value made the iron near black)."""
    return cm.Color(*c, cm.GenericColorSpace.sRGB)


def gray(v):
    return cm.Color(v, v, v, cm.GenericColorSpace.Raw)


def find(name):
    """A shelf resource by its identifier name ("grunge_rock" or "grunge_rock/grunge_rock").
    resource.search misses underscored names, so list each usage once and filter."""
    for usage in ("u:procedural", "u:texture", "u:generator", "u:material", "u:filter"):
        if usage not in _cache:
            _cache[usage] = {r.identifier().name: r.identifier() for r in res.search(usage)}
        for full, ident in _cache[usage].items():
            if full == name or full.split("/")[0] == name:
                return ident
    raise LookupError(name)


def setp(source, params):
    """Set only the parameters the resource has (grunges differ: some have no balance);
    the names left out are reported in RESULT."""
    have = source.get_parameters()
    keep = {k: v for k, v in params.items() if k in have}
    SKIPPED.extend(sorted(set(params) - set(keep)))
    if keep:
        source.set_parameters(keep)


def triplanar(node, scale):
    node.set_projection_mode(ls.ProjectionMode.Triplanar)
    p = node.get_projection_parameters()
    p.uv_transformation.scale = [scale, scale]
    node.set_projection_parameters(p)


def layer(stack, name, channels, color=None, rough=None):
    f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack))
    f.set_name(PREFIX + name)
    f.active_channels = set(channels)
    if color is not None:
        f.set_source(CT.BaseColor, rgb(color))
    if rough is not None:
        f.set_source(CT.Roughness, gray(rough))
    return f


def opacity(node, value, channels):
    for ch in channels:
        node.set_opacity(value, ch)


def mask_gen(lay, generator, params):
    lay.add_mask(ls.MaskBackground.Black)
    g = ls.insert_generator_effect(ls.InsertPosition.inside_node(lay, ls.NodeStack.Mask), find(generator))
    setp(g.get_source(), params)


def mask_tex(lay, procedural, params, scale):
    lay.add_mask(ls.MaskBackground.Black)
    m = ls.insert_fill(ls.InsertPosition.inside_node(lay, ls.NodeStack.Mask))
    m.set_source(None, find(procedural))
    if params:
        setp(m.get_source(), params)
    triplanar(m, scale)


def tone(stack, name, color, procedural, params, scale, op):
    t = layer(stack, name, [CT.BaseColor], color)
    mask_tex(t, procedural, params, scale)
    opacity(t, op, [CT.BaseColor])


def occlusion(stack, op):
    """The baked occlusion into the albedo: corners, courtyards and wall bases darker.
    Depth still reads at map distance; the _s map's AO only dims ambient light."""
    d = layer(stack, "occlusion", [CT.BaseColor], C["occlusion"])
    mask_gen(d, "mg_ambient_occlusion", {"ao_invert": 1, "ao_balance": 0.5, "ao_contrast": 0.3})
    opacity(d, op, [CT.BaseColor])


def stone_colour(stack, base):
    """The approved stone: a plain fill (stone_slate alone lifted 0.11 to about 0.25
    whatever its stone_color), the slate as grain only, in Overlay, then broad tone."""
    layer(stack, "stone", [CT.BaseColor, CT.Roughness], base, 0.82)
    grain = layer(stack, "stone grain", [CT.BaseColor])
    grain.set_material_source(find("stone_slate"))
    setp(grain.get_material_source(), {"color_variation": 0.1, "slate_density": 0.4, "detail_intensity": 0.6})
    grain.active_channels = {CT.BaseColor}
    triplanar(grain, SCALE * 2)
    grain.set_blending_mode(BM.Overlay, CT.BaseColor)
    opacity(grain, 0.5, [CT.BaseColor])
    tone(stack, "patch tone", C["stone_light"], "grunge_rock", {"balance": 0.42, "contrast": 0.3}, SCALE * 0.6, 0.25)
    tone(stack, "deep tone", C["stone_dark"], "grunge_concrete_old", {}, SCALE * 0.35, 0.3)
    tone(stack, "cold tint", C["tint"], "grunge_dirt_large", {}, SCALE * 0.25, 0.22)


def stone(stack):
    stone_colour(stack, C["stone"])
    occlusion(stack, 0.45)


def roof(stack):
    layer(stack, "slate", [CT.BaseColor, CT.Roughness], C["roof"], 0.7)
    tone(stack, "slate tone", [0.2, 0.205, 0.225], "grunge_stone_details", {"balance": 0.4}, SCALE, 0.35)
    occlusion(stack, 0.35)


def iron(stack):
    base = layer(stack, "wrought iron", [CT.BaseColor, CT.Roughness, CT.Metallic])
    base.set_material_source(find("iron_forged"))
    setp(base.get_material_source(), {"iron_color": rgb_param(C["iron"]), "iron_roughness": 0.45,
                                      "scratches": 0.3})
    base.active_channels = {CT.BaseColor, CT.Roughness, CT.Metallic}
    triplanar(base, SCALE * 4)


def dark(stack):
    layer(stack, "opening", [CT.BaseColor, CT.Roughness], C["dark"], 0.95)
    occlusion(stack, 0.7)


RECIPES = {"stone": stone, "roof": roof, "iron": iron, "dark": dark}
wanted = A.get("sets")
done = {}
with ls.ScopedModification("TAOM Angmar fortress stacks"):
    for t in ts.all_texture_sets():
        name = t.name()
        recipe = wanted.get(name) if wanted else next(
            (r for k, r in RECIPE_BY_NAME.items() if name.endswith("_" + k)), None)
        if recipe is None:
            continue
        stack = t.get_stack()
        for n in list(ls.get_root_layer_nodes(stack)):
            if n.get_name().startswith(PREFIX):
                ls.delete_node(n)
        RECIPES[recipe](stack)
        done[name] = recipe
RESULT = {"stacks": done, "skipped_params": sorted(set(SKIPPED))}
