# Gondor meshes

Every part in the six Gondor source folders, **measured** in headless Blender on 2026-10-15
(`tools/oneoff/blender_catalog_erebor.py --kit gondor`) and checked against 51,491 authored
placements in the TAOM_Map prefabs (`tools/oneoff/mine_erebor_prefab_joins.py --kit gondor`). The
join rules and recipes are in [design-patterns.md](design-patterns.md); this file is the per-part
reference.

**Measured outputs** (desktop, not in git): `E:\LOTRAOMAssets\_export\gondor\catalog\` holds
`gondor_catalog_<folder>.json` (every number below plus normal-area buckets, down-ray profiles,
`bo_` bounds, per-sub-object bounds and LOD ladders), one four-view sheet per part in
`views\<part>.png` and numbered contact sheets per FBX in `sheets\`. The `#` column is the
number on those sheets.

## Read this first

- **A row is a part family, measured as one.** Its non-decal base sub-objects are joined and
  measured together; the last column lists each sub-object with its own LOD numbers. `(base)` is
  the object named exactly `<prefix>`.
- **All coordinates are in the Blender import frame.** The engine sees every kit FBX turned
  180 degrees about Z from that frame ([design-patterns.md](design-patterns.md)).
- **Place by pivot, never by bounding box.** Many trims, roofs and merlons have the pivot on an
  edge or outside the mesh (`outside`); see Lond Cirion rule 4.
- **Decal sub-objects are not in these bounds.** They are listed in the JSON (`decals`), never
  measured or placed.
- **A Blender `.NNN` copy never replaces its plain name.** Where both exist (a base, a LOD or a
  `bo_`), the copy is dropped with a warning in the JSON: `gondor_castle_wall_tower_L1_A_int` keeps
  its 1,166-tri body, not the 36-tri `.001`.
- **Tris** are the merged base sub-objects. One part, `gondor_castle_wall_tower_l3_b`, counts
  twice (12,700): it has no `.wall` base, so its `.wall.lod3` is promoted beside the bare-prefix
  base it probably duplicates (equal bounds, 5,762 against 6,938 tris).
- **LOD numbers vary per family** (2/4/5 on walls and paving, 3/6 on trims and castle pieces,
  2/5 on most roofs). Use the Lond Cirion ladder: lod3 tier from `.lod3`, `.lod2`, `.lod4`; lod6
  tier from `.lod6`, `.lod5`, `.lod4`, `.lod2`.

## The kit: `meshes/`

### Wall panels (3 m and 6 m, straight)

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 203 | `gondor_wall_3m_a_clean` | 3.00 x 0.53 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 660 | lod2, lod5 |  | 2: stonebrick [lod2], wall [lod2/lod5] |
| 204 | `gondor_wall_3m_a_dest_a` | 2.17 x 0.67 x 3.03 | -0.02 to 3.01 | 0.69/0.53/0.01 | 3.00 | 2702 | lod2, lod4, lod5 |  | 4: stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 205 | `gondor_wall_3m_a_dest_b` | 3.00 x 0.86 x 3.00 | 0.00 to 3.00 | mid/0.44/min | 3.00 | 2096 | lod2, lod4, lod5 |  | 4: stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], walliside [lod2/lod4/lod5] |
| 206 | `gondor_wall_3m_a_dest_c` | 3.04 x 0.66 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 4014 | lod2, lod4, lod5 | opening 1.01 x 2.50 at z 0.00 (through Y) | 4: stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 207 | `gondor_wall_3m_a_dest_d` | 3.06 x 0.70 x 3.00 | 0.00 to 3.00 | 0.51/0.47/min | 3.00 | 2770 | lod2, lod4, lod5 |  | 3: stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 208 | `gondor_wall_3m_a_dirty` | 3.00 x 0.53 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 660 | lod2, lod5 |  | 2: stonebrick [lod2], wall [lod2/lod5] |
| 209 | `gondor_wall_3m_a_door2_a_clean` | 3.00 x 0.84 x 3.01 | -0.01 to 3.00 | mid/mid/min | 3.00 | 6012 | lod2, lod4, lod5 | opening 1.10 x 2.61 at z -0.01 (through Y); opening 1.10 x 2.61 at z -0.01 (through Y) | 3: door [lod2/lod4/lod5], pillar [lod2/lod4/lod5], wall [lod2/lod4/lod5] |
| 210 | `gondor_wall_3m_a_door2_a_dest_a` | 3.05 x 0.91 x 3.06 | -0.04 to 3.03 | 0.49/0.47/0.01 | 2.85 | 5046 | lod2, lod4, lod5 | opening 1.32 x 0.25 at z -0.04 (through Y); opening 0.15 x 0.50 at z 1.02 (through Y) | 4: door [lod2/lod4/lod5], pillar [lod2/lod4/lod5], stonebrickdest [lod2/lod4/lod5], wall [lod2/lod4/lod5] |
| 211 | `gondor_wall_3m_a_door2_a_dirty` | 3.00 x 0.84 x 3.01 | -0.01 to 3.00 | mid/mid/min | 3.00 | 6012 | lod2, lod4, lod5 | opening 1.10 x 2.61 at z -0.01 (through Y); opening 1.10 x 2.61 at z -0.01 (through Y) | 3: door [lod2/lod4/lod5], pillar [lod2/lod4/lod5], wall [lod2/lod4/lod5] |
| 212 | `gondor_wall_3m_a_door_a_clean` | 3.00 x 0.57 x 3.01 | -0.01 to 3.00 | mid/mid/min | 3.00 | 660 | lod2, lod4, lod5 | opening 1.20 x 2.61 at z -0.01 (through Y) | 3: door [lod2/lod4/lod5], stonebrick [lod2], wall [lod2/lod4/lod5] |
| 213 | `gondor_wall_3m_a_door_a_cracked` | 3.37 x 0.93 x 2.50 | -0.01 to 2.49 | 0.54/mid/min | 2.26 | 3082 | lod2, lod4, lod5 |  | 4: archdoor [lod2/lod4], brick [lod2/lod4], brickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 214 | `gondor_wall_3m_a_door_a_dirty` | 3.00 x 0.57 x 3.01 | -0.01 to 3.00 | mid/mid/min | 3.00 | 660 | lod2, lod4, lod5 | opening 1.20 x 2.61 at z -0.01 (through Y) | 3: door [lod2/lod4/lod5], stonebrick [lod2], wall [lod2/lod4/lod5] |
| 215 | `gondor_wall_3m_window2_a_clean` | 3.01 x 0.55 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 3274 | lod2, lod4, lod5 | opening 0.45 x 1.05 at z 1.05 (through Y); opening 0.45 x 1.05 at z 1.05 (through Y) | 3: stonebrick [lod2], wall [lod2/lod4/lod5], window [lod2/lod4/lod5] |
| 216 | `gondor_wall_3m_window2_a_dest_a` | 3.04 x 0.76 x 2.01 | 0.00 to 2.01 | mid/0.53/min | 1.76 | 3090 | lod2, lod4, lod5 |  | 4: arch [lod2/lod4/lod5], stonebrick [lod2/lod4/lod5], stonebrickdest [lod2/lod4/lod5], wall [lod2/lod4/lod5] |
| 217 | `gondor_wall_3m_window2_a_dirty` | 3.01 x 0.55 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 3274 | lod2, lod4, lod5 | opening 0.45 x 1.05 at z 1.05 (through Y); opening 0.45 x 1.05 at z 1.05 (through Y) | 3: stonebrick [lod2], wall [lod2/lod4/lod5], window [lod2/lod4/lod5] |
| 218 | `gondor_wall_3m_window3_a_clean` | 3.01 x 0.55 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 4430 | lod2, lod4, lod5 | opening 0.50 x 1.05 at z 1.05 (through Y); opening 0.45 x 1.05 at z 1.05 (through Y); +1 more openings | 3: stonebrick [lod2], wall [lod2/lod4/lod5], window [lod2/lod4/lod5] |
| 219 | `gondor_wall_3m_window3_a_dest_a` | 3.01 x 0.64 x 3.01 | 0.00 to 3.01 | mid/0.52/min | 3.00 | 3900 | lod2, lod4, lod5 | opening 0.45 x 1.05 at z 1.05 (through Y); opening 0.30 x 0.35 at z 1.05 (through Y) | 3: stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], window [lod2/lod4/lod5] |
| 220 | `gondor_wall_3m_window3_a_dirty` | 3.01 x 0.55 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 4430 | lod2, lod4, lod5 | opening 0.50 x 1.05 at z 1.05 (through Y); opening 0.45 x 1.05 at z 1.05 (through Y); +1 more openings | 3: stonebrick [lod2], wall [lod2/lod4/lod5], window [lod2/lod4/lod5] |
| 221 | `gondor_wall_3m_window_a_clean` | 3.00 x 0.56 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 2114 | lod2, lod4, lod5 | opening 0.50 x 1.05 at z 1.05 (through Y) | 3: stonebrick [lod2], wall [lod2/lod4/lod5], window [lod2/lod4/lod5] |
| 222 | `gondor_wall_3m_window_a_cracked_a` | 3.07 x 0.73 x 3.00 | 0.00 to 3.00 | 0.51/mid/min | 2.50 | 3337 | lod2, lod4, lod5 |  | 4: arch [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 223 | `gondor_wall_3m_window_a_dirty` | 3.00 x 0.56 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 2222 | lod2, lod4, lod5 | opening 0.50 x 1.05 at z 1.05 (through Y) | 3: stonebrick [lod2], wall [lod2/lod4/lod5], window [lod2/lod4/lod5] |
| 236 | `gondor_wall_6m_a_clean` | 6.00 x 0.53 x 6.00 | 0.00 to 6.00 | mid/mid/min | 6.00 | 1956 | lod2, lod4 |  | 2: (base) [lod2/lod4], stonebrick [lod2] |
| 237 | `gondor_wall_6m_a_dest_a` | 6.02 x 0.84 x 6.00 | 0.00 to 6.00 | mid/mid/min | 6.00 | 8874 | lod2, lod4, lod5 |  | 5: (base) [lod5], side [lod2], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 238 | `gondor_wall_6m_a_dest_b` | 4.62 x 0.89 x 6.01 | -0.01 to 6.00 | 0.35/mid/min | 6.00 | 4744 | lod2, lod4, lod5 |  | 4: side [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 239 | `gondor_wall_6m_a_dest_c` | 6.14 x 0.78 x 6.01 | 0.00 to 6.01 | mid/0.53/min | 5.50 | 5064 | lod2, lod4, lod5 |  | 4: side [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 240 | `gondor_wall_6m_a_dest_d` | 6.04 x 0.89 x 6.02 | -0.02 to 6.00 | mid/0.45/min | 6.00 | 5396 | lod2, lod4, lod5 | opening 0.76 x 0.66 at z -0.02 (through Y); opening 0.35 x 0.96 at z -0.02 (through Y) | 4: side [lod2/lod4], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 241 | `gondor_wall_6m_a_dirty` | 6.00 x 0.53 x 6.00 | 0.00 to 6.00 | mid/mid/min | 6.00 | 1956 | lod2, lod4 |  | 2: (base) [lod2/lod4], stonebrick [lod2] |
| 242 | `gondor_wall_6m_arch_a_clean` | 6.08 x 0.72 x 6.00 | 0.00 to 6.00 | mid/mid/min | 6.00 | 4592 | lod2, lod4, lod5 | opening 1.32 x 2.04 at z 0.61 (through Y); opening 1.32 x 2.04 at z 0.61 (through Y) | 5: arch [lod2/lod4/lod5], archwin [lod2/lod4/lod5], stonebrick [lod2/lod4/lod5], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 243 | `gondor_wall_6m_arch_a_dest_a` | 6.04 x 0.74 x 6.01 | -0.01 to 6.00 | mid/0.48/min | 6.00 | 6218 | lod2, lod4, lod5 | opening 1.31 x 2.07 at z 0.60 (through Y); opening 1.31 x 2.02 at z 0.60 (through Y) | 7: arch [lod2/lod4/lod5], archwin [lod2/lod4/lod5], stonebrick [lod2/lod4/lod5], stonebrickdest [lod2/lod4], stonedest [lod2/lod4/lod5], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 244 | `gondor_wall_6m_arch_a_dest_b` | 6.05 x 0.86 x 6.03 | -0.01 to 6.03 | mid/0.56/min | 6.01 | 7215 | lod2, lod4, lod5 | opening 0.76 x 0.35 at z 0.60 (through Y) | 7: arch [lod2/lod4/lod5], archwin [lod2/lod4/lod5], stonebrick [lod2/lod4/lod5], stonebrickdest [lod2/lod4], stonedest [lod2/lod4/lod5], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 245 | `gondor_wall_6m_arch_a_dirty` | 6.08 x 0.72 x 6.01 | -0.01 to 6.00 | mid/mid/min | 6.00 | 4592 | lod2, lod4, lod5 | opening 1.32 x 2.04 at z 0.61 (through Y); opening 1.32 x 2.04 at z 0.61 (through Y) | 5: arch [lod2/lod4/lod5], archwin [lod2/lod4/lod5], stonebrick [lod2/lod4/lod5], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 246 | `gondor_wall_6m_arch_b_clean` | 6.08 x 0.72 x 6.00 | 0.00 to 6.00 | mid/mid/min | 6.00 | 2008 | lod2, lod4, lod5 | opening 4.56 x 5.24 at z 0.00 (through Y) | 3: arch [lod2/lod4/lod5], stonebrick [lod2], wall [lod2/lod4/lod5] |
| 247 | `gondor_wall_6m_arch_b_dest_a` | 6.12 x 0.72 x 6.01 | -0.01 to 6.00 | mid/mid/min | 6.00 | 2368 | lod2, lod4, lod5 |  | 5: arch [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], stonedest [lod2/lod4/lod5], wall [lod2/lod4/lod5] |
| 248 | `gondor_wall_6m_arch_b_dest_b` | 6.05 x 0.77 x 6.01 | -0.01 to 6.01 | mid/0.47/min | 6.00 | 3133 | lod2, lod4, lod5 |  | 5: arch [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], stonedest [lod2/lod4/lod5], wall [lod2/lod4/lod5] |
| 249 | `gondor_wall_6m_arch_b_dirty` | 6.08 x 0.72 x 6.01 | -0.01 to 6.00 | mid/mid/min | 6.00 | 2008 | lod2, lod4, lod5 | opening 4.51 x 5.25 at z -0.01 (through Y) | 3: arch [lod2/lod4/lod5], stonebrick [lod2], wall [lod2/lod4/lod5] |
| 250 | `gondor_wall_6m_arch_c_clean` | 6.03 x 0.72 x 6.01 | -0.01 to 6.00 | mid/mid/min | 6.00 | 2996 | lod2, lod4, lod5 | opening 1.21 x 2.63 at z -0.01 (through Y); node scale 1/0.833/1 (baked) | 5: arch [lod2/lod4/lod5], archdoor [lod2/lod4/lod5], stonebrick [lod2], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 251 | `gondor_wall_6m_arch_c_dest_a` | 6.05 x 0.94 x 5.02 | -0.01 to 5.01 | mid/0.62/min | 2.00 | 4348 | lod2, lod4, lod5 | opening 0.76 x 0.30 at z 1.61 (through Y); opening 0.20 x 0.61 at z -0.01 (through Y); +1 more openings | 7: arch [lod2/lod4/lod5], archdoor [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], stonedest [lod2/lod4/lod5], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 252 | `gondor_wall_6m_arch_c_dest_b` | 6.00 x 0.72 x 3.12 | -0.01 to 3.11 | mid/mid/min | 2.50 | 2552 | lod2, lod4, lod5 |  | 7: arch [lod2/lod4/lod5], archdoor [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], stonedest [lod2/lod4/lod5], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 253 | `gondor_wall_6m_arch_c_dest_c` | 6.09 x 0.93 x 6.03 | -0.01 to 6.03 | mid/mid/min | 5.49 | 4482 | lod2, lod4, lod5 | opening 3.81 x 5.16 at z -0.01 (through Y) | 6: arch [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], stonedest [lod2/lod4/lod5], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 254 | `gondor_wall_6m_arch_c_dirty` | 6.03 x 0.72 x 6.01 | -0.01 to 6.00 | mid/mid/min | 6.00 | 2996 | lod2, lod4, lod5 | opening 1.21 x 2.63 at z -0.01 (through Y); node scale 1/0.833/1 (baked) | 5: arch [lod2/lod4/lod5], archdoor [lod2/lod4/lod5], stonebrick [lod2], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 255 | `gondor_wall_6m_arch_d_clean` | 6.03 x 0.72 x 6.01 | 0.00 to 6.01 | mid/mid/min | 6.00 | 3422 | lod2, lod4, lod5 | opening 1.61 x 4.79 at z 0.00 (through Y); opening 1.61 x 4.79 at z 0.00 (through Y) | 3: arch [lod2/lod4/lod5], stonebrick [lod2], wall [lod2/lod4/lod5] |
| 256 | `gondor_wall_6m_arch_d_dest_a` | 6.05 x 1.64 x 6.08 | 0.00 to 6.07 | mid/0.53/min | 0.92 | 4670 | lod2, lod4, lod5 | opening 0.61 x 1.22 at z 0.00 (through Y); opening 0.96 x 0.25 at z 0.86 (through Y) | 5: arch [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 257 | `gondor_wall_6m_arch_d_dest_b` | 6.03 x 1.14 x 6.01 | 0.00 to 6.01 | mid/0.49/min | 6.00 | 5424 | lod2, lod4, lod5 | opening 1.61 x 4.79 at z 0.00 (through Y); opening 0.65 x 0.81 at z 0.00 (through Y); +4 more openings | 5: arch [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 258 | `gondor_wall_6m_arch_d_dirty` | 6.03 x 0.72 x 6.01 | 0.00 to 6.01 | mid/mid/min | 6.00 | 3422 | lod2, lod4, lod5 | opening 1.61 x 4.79 at z 0.00 (through Y); opening 1.61 x 4.79 at z 0.00 (through Y) | 3: arch [lod2/lod4/lod5], stonebrick [lod2], wall [lod2/lod4/lod5] |
| 259 | `gondor_wall_6m_window3_a_clean` | 6.00 x 0.60 x 6.01 | 0.00 to 6.01 | mid/mid/min | 6.00 | 5914 | lod2, lod4, lod5 | opening 0.81 x 3.40 at z 1.00 (through Y); opening 0.81 x 3.40 at z 1.00 (through Y); +1 more openings | 3: archwin [lod2/lod4/lod5], stonebrick [lod2], wall [lod2/lod4/lod5] |
| 260 | `gondor_wall_6m_window3_a_dest_a` | 6.11 x 0.95 x 6.01 | 0.00 to 6.01 | 0.51/0.59/min | 5.76 | 7414 | lod2, lod4, lod5 | opening 0.81 x 3.44 at z 0.98 (through Y); opening 0.76 x 3.44 at z 0.98 (through Y) | 5: archwin [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 261 | `gondor_wall_6m_window3_a_dest_b` | 6.03 x 0.80 x 5.02 | 0.00 to 5.02 | mid/0.45/min | 1.12 | 6363 | lod2, lod4, lod5 | opening 0.60 x 0.41 at z 2.89 (through Y); opening 0.40 x 0.51 at z 1.37 (through Y); +1 more openings | 5: archwin [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 262 | `gondor_wall_6m_window3_a_dirty` | 6.00 x 0.70 x 6.01 | 0.00 to 6.01 | mid/mid/min | 6.00 | 6886 | lod2, lod4, lod5 | opening 0.81 x 3.30 at z 1.10 (through Y); opening 0.81 x 3.30 at z 1.10 (through Y); +1 more openings | 3: archwin [lod2/lod4/lod5], stonebrick [lod2], wall [lod2/lod4/lod5] |

### Wall panels (curved)

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 194 | `gondor_wall_3m_curv_a_clean` | 3.27 x 3.29 x 3.00 | 0.00 to 3.00 | min/0.99/min | 3.00 | 2292 | lod2, lod4 |  | 2: stonebrick [lod2], wall [lod2/lod4] |
| 195 | `gondor_wall_3m_curv_a_dest_a` | 3.25 x 3.33 x 3.03 | -0.02 to 3.01 | min/max/0.01 | 3.00 | 5278 | lod2, lod4, lod5 |  | 3: stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 196 | `gondor_wall_3m_curv_a_dirty` | 3.27 x 3.29 x 3.00 | 0.00 to 3.00 | min/max/min | 3.00 | 2292 | lod2, lod4 |  | 2: stonebrick [lod2], wall [lod2/lod4] |
| 197 | `gondor_wall_3m_curv_door_a_clean` | 3.25 x 3.25 x 3.01 | -0.01 to 3.00 | min/max/min | 3.00 | 908 | lod2, lod4, lod5 | opening 0.30 x 2.46 at z -0.01 (through Y); opening 0.30 x 2.46 at z -0.01 (through X) | 3: brick [lod2], door [lod2/lod4/lod5], wall [lod2/lod4/lod5] |
| 198 | `gondor_wall_3m_curv_door_a_dest_a` | 3.33 x 3.33 x 2.50 | -0.01 to 2.49 | 0.02/0.98/min | 2.25 | 5808 | lod2, lod4, lod5 |  | 3: brick [lod2/lod4], brickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 199 | `gondor_wall_3m_curv_door_a_dirty` | 3.25 x 3.25 x 3.01 | -0.01 to 3.00 | min/max/min | 3.00 | 908 | lod2, lod4, lod5 | opening 0.30 x 2.46 at z -0.01 (through Y); opening 0.30 x 2.46 at z -0.01 (through X) | 3: brick [lod2], door [lod2/lod4/lod5], wall [lod2/lod4/lod5] |
| 200 | `gondor_wall_3m_curv_window_a_clean` | 3.27 x 3.27 x 3.00 | 0.00 to 3.00 | min/max/min | 3.00 | 5420 | lod2, lod4, lod5 | opening 0.25 x 1.05 at z 1.05 (through Y); opening 0.25 x 1.05 at z 1.05 (through X) | 3: stonebrick [lod2], wall [lod2/lod4/lod5], window [lod2/lod4/lod5] |
| 201 | `gondor_wall_3m_curv_window_a_dest_a` | 3.33 x 3.28 x 3.00 | 0.00 to 3.00 | 0.02/max/min | 3.00 | 6601 | lod2, lod4, lod5 |  | 4: stonebrick [lod2/lod5], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], window [lod2/lod4/lod5] |
| 202 | `gondor_wall_3m_curv_window_a_dirty` | 3.27 x 3.27 x 3.00 | 0.00 to 3.00 | min/max/min | 3.00 | 5420 | lod2, lod4, lod5 | opening 0.25 x 1.05 at z 1.05 (through Y); opening 0.25 x 1.05 at z 1.05 (through X) | 3: stonebrick [lod2], wall [lod2/lod4/lod5], window [lod2/lod4/lod5] |
| 224 | `gondor_wall_6m_curv_a_dest_a` | 6.37 x 6.49 x 6.03 | -0.02 to 6.01 | min/0.99/min | 6.00 | 15208 | lod2, lod4, lod5 | opening 2.23 x 4.53 at z -0.02 (through Y); opening 0.21 x 0.53 at z -0.02 (through Y); node scale 1.2/1.2/1 (baked) | 3: brick [lod2/lod4], brickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 225 | `gondor_wall_6m_curv_a_dirty` | 6.25 x 6.27 x 6.00 | 0.00 to 6.00 | min/max/min | 6.00 | 6740 | lod2, lod5 |  | 2: brick [lod2], wall [lod2/lod5] |
| 226 | `gondor_wall_6m_curv_a_door_dest_a` | 6.36 x 6.36 x 5.06 | -0.05 to 5.01 | min/max/0.01 | 2.50 | 7446 | lod2, lod4, lod5 | opening 0.05 x 0.91 at z 0.27 (through Y); opening 0.05 x 0.96 at z 1.01 (through X) | 3: brick [lod2/lod4], brickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 227 | `gondor_wall_6m_curv_a_door_dirty` | 6.40 x 6.36 x 6.01 | -0.01 to 6.00 | 0.01/max/min | 6.00 | 6500 | lod2, lod4, lod5 | opening 0.80 x 2.58 at z -0.01 (through Y); opening 0.90 x 2.56 at z -0.01 (through X) | 2: brick [lod2], wall [lod2/lod4/lod5] |
| 228 | `gondor_wall_6m_curv_a_door_normal` | 6.40 x 6.36 x 6.01 | -0.01 to 6.00 | 0.01/max/min | 6.00 | 6500 | lod2, lod4, lod5 | opening 0.80 x 2.58 at z -0.01 (through Y); opening 0.90 x 2.56 at z -0.01 (through X) | 2: brick [lod2], wall [lod2/lod4/lod5] |
| 229 | `gondor_wall_6m_curv_a_normal` | 6.25 x 6.27 x 6.00 | 0.00 to 6.00 | min/max/min | 6.00 | 6740 | lod2, lod5 |  | 2: brick [lod2], wall [lod2/lod5] |
| 230 | `gondor_wall_6m_curv_a_window_dest_a` | 6.35 x 6.34 x 6.01 | 0.00 to 6.01 | min/max/min | 1.12 | 16210 | lod2, lod4, lod5 | opening 0.37 x 0.37 at z 2.92 (through Y); opening 0.53 x 3.24 at z 1.12 (through X); +1 more openings | 2: brick [lod2/lod4], wall [lod2/lod4/lod5] |
| 231 | `gondor_wall_6m_curv_a_window_dirty` | 6.34 x 6.34 x 6.45 | -0.41 to 6.03 | min/max/0.06 | 6.00 | 14078 | lod2, lod4, lod5 | opening 0.54 x 3.22 at z 1.15 (through Y); opening 3.74 x 0.43 at z -0.41 (through Y); +5 more openings | 2: brick [lod2], wall [lod2/lod4/lod5] |
| 232 | `gondor_wall_6m_curv_a_window_normal` | 6.34 x 6.34 x 6.01 | 0.00 to 6.01 | min/max/min | 6.00 | 13732 | lod2, lod4, lod5 | opening 0.58 x 3.24 at z 1.12 (through Y); opening 0.37 x 3.24 at z 1.12 (through Y); +4 more openings | 2: brick [lod2], wall [lod2/lod4/lod5] |
| 233 | `gondor_wall_6m_curv_b_door_dest_a` | 6.34 x 6.37 x 6.53 | -0.46 to 6.07 | min/0.99/0.07 | 6.00 | 10330 | lod2, lod4, lod5 |  | 3: brick [lod2/lod4], brickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 234 | `gondor_wall_6m_curv_b_door_dirty` | 6.29 x 6.33 x 6.11 | -0.03 to 6.08 | min/max/0.01 | 6.00 | 8144 | lod2, lod4, lod5 | opening 1.78 x 4.21 at z -0.03 (through Y); opening 3.72 x 4.83 at z -0.03 (through X) | 1: wall [lod2/lod4/lod5] |
| 235 | `gondor_wall_6m_curv_b_door_normal` | 6.28 x 6.33 x 6.01 | 0.00 to 6.01 | min/max/min | 6.00 | 7952 | lod2, lod4, lod5 | opening 1.05 x 4.79 at z 0.00 (through Y); opening 0.79 x 4.74 at z 0.00 (through Y); +4 more openings | 1: wall [lod2/lod4/lod5] |

### Trims (string courses, cornices)

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 160 | `gondor_wall_trim_3m_curv_b_clean` | 3.40 x 3.40 x 0.25 | -0.12 to 0.12 | min/max/mid | 0.13 | 556 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 161 | `gondor_wall_trim_3m_curv_b_dirty` | 3.40 x 3.40 x 0.25 | -0.12 to 0.13 | min/max/mid | 0.13 | 748 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 162 | `gondor_wall_trim_3m_curv_c_clean` | 3.40 x 3.40 x 0.25 | -0.12 to 0.12 | min/max/mid | 0.13 | 1076 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 163 | `gondor_wall_trim_3m_curv_c_dirty` | 3.40 x 3.40 x 0.37 | -0.24 to 0.13 | min/max/0.65 | 0.12 | 1388 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 164 | `gondor_wall_trim_3m_curv_d_clean` | 3.38 x 3.38 x 0.24 | -0.12 to 0.12 | min/max/mid | 0.12 | 1184 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 165 | `gondor_wall_trim_3m_curv_d_dirty` | 3.38 x 3.38 x 0.34 | -0.21 to 0.13 | min/max/0.61 | 0.12 | 1704 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 166 | `gondor_wall_trim_6m_b_clean` | 6.40 x 6.40 x 0.25 | -0.12 to 0.12 | min/max/mid | 0.13 | 1072 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 167 | `gondor_wall_trim_6m_b_dirty` | 6.40 x 6.40 x 0.25 | -0.12 to 0.13 | min/max/mid | 0.13 | 1456 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 168 | `gondor_wall_trim_6m_c_clean` | 6.40 x 6.40 x 0.25 | -0.12 to 0.12 | min/max/mid | 0.13 | 1316 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 169 | `gondor_wall_trim_6m_c_dirty` | 6.40 x 6.40 x 0.39 | -0.24 to 0.15 | min/max/0.62 | 0.13 | 1700 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 170 | `gondor_wall_trim_6m_d_clean` | 6.38 x 6.38 x 0.25 | -0.12 to 0.12 | min/max/mid | 0.13 | 1712 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 171 | `gondor_wall_trim_6m_d_dirty` | 6.38 x 6.38 x 0.34 | -0.21 to 0.13 | min/max/0.61 | 0.13 | 2352 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 172 | `gondor_column_3m_a_1x1` | 1.04 x 1.04 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 300 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 173 | `gondor_column_3m_a_1x1_dest_a` | 1.45 x 1.30 x 2.53 | 0.00 to 2.53 | 0.53/0.49/min | 2.00 | 1778 | lod2, lod4 |  | 3: stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4] |
| 174 | `gondor_column_corner_trim_a_clean` | 1.25 x 1.25 x 3.00 | 0.00 to 3.00 | 0.22/0.22/min | 3.00 | 1296 | lod2, lod5 |  | 1: (base) [lod2/lod5] |
| 175 | `gondor_column_corner_trim_a_dest_a` | 0.98 x 0.84 x 3.03 | -0.01 to 3.01 | 0.30/0.35/min | 3.01 | 3420 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 176 | `gondor_wall_trim_3m_a` | 3.00 x 1.02 x 0.52 | -0.26 to 0.26 | mid/mid/mid | 0.25 | 236 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 177 | `gondor_wall_trim_3m_b` | 3.00 x 1.02 x 0.52 | -0.26 to 0.26 | mid/mid/mid | 0.25 | 236 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 178 | `gondor_wall_trim_3m_b_clean` | 3.00 x 0.80 x 0.25 | -0.12 to 0.12 | mid/mid/mid | 0.13 | 130 | lod4, lod6 |  | 1: (base) [lod4/lod6] |
| 179 | `gondor_wall_trim_3m_b_dirty` | 3.00 x 0.81 x 0.25 | -0.12 to 0.13 | mid/mid/mid | 0.13 | 143 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 180 | `gondor_wall_trim_3m_c_clean` | 3.00 x 0.80 x 0.25 | -0.12 to 0.12 | mid/mid/mid | 0.13 | 76 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 181 | `gondor_wall_trim_3m_c_dest_a` | 2.07 x 0.80 x 0.29 | -0.15 to 0.14 | 0.72/mid/mid | 0.12 | 818 | lod2, lod4, lod5 |  | 3: stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5] |
| 182 | `gondor_wall_trim_3m_c_dirty` | 3.00 x 0.81 x 0.39 | -0.24 to 0.15 | mid/mid/0.62 | 0.13 | 88 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 183 | `gondor_wall_trim_3m_d_clean` | 3.00 x 0.77 x 0.25 | -0.12 to 0.12 | mid/mid/mid | 0.13 | 100 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 184 | `gondor_wall_trim_3m_d_dest_a` | 3.01 x 0.77 x 0.41 | -0.28 to 0.13 | mid/mid/0.69 | 0.12 | 748 | lod2, lod5 |  | 3: stonebroken [lod2/lod5], wall [lod2/lod5], wallinside [lod2/lod5] |
| 185 | `gondor_wall_trim_3m_d_dirty` | 3.00 x 0.77 x 0.34 | -0.21 to 0.13 | mid/mid/0.61 | 0.13 | 120 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 186 | `gondor_wall_trim_6m_a` | 6.00 x 1.02 x 0.52 | -0.26 to 0.26 | mid/mid/mid | 0.26 | 300 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 187 | `gondor_wall_trim_6m_b` | 6.00 x 1.02 x 0.52 | -0.26 to 0.26 | mid/mid/mid | 0.26 | 300 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 188 | `gondor_wall_trim_corner_c_clean` | 0.90 x 0.90 x 0.30 | -0.15 to 0.15 | mid/mid/mid | 0.15 | 156 | lod3, lod6 | node rot -0/-0/90 (baked) | 1: (base) [lod3/lod6] |
| 189 | `gondor_wall_trim_corner_c_dest_a` | 0.90 x 0.90 x 0.30 | -0.15 to 0.15 | mid/mid/mid | 0.15 | 156 | lod3, lod5 | node rot -0/-0/90 (baked) | 1: (base) [lod3/lod5] |
| 190 | `gondor_wall_trim_corner_c_dirty` | 0.91 x 0.91 x 0.39 | -0.24 to 0.15 | mid/mid/0.62 | 0.15 | 204 | lod3, lod6 | node rot -0/-0/90 (baked) | 1: (base) [lod3/lod6] |
| 191 | `gondor_wall_trim_corner_d_clean` | 0.85 x 0.85 x 0.27 | -0.13 to 0.13 | mid/mid/mid | 0.13 | 204 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 192 | `gondor_wall_trim_corner_d_dest_a` | 0.85 x 0.85 x 0.27 | -0.13 to 0.13 | mid/mid/mid | 0.13 | 204 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 193 | `gondor_wall_trim_corner_d_dirty` | 0.85 x 0.85 x 0.34 | -0.21 to 0.13 | mid/mid/0.61 | 0.13 | 284 | lod3, lod6 |  | 1: (base) [lod3/lod6] |

### Roofs

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 81 | `gondor_roof_circular_full_6m_b` | 6.41 x 6.41 x 3.11 | -0.03 to 3.07 | mid/mid/0.01 | 0.38 | 1788 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 82 | `gondor_roof_a_30_1-5m_corner_in` | 1.58 x 1.58 x 0.86 | 0.09 to 0.95 | 0.03/0.03/outside | 0.89 | 384 | lod2, lod5 | 20 deg slope rising toward +Y; 20 deg slope rising toward +X | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 83 | `gondor_roof_a_30_1-5m_corner_out` | 1.58 x 1.58 x 0.86 | 0.09 to 0.95 | 0.97/0.03/outside | 0.18 | 396 | lod2, lod5 | 20 deg slope rising toward +Y; 20 deg slope rising toward -X | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 84 | `gondor_roof_a_30_1-5m_edge_corner_in` | 1.54 x 1.54 x 0.49 | -0.29 to 0.20 | 0.98/0.98/0.59 | 0.14 | 228 | lod2, lod5 | 20 deg slope rising toward +Y; 20 deg slope rising toward +X | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 85 | `gondor_roof_a_30_1-5m_edge_corner_out` | 0.84 x 0.84 x 0.48 | -0.28 to 0.20 | 0.05/0.95/0.59 | -0.19 | 218 | lod2, lod5 | 20 deg slope rising toward +Y; 20 deg slope rising toward -X | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 86 | `gondor_roof_a_30_1-5m_edge_straight` | 1.50 x 0.78 x 0.42 | -0.27 to 0.15 | mid/0.97/0.64 | -0.18 | 28 | lod2, lod5 | 20 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 87 | `gondor_roof_a_30_1-5m_straight` | 1.50 x 1.53 x 0.80 | 0.10 to 0.90 | mid/min/outside | 0.20 | 46 | lod2, lod5 | 20 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 88 | `gondor_roof_a_30_3m_corner_in` | 3.08 x 3.08 x 1.61 | 0.09 to 1.70 | 0.02/0.02/outside | 1.64 | 756 | lod2 | 20 deg slope rising toward +Y; 20 deg slope rising toward +X | 2: roof [lod2], wood [lod2] |
| 89 | `gondor_roof_a_30_3m_corner_out` | 3.08 x 3.08 x 1.61 | 0.09 to 1.70 | 0.98/0.02/outside | 0.18 | 766 | lod2, lod5 | 20 deg slope rising toward +Y; 20 deg slope rising toward -X | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 90 | `gondor_roof_a_30_3m_curv_a` | 3.01 x 3.01 x 1.54 | 0.10 to 1.65 | min/max/outside | 0.23 | 1738 | lod2, lod5 | 27 deg slope rising toward -X; 27 deg slope rising toward -X+Y | 1: (base) [lod2/lod5] |
| 91 | `gondor_roof_a_30_3m_curv_edge_a` | 3.62 x 3.61 x 0.35 | -0.20 to 0.16 | min/max/0.55 | 0.06 | 412 | lod2, lod5 | 27 deg slope rising toward -X; 27 deg slope rising toward -X+Y | 1: (base) [lod2/lod5] |
| 92 | `gondor_roof_a_30_3m_edge_straight` | 3.00 x 0.78 x 0.42 | -0.27 to 0.15 | mid/0.97/0.64 | -0.18 | 28 | lod2, lod5 | 20 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 93 | `gondor_roof_a_30_3m_side_a_clean` | 0.70 x 3.00 x 1.80 | 0.00 to 1.80 | mid/mid/min | 0.32 | 72 | lod2, lod5 | 27 deg slope rising toward +Y; node rot -0/-0/90 (baked) | 2: edge [lod2/lod5], stone [lod2/lod5] |
| 94 | `gondor_roof_a_30_3m_side_a_dest_a` | 0.72 x 1.62 x 1.26 | 0.00 to 1.26 | 0.45/0.06/min | 1.26 | 1936 | lod2, lod4, lod5 | opening 0.15 x 0.51 at z 0.50 (through X) | 5: edge [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 95 | `gondor_roof_a_30_3m_side_a_dirty` | 0.70 x 3.00 x 1.80 | 0.00 to 1.80 | mid/mid/min | 0.32 | 72 | lod2, lod5 | 27 deg slope rising toward +Y; node rot -0/-0/90 (baked) | 2: edge [lod2/lod5], stone [lod2/lod5] |
| 96 | `gondor_roof_a_30_3m_side_b_clean` | 0.70 x 3.00 x 3.30 | 0.00 to 3.30 | mid/mid/min | 1.82 | 504 | lod2, lod5 | 27 deg slope rising toward +Y; node rot -0/-0/90 (baked) | 3: edge [lod2/lod5], stonebrick [lod2], wall [lod2/lod5] |
| 97 | `gondor_roof_a_30_3m_side_b_dest_a` | 0.70 x 3.06 x 2.29 | 0.00 to 2.29 | mid/0.49/min | 2.27 | 1110 | lod2, lod4, lod5 | node rot -0/-0/90 (baked) | 5: edge [lod2/lod4/lod5], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod5], wallinside [lod2/lod4/lod5] |
| 98 | `gondor_roof_a_30_3m_side_b_dirty` | 0.70 x 3.00 x 3.30 | 0.00 to 3.30 | mid/mid/min | 1.82 | 504 | lod2, lod5 | 27 deg slope rising toward +Y; node rot -0/-0/90 (baked) | 3: edge [lod2/lod5], stonebrick [lod2], wall [lod2/lod5] |
| 99 | `gondor_roof_a_30_3m_straight` | 3.00 x 3.03 x 1.54 | 0.10 to 1.65 | mid/min/outside | 0.20 | 82 | lod2, lod5 | 20 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 100 | `gondor_roof_a_30_6m_curv_a` | 6.01 x 6.01 x 1.54 | -1.40 to 0.15 | min/max/0.90 | -1.29 | 3540 | lod2, lod5 | 27 deg slope rising toward -X+Y; 20 deg slope rising toward -X+Y | 1: (base) [lod2/lod5] |
| 101 | `gondor_roof_a_30_6m_curv_edge_a` | 6.56 x 6.55 x 0.32 | -1.67 to -1.35 | min/max/outside | -1.56 | 820 | lod2, lod5 | 26 deg slope rising toward +Y; 26 deg slope rising toward -X+Y | 1: (base) [lod2/lod5] |
| 102 | `gondor_roof_a_45_1-5m_corner_in` | 1.59 x 1.59 x 1.59 | -0.05 to 1.54 | 0.03/0.03/0.03 | 1.49 | 763 | lod2, lod5 | 45 deg slope rising toward +Y; 45 deg slope rising toward +X | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 103 | `gondor_roof_a_45_1-5m_corner_out` | 1.59 x 1.59 x 1.59 | -0.05 to 1.54 | 0.97/0.03/0.03 | 0.04 | 766 | lod2, lod5 | 45 deg slope rising toward -X; 45 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 104 | `gondor_roof_a_45_1-5m_edge_corner_in` | 1.54 x 1.54 x 0.78 | -0.74 to 0.04 | 0.97/0.97/0.94 | 0.00 | 344 | lod2, lod5 | 45 deg slope rising toward +Y; 45 deg slope rising toward +X | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 105 | `gondor_roof_a_45_1-5m_edge_corner_out` | 0.76 x 0.76 x 0.76 | -0.72 to 0.04 | 0.06/0.94/0.94 | -0.63 | 346 | lod2, lod5 | 38 deg slope rising toward -X; 38 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 106 | `gondor_roof_a_45_1-5m_edge_straight` | 1.50 x 0.70 x 0.69 | -0.69 to 0.00 | mid/0.94/max | -0.58 | 36 | lod2, lod5 | 45 deg slope rising toward +Y; 38 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 107 | `gondor_roof_a_45_1-5m_straight` | 1.50 x 1.55 x 1.53 | -0.03 to 1.50 | mid/0.01/0.02 | 0.07 | 64 | lod2, lod5 | 38 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 108 | `gondor_roof_a_45_3m_corner_in` | 3.09 x 3.09 x 3.09 | -0.05 to 3.04 | 0.02/0.02/0.02 | 2.99 | 1528 | lod2, lod5 | 45 deg slope rising toward +X; 45 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 109 | `gondor_roof_a_45_3m_corner_out` | 3.09 x 3.09 x 3.09 | -0.05 to 3.04 | 0.98/0.02/0.02 | 0.04 | 1534 | lod2, lod5 | 45 deg slope rising toward +Y; 45 deg slope rising toward -X | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 110 | `gondor_roof_a_45_3m_curv_a` | 3.02 x 3.02 x 3.03 | -0.03 to 3.00 | min/max/0.01 | 0.14 | 2740 | lod3, lod6 | 45 deg slope rising toward +Y; 38 deg slope rising toward +Y | 1: (base) [lod3/lod6] |
| 111 | `gondor_roof_a_45_3m_curv_edge_a` | 3.36 x 3.36 x 0.37 | -0.37 to 0.00 | min/max/max | -0.28 | 820 | lod2, lod5 | 45 deg slope rising toward -X+Y; 45 deg slope rising toward -X+Y | 1: (base) [lod2/lod5] |
| 112 | `gondor_roof_a_45_3m_edge_straight` | 3.00 x 0.70 x 0.69 | -0.69 to 0.00 | mid/0.94/max | -0.58 | 36 | lod2, lod5 | 45 deg slope rising toward +Y; 38 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 113 | `gondor_roof_a_45_3m_side_a_clean` | 0.52 x 3.04 x 3.50 | 0.00 to 3.50 | mid/max/min | 1.00 | 1308 | lod2, lod4, lod5 | 45 deg slope rising toward +Y; node rot -0/-0/90 (baked) | 2: stonebrick [lod2/lod4], wall [lod2/lod4/lod5] |
| 114 | `gondor_roof_a_45_3m_side_a_dirty` | 0.52 x 3.04 x 3.50 | 0.00 to 3.50 | mid/max/min | 1.00 | 1308 | lod2, lod4, lod5 | 45 deg slope rising toward +Y; node rot -0/-0/90 (baked) | 2: stonebrick [lod2/lod4], wall [lod2/lod4/lod5] |
| 115 | `gondor_roof_a_45_3m_straight` | 3.00 x 3.05 x 3.03 | -0.03 to 3.00 | mid/min/0.01 | 0.07 | 120 | lod2, lod5 | 45 deg slope rising toward +Y; 38 deg slope rising toward +Y | 2: roof [lod2/lod5], wood [lod2/lod5] |
| 116 | `gondor_roof_a_45_6m_curv_a` | 6.02 x 6.02 x 3.03 | -0.03 to 3.00 | min/max/0.01 | 0.08 | 5172 | lod2, lod5 | 45 deg slope rising toward -X+Y; 38 deg slope rising toward -X+Y | 1: (base) [lod2/lod5] |
| 117 | `gondor_roof_a_45_6m_curv_edge_a` | 6.38 x 6.38 x 0.37 | -0.36 to 0.00 | min/max/max | -0.05 | 892 | lod2, lod5 | 43 deg slope rising toward -X+Y; 36 deg slope rising toward -X+Y; node scale 1.059/1.059/1 (baked) | 1: (base) [lod2/lod5] |
| 118 | `gondor_roof_circular_full_6m_a` | 6.05 x 6.05 x 2.41 | 0.00 to 2.41 | mid/mid/min | 1.90 | 5664 | lod2 |  | 1: (base) [lod2] |

### Pillars

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 65 | `gondor_pillar_bot_a` | 0.75 x 0.75 x 0.50 | 0.00 to 0.50 | mid/mid/min | 0.50 | 1566 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 66 | `gondor_pillar_bot_b` | 0.75 x 0.75 x 0.50 | 0.00 to 0.50 | mid/mid/min | 0.50 | 1184 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 67 | `gondor_pillar_bot_c` | 0.75 x 0.75 x 0.50 | 0.00 to 0.50 | mid/mid/min | 0.50 | 1648 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 68 | `gondor_pillar_bot_d` | 0.76 x 0.76 x 0.50 | 0.00 to 0.50 | mid/mid/min | 0.50 | 232 | lod1, lod2, lod3, lod4, lod5 | node scale 1.055/1.055/1 (baked) | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 69 | `gondor_pillar_mid_a1` | 0.50 x 0.50 x 1.00 | -0.50 to 0.50 | mid/mid/mid | 0.50 | 256 | lod2, lod3, lod4, lod5 |  | 1: (base) [lod2/lod3/lod4/lod5] |
| 70 | `gondor_pillar_mid_a2` | 0.50 x 0.50 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 256 | lod2, lod3, lod4, lod5 |  | 1: (base) [lod2/lod3/lod4/lod5] |
| 71 | `gondor_pillar_mid_b1` | 0.50 x 0.50 x 1.00 | -0.50 to 0.50 | mid/mid/mid | 0.50 | 512 | lod2, lod3, lod4, lod5 |  | 1: (base) [lod2/lod3/lod4/lod5] |
| 72 | `gondor_pillar_mid_b2` | 0.50 x 0.50 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 512 | lod2, lod3, lod4, lod5 |  | 1: (base) [lod2/lod3/lod4/lod5] |
| 73 | `gondor_pillar_top_a` | 0.75 x 0.75 x 0.50 | 0.00 to 0.50 | mid/mid/min | 0.50 | 1432 | lod1, lod2, lod3, lod4, lod5, lod6 | node scale 1.071/1.071/1 (baked) | 1: (base) [lod1/lod2/lod3/lod4/lod5/lod6] |
| 74 | `gondor_pillar_top_b` | 0.99 x 0.98 x 1.00 | 0.00 to 1.00 | mid/mid/min | 1.00 | 1692 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 75 | `gondor_pillar_top_c` | 0.89 x 0.89 x 1.00 | 0.00 to 1.00 | mid/mid/min | 1.00 | 4256 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 76 | `gondor_pillar_top_d` | 0.75 x 0.75 x 1.00 | 0.00 to 1.00 | mid/mid/min | 1.00 | 1056 | lod2, lod3, lod4, lod5 |  | 1: (base) [lod2/lod3/lod4/lod5] |

### Buttress

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 32 | `gondor_buttress_a_clean` | 1.02 x 0.56 x 6.03 | 0.00 to 6.03 | mid/0.98/min | 5.52 | 608 | lod2, lod4, lod5 |  | 2: roof [lod2/lod4/lod5], wall [lod2/lod4/lod5] |
| 33 | `gondor_buttress_a_dirty` | 1.02 x 0.56 x 6.03 | 0.00 to 6.03 | mid/0.98/min | 5.52 | 608 | lod2, lod4, lod5 |  | 2: roof [lod2/lod4/lod5], wall [lod2/lod4/lod5] |
| 34 | `gondor_buttress_arch_a_clean` | 6.00 x 0.60 x 6.02 | 0.00 to 6.02 | mid/mid/min | 6.01 | 1144 | lod1, lod3, lod5 |  | 3: arch [lod1/lod3/lod5], roof [lod1/lod3/lod5], wall [lod1/lod3/lod5] |
| 35 | `gondor_buttress_arch_a_dirty` | 6.00 x 0.60 x 6.02 | 0.00 to 6.02 | mid/mid/min | 6.01 | 1144 | lod1, lod3, lod5 |  | 3: arch [lod1/lod3/lod5], roof [lod1/lod3/lod5], wall [lod1/lod3/lod5] |

### Stairs

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 136 | `gondor_stairs_12m_a_clean` | 3.00 x 12.01 x 12.35 | -0.01 to 12.34 | mid/max/min | 4.20 | 4170 | lod2, lod4, lod6 |  | 1: (base) [lod2/lod4/lod6] |
| 137 | `gondor_stairs_curv_3m_a_dest_a` | 3.05 x 3.04 x 3.69 | -0.23 to 3.45 | 0.03/0.98/0.06 | 0.30 | 3068 | lod2, lod4, lod6 | opening 1.87 x 0.20 at z -0.23 (through X) | 3: stairs [lod2/lod4/lod6], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4] |
| 138 | `gondor_stairs_cuv_3m_a_clean` | 3.08 x 3.03 x 3.69 | -0.23 to 3.45 | 0.03/0.99/0.06 | 2.40 | 1420 | lod2, lod4, lod6 | opening 1.87 x 0.20 at z -0.23 (through X) | 1: (base) [lod2/lod4/lod6] |
| 139 | `gondor_stairs_cuv_3m_a_dirty` | 3.08 x 3.03 x 3.69 | -0.23 to 3.45 | 0.03/0.99/0.06 | 2.40 | 1420 | lod2, lod4, lod6 | opening 1.87 x 0.20 at z -0.23 (through X) | 1: (base) [lod2/lod4/lod6] |
| 140 | `gondor_stair_trim_bot_clean` | 3.02 x 0.36 x 0.70 | -0.24 to 0.46 | mid/0.28/0.35 | 0.12 | 184 | lod2, lod4, lod5 | opening 2.52 x 0.20 at z -0.24 (through Y) | 1: (base) [lod2/lod4/lod5] |
| 141 | `gondor_stair_trim_bot_dirty` | 3.02 x 0.36 x 0.70 | -0.24 to 0.46 | mid/0.28/0.35 | 0.12 | 184 | lod2, lod4, lod5 | opening 2.52 x 0.20 at z -0.24 (through Y); **no bo_** | 1: (base) [lod2/lod4/lod5] |
| 142 | `gondor_stair_trim_top_clean` | 3.02 x 0.45 x 0.75 | -0.30 to 0.45 | mid/mid/0.40 | 0.12 | 114 | lod2, lod4, lod5 | opening 2.52 x 0.30 at z -0.30 (through Y); 59 deg slope rising toward -Y | 1: (base) [lod2/lod4/lod5] |
| 143 | `gondor_stair_trim_top_dirty` | 3.02 x 0.45 x 0.75 | -0.30 to 0.45 | mid/mid/0.40 | 0.12 | 114 | lod2, lod4, lod5 | opening 2.52 x 0.30 at z -0.30 (through Y); 59 deg slope rising toward -Y; **no bo_** | 1: (base) [lod2/lod4/lod5] |
| 144 | `gondor_stairs_3m_a_clean` | 3.02 x 3.10 x 3.69 | -0.23 to 3.45 | mid/0.98/0.06 | 1.20 | 1264 | lod2, lod4, lod6 | opening 2.52 x 0.20 at z -0.23 (through Y); 45 deg slope rising toward -Y | 3: edge [lod2/lod4/lod6], stairs [lod2/lod4/lod6], wall [lod2/lod4/lod6] |
| 145 | `gondor_stairs_3m_a_dest_a` | 3.01 x 3.17 x 3.69 | -0.23 to 3.45 | mid/0.96/0.06 | 0.31 | 3046 | lod2, lod4, lod6 | opening 2.51 x 0.20 at z -0.23 (through Y); 45 deg slope rising toward -Y | 6: edge [lod2/lod4/lod6], scatrubble [lod2/lod4], stairs [lod2/lod4/lod6], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], wall [lod2/lod4/lod6] |
| 146 | `gondor_stairs_3m_a_dirty` | 3.02 x 3.10 x 3.69 | -0.23 to 3.45 | mid/0.98/0.06 | 1.20 | 1264 | lod2, lod4, lod6 | opening 2.52 x 0.20 at z -0.23 (through Y); 45 deg slope rising toward -Y | 3: edge [lod2/lod4/lod6], stairs [lod2/lod4/lod6], wall [lod2/lod4/lod6] |
| 147 | `gondor_stairs_3m_b_clean` | 3.04 x 3.01 x 3.35 | -0.01 to 3.34 | mid/max/min | 0.30 | 1236 | lod2, lod4, lod6 | node scale 1.5/1/1.5 (baked) | 4: side [lod2/lod4/lod6], stairs [lod2/lod4/lod6], stonebrick [lod2], wall [lod2/lod4/lod6] |
| 148 | `gondor_stairs_3m_b_dest_a` | 3.05 x 3.04 x 3.35 | -0.01 to 3.34 | 0.49/max/min | 0.60 | 3508 | lod2, lod4, lod6 |  | 7: rubble [lod2/lod4/lod6], side [lod2/lod4/lod6], stairs [lod2/lod4/lod6], stonebrick [lod2/lod4], stonebrickdest [lod2/lod4], stonedest [lod2/lod4], wall [lod2/lod4/lod6] |
| 149 | `gondor_stairs_3m_b_dirty` | 3.04 x 3.01 x 3.35 | -0.01 to 3.34 | mid/max/min | 0.30 | 1236 | lod2, lod4, lod6 | node scale 1.5/1/1.5 (baked) | 4: side [lod2/lod4/lod6], stairs [lod2/lod4/lod6], stonebrick [lod2], wall [lod2/lod4/lod6] |

### Ground and paving

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 46 | `gondor_ground_3m_curv_a_dest_a` | 3.01 x 2.06 x 0.30 | -0.06 to 0.24 | min/0.97/0.19 | 0.10 | 992 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 47 | `gondor_ground_3m_curv_a_dirty` | 3.01 x 3.01 x 0.20 | -0.07 to 0.13 | min/max/0.37 | 0.10 | 1002 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 48 | `gondor_ground_3m_curv_a_normal` | 3.01 x 3.01 x 0.20 | -0.07 to 0.13 | min/max/0.37 | 0.10 | 1002 | lod2, lod4, lod6 |  | 1: (base) [lod2/lod4/lod6] |
| 49 | `gondor_ground_6m_curv_a_dest_a` | 5.02 x 6.01 x 0.21 | -0.06 to 0.16 | min/max/0.27 | 0.10 | 4396 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 50 | `gondor_ground_6m_curv_a_dirty` | 6.01 x 6.02 x 0.20 | -0.07 to 0.13 | min/max/0.35 | 0.10 | 3200 | lod2, lod4, lod6 |  | 1: (base) [lod2/lod4/lod6] |
| 51 | `gondor_ground_6m_curv_a_normal` | 6.01 x 6.02 x 0.20 | -0.07 to 0.13 | min/max/0.35 | 0.10 | 3200 | lod2, lod4, lod6 |  | 1: (base) [lod2/lod4/lod6] |
| 52 | `gondor_ground_1m_a_cracked` | 1.01 x 1.01 x 0.19 | -0.06 to 0.13 | mid/mid/0.31 | 0.10 | 320 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 53 | `gondor_ground_1m_a_dirty` | 1.01 x 1.01 x 0.16 | -0.06 to 0.10 | mid/mid/0.37 | 0.10 | 188 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 54 | `gondor_ground_1m_a_normal` | 1.01 x 1.01 x 0.16 | -0.06 to 0.10 | mid/mid/0.37 | 0.10 | 188 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 55 | `gondor_ground_1m_b_cracked` | 1.06 x 1.13 x 0.30 | -0.06 to 0.24 | 0.48/0.45/0.19 | 0.11 | 292 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 56 | `gondor_ground_2m_a_cracked` | 2.01 x 2.01 x 0.18 | -0.06 to 0.13 | mid/mid/0.30 | 0.10 | 700 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 57 | `gondor_ground_2m_a_dirty` | 2.01 x 2.01 x 0.17 | -0.06 to 0.11 | mid/mid/0.35 | 0.10 | 532 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 58 | `gondor_ground_2m_a_normal` | 2.01 x 2.01 x 0.17 | -0.06 to 0.11 | mid/mid/0.35 | 0.10 | 532 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 59 | `gondor_ground_2m_b_cracked` | 2.02 x 2.02 x 0.18 | -0.06 to 0.13 | mid/mid/0.30 | 0.10 | 684 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 60 | `gondor_ground_3m_a_cracked` | 3.02 x 3.02 x 0.21 | -0.06 to 0.15 | mid/mid/0.28 | 0.10 | 1520 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 61 | `gondor_ground_3m_a_dirty` | 3.02 x 3.02 x 0.17 | -0.06 to 0.11 | mid/mid/0.35 | 0.10 | 1010 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 62 | `gondor_ground_3m_a_normal` | 3.02 x 3.02 x 0.17 | -0.06 to 0.11 | mid/mid/0.35 | 0.10 | 1010 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 63 | `gondor_ground_3m_b_cracked` | 2.02 x 3.02 x 0.21 | -0.06 to 0.16 | 0.75/mid/0.27 | 0.10 | 1376 | lod2, lod4, lod5 |  | 2: floor [lod2/lod4/lod5], wood [lod2/lod4/lod5] |
| 64 | `gondor_ground_platform_3m_a_normal` | 3.00 x 3.00 x 1.54 | -1.40 to 0.14 | mid/mid/0.91 | 0.10 | 76 | lod5 |  | 1: (base) [lod5] |

### Railing

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 77 | `gondor_railing_corner_a1_normal` | 0.30 x 0.30 x 1.20 | 0.00 to 1.20 | mid/mid/min | 1.20 | 240 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 78 | `gondor_railing_full_a1_normal` | 3.15 x 0.30 x 1.20 | 0.00 to 1.20 | 0.05/mid/min | 1.17 | 1680 | lod3, lod6 | opening 0.40 x 0.90 at z 0.15 (through Y); opening 0.40 x 0.90 at z 0.15 (through Y); +3 more openings | 1: (base) [lod3/lod6] |
| 79 | `gondor_railing_mid_a1_normal` | 3.00 x 0.28 x 1.11 | 0.06 to 1.17 | min/mid/outside | 1.17 | 1440 | lod3, lod6 | opening 0.40 x 0.91 at z 0.16 (through Y); opening 0.40 x 0.91 at z 0.16 (through Y); +2 more openings | 1: (base) [lod3/lod6] |
| 80 | `gondor_railing_stairs_a1_normal` | 0.39 x 3.19 x 4.37 | 0.00 to 4.37 | mid/0.94/min | 1.58 | 1680 | lod3, lod6 | opening 0.35 x 1.21 at z 2.46 (through X); opening 0.35 x 1.21 at z 1.96 (through X); +3 more openings; 45 deg slope rising toward -Y; node rot -0/0/-90 (baked) | 1: (base) [lod3/lod6] |

### Bridge

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 4 | `gd_bridge_a_clean` | 15.00 x 6.20 x 9.02 | 0.00 to 9.02 | mid/mid/min | 9.01 | 6850 | lod2, lod4, lod5 |  | 7: arch [lod2/lod4/lod5], archinside [lod2/lod4/lod5], base [lod2/lod4/lod5], basetrim [lod2/lod4/lod5], stonebrick [lod2], tiles [lod2/lod4/lod5], trim [lod2/lod4/lod5] |
| 5 | `gd_bridge_a_dirty` | 15.00 x 6.20 x 9.02 | 0.00 to 9.02 | mid/mid/min | 9.01 | 6850 | lod2, lod4, lod5 |  | 7: arch [lod2/lod4/lod5], archinside [lod2/lod4/lod5], base [lod2/lod4/lod5], basetrim [lod2/lod4/lod5], stonebrick [lod2], tiles [lod2/lod4/lod5], trim [lod2/lod4/lod5] |
| 6 | `gd_bridge_a_ext_clean` | 8.00 x 6.20 x 6.00 | -6.00 to 0.00 | mid/mid/max | 0.00 | 1980 | lod2, lod4, lod5 |  | 3: arch [lod2/lod4/lod5], archinside [lod2/lod4/lod5], base [lod2/lod4/lod5] |
| 7 | `gd_bridge_a_ext_dirty` | 8.00 x 6.20 x 6.00 | -6.00 to 0.00 | mid/mid/max | 0.00 | 1980 | lod2, lod4, lod5 |  | 3: arch [lod2/lod4/lod5], archinside [lod2/lod4/lod5], base [lod2/lod4/lod5] |
| 8 | `gd_bridge_a_merlon_a_clean` | 6.25 x 0.26 x 1.01 | 8.50 to 9.51 | outside/outside/outside | 9.51 | 316 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 9 | `gd_bridge_a_merlon_a_dirty` | 6.25 x 0.26 x 1.01 | 8.50 to 9.51 | outside/outside/outside | 9.51 | 316 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 10 | `gd_bridge_a_merlon_b_clean` | 2.50 x 0.26 x 1.01 | 8.50 to 9.51 | mid/outside/outside | 9.50 | 124 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 11 | `gd_bridge_a_merlon_b_dirty` | 2.50 x 0.26 x 1.01 | 8.50 to 9.51 | mid/outside/outside | 9.50 | 124 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 12 | `gd_bridge_a_merlon_c_clean` | 6.25 x 0.26 x 1.01 | 8.50 to 9.51 | outside/outside/outside | 9.50 | 316 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 13 | `gd_bridge_a_merlon_c_dirty` | 6.25 x 0.26 x 1.01 | 8.50 to 9.51 | outside/outside/outside | 9.50 | 316 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 14 | `gd_bridge_a_merlon_d_clean` | 6.25 x 0.26 x 1.01 | 8.50 to 9.51 | outside/outside/outside | 9.51 | 316 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 15 | `gd_bridge_a_merlon_d_dirty` | 6.25 x 0.26 x 1.01 | 8.50 to 9.51 | outside/outside/outside | 9.51 | 316 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 16 | `gd_bridge_a_merlon_e_clean` | 2.50 x 0.26 x 1.01 | 8.50 to 9.51 | mid/outside/outside | 9.50 | 124 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 17 | `gd_bridge_a_merlon_e_dirty` | 2.50 x 0.26 x 1.01 | 8.50 to 9.51 | mid/outside/outside | 9.50 | 124 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 18 | `gd_bridge_a_merlon_f_clean` | 6.25 x 0.26 x 1.01 | 8.50 to 9.51 | outside/outside/outside | 9.50 | 316 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 19 | `gd_bridge_a_merlon_f_dirty` | 6.25 x 0.26 x 1.01 | 8.50 to 9.51 | outside/outside/outside | 9.50 | 316 | lod2, lod4, lod5 |  | 1: (base) [lod2/lod4/lod5] |
| 20 | `gd_bridge_a_middle_clean` | 6.20 x 6.20 x 9.03 | 0.00 to 9.03 | mid/mid/min | 9.00 | 1402 | lod2, lod4, lod5 | node rot -0/-0/90 (baked) | 6: arch [lod2/lod4/lod5], archinside [lod2/lod4/lod5], base [lod2/lod4/lod5], column [lod2/lod4/lod5], tiles [lod2/lod4/lod5], trim [lod2/lod4] |
| 21 | `gd_bridge_a_middle_dirty` | 6.20 x 6.20 x 9.03 | 0.00 to 9.03 | mid/mid/min | 9.00 | 1402 | lod2, lod4, lod5 |  | 6: arch [lod2/lod4/lod5], archinside [lod2/lod4/lod5], base [lod2/lod4/lod5], column [lod2/lod4/lod5], tiles [lod2/lod4/lod5], trim [lod2/lod4] |
| 22 | `gd_bridge_a_middle_ext_clean` | 2.73 x 2.73 x 6.00 | -6.00 to 0.00 | mid/mid/max | 0.00 | 1132 | none | node rot -0/-0/90 (baked) | 3: column [-], column2 [-], column4 [-] |
| 23 | `gd_bridge_a_middle_ext_dirty` | 2.73 x 2.73 x 6.00 | -6.00 to 0.00 | mid/mid/max | 0.00 | 748 | lod2, lod4 |  | 1: column [lod2/lod4] |
| 24 | `gd_bridge_sup_a_clean` | 3.33 x 2.69 x 10.03 | -0.03 to 10.00 | mid/0.99/min | 9.00 | 310 | lod2, lod4 |  | 2: base [lod2/lod4], trim [lod2/lod4] |
| 25 | `gd_bridge_sup_a_dirty` | 3.33 x 2.69 x 10.03 | -0.03 to 10.00 | mid/0.99/min | 9.00 | 310 | lod2, lod4 |  | 2: base [lod2/lod4], trim [lod2/lod4] |
| 26 | `gd_bridge_sup_a_ext_clean` | 2.98 x 2.54 x 6.00 | -6.00 to 0.00 | mid/0.98/max | 0.00 | 34 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 27 | `gd_bridge_sup_a_ext_dirty` | 2.98 x 2.54 x 6.00 | -6.00 to 0.00 | mid/0.98/max | 0.00 | 34 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 28 | `gd_bridge_sup_side_a_clean` | 4.95 x 0.53 x 9.95 | 0.00 to 9.95 | 0.49/0.95/min | 9.95 | 168 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 29 | `gd_bridge_sup_side_a_dirty` | 4.95 x 0.53 x 9.95 | 0.00 to 9.95 | 0.49/0.95/min | 9.95 | 168 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 30 | `gd_bridge_sup_side_a_ext_clean` | 4.95 x 0.53 x 6.00 | -6.00 to 0.00 | 0.49/0.95/max | 0.00 | 80 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 31 | `gd_bridge_sup_side_a_ext_dirty` | 4.95 x 0.53 x 6.00 | -6.00 to 0.00 | 0.49/0.95/max | 0.00 | 80 | lod2, lod4 |  | 1: (base) [lod2/lod4] |

### Castle tower and wall (meshes)

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 36 | `gondor_castle_wall_tower_L1_A` | 9.00 x 9.99 x 25.52 | -10.01 to 15.52 | mid/0.65/0.39 | 0.00 | 2330 | lod3, lod6 | opening 1.09 x 2.55 at z 9.98 (through X) | 1: wall [lod3/lod6] |
| 37 | `gondor_castle_wall_tower_L1_A_int` | 7.29 x 8.24 x 15.15 | 0.00 to 15.15 | mid/0.68/min | 10.05 | 16536 | lod3, lod6 | opening 2.56 x 2.42 at z 10.06 (through Y); opening 1.92 x 2.93 at z 5.09 (through Y); +14 more openings | 2: (base) [lod3/lod6], platform [lod3/lod6] |
| 38 | `gondor_castle_wall_tower_L1_A_top` | 7.24 x 8.23 x 0.12 | 14.94 to 15.06 | mid/0.68/outside | 15.05 | 3986 | lod3, lod6 | **no bo_** | 1: (base) [lod3/lod6] |
| 39 | `gondor_castle_wall_20m_L1_A` | 20.00 x 6.23 x 15.31 | -5.00 to 10.31 | mid/0.48/0.33 | 10.02 | 15130 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 40 | `gondor_castle_wall_20m_L1_A_dest_A` | 20.00 x 19.36 x 15.30 | -5.00 to 10.30 | mid/mid/0.33 | 10.00 | 34282 | lod3, lod6 |  | 4: brick [lod3/lod6], brickdest [lod3/lod6], rubble [lod3/lod6], wall [lod3/lod6] |
| 41 | `gondor_castle_wall_20m_L1_A_M1` | 4.26 x 0.94 x 2.70 | 9.09 to 11.79 | outside/outside/outside | 11.79 | 2066 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 42 | `gondor_castle_wall_20m_L1_A_M2` | 4.71 x 0.94 x 2.70 | 9.09 to 11.79 | outside/outside/outside | 11.79 | 2174 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 43 | `gondor_castle_wall_20m_L1_A_M3` | 5.04 x 0.94 x 2.70 | 9.09 to 11.79 | 0.47/outside/outside | 10.99 | 2174 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 44 | `gondor_castle_wall_20m_L1_A_M4` | 4.98 x 0.94 x 2.70 | 9.09 to 11.79 | outside/outside/outside | 11.79 | 2174 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 45 | `gondor_castle_wall_20m_L1_A_M5` | 4.58 x 0.94 x 2.70 | 9.09 to 11.79 | outside/outside/outside | 11.79 | 2066 | lod3, lod6 |  | 1: (base) [lod3/lod6] |

### Bricks, rubble and broken stone

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 0 | `gondor_stone_brick_a` | 1.00 x 0.25 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 108 | lod2 |  | 1: (base) [lod2] |
| 1 | `gondor_stone_brick_b` | 1.00 x 0.25 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 108 | lod2 |  | 1: (base) [lod2] |
| 2 | `gondor_stone_brick_c` | 1.00 x 0.25 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 108 | lod2 |  | 1: (base) [lod2] |
| 3 | `gondor_stone_brick_d` | 1.00 x 0.25 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 108 | lod2 |  | 1: (base) [lod2] |
| 119 | `gondor_brick_rubble_a` | 3.45 x 3.62 x 1.00 | 0.00 to 1.01 | 0.49/0.55/min | 0.25 | 2716 | lod2, lod4, lod6 |  | 2: brick [lod2/lod4/lod6], brickdest [lod2/lod4] |
| 120 | `gondor_brick_rubble_b` | 3.80 x 4.74 x 0.72 | 0.01 to 0.73 | mid/0.51/min | 0.26 | 4336 | lod1, lod2, lod3, lod4, lod6 |  | 3: brick [lod1/lod2/lod3/lod4/lod6], brickdest [lod1/lod2/lod3/lod4], stonebroken [lod1/lod2/lod3/lod4] |
| 121 | `gondor_brick_rubble_c` | 3.51 x 4.17 x 0.93 | 0.00 to 0.93 | mid/0.44/min | 0.25 | 5314 | lod1, lod3, lod5, lod6 | bo_ is bo_gondor_brick_rubble_c.brick | 2: brick [lod1/lod3/lod5/lod6], brickdest [lod1/lod3/lod5] |
| 122 | `gondor_brick_rubble_corner_a` | 3.46 x 7.07 x 1.04 | 0.00 to 1.04 | min/0.51/min | 0.25 | 7786 | lod1, lod2, lod3, lod4, lod6 |  | 3: brick [lod1/lod2/lod3/lod4/lod6], brickdest [lod1/lod2/lod3/lod4], stonebroken [lod1/lod2/lod3/lod4] |
| 123 | `gondor_brick_rubble_corner_b` | 4.81 x 4.51 x 1.30 | -0.13 to 1.17 | min/min/0.10 | 0.12 | 6722 | lod1, lod2, lod3, lod6 |  | 2: brick [lod1/lod2/lod3/lod6], brickdest [lod1/lod2/lod3] |
| 124 | `gondor_brick_rubble_corner_c` | 4.99 x 4.00 x 1.05 | 0.01 to 1.06 | min/min/min | 0.26 | 6722 | lod1, lod2, lod5, lod6 |  | 2: brick [lod1/lod2/lod5/lod6], brickdest [lod1/lod2/lod5] |
| 125 | `gondor_brick_rubble_corner_d` | 4.00 x 3.84 x 1.25 | 0.00 to 1.25 | min/min/min | 0.25 | 6722 | lod1, lod2, lod4, lod6 |  | 2: brick [lod1/lod2/lod4/lod6], brickdest [lod1/lod2/lod4] |
| 126 | `gondor_brick_rubble_ground_a` | 5.59 x 2.66 x 0.50 | 0.00 to 0.51 | 0.48/0.58/min | 0.25 | 2376 | lod2, lod6 |  | 1: (base) [lod2/lod6] |
| 127 | `gondor_brick_rubble_ground_b` | 3.65 x 3.62 x 0.53 | 0.00 to 0.53 | 0.51/0.56/min | 0.25 | 1404 | lod2, lod6 |  | 1: (base) [lod2/lod6] |
| 128 | `gondor_brick_rubble_ground_c` | 5.78 x 3.66 x 0.61 | 0.01 to 0.62 | 0.51/0.49/min | 0.26 | 2376 | lod2, lod6 |  | 1: (base) [lod2/lod6] |
| 129 | `gondor_brick_rubble_ground_d` | 2.94 x 4.55 x 0.53 | 0.01 to 0.53 | 0.44/0.46/min | 0.26 | 1404 | lod2, lod6 |  | 1: (base) [lod2/lod6] |
| 130 | `gondor_brick_rubble_small_a` | 2.63 x 1.83 x 0.98 | 0.00 to 0.98 | 0.47/0.55/min | 0.25 | 2058 | lod1, lod2, lod3, lod4, lod6 |  | 3: brick [lod1/lod2/lod3/lod4/lod6], brickdest [lod1/lod2/lod3/lod4], stonebroken [lod1/lod2/lod3/lod4] |
| 131 | `gondor_brick_rubble_small_b` | 2.56 x 2.69 x 0.80 | 0.01 to 0.81 | 0.54/0.58/min | 0.26 | 2060 | lod1, lod2, lod3, lod4, lod6 |  | 3: brick [lod1/lod2/lod3/lod4/lod6], brickdest [lod1/lod2/lod3/lod4], stonebroken [lod1/lod2/lod3/lod4] |
| 132 | `gondor_brick_rubble_wall_a` | 5.79 x 2.62 x 0.83 | 0.00 to 0.84 | 0.47/0.74/min | 0.25 | 2114 | lod2, lod3, lod4, lod6 |  | 2: brick [lod2/lod3/lod4/lod6], brickdest [lod2/lod3/lod4] |
| 133 | `gondor_brick_rubble_wall_b` | 5.11 x 2.77 x 1.00 | 0.01 to 1.01 | 0.56/0.73/min | 0.26 | 2114 | lod2, lod3, lod4, lod6 |  | 2: brick [lod2/lod3/lod4/lod6], brickdest [lod2/lod3/lod4] |
| 134 | `gondor_brick_rubble_wall_c` | 4.38 x 1.66 x 1.51 | 0.00 to 1.51 | 0.48/0.72/min | 0.50 | 2898 | lod2, lod3, lod4, lod6 |  | 2: brick [lod2/lod3/lod4/lod6], brickdest [lod2/lod3/lod4] |
| 135 | `gondor_brick_rubble_wall_d` | 4.93 x 3.42 x 0.80 | 0.00 to 0.80 | 0.51/0.58/min | 0.25 | 2898 | lod2, lod3, lod4, lod6 |  | 2: brick [lod2/lod3/lod4/lod6], brickdest [lod2/lod3/lod4] |
| 150 | `gondor_stone_brick_destroyed_a` | 0.55 x 0.25 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 244 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 151 | `gondor_stone_brick_destroyed_b` | 0.50 x 0.25 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 308 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 152 | `gondor_stone_brick_destroyed_c` | 0.57 x 0.25 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 306 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 153 | `gondor_stone_brick_destroyed_d` | 0.51 x 0.25 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 374 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 154 | `gondor_stone_brick_destroyed_e` | 0.68 x 0.25 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 258 | lod2, lod4 |  | 1: (base) [lod2/lod4] |
| 155 | `gondor_stone_brick_destroyed_f` | 0.69 x 0.25 x 0.50 | -0.25 to 0.25 | mid/mid/mid | 0.25 | 220 | lod2, lod4 | node scale 1.002/0.999/1.001 (baked) | 1: (base) [lod2/lod4] |
| 156 | `gondor_stone_broken_a` | 0.57 x 0.37 x 0.26 | -0.13 to 0.13 | mid/mid/mid | 0.12 | 112 | lod1, lod2, lod3, lod4 |  | 1: (base) [lod1/lod2/lod3/lod4] |
| 157 | `gondor_stone_broken_b` | 0.54 x 0.45 x 0.21 | -0.10 to 0.10 | mid/mid/mid | 0.09 | 134 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 158 | `gondor_stone_broken_c` | 0.40 x 0.37 x 0.28 | -0.14 to 0.14 | mid/mid/mid | 0.13 | 162 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 159 | `gondor_stone_broken_d` | 0.40 x 0.28 x 0.37 | -0.19 to 0.18 | mid/mid/mid | 0.09 | 124 | lod1, lod2, lod3, lod4 |  | 1: (base) [lod1/lod2/lod3/lod4] |

## The kit: `walls/`

### Castle walls and towers

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 0 | `gd_ramp_large_a1` | 80.00 x 14.12 x 60.30 | -20.00 to 40.30 | mid/mid/0.33 | 0.50 | 5352 | lod3, lod6 | 27 deg slope rising toward +X; node rot -0/0/-90 (baked) | 1: (base) [lod3/lod6] |
| 1 | `gondor_castle_gatehouse_l1_a` | 16.40 x 12.40 x 20.05 | -5.00 to 15.05 | mid/mid/0.25 | 15.00 | 10244 | lod3, lod6 | opening 5.69 x 12.81 at z -5.00 (through Y) | 1: (base) [lod3/lod6] |
| 2 | `gondor_castle_gatehouse_l1_a_m1` | 4.26 x 0.94 x 2.70 | 14.09 to 16.79 | outside/outside/outside | 16.79 | 1950 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 3 | `gondor_castle_gatehouse_l1_a_m2` | 4.71 x 0.94 x 2.70 | 14.09 to 16.79 | 0.06/outside/outside | 16.79 | 2058 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 4 | `gondor_castle_gatehouse_l1_a_m3` | 5.04 x 0.94 x 2.70 | 14.09 to 16.79 | 0.87/outside/outside | 15.99 | 2058 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 5 | `gondor_castle_gatehouse_l1_a_m4` | 4.53 x 0.94 x 2.70 | 14.09 to 16.79 | outside/outside/outside | 16.79 | 1950 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 6 | `gondor_castle_wall_20m_l3_a` | 20.00 x 6.87 x 35.06 | -20.00 to 15.06 | mid/0.45/0.57 | 15.00 | 9712 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 7 | `gondor_castle_wall_20m_l3_a_m1` | 4.00 x 1.19 x 4.88 | 13.00 to 17.88 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.20 x 1.26 at z 13.00 (through Y) | 1: (base) [lod3/lod6] |
| 8 | `gondor_castle_wall_20m_l3_a_m2` | 4.00 x 1.19 x 4.88 | 13.00 to 17.88 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.20 x 1.26 at z 13.00 (through Y) | 1: (base) [lod3/lod6] |
| 9 | `gondor_castle_wall_20m_l3_a_m3` | 4.00 x 1.19 x 4.88 | 13.00 to 17.88 | mid/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.20 x 1.26 at z 13.00 (through Y) | 1: (base) [lod3/lod6] |
| 10 | `gondor_castle_wall_20m_l3_a_m4` | 4.00 x 1.19 x 4.88 | 13.00 to 17.88 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through Y) | 1: (base) [lod3/lod6] |
| 11 | `gondor_castle_wall_20m_l3_a_m5` | 4.00 x 1.19 x 4.88 | 13.00 to 17.88 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through Y) | 1: (base) [lod3/lod6] |
| 12 | `gondor_castle_wall_20m_l3_a_m6` | 20.00 x 0.54 x 3.01 | 13.00 to 16.01 | mid/outside/outside | 15.84 | 5624 | lod3, lod6 | opening 1.33 x 1.17 at z 13.00 (through Y); opening 1.33 x 1.17 at z 13.00 (through Y); +4 more openings; 31 deg slope rising toward +Y | 1: (base) [lod3/lod6] |
| 13 | `gondor_castle_wall_20m_l3_a_dest` | 20.00 x 19.34 x 35.63 | -20.00 to 15.63 | mid/mid/0.56 | 15.00 | 23084 | lod3, lod6 |  | 3: brick [lod3/lod6], rubble [lod3/lod6], wall [lod3/lod6] |
| 14 | `gondor_castle_wall_tower_l1_b` | 16.00 x 8.40 x 30.07 | -20.05 to 10.02 | mid/0.02/0.67 | 10.00 | 2450 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 15 | `gondor_castle_wall_tower_l1_b_m1` | 1.22 x 4.64 x 3.91 | 8.40 to 12.31 | outside/min/outside | 9.95 | 1459 | lod3, lod6 | opening 1.16 x 1.00 at z 8.40 (through X) | 1: (base) [lod3/lod6] |
| 16 | `gondor_castle_wall_tower_l1_b_m2` | 3.10 x 3.10 x 3.91 | 8.40 to 12.31 | outside/outside/outside | 9.99 | 1488 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 17 | `gondor_castle_wall_tower_l1_b_m3` | 3.10 x 3.10 x 3.91 | 8.40 to 12.31 | outside/outside/outside | 9.99 | 1488 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 18 | `gondor_castle_wall_tower_l1_b_m4` | 4.69 x 1.22 x 3.91 | 8.40 to 12.31 | max/outside/outside | 9.95 | 1459 | lod3, lod6 | opening 1.16 x 1.00 at z 8.40 (through Y) | 1: (base) [lod3/lod6] |
| 19 | `gondor_castle_wall_tower_l1_b_m5` | 4.69 x 1.22 x 3.91 | 8.40 to 12.31 | min/outside/outside | 9.95 | 1459 | lod3, lod6 | opening 1.16 x 1.00 at z 8.40 (through Y) | 1: (base) [lod3/lod6] |
| 20 | `gondor_castle_wall_tower_l1_b_m6` | 3.10 x 3.10 x 3.91 | 8.40 to 12.31 | outside/outside/outside | 9.99 | 1488 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 21 | `gondor_castle_wall_tower_l1_b_m7` | 3.10 x 3.10 x 3.91 | 8.40 to 12.31 | outside/outside/outside | 9.99 | 1488 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 22 | `gondor_castle_wall_tower_l1_b_m8` | 1.22 x 4.64 x 3.91 | 8.40 to 12.31 | outside/min/outside | 9.95 | 1459 | lod3, lod6 | opening 1.16 x 1.00 at z 8.40 (through X) | 1: (base) [lod3/lod6] |
| 23 | `gondor_castle_wall_tower_l1_c` | 7.14 x 9.14 x 38.74 | -5.05 to 33.69 | mid/mid/0.13 | 30.12 | 18192 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 24 | `gondor_castle_wall_tower_l1_d` | 8.20 x 30.66 x 20.00 | -5.00 to 15.00 | mid/0.61/0.25 | 15.00 | 1923 | lod3, lod6 | opening 0.52 x 1.79 at z 1.15 (through X); opening 0.52 x 1.54 at z 1.15 (through X); +1 more openings | 1: (base) [lod3/lod6] |
| 25 | `gondor_castle_wall_tower_l1_d1_m1` | 1.19 x 4.00 x 4.58 | 13.00 to 17.58 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through X) | 1: (base) [lod3/lod6] |
| 26 | `gondor_castle_wall_tower_l1_d1_m10` | 1.19 x 4.00 x 4.58 | 13.00 to 17.58 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through X) | 1: (base) [lod3/lod6] |
| 27 | `gondor_castle_wall_tower_l1_d1_m11` | 1.19 x 4.00 x 4.58 | 13.00 to 17.58 | outside/0.37/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through X) | 1: (base) [lod3/lod6] |
| 28 | `gondor_castle_wall_tower_l1_d1_m12` | 1.19 x 4.00 x 4.58 | 13.00 to 17.58 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through X) | 1: (base) [lod3/lod6] |
| 29 | `gondor_castle_wall_tower_l1_d1_m13` | 1.19 x 4.00 x 4.58 | 13.00 to 17.58 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through X) | 1: (base) [lod3/lod6] |
| 30 | `gondor_castle_wall_tower_l1_d1_m2` | 1.19 x 4.00 x 4.58 | 13.00 to 17.58 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through X) | 1: (base) [lod3/lod6] |
| 31 | `gondor_castle_wall_tower_l1_d1_m3` | 1.19 x 4.00 x 4.58 | 13.00 to 17.58 | outside/0.37/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through X) | 1: (base) [lod3/lod6] |
| 32 | `gondor_castle_wall_tower_l1_d1_m4` | 1.19 x 4.00 x 4.58 | 13.00 to 17.58 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through X) | 1: (base) [lod3/lod6] |
| 33 | `gondor_castle_wall_tower_l1_d1_m5` | 1.19 x 4.00 x 4.58 | 13.00 to 17.58 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through X) | 1: (base) [lod3/lod6] |
| 34 | `gondor_castle_wall_tower_l1_d1_m6` | 3.17 x 3.17 x 2.99 | 13.00 to 15.99 | outside/outside/outside | 14.88 | 732 | lod3, lod6 | 23 deg slope rising toward -X+Y | 1: (base) [lod3/lod6] |
| 35 | `gondor_castle_wall_tower_l1_d1_m7` | 3.00 x 1.19 x 4.58 | 13.00 to 17.58 | mid/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 0.90 x 1.26 at z 13.00 (through Y) | 1: (base) [lod3/lod6] |
| 36 | `gondor_castle_wall_tower_l1_d1_m8` | 3.17 x 3.17 x 2.99 | 13.00 to 15.99 | outside/outside/outside | 14.88 | 732 | lod3, lod6 | 23 deg slope rising toward +X+Y | 1: (base) [lod3/lod6] |
| 37 | `gondor_castle_wall_tower_l1_d1_m9` | 1.19 x 4.00 x 4.58 | 13.00 to 17.58 | outside/outside/outside | 14.93 | 2036 | lod3, lod6 | opening 1.25 x 1.26 at z 13.00 (through X) | 1: (base) [lod3/lod6] |
| 38 | `gondor_castle_wall_tower_l3_a` | 10.82 x 10.82 x 35.05 | -10.00 to 25.05 | mid/mid/0.29 | 0.00 | 4954 | lod3, lod6 | opening 0.88 x 2.34 at z 15.41 (through X) | 2: ground [lod3/lod6], wall [lod3/lod6] |
| 39 | `gondor_castle_wall_tower_l3_a_int` | 9.20 x 9.20 x 25.70 | -0.24 to 25.45 | mid/mid/0.01 | 25.10 | 14238 | lod3, lod6 | opening 1.53 x 0.21 at z -0.24 (through Y) | 2: floor [lod3/lod6], stairs [lod3/lod6] |
| 40 | `gondor_castle_wall_tower_l3_a_m1` | 4.39 x 1.19 x 3.91 | 23.40 to 27.31 | outside/outside/outside | 24.95 | 1459 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through Y) | 1: (base) [lod3/lod6] |
| 41 | `gondor_castle_wall_tower_l3_a_m10` | 1.19 x 4.39 x 3.91 | 23.40 to 27.31 | outside/outside/outside | 24.95 | 1459 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through X) | 1: (base) [lod3/lod6] |
| 42 | `gondor_castle_wall_tower_l3_a_m11` | 1.19 x 3.53 x 3.91 | 23.40 to 27.31 | outside/mid/outside | 24.94 | 1488 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through X) | 1: (base) [lod3/lod6] |
| 43 | `gondor_castle_wall_tower_l3_a_m12` | 1.19 x 4.39 x 3.91 | 23.40 to 27.31 | outside/outside/outside | 24.95 | 1459 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through X) | 1: (base) [lod3/lod6] |
| 44 | `gondor_castle_wall_tower_l3_a_m2` | 3.53 x 1.19 x 3.91 | 23.40 to 27.31 | mid/outside/outside | 24.94 | 1488 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through Y) | 1: (base) [lod3/lod6] |
| 45 | `gondor_castle_wall_tower_l3_a_m3` | 4.39 x 1.19 x 3.91 | 23.40 to 27.31 | outside/outside/outside | 24.95 | 1459 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through Y) | 1: (base) [lod3/lod6] |
| 46 | `gondor_castle_wall_tower_l3_a_m4` | 1.19 x 4.39 x 3.91 | 23.40 to 27.31 | outside/outside/outside | 24.95 | 1459 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through X) | 1: (base) [lod3/lod6] |
| 47 | `gondor_castle_wall_tower_l3_a_m5` | 1.19 x 3.53 x 3.91 | 23.40 to 27.31 | outside/mid/outside | 24.94 | 1488 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through X) | 1: (base) [lod3/lod6] |
| 48 | `gondor_castle_wall_tower_l3_a_m6` | 1.19 x 4.39 x 3.91 | 23.40 to 27.31 | outside/outside/outside | 24.95 | 1459 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through X) | 1: (base) [lod3/lod6] |
| 49 | `gondor_castle_wall_tower_l3_a_m7` | 4.39 x 1.19 x 3.91 | 23.40 to 27.31 | outside/outside/outside | 24.95 | 1459 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through Y) | 1: (base) [lod3/lod6] |
| 50 | `gondor_castle_wall_tower_l3_a_m8` | 3.53 x 1.19 x 3.91 | 23.40 to 27.31 | mid/outside/outside | 24.94 | 1488 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through Y) | 1: (base) [lod3/lod6] |
| 51 | `gondor_castle_wall_tower_l3_a_m9` | 4.39 x 1.19 x 3.91 | 23.40 to 27.31 | outside/outside/outside | 24.95 | 1459 | lod3, lod6 | opening 1.11 x 1.00 at z 23.40 (through Y) | 1: (base) [lod3/lod6] |
| 52 | `gondor_castle_wall_tower_l3_b` | 14.16 x 14.16 x 40.21 | -20.05 to 20.16 | mid/mid/mid | -20.00 | 12700 | lod6 | opening 0.67 x 1.69 at z 11.03 (through Y); opening 0.67 x 1.69 at z 11.03 (through Y); +2 more openings | 2: (base) [-], wall [lod6] |
| 53 | `gondor_castle_wall_tower_l3_b_int` | 13.80 x 13.80 x 20.70 | -0.24 to 20.45 | mid/mid/0.01 | 20.00 | 11620 | lod3, lod6 | opening 1.55 x 0.17 at z -0.24 (through Y) | 2: floor [lod3/lod6], stairs [lod3/lod6] |
| 54 | `gondor_castle_wall_tower_l3_b_m1` | 4.50 x 1.19 x 4.88 | 18.00 to 22.88 | min/outside/outside | 19.93 | 2036 | lod3, lod6 | opening 1.40 x 1.26 at z 18.00 (through Y) | 1: (base) [lod3/lod6] |
| 55 | `gondor_castle_wall_tower_l3_b_m10` | 1.19 x 4.50 x 4.88 | 18.00 to 22.88 | outside/max/outside | 19.93 | 2036 | lod3, lod6 | opening 1.40 x 1.26 at z 18.00 (through X) | 1: (base) [lod3/lod6] |
| 56 | `gondor_castle_wall_tower_l3_b_m11` | 1.19 x 4.50 x 4.88 | 18.00 to 22.88 | outside/min/outside | 19.93 | 2036 | lod3, lod6 | opening 1.40 x 1.26 at z 18.00 (through X) | 1: (base) [lod3/lod6] |
| 57 | `gondor_castle_wall_tower_l3_b_m12` | 3.17 x 3.17 x 2.99 | 18.00 to 20.99 | outside/outside/outside | 19.88 | 732 | lod3, lod6 | 23 deg slope rising toward -X-Y; node rot -0/-0/90 (baked) | 1: (base) [lod3/lod6] |
| 58 | `gondor_castle_wall_tower_l3_b_m2` | 4.50 x 1.19 x 4.88 | 18.00 to 22.88 | max/outside/outside | 19.93 | 2036 | lod3, lod6 | opening 1.40 x 1.26 at z 18.00 (through Y) | 1: (base) [lod3/lod6] |
| 59 | `gondor_castle_wall_tower_l3_b_m3` | 3.17 x 3.17 x 2.99 | 18.00 to 20.99 | outside/outside/outside | 19.88 | 732 | lod3, lod6 | 23 deg slope rising toward +X-Y; node rot 0/0/-180 (baked) | 1: (base) [lod3/lod6] |
| 60 | `gondor_castle_wall_tower_l3_b_m4` | 1.19 x 4.50 x 4.88 | 18.00 to 22.88 | outside/min/outside | 19.93 | 2036 | lod3, lod6 | opening 1.40 x 1.26 at z 18.00 (through X) | 1: (base) [lod3/lod6] |
| 61 | `gondor_castle_wall_tower_l3_b_m5` | 1.19 x 4.50 x 4.88 | 18.00 to 22.88 | outside/max/outside | 19.93 | 2036 | lod3, lod6 | opening 1.40 x 1.26 at z 18.00 (through X) | 1: (base) [lod3/lod6] |
| 62 | `gondor_castle_wall_tower_l3_b_m6` | 3.17 x 3.17 x 2.99 | 18.00 to 20.99 | outside/outside/outside | 19.88 | 732 | lod3, lod6 | 23 deg slope rising toward +X+Y; node rot -0/0/-90 (baked) | 1: (base) [lod3/lod6] |
| 63 | `gondor_castle_wall_tower_l3_b_m7` | 4.50 x 1.19 x 4.88 | 18.00 to 22.88 | max/outside/outside | 19.93 | 2036 | lod3, lod6 | opening 1.40 x 1.26 at z 18.00 (through Y) | 1: (base) [lod3/lod6] |
| 64 | `gondor_castle_wall_tower_l3_b_m8` | 4.50 x 1.19 x 4.88 | 18.00 to 22.88 | min/outside/outside | 19.93 | 2036 | lod3, lod6 | opening 1.40 x 1.26 at z 18.00 (through Y) | 1: (base) [lod3/lod6] |
| 65 | `gondor_castle_wall_tower_l3_b_m9` | 3.17 x 3.17 x 2.99 | 18.00 to 20.99 | outside/outside/outside | 19.88 | 732 | lod3, lod6 | 23 deg slope rising toward -X+Y | 1: (base) [lod3/lod6] |
| 66 | `gondor_castle_wall_tower_l1_e` | 13.00 x 11.56 x 34.52 | -20.01 to 14.52 | mid/0.52/0.58 | 10.00 | 915 | none |  | 1: (base) [-] |

## Props: `statues/`

### Statues

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 0 | `SM_GD_Statue_Fountain_MT` | 0.52 x 0.51 x 0.98 | -0.49 to 0.49 | mid/mid/mid | 0.49 | 38628 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 1 | `SM_GD_Statue_MT_Top` | 0.38 x 0.49 x 0.98 | -0.49 to 0.49 | mid/mid/mid | -0.08 | 38158 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 2 | `SM_GD_Statue_Rock_A` | 0.38 x 0.36 x 0.98 | -0.49 to 0.49 | mid/mid/mid | 0.13 | 38094 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 3 | `SM_GD_Statue_Rock_B` | 0.28 x 0.33 x 0.98 | -0.49 to 0.49 | mid/mid/mid | 0.27 | 38782 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 4 | `SM_GD_Statue_Rock_C` | 0.33 x 0.35 x 0.98 | -0.49 to 0.49 | mid/mid/mid | -0.45 | 37297 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 5 | `sm_gd_statue_swan` | 0.96 x 2.00 x 1.81 | -0.01 to 1.80 | mid/0.49/min | 0.74 | 9433 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 6 | `SM_GD_White_Tree` | 0.63 x 0.98 x 0.97 | -0.49 to 0.49 | mid/mid/mid | 0.03 | 58254 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 7 | `sm_gd_mi_statue_001` | 0.41 x 0.42 x 0.98 | -0.49 to 0.49 | mid/mid/mid | -0.35 | 28534 | lod1, lod2, lod3, lod4 |  | 1: (base) [lod1/lod2/lod3/lod4] |
| 8 | `sm_gd_mi_statue_002` | 0.40 x 0.44 x 0.98 | -0.49 to 0.49 | mid/mid/mid | -0.44 | 51422 | lod1, lod2, lod3, lod4 |  | 1: (base) [lod1/lod2/lod3/lod4] |
| 9 | `sm_gd_mi_statue_003` | 0.29 x 0.37 x 0.98 | -0.49 to 0.49 | mid/mid/mid | -0.43 | 52196 | lod1, lod2, lod3, lod4 |  | 1: (base) [lod1/lod2/lod3/lod4] |
| 10 | `sm_gd_mi_statue_004` | 0.40 x 0.54 x 0.98 | -0.49 to 0.49 | mid/mid/mid | -0.37 | 56678 | lod1, lod2, lod3, lod4 |  | 1: (base) [lod1/lod2/lod3/lod4] |
| 11 | `taom_isildur_statue_mt` | 0.56 x 1.06 x 1.51 | 0.00 to 1.51 | mid/0.45/min | 0.49 | 15232 | lod1, lod2, lod3, lod4, lod5 | opening 0.51 x 0.35 at z 0.50 (through X) | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 12 | `clo_gondor_banners` | 0.68 x 0.26 x 2.10 | -0.81 to 1.29 | mid/0.80/0.39 | 1.23 | 2827 | none | **no bo_** | 1: (base) [-] |
| 13 | `clo_gondor_spear_banner` | 2.47 x 0.03 x 0.68 | 0.19 to 0.88 | 0.99/mid/outside | 0.85 | 960 | none | **no bo_** | 1: (base) [-] |
| 14 | `wm_banner_arthedain_a01` | 1.49 x 0.26 x 3.48 | -1.74 to 1.74 | mid/0.80/mid | 1.23 | 11819 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 15 | `wm_banner_arthedain_a02` | 1.49 x 0.26 x 3.50 | -1.75 to 1.75 | mid/0.80/mid | 1.23 | 18438 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 16 | `wm_banner_arthedain_a03` | 2.51 x 0.03 x 3.43 | -1.72 to 1.72 | 0.98/mid/mid | 0.86 | 1656 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 17 | `wm_banner_cardolan_a01` | 1.49 x 0.26 x 3.48 | -1.74 to 1.74 | mid/0.80/mid | 1.23 | 11819 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 18 | `wm_banner_cardolan_a02` | 1.49 x 0.26 x 3.50 | -1.75 to 1.75 | mid/0.80/mid | 1.23 | 18438 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 19 | `wm_banner_cardolan_a03` | 2.51 x 0.03 x 3.43 | -1.72 to 1.72 | 0.98/mid/mid | 0.86 | 1656 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 20 | `wm_banner_gondor_a01` | 1.49 x 0.26 x 3.48 | -1.74 to 1.74 | mid/0.80/mid | 1.23 | 11819 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 21 | `wm_banner_gondor_a02` | 1.49 x 0.26 x 3.50 | -1.75 to 1.75 | mid/0.80/mid | 1.23 | 18438 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 22 | `wm_banner_gondor_a03` | 2.51 x 0.03 x 3.43 | -1.72 to 1.72 | 0.98/mid/mid | 0.86 | 1656 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 23 | `wm_banner_rhudaur_a01` | 1.49 x 0.26 x 3.48 | -1.74 to 1.74 | mid/0.80/mid | 1.23 | 11819 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 24 | `wm_banner_rhudaur_a02` | 1.49 x 0.26 x 3.50 | -1.75 to 1.75 | mid/0.80/mid | 1.23 | 18438 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |
| 25 | `wm_banner_rhudaur_a03` | 2.51 x 0.03 x 3.43 | -1.72 to 1.72 | 0.98/mid/mid | 0.86 | 1656 | lod1, lod2, lod3, lod4, lod5 |  | 1: (base) [lod1/lod2/lod3/lod4/lod5] |

## Props: `Comprehend/`

### Comprehend

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 0 | `IMRA` | 0.01 x 0.01 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.007/0.007/0.007 (baked); **no bo_** | 1: (base) [-] |
| 1 | `GondorStained1` | 0.00 x 0.00 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.002/0.002/0.002 (baked); **no bo_** | 1: (base) [-] |
| 2 | `GondorStained2` | 0.00 x 0.00 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.002/0.002/0.002 (baked); **no bo_** | 1: (base) [-] |
| 3 | `GondorStained3` | 0.00 x 0.00 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.002/0.002/0.002 (baked); **no bo_** | 1: (base) [-] |
| 4 | `GondorStained4` | 0.00 x 0.00 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.002/0.002/0.002 (baked); **no bo_** | 1: (base) [-] |
| 5 | `GondorStained5` | 0.00 x 0.00 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.002/0.002/0.002 (baked); **no bo_** | 1: (base) [-] |
| 6 | `GondorStained6` | 0.00 x 0.00 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.002/0.002/0.002 (baked); **no bo_** | 1: (base) [-] |
| 7 | `StainedM1` | 0.01 x 0.01 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.003/0.003/0.003 (baked); **no bo_** | 1: (base) [-] |
| 8 | `StainedM2` | 0.01 x 0.01 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.003/0.003/0.003 (baked); **no bo_** | 1: (base) [-] |
| 9 | `StainedM3` | 0.01 x 0.01 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.003/0.003/0.003 (baked); **no bo_** | 1: (base) [-] |
| 10 | `StainedM4` | 0.01 x 0.01 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.003/0.003/0.003 (baked); **no bo_** | 1: (base) [-] |
| 11 | `StainedM5` | 0.01 x 0.01 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.003/0.003/0.003 (baked); **no bo_** | 1: (base) [-] |
| 12 | `StainedM6` | 0.01 x 0.01 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.003/0.003/0.003 (baked); **no bo_** | 1: (base) [-] |
| 13 | `gondor_statue_A` | 0.65 x 0.72 x 1.80 | -0.02 to 1.78 | 0.40/0.63/0.01 | 1.18 | 4329 | none | **no bo_** | 1: (base) [-] |
| 14 | `gondor_statue_A_broken` | 0.59 x 0.67 x 1.78 | -0.02 to 1.76 | 0.44/0.66/0.01 | 1.41 | 8063 | none | **no bo_** | 1: (base) [-] |
| 15 | `gondor_statue_B` | 0.65 x 0.67 x 2.52 | -0.02 to 2.50 | 0.40/0.60/0.01 | 1.43 | 6688 | none | **no bo_** | 1: (base) [-] |
| 16 | `gondor_statue_B_broken` | 0.62 x 0.64 x 1.75 | -0.01 to 1.73 | 0.42/0.60/0.01 | 1.09 | 9836 | none | **no bo_** | 1: (base) [-] |
| 17 | `gondor_statue_B_spear` | 2.52 x 0.09 x 0.05 | -0.03 to 0.02 | 0.55/mid/mid | 0.01 | 680 | none | **no bo_** | 1: (base) [-] |
| 18 | `gondor_statue_C3_s1` | 0.79 x 0.68 x 1.94 | -0.02 to 1.92 | 0.53/0.53/0.01 | 1.14 | 8598 | none | **no bo_** | 1: (base) [-] |
| 19 | `gondor_statue_C3_s1_broken` | 0.74 x 0.62 x 1.79 | -0.02 to 1.77 | 0.53/mid/0.01 | 1.50 | 9607 | none | **no bo_** | 1: (base) [-] |
| 20 | `gondor_statue_C3_s2` | 0.79 x 0.68 x 1.94 | -0.02 to 1.92 | 0.53/0.53/0.01 | 1.14 | 10306 | none | **no bo_** | 1: (base) [-] |
| 21 | `gondor_statue_C3_s2_broken` | 0.74 x 0.62 x 1.79 | -0.02 to 1.77 | 0.53/mid/0.01 | 1.50 | 11315 | none | **no bo_** | 1: (base) [-] |
| 22 | `gondor_statue_C3_s3` | 0.79 x 0.68 x 1.94 | -0.02 to 1.92 | 0.53/0.53/0.01 | 1.14 | 10620 | none | **no bo_** | 1: (base) [-] |
| 23 | `gondor_statue_C3_s3_broken` | 0.74 x 0.62 x 1.79 | -0.02 to 1.77 | 0.53/mid/0.01 | 1.50 | 11629 | none | **no bo_** | 1: (base) [-] |
| 24 | `gondor_statue_C4_s1` | 0.70 x 0.59 x 1.94 | -0.03 to 1.92 | mid/0.62/0.01 | 1.53 | 6171 | none | **no bo_** | 1: (base) [-] |
| 25 | `gondor_statue_C4_s1_broken` | 0.66 x 0.54 x 1.86 | -0.02 to 1.84 | 0.52/0.59/0.01 | 1.52 | 9635 | none | **no bo_** | 1: (base) [-] |
| 26 | `gondor_statue_C4_s2` | 0.70 x 0.59 x 1.94 | -0.03 to 1.92 | mid/0.62/0.01 | 1.53 | 7879 | none | **no bo_** | 1: (base) [-] |
| 27 | `gondor_statue_C4_s2_broken` | 0.66 x 0.54 x 1.86 | -0.02 to 1.84 | 0.52/0.59/0.01 | 1.52 | 11343 | none | **no bo_** | 1: (base) [-] |
| 28 | `gondor_statue_C4_s3` | 0.70 x 0.59 x 1.94 | -0.03 to 1.92 | mid/0.62/0.01 | 1.53 | 8193 | none | **no bo_** | 1: (base) [-] |
| 29 | `gondor_statue_C4_s3_broken` | 0.66 x 0.54 x 1.86 | -0.02 to 1.84 | 0.52/0.59/0.01 | 1.52 | 11657 | none | **no bo_** | 1: (base) [-] |
| 30 | `gondor_statue_D1` | 0.71 x 0.53 x 1.94 | -0.02 to 1.92 | 0.56/0.58/0.01 | 1.52 | 9672 | none | **no bo_** | 1: (base) [-] |
| 31 | `gondor_statue_D1_broken` | 0.70 x 0.52 x 1.58 | -0.02 to 1.56 | 0.57/0.58/0.01 | 1.45 | 12106 | none | **no bo_** | 1: (base) [-] |
| 32 | `gondor_statue_D2_s1` | 0.71 x 0.61 x 1.94 | -0.02 to 1.92 | 0.53/0.64/0.01 | 1.40 | 11866 | none | **no bo_** | 1: (base) [-] |
| 33 | `gondor_statue_D2_s1_broken` | 0.71 x 0.61 x 1.58 | -0.02 to 1.56 | 0.53/0.64/0.02 | 1.40 | 11672 | none | **no bo_** | 1: (base) [-] |
| 34 | `gondor_statue_D2_s2` | 0.71 x 0.61 x 1.94 | -0.02 to 1.92 | 0.53/0.64/0.01 | 1.40 | 13574 | none | **no bo_** | 1: (base) [-] |
| 35 | `gondor_statue_D2_s2_broken` | 0.71 x 0.61 x 1.58 | -0.02 to 1.56 | 0.53/0.64/0.02 | 1.40 | 13380 | none | **no bo_** | 1: (base) [-] |
| 36 | `gondor_statue_D2_s3` | 0.71 x 0.61 x 1.94 | -0.02 to 1.92 | 0.53/0.64/0.01 | 1.40 | 13888 | none | **no bo_** | 1: (base) [-] |
| 37 | `gondor_statue_D2_s3_broken` | 0.71 x 0.61 x 1.58 | -0.02 to 1.56 | 0.53/0.64/0.02 | 1.40 | 13694 | none | **no bo_** | 1: (base) [-] |
| 38 | `gondor_statue_E_s1` | 0.59 x 0.52 x 1.79 | 0.00 to 1.79 | 0.54/0.72/min | 1.15 | 9522 | none | **no bo_** | 1: (base) [-] |
| 39 | `gondor_statue_E_s1_broken` | 0.59 x 0.51 x 1.75 | 0.00 to 1.75 | 0.55/0.71/min | 1.15 | 12602 | none | **no bo_** | 1: (base) [-] |
| 40 | `gondor_statue_E_s2` | 0.59 x 0.52 x 1.79 | 0.00 to 1.79 | 0.54/0.72/min | 1.15 | 11230 | none | **no bo_** | 1: (base) [-] |
| 41 | `gondor_statue_E_s2_broken` | 0.59 x 0.51 x 1.75 | 0.00 to 1.75 | 0.55/0.71/min | 1.15 | 14310 | none | **no bo_** | 1: (base) [-] |
| 42 | `gondor_statue_E_s3` | 0.59 x 0.52 x 1.79 | 0.00 to 1.79 | 0.54/0.72/min | 1.15 | 11544 | none | **no bo_** | 1: (base) [-] |
| 43 | `gondor_statue_E_s3_broken` | 0.59 x 0.51 x 1.76 | 0.00 to 1.75 | 0.55/0.71/min | 1.15 | 14624 | none | **no bo_** | 1: (base) [-] |
| 44 | `gondor_statue_F` | 0.61 x 0.47 x 1.81 | 0.00 to 1.81 | 0.48/0.62/min | 1.53 | 5194 | none | **no bo_** | 1: (base) [-] |
| 45 | `gondor_statue_F_broken` | 0.55 x 0.40 x 1.78 | 0.01 to 1.80 | mid/0.56/outside | 1.32 | 8436 | none | **no bo_** | 1: (base) [-] |
| 46 | `Swan2` | 0.01 x 0.01 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.003/0.003/0.003 (baked); **no bo_** | 1: (base) [-] |
| 47 | `SwanS` | 0.01 x 0.01 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.003/0.003/0.003 (baked); **no bo_** | 1: (base) [-] |
| 48 | `swanss` | 0.01 x 0.01 x 0.00 | 0.00 to 0.00 | min/min/min | 0.00 | 492032 | none | node scale 0.007/0.007/0.007 (baked); **no bo_** | 1: (base) [-] |

## Props: `ships/`

### Ships

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 0 | `sm_gondor_ship_cog_001` | 20.00 x 10.77 x 11.44 | 0.00 to 11.44 | mid/mid/min | 2.15 | 40000 | none | opening 0.50 x 0.34 at z 3.53 (through Y); opening 0.33 x 0.51 at z 9.59 (through Y); +9 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked) | 1: (base) [-] |
| 1 | `sm_gondor_ship_longship_001` | 24.00 x 10.03 x 15.41 | 0.00 to 15.42 | mid/mid/min | 2.24 | 40000 | none | opening 0.20 x 0.60 at z 4.00 (through Y); opening 0.20 x 0.60 at z 4.00 (through Y); +12 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked) | 1: (base) [-] |
| 2 | `sm_gondor_ship_war_001` | 29.99 x 13.63 x 20.95 | 0.01 to 20.96 | mid/mid/min | 3.93 | 40000 | none | opening 0.75 x 0.51 at z 18.69 (through Y); opening 0.50 x 0.51 at z 18.69 (through Y); +14 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked) | 1: (base) [-] |

## References: `buildings/`

### Buildings (props and references)

| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes | Sub-objects [LODs] |
|---|---|---|---|---|---|---|---|---|---|
| 0 | `geo_bld_C_roof_001` | 12.10 x 5.80 x 8.05 | 3.79 to 11.85 | mid/0.47/outside | 4.00 | 60 | none | 43 deg slope rising toward -X; 32 deg slope rising toward +X; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 1 | `geo_bld_C_walls_001` | 11.85 x 6.46 x 12.26 | 0.00 to 12.26 | mid/0.47/min | 3.91 | 18786 | none | opening 1.65 x 3.47 at z 0.00 (through Y); opening 1.65 x 3.47 at z 0.00 (through Y); +8 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 2 | `geo_bld_D_roof_001` | 10.59 x 9.84 x 3.35 | 10.01 to 13.36 | mid/0.48/outside | 10.09 | 16 | none | 33 deg slope rising toward +X; 33 deg slope rising toward -X; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 3 | `geo_bld_D_walls_001` | 12.00 x 12.24 x 13.95 | 0.00 to 13.95 | mid/mid/min | 10.01 | 22588 | none | opening 0.58 x 2.91 at z 0.23 (through Y); opening 0.58 x 2.91 at z 0.23 (through Y); +6 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 4 | `geo_bld_E_roof_001` | 10.37 x 9.01 x 4.45 | 11.00 to 15.45 | mid/mid/outside | 13.20 | 58 | none | 43 deg slope rising toward -X; 43 deg slope rising toward +X; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 5 | `geo_bld_E_walls_001` | 10.27 x 12.30 x 14.66 | -0.11 to 14.55 | mid/0.49/0.01 | 12.53 | 8756 | none | opening 2.81 x 2.08 at z 9.30 (through Y); opening 2.81 x 2.08 at z 9.30 (through Y); +11 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 6 | `geo_bld_E_wood_001` | 5.62 x 0.10 x 3.29 | 0.00 to 3.29 | mid/outside/min | 2.81 | 240 | none | node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 7 | `geo_bld_F_roof_001` | 23.05 x 10.47 x 18.92 | 10.18 to 29.09 | 0.23/mid/outside | 10.49 | 1664 | none | 47 deg slope rising toward +Y; 47 deg slope rising toward -Y; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 8 | `geo_bld_F_walls_001` | 24.71 x 12.00 x 25.63 | 0.00 to 25.63 | 0.24/mid/min | 18.80 | 16538 | none | opening 4.73 x 1.92 at z 16.87 (through Y); opening 1.72 x 3.20 at z 0.00 (through Y); +9 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 9 | `geo_bld_F_windows_001` | 10.66 x 6.50 x 3.63 | 4.90 to 8.53 | outside/mid/outside |  | 288 | none | node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 10 | `geo_bld_F_wood_001` | 6.08 x 9.62 x 12.73 | 0.00 to 12.73 | 0.79/mid/min |  | 4152 | none | node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 11 | `gondor_building_citadel_a1` | 30.91 x 49.33 x 26.93 | -1.36 to 25.57 | mid/0.78/0.05 | 16.05 | 1259857 | lod6 | opening 21.64 x 1.29 at z -1.36 (through Y); opening 2.32 x 2.07 at z 14.17 (through Y); +12 more openings; node scale 0.8/0.8/0.96 (baked) | 4: pillars [lod6], roofs [lod6], tower [lod6], walls [lod6] |
| 12 | `gondor_building_tower_ecthelion_a` | 16.73 x 14.99 x 33.83 | -0.83 to 33.00 | mid/mid/0.02 | 30.19 | 358434 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 13 | `gondor_building_tower_ecthelion_b` | 14.05 x 14.05 x 77.50 | 32.47 to 109.97 | mid/mid/outside | 51.97 | 154524 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 14 | `gondor_building_healing_a` | 37.59 x 37.37 x 21.25 | -3.00 to 18.25 | mid/mid/0.14 | 0.10 | 625958 | lod3, lod6 |  | 5: brick [lod3], door [lod3/lod6], floor [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 15 | `gondor_building_large_a` | 36.91 x 24.91 x 21.64 | -3.00 to 18.64 | mid/mid/0.14 | 6.10 | 401332 | lod3, lod6 | opening 1.84 x 5.26 at z 0.09 (through Y); opening 1.84 x 5.26 at z 0.09 (through Y); +13 more openings | 6: brick [lod3/lod6], floor [lod3/lod6], pillars [lod3/lod6], roof [lod3/lod6], stairs [lod3/lod6], wall [lod3/lod6] |
| 16 | `gondor_building_large_e` | 12.90 x 15.90 x 16.04 | -0.54 to 15.50 | mid/0.47/0.03 | 9.00 | 97458 | lod2, lod5 | opening 2.02 x 0.53 at z -0.54 (through Y); opening 2.02 x 0.53 at z -0.54 (through Y); +9 more openings | 1: (base) [lod2/lod5] |
| 17 | `gondor_building_large_f` | 42.90 x 6.90 x 15.75 | -0.25 to 15.50 | mid/mid/0.02 | 12.13 | 375332 | lod2, lod5 | opening 4.29 x 0.36 at z -0.25 (through Y); opening 4.29 x 0.36 at z -0.25 (through Y); +4 more openings | 1: (base) [lod2/lod5] |
| 18 | `gondor_building_large_g` | 18.90 x 12.90 x 12.84 | -0.54 to 12.30 | mid/mid/0.04 | 6.31 | 79200 | lod2, lod5 | opening 0.94 x 0.32 at z -0.54 (through Y); opening 0.94 x 0.32 at z -0.54 (through Y); +9 more openings | 1: (base) [lod2/lod5] |
| 19 | `gondor_building_large_h` | 12.90 x 12.90 x 21.73 | -0.54 to 21.20 | mid/mid/0.02 | 18.35 | 340716 | lod2, lod5 | opening 1.09 x 2.54 at z 15.04 (through Y); opening 1.09 x 2.54 at z 15.04 (through Y); +14 more openings | 1: (base) [lod2/lod5] |
| 20 | `gondor_building_large_i` | 7.90 x 7.69 x 17.85 | -0.54 to 17.31 | 0.51/0.49/0.03 | 15.00 | 95960 | lod2, lod5 | opening 1.49 x 0.60 at z -0.54 (through Y); opening 0.60 x 0.60 at z -0.54 (through Y); +3 more openings; node scale 1.302/1.302/1.302 (baked) | 1: (base) [lod2/lod5] |
| 21 | `gondor_building_large_j` | 9.20 x 9.29 x 21.29 | -3.89 to 17.41 | mid/mid/0.18 | 15.12 | 189164 | lod2, lod5 | opening 2.53 x 3.73 at z -3.89 (through Y); opening 2.35 x 3.73 at z -3.89 (through Y); +14 more openings | 1: (base) [lod2/lod5] |
| 22 | `gondor_building_large_k` | 37.90 x 13.00 x 29.15 | -4.15 to 25.00 | mid/0.43/0.14 | 18.63 | 733972 | lod2, lod5 | opening 4.78 x 10.46 at z -4.15 (through Y); opening 2.55 x 2.54 at z 15.49 (through Y); +14 more openings; node scale 0.83/0.83/0.83 (baked) | 1: (base) [lod2/lod5] |
| 23 | `gondor_building_tower_e` | 15.53 x 15.51 x 38.94 | -4.42 to 34.52 | mid/mid/0.11 | 30.12 | 300144 | lod2, lod5 | opening 4.63 x 4.22 at z -4.42 (through Y); opening 4.63 x 4.22 at z -4.42 (through Y); +14 more openings; node rot -0/0/-90 (baked) | 1: (base) [lod2/lod5] |
| 24 | `gondor_building_tower_m` | 15.48 x 15.48 x 32.59 | -6.25 to 26.34 | mid/mid/0.19 | 21.13 | 268922 | lod2, lod5 | opening 0.81 x 3.26 at z 7.06 (through Y); opening 0.81 x 3.26 at z 7.06 (through Y); +14 more openings | 1: (base) [lod2/lod5] |
| 25 | `gondor_building_tower_n` | 18.80 x 18.80 x 30.32 | -0.25 to 30.07 | mid/mid/0.01 | 24.13 | 394480 | lod2, lod5 | opening 1.02 x 3.03 at z 19.21 (through Y); opening 0.76 x 3.29 at z 19.21 (through Y); +14 more openings | 1: (base) [lod2/lod5] |
| 26 | `gondor_building_medium_a` | 12.85 x 12.85 x 18.04 | -3.00 to 15.04 | mid/mid/0.17 | 9.12 | 187184 | lod3, lod6 | opening 1.66 x 4.66 at z 0.16 (through Y); opening 1.66 x 4.66 at z 0.16 (through Y); +10 more openings | 6: brick [lod3/lod6], floor [lod3/lod6], pillar [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6], window [lod3/lod6] |
| 27 | `gondor_building_medium_a_dest` | 19.15 x 16.27 x 9.07 | -0.06 to 9.01 | mid/0.60/0.01 | 0.10 | 139707 | lod3, lod6 | opening 1.60 x 4.53 at z 0.27 (through Y); opening 1.60 x 4.53 at z 0.27 (through Y); +12 more openings | 3: brick [lod3/lod6], floor [lod3/lod6], wall [lod3/lod6] |
| 28 | `gondor_building_medium_b` | 24.85 x 12.85 x 11.45 | -3.00 to 8.45 | mid/mid/0.26 | 6.10 | 138856 | lod3, lod6 | opening 1.66 x 4.58 at z 0.12 (through Y); opening 1.66 x 4.58 at z 0.12 (through Y); +12 more openings | 4: brick [lod3/lod6], floor [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 29 | `gondor_building_medium_b_dest` | 27.94 x 19.60 x 7.36 | -0.06 to 7.30 | 0.44/0.49/0.01 | 6.10 | 178669 | lod3, lod6 | opening 1.63 x 4.27 at z 0.42 (through Y); opening 1.40 x 1.90 at z 0.65 (through Y); +14 more openings | 3: brick [lod3/lod6], floor [lod3/lod6], wall [lod3/lod6] |
| 30 | `gondor_building_outcrop_a_01` | 6.91 x 3.91 x 13.00 | -3.50 to 9.50 | mid/0.88/0.27 | 9.13 | 24620 | lod3, lod6 | opening 5.59 x 3.36 at z -3.50 (through Y) | 4: brick [lod3/lod6], door [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 31 | `gondor_building_small_a_01` | 12.91 x 7.21 x 15.30 | -3.00 to 12.30 | mid/0.94/0.20 | 12.27 | 66052 | lod3, lod6 | opening 0.52 x 1.03 at z 7.03 (through X); opening 0.39 x 1.03 at z 7.03 (through X) | 4: brick [lod3], floor [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 32 | `gondor_building_small_a_01_dest` | 13.23 x 10.31 x 8.59 | -0.06 to 8.53 | 0.51/0.96/0.01 | 0.10 | 81242 | lod3, lod6 | opening 0.44 x 0.67 at z 3.96 (through Y); opening 0.22 x 0.67 at z 0.50 (through Y); +4 more openings | 3: brick [lod3/lod6], floor [lod3/lod6], wall [lod3/lod6] |
| 33 | `gondor_building_small_b_01` | 9.91 x 6.91 x 12.04 | -3.00 to 9.04 | mid/mid/0.25 | 6.12 | 41310 | lod3, lod6 |  | 4: brick [lod3], door [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 34 | `gondor_building_small_b_01_dest` | 12.11 x 9.22 x 6.07 | -0.06 to 6.01 | 0.59/0.63/0.01 | 0.10 | 67161 | lod3, lod6 | opening 1.01 x 2.33 at z 0.14 (through Y); opening 0.51 x 1.01 at z 1.05 (through Y) | 3: brick [lod3/lod6], floor [lod3/lod6], wall [lod3/lod6] |
| 35 | `gondor_building_small_c_01` | 6.89 x 6.89 x 8.62 | -0.21 to 8.41 | mid/mid/0.02 | 6.12 | 67040 | lod3, lod6 |  | 4: brick [lod3], door [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 36 | `gondor_building_small_c_01_dest` | 6.85 x 6.85 x 6.34 | -0.21 to 6.13 | mid/mid/0.03 | 6.12 | 53798 | lod3, lod6 | opening 0.23 x 0.97 at z 4.08 (through Y); opening 0.23 x 0.51 at z 2.48 (through Y); +1 more openings | 2: brick [lod3/lod6], wall [lod3/lod6] |
| 37 | `gondor_building_small_d_01` | 6.91 x 10.23 x 8.85 | -1.15 to 7.70 | mid/0.52/0.13 | 6.13 | 37538 | lod2, lod5 |  | 1: (base) [lod2/lod5] |
| 38 | `gondor_building_small_e_01` | 4.27 x 4.24 x 11.51 | -0.90 to 10.61 | 0.48/0.61/0.08 | 9.13 | 26866 | lod2, lod5 |  | 1: (base) [lod2/lod5] |
| 39 | `gondor_building_small_e_02` | 10.20 x 8.06 x 10.28 | -0.78 to 9.50 | 0.29/0.53/0.08 | 3.10 | 56665 | lod2, lod5 |  | 1: (base) [lod2/lod5] |
| 40 | `gondor_building_small_e_03` | 6.90 x 10.21 x 9.96 | -0.46 to 9.50 | 0.57/0.47/0.05 | 6.13 | 30372 | lod2, lod5 |  | 1: (base) [lod2/lod5] |
| 41 | `gondor_building_small_e_04` | 8.53 x 8.50 x 20.35 | -3.03 to 17.32 | 0.51/mid/0.15 | 15.13 | 127007 | lod2, lod5 | opening 2.22 x 3.05 at z -3.03 (through Y); opening 2.22 x 3.05 at z -3.03 (through Y); +6 more openings | 1: (base) [lod2/lod5] |
| 42 | `gondor_building_small_e_05` | 9.90 x 10.26 x 11.08 | -0.54 to 10.54 | mid/0.47/0.05 | 9.13 | 53238 | lod2, lod5 | opening 2.13 x 0.55 at z -0.54 (through Y); opening 2.04 x 0.55 at z -0.54 (through Y); +1 more openings | 1: (base) [lod2/lod5] |
| 43 | `gondor_building_small_e_06` | 13.23 x 9.90 x 13.29 | -0.75 to 12.54 | 0.53/0.45/0.06 | 6.13 | 59860 | lod2, lod5 |  | 1: (base) [lod2/lod5] |
| 44 | `gondor_building_small_e_07` | 10.77 x 7.24 x 9.79 | -0.58 to 9.20 | 0.48/0.52/0.06 | 3.13 | 73078 | lod2, lod5 |  | 1: (base) [lod2/lod5] |
| 45 | `gondor_building_small_e_08` | 21.90 x 22.32 x 15.97 | -0.56 to 15.41 | mid/0.51/0.04 | 7.85 | 134080 | lod2, lod5 | opening 2.01 x 0.55 at z -0.56 (through Y); opening 2.01 x 0.55 at z -0.56 (through Y); +13 more openings | 1: (base) [lod2/lod5] |
| 46 | `gondor_building_small_e_09` | 12.90 x 10.22 x 12.56 | -0.91 to 11.65 | 0.62/0.47/0.07 | 9.13 | 75167 | lod2, lod5 | opening 2.42 x 2.72 at z 3.17 (through X) | 1: (base) [lod2/lod5] |
| 47 | `gondor_building_small_e_10` | 9.91 x 7.24 x 8.49 | -0.69 to 7.80 | mid/0.52/0.08 | 6.13 | 42756 | lod2, lod5 |  | 1: (base) [lod2/lod5] |
| 48 | `gondor_building_small_e_11` | 8.20 x 8.44 x 18.47 | -0.64 to 17.83 | mid/0.52/0.03 | 15.15 | 61441 | lod2, lod5 | opening 1.88 x 0.62 at z -0.64 (through X) | 1: (base) [lod2/lod5] |
| 49 | `gondor_building_small_e_12` | 13.27 x 13.33 x 18.90 | -1.07 to 17.83 | mid/mid/0.06 | 15.15 | 82304 | lod2, lod5 |  | 1: (base) [lod2/lod5] |
| 50 | `gondor_building_small_e_13` | 9.98 x 13.25 x 12.52 | -0.86 to 11.66 | mid/0.59/0.07 | 6.10 | 132450 | lod2, lod5 | opening 1.10 x 1.44 at z 4.12 (through X); opening 1.10 x 1.33 at z 4.12 (through X); +6 more openings | 1: (base) [lod2/lod5] |
| 51 | `gondor_building_small_e_14` | 17.90 x 10.15 x 12.02 | -0.76 to 11.26 | mid/0.56/0.06 | 6.13 | 63324 | lod2, lod5 | node rot 90/-0/0 (baked) | 1: (base) [lod2/lod5] |
| 52 | `gondor_building_tower_a` | 6.91 x 6.91 x 24.07 | -3.00 to 21.07 | mid/mid/0.12 | 18.12 | 95292 | lod3, lod6 | opening 0.81 x 3.41 at z 7.03 (through Y); opening 0.81 x 3.41 at z 7.03 (through Y); +11 more openings | 4: brick [lod3/lod6], floor [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 53 | `gondor_building_tower_a_dest` | 7.02 x 6.78 x 12.09 | -0.06 to 12.03 | 0.49/mid/min | 6.10 | 66775 | lod3, lod6 | opening 1.32 x 2.01 at z 0.65 (through Y); opening 1.32 x 2.01 at z 0.65 (through Y); +10 more openings | 3: brick [lod3/lod6], floor [lod3/lod6], wall [lod3/lod6] |
| 54 | `gondor_building_tower_b` | 7.59 x 7.59 x 16.70 | -3.00 to 13.70 | mid/mid/0.18 | 12.00 | 83768 | lod3, lod6 |  | 4: brick [lod3], door [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 55 | `gondor_building_tower_c` | 7.14 x 7.14 x 19.07 | -3.00 to 16.07 | mid/mid/0.16 | 6.17 | 33620 | lod3, lod6 |  | 4: brick [lod3], door [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 56 | `gondor_building_tower_d` | 6.85 x 6.85 x 21.25 | -3.00 to 18.25 | mid/mid/0.14 | 9.13 | 118998 | lod3, lod6 | opening 4.33 x 5.13 at z 0.19 (through Y) | 5: brick [lod3], door [lod3/lod6], floor [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 57 | `gondor_tower_building_d` | 14.94 x 14.94 x 101.18 | -13.03 to 88.15 | mid/mid/0.13 | 74.75 | 85744 | lod3, lod5 | node scale 0.9/0.9/1 (baked) | 1: (base) [lod3/lod5] |
| 58 | `gondor_tower_small_a1` | 4.89 x 4.88 x 22.13 | -1.00 to 21.13 | mid/mid/0.05 | 16.83 | 92724 | lod3, lod6 | opening 1.50 x 2.79 at z 16.85 (through Y); opening 2.26 x 2.04 at z 14.06 (through Y); +2 more openings | 5: door [lod3/lod6], floor [lod3/lod6], pillars [lod3/lod6], roof [lod3/lod6], wall [lod3/lod6] |
| 59 | `bld_bld_A_roof_001` | 73.14 x 22.97 x 62.22 | 10.82 to 73.05 | 0.88/mid/outside | 22.95 | 2770 | none | opening 1.57 x 0.52 at z 10.82 (through X); opening 0.52 x 0.52 at z 10.82 (through X); 46 deg slope rising toward +Y; 46 deg slope rising toward -Y; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 60 | `bld_bld_A_wood_001` | 6.47 x 11.61 x 3.22 | 1.80 to 5.02 | 0.15/mid/outside | 4.87 | 4944 | none | node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 61 | `geo_bld_A_interior_001` | 13.60 x 12.62 x 29.91 | 1.57 to 31.48 | mid/mid/outside | 24.84 | 2936 | none | node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 62 | `geo_bld_A_tower_001` | 11.13 x 12.12 x 27.15 | 32.44 to 59.59 | mid/mid/outside | 55.09 | 30576 | none | opening 0.92 x 2.04 at z 55.06 (through X); opening 0.92 x 2.04 at z 55.06 (through X); +6 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 63 | `geo_bld_A_tower_002` | 11.47 x 13.24 x 10.99 | 58.82 to 69.82 | mid/mid/outside | 64.91 | 10935 | none | opening 2.68 x 4.59 at z 59.59 (through Y); opening 2.68 x 4.59 at z 59.59 (through Y); +11 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 64 | `geo_bld_A_tower_003` | 20.48 x 19.39 x 33.51 | 0.00 to 33.51 | mid/mid/min | 0.33 | 18118 | none | opening 1.96 x 3.35 at z 1.68 (through Y); opening 1.40 x 1.96 at z 11.73 (through Y); +14 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 65 | `geo_bld_A_tower_004` | 8.93 x 9.30 x 2.05 | 55.09 to 57.13 | mid/mid/outside | 57.12 | 837520 | none | node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 66 | `geo_bld_A_walls_001` | 49.93 x 19.52 x 11.42 | 0.00 to 11.42 | outside/mid/min | 8.58 | 6196 | none | opening 6.66 x 11.00 at z 0.00 (through Y); opening 6.24 x 11.00 at z 0.00 (through Y); +6 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 67 | `geo_bld_A_walls_002` | 50.49 x 5.28 x 15.77 | 12.86 to 28.63 | outside/mid/outside | 25.03 | 56536 | none | opening 2.52 x 1.71 at z 25.65 (through Y); opening 2.52 x 1.71 at z 25.65 (through Y); +6 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 68 | `geo_bld_A_walls_003` | 62.06 x 23.04 x 30.76 | 0.00 to 30.76 | outside/mid/min | 14.75 | 633 | none | node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 69 | `geo_bld_A_walls_004` | 48.62 x 24.03 x 24.27 | 0.00 to 24.27 | outside/mid/min | 16.53 | 74028 | none | opening 1.22 x 3.70 at z 16.46 (through Y); opening 1.22 x 3.70 at z 16.46 (through Y); +10 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 70 | `geo_bld_A_walls_005` | 50.53 x 13.18 x 4.68 | 16.53 to 21.21 | outside/mid/outside | 20.22 | 1903884 | none | opening 2.10 x 3.40 at z 16.53 (through Y); opening 1.68 x 3.40 at z 16.53 (through Y); +6 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 71 | `bld_bld_B_roof_001` | 55.24 x 55.24 x 23.09 | 20.65 to 43.75 | mid/mid/outside | 39.48 | 1160 | none | 22 deg slope rising toward -X+Y; 22 deg slope rising toward +Y; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 72 | `bld_bld_B_walls_001` | 50.78 x 50.78 x 14.60 | 27.43 to 42.04 | mid/mid/outside | 30.54 | 173656 | none | opening 1.28 x 2.58 at z 27.43 (through Y); opening 1.28 x 2.58 at z 27.43 (through Y); +14 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 73 | `bld_bld_B_walls_002` | 57.59 x 57.59 x 27.44 | 0.00 to 27.44 | mid/mid/min | 26.93 | 5062 | none | opening 31.68 x 26.48 at z 0.00 (through Y); opening 31.68 x 26.48 at z 0.00 (through X); node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 74 | `bld_bld_B_walls_003` | 26.20 x 26.20 x 13.09 | 26.82 to 39.91 | mid/mid/outside | 39.15 | 64380 | none | opening 0.87 x 3.33 at z 26.82 (through Y); opening 0.87 x 3.33 at z 26.82 (through Y); +14 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 75 | `bld_bld_B_walls_004` | 57.85 x 57.85 x 14.31 | 14.10 to 28.41 | mid/mid/outside | 27.13 | 100920 | none | opening 1.93 x 4.93 at z 14.10 (through Y); opening 0.48 x 1.97 at z 19.03 (through Y); +8 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 76 | `bld_bld_B_walls_005` | 62.84 x 62.84 x 18.80 | 0.00 to 18.80 | mid/mid/min | 14.10 | 110492 | none | opening 0.52 x 2.69 at z 6.44 (through Y); opening 0.52 x 2.69 at z 6.44 (through Y); +6 more openings; node rot 90/-0/0, scale 0.01/0.01/0.01 (baked); **no bo_** | 1: (base) [-] |
| 77 | `mns_trth_wall_l0` | 466.25 x 421.99 x 16.79 | 35.00 to 51.79 | mid/0.26/outside | 50.00 | 956544 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 78 | `mns_trth_wall_l1` | 325.36 x 285.85 x 69.88 | 34.50 to 104.38 | 0.51/0.10/outside | 101.50 | 969608 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 79 | `mns_trth_wall_l2` | 252.37 x 264.24 x 57.88 | 86.50 to 144.38 | mid/outside/outside | 141.50 | 893060 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 80 | `mns_trth_wall_l3` | 185.24 x 194.64 x 57.88 | 126.50 to 184.38 | 0.51/outside/outside | 181.51 | 637900 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 81 | `mns_trth_wall_l4` | 133.82 x 183.35 x 58.14 | 176.25 to 234.38 | 0.52/outside/outside | 231.25 | 535836 | lod3, lod6 |  | 1: (base) [lod3/lod6] |
| 82 | `minas_tirith_gate_state1` | 7.54 x 1.65 x 7.94 | -0.47 to 7.46 | mid/mid/0.06 | 7.42 | 152044 | none | opening 0.60 x 0.46 at z -0.47 (through Y); opening 0.60 x 0.46 at z -0.47 (through Y); +2 more openings; **no bo_** | 1: (base) [-] |
| 83 | `minas_tirith_gate_state2` | 7.54 x 1.65 x 7.94 | -0.47 to 7.46 | mid/mid/0.06 | 7.42 | 123064 | none | opening 0.60 x 0.46 at z -0.47 (through Y); opening 0.60 x 0.46 at z -0.47 (through Y); +2 more openings; **no bo_** | 1: (base) [-] |
| 84 | `minas_tirith_gate_state3` | 7.54 x 1.65 x 7.94 | -0.47 to 7.46 | mid/mid/0.06 | 7.42 | 122055 | none | opening 0.60 x 0.46 at z -0.47 (through Y); opening 0.60 x 0.46 at z -0.47 (through Y); +3 more openings; **no bo_** | 1: (base) [-] |
| 85 | `minas_tirith_gate_state4` | 7.54 x 1.93 x 7.96 | -0.50 to 7.46 | mid/0.42/0.06 | 7.42 | 112103 | none | opening 0.60 x 0.47 at z -0.50 (through Y); opening 0.60 x 0.47 at z -0.50 (through Y); +2 more openings; **no bo_** | 1: (base) [-] |
| 86 | `minas_tirith_gate_state5` | 7.76 x 4.01 x 7.95 | -0.46 to 7.50 | mid/0.05/0.06 | 7.42 | 110097 | none | opening 0.40 x 0.40 at z -0.46 (through X); opening 0.47 x 0.40 at z -0.46 (through X) | 1: (base) [-] |
| 87 | `minas_tirith_gate_top` | 6.78 x 1.28 x 2.92 | 7.22 to 10.14 | mid/0.59/outside | 7.42 | 9062 | none |  | 1: (base) [-] |
