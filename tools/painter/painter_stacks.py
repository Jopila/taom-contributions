"""Painter-side script (run through painter_run.py): the TAOM layer stacks for a
baked mesh (painter_new_project.py; for the Angmar fortress recipes also a unique
unwrap from tools/blender/unwrap_texture_sets.py, with low_as_high). First used on
the Carn Dum map icon (2026-10-16). Idempotent: it deletes its own layers (prefix
"TAOM ") and rebuilds them.

The look Mike approved for the fortress is the stone colour alone: no snow, rime,
worn edges, soot, water streaks or stone courses ("they won't show at 1K"; round 13
had all of them). Every pattern is TRIPLANAR (world space), because the packer
rotates islands, and a Tripo re-UV has hundreds of islands where UV projection
shows a seam at every one.

Recipes, bottom to top:
  stone        dark blue-grey fill, slate grain (Overlay), light and dark tone
               patches, a cold tint, the baked occlusion; the paving set too (walks
               and tops are stone, Mike on round 14)
  roof         dark slate fill, tone patches, occlusion
  iron         iron_forged (wrought iron, no rust: Mike on the tiling pass)
  dark         near-black openings, darker in their depth
  frost_stone  a Tripo retexture: stone_slate base (colour and roughness only),
               cavity grime (mg_dirt), edge wear (mg_metal_edge_wear), then frost
               and snow on upward faces (mg_world_space_normals)

A set takes the recipe its name ends with (RECIPE_BY_NAME). A Tripo retexture has one
set named t_<mesh>, which matches no suffix: select the recipe by name,
ARGS["sets"] = {"t_<mesh>": "frost_stone"}.

ARGS (all optional): sets ({"set name": "recipe"}; default: by name), scale
(triplanar repeats across the mesh, 8), any colour as an sRGB RGB list (stone,
stone_light, stone_dark, tint, roof, iron, dark, occlusion; frost_stone reads stone,
grime, edge and frost). frost_stone only: triplanar_scale (8), dirt_level (0.8),
wear_level (0.5), frost_opacity (0.85), frost_balance (0.8), frost_contrast (0.85),
skip_frost (false), stone_relief (false: the slate gives colour and roughness only).
An unknown key raises, so a misspelt colour cannot pass for "no change". A sets entry
that names no texture set raises, and so does a run that would paint nothing.

Every shelf resource of the recipes about to run is resolved before the first old
layer is deleted, so a missing resource raises and leaves the stacks as they were.

frost_stone tuning notes from the first run:
- The world-space-normal mask maps normal.y [-1, 1] to [0, 1], so walls sit at
  0.5, and ws_balance is the threshold: 0.4 frosted every wall, 0.8 keeps it to
  faces more than about 50 deg upward.
- Generator grunge runs in UV space unless Use_Triplanar=1: blotches that
  break at every island.
- Triplanar scale 20 read as noise at map distance; 8 reads as blocks.
- The slate's own normal and height buried the windows and arches the baked
  normal carries: in the Kit it read as melted wax. stone_relief stays off
  unless the mesh has no architecture to show.

Painter 12.1.5 traps met on the way (rounds 1 to 13; the pipeline doc has the detail):
- A fill takes the raw numbers as LINEAR whatever the Color's tag: rgb() converts.
  A Substance material parameter (iron_color, stone_color) takes sRGB: rgb_param().
- set_material_source turns every output of the material back on: set
  active_channels after it (the slate's normal tilted 20% of the texels).
- Height becomes normal per texel: with a 55 m mesh in one 1K tile even 3% relief
  tilted a quarter of the texels and the lit preview went blotchy. No height here.
- A feature under 2 texels (about 18 cm at 11 px/m) vanishes at 1K.
- bricks_01 is white in its joints (invert=1 paints the blocks).
- Layer.set_opacity needs a channel outside a mask stack.
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
     "iron": [0.17, 0.17, 0.18], "dark": [0.022, 0.022, 0.027], "occlusion": [0.0, 0.0, 0.0],
     "grime": [0.03, 0.03, 0.035], "edge": [0.28, 0.29, 0.32], "frost": [0.78, 0.82, 0.88]}
KNOWN_ARGS = set(C) | {"sets", "scale", "triplanar_scale", "dirt_level", "wear_level", "frost_opacity",
                       "frost_balance", "frost_contrast", "skip_frost", "stone_relief"}
unknown = sorted(set(A) - KNOWN_ARGS)
if unknown:
    raise ValueError(f"unknown ARGS {unknown}; known: {sorted(KNOWN_ARGS)}")
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
    Depth still reads at map distance. The _s map's AO dims ambient light and the sun's specular,
    never the sun's diffuse; keep occlusion in one of the two, or it darkens twice in game."""
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


def frost_stone(stack):
    """Cold dark stone for a Tripo retexture: slate base, cavity grime, edge wear, frost."""
    base = layer(stack, "stone base", [CT.BaseColor, CT.Roughness])
    base.set_material_source(find("stone_slate"))
    setp(base.get_material_source(), {"stone_color": rgb_param(C["stone"]), "stone_roughness": 0.78,
                                      "color_variation": 0.25, "slate_density": 0.45})
    channels = {CT.BaseColor, CT.Roughness}
    if A.get("stone_relief", False):
        channels |= {CT.Normal, CT.Height}
    base.active_channels = channels
    triplanar(base, float(A.get("triplanar_scale", 8.0)))

    grime = layer(stack, "cavity grime", [CT.BaseColor, CT.Roughness], C["grime"], 0.92)
    mask_gen(grime, "mg_dirt", {"Use_Triplanar": 1, "dirt_level": float(A.get("dirt_level", 0.8))})
    wear = layer(stack, "edge wear", [CT.BaseColor, CT.Roughness], C["edge"], 0.7)
    mask_gen(wear, "mg_metal_edge_wear", {"Use_Triplanar": 1, "Wear_Level": float(A.get("wear_level", 0.5))})
    if not A.get("skip_frost", False):
        frost = layer(stack, "frost and snow", [CT.BaseColor, CT.Roughness], C["frost"], 0.85)
        opacity(frost, float(A.get("frost_opacity", 0.85)), [CT.BaseColor, CT.Roughness])
        mask_gen(frost, "mg_world_space_normals", {"ws_balance": float(A.get("frost_balance", 0.8)),
                                                   "ws_contrast": float(A.get("frost_contrast", 0.85))})


RECIPES = {"stone": stone, "roof": roof, "iron": iron, "dark": dark, "frost_stone": frost_stone}
# the shelf names each recipe calls, resolved for every set to paint before anything is deleted
RECIPE_RESOURCES = {
    "stone": ["stone_slate", "grunge_rock", "grunge_concrete_old", "grunge_dirt_large", "mg_ambient_occlusion"],
    "roof": ["grunge_stone_details", "mg_ambient_occlusion"],
    "iron": ["iron_forged"],
    "dark": ["mg_ambient_occlusion"],
    "frost_stone": ["stone_slate", "mg_dirt", "mg_metal_edge_wear"]
                   + ([] if A.get("skip_frost", False) else ["mg_world_space_normals"]),
}
wanted = A.get("sets")
all_sets = ts.all_texture_sets()
missing = sorted(set(wanted or {}) - {t.name() for t in all_sets})
if missing:
    raise ValueError(f"sets {missing} are not in the project; have {sorted(t.name() for t in all_sets)}")
plan = {}
for t in all_sets:
    name = t.name()
    recipe = wanted.get(name) if wanted else next(
        (r for k, r in RECIPE_BY_NAME.items() if name.endswith("_" + k)), None)
    if recipe is None:
        continue
    if recipe not in RECIPES:
        raise ValueError(f"set {name}: unknown recipe {recipe}, have {sorted(RECIPES)}")
    plan[name] = (t, recipe)
if not plan:
    raise ValueError('no texture set matches a recipe: name them in ARGS sets (a Tripo retexture: '
                     f'{{"t_<mesh>": "frost_stone"}}); have {[t.name() for t in all_sets]}')
for needed in sorted({n for _, recipe in plan.values() for n in RECIPE_RESOURCES[recipe]}):
    find(needed)

done = {}
with ls.ScopedModification("TAOM layer stacks"):
    for name, (t, recipe) in plan.items():
        stack = t.get_stack()
        for n in list(ls.get_root_layer_nodes(stack)):
            if n.get_name().startswith(PREFIX):
                ls.delete_node(n)
        RECIPES[recipe](stack)
        done[name] = recipe
RESULT = {"stacks": done, "skipped_params": sorted(set(SKIPPED))}
