"""gen_troll_anim_clips.ps1: the melee attack table key a generated clip carries (2026-09-26).

A clip gets a row in the engine's melee attack table only when its "Blends with animation" box (ShortJake's
TpacTool v0.5.1 BlendWithAnimation; 0.4.0 called it UnknownClipName) holds its own name, or through the blend
children the Kit generates toward a _balanced twin; a release or blocked action on a clip with no row crashes. The generator used to blank the box on every clone, so a
re-cut silently undid set_clip_balance_name.py. The rule now: a clone of a KEYED vanilla template (BlendWithAnimation
or ClipSource1Name set) is self-keyed, "Blends with action" (BlendsWithAction) emptied, the generated-child fields
cleared; a clone of an unkeyed template keeps the three names blank.

The generator itself needs TpacTool, the Kit's loader and the install, so these tests lift the rule's three
functions out of the script with the PowerShell parser and run them on plain objects shaped like TpacTool's
AnimationClip. The same parse checks that both write paths and both -Verify modes go through them.
"""
import glob
import json
import os
import re
import shutil
import subprocess
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gen_troll_anim_clips.ps1")
SHELL = shutil.which("powershell") or shutil.which("pwsh")   # Windows PowerShell 5.1 first: the script's runtime

HARNESS = r"""
param([string]$Script)
$ErrorActionPreference = 'Stop'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$null)
$want = @('Test-KeyedTemplate', 'Set-ClipKey', 'Get-ClipKeyFault')
$defs = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true) |
  Where-Object { $want -contains $_.Name })
$out = [ordered]@{ found = @($defs | ForEach-Object { $_.Name }) }

# calls outside the definitions, and writes to the key fields anywhere but Set-ClipKey
$calls = [ordered]@{}
foreach ($n in $want) {
  $calls[$n] = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.CommandAst] -and $a.GetCommandName() -eq $n }, $true)).Count
}
$out.calls = $calls
$setDef = $defs | Where-Object { $_.Name -eq 'Set-ClipKey' } | Select-Object -First 1
$keyFields = @('BlendWithAnimation', 'ClipSource1Name', 'ClipSource2Name', 'BlendsWithAction', 'GeneratedIndex')
# every Set-ClipKey call comes after an if on the clone name's length in its own block: the name IS the key, and
# the engine's clip name and row key are 64-byte buffers, 63 usable
$out.name_guarded = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.CommandAst] -and $a.GetCommandName() -eq 'Set-ClipKey' }, $true) | ForEach-Object {
  $call = $_
  $p = $call.Parent
  while ($null -ne $p -and -not ($p -is [System.Management.Automation.Language.StatementBlockAst])) { $p = $p.Parent }
  $before = @($p.Statements | Where-Object { $_.Extent.EndOffset -le $call.Extent.StartOffset })
  [bool](@($before | Where-Object { $_ -is [System.Management.Automation.Language.IfStatementAst] -and $_.Clauses[0].Item1.Extent.Text -match '\.Length\s+-gt\s+63' }).Count)
})
$out.stray_writes = @($ast.FindAll({ param($a)
    $a -is [System.Management.Automation.Language.AssignmentStatementAst] -and
    $a.Left -is [System.Management.Automation.Language.MemberExpressionAst] -and
    $keyFields -contains $a.Left.Member.Extent.Text }, $true) |
  Where-Object { $null -eq $setDef -or $_.Extent.StartOffset -lt $setDef.Extent.StartOffset -or $_.Extent.EndOffset -gt $setDef.Extent.EndOffset } |
  ForEach-Object { $_.Extent.StartLineNumber })

if ($defs.Count -ne $want.Count) { $out | ConvertTo-Json -Depth 6 -Compress; exit 0 }
foreach ($d in $defs) { . ([scriptblock]::Create($d.Extent.Text)) }

function Clip($name, $bwanim = '', $bwa = '', $cs1 = '', $cs2 = '', $gen = -1) {
  [pscustomobject]@{ Name = $name; BlendWithAnimation = $bwanim; BlendsWithAction = $bwa; ClipSource1Name = $cs1; ClipSource2Name = $cs2; GeneratedIndex = [sbyte]$gen }
}
function Snap($c) {
  [ordered]@{ BlendWithAnimation = $c.BlendWithAnimation; BlendsWithAction = $c.BlendsWithAction; ClipSource1Name = $c.ClipSource1Name
              ClipSource2Name = $c.ClipSource2Name; GeneratedIndex = [int]$c.GeneratedIndex }
}
function Twin { Clip 'release_overswing_2h' 'release_overswing_2h_balanced' 'act_release_overswing_2h_balanced' }

$out.keyed = [ordered]@{
  self  = Test-KeyedTemplate (Clip 'release_lance' 'release_lance')
  twin  = Test-KeyedTemplate (Twin)
  child = Test-KeyedTemplate (Clip 'release_overswing_2h_3' '' '' 'release_overswing_2h' 'release_overswing_2h_balanced' 3)
  plain = Test-KeyedTemplate (Clip 'walk_forward_unarmed' '' 'act_walk')
}

$set = [ordered]@{}
$c = Twin; $r = @(Set-ClipKey $c 'anim_hill_troll_release_overswing_2h' $true)
$set.keyed = [ordered]@{ out = $r.Count; clip = Snap $c; fault = Get-ClipKeyFault ([pscustomobject]@{ Name = 'anim_hill_troll_release_overswing_2h'; BlendWithAnimation = $c.BlendWithAnimation; BlendsWithAction = $c.BlendsWithAction; ClipSource1Name = $c.ClipSource1Name; ClipSource2Name = $c.ClipSource2Name; GeneratedIndex = $c.GeneratedIndex }) $true }
$c = Clip 'release_overswing_2h_3' '' 'act_x' 'release_overswing_2h' 'release_overswing_2h_balanced' 3; $r = @(Set-ClipKey $c 'anim_hill_troll_child' $true)
$set.keyed_child = [ordered]@{ out = $r.Count; clip = Snap $c }
$c = Clip 'walk_forward_unarmed' '' 'act_walk' '' 'stale'; $r = @(Set-ClipKey $c 'anim_hill_troll_walk' $false)
$set.unkeyed = [ordered]@{ out = $r.Count; clip = Snap $c; fault = Get-ClipKeyFault ([pscustomobject]@{ Name = 'anim_hill_troll_walk'; BlendWithAnimation = $c.BlendWithAnimation; BlendsWithAction = $c.BlendsWithAction; ClipSource1Name = $c.ClipSource1Name; ClipSource2Name = $c.ClipSource2Name; GeneratedIndex = $c.GeneratedIndex }) $false }
$out.set = $set

$n = 'anim_hill_troll_release_overswing_2h'
$out.fault = [ordered]@{
  keyed_self       = Get-ClipKeyFault (Clip $n $n) $true
  keyed_blank      = Get-ClipKeyFault (Clip $n) $true
  keyed_other      = Get-ClipKeyFault (Clip $n 'release_overswing_2h_balanced') $true
  keyed_action     = Get-ClipKeyFault (Clip $n $n 'act_release_overswing_2h_balanced') $true
  keyed_child      = Get-ClipKeyFault (Clip $n $n '' 'a' 'b' 3) $true
  keyed_case       = Get-ClipKeyFault (Clip $n $n.ToUpper()) $true
  unkeyed_blank    = Get-ClipKeyFault (Clip $n '' 'act_x') $false
  unkeyed_key      = Get-ClipKeyFault (Clip $n $n) $false
  unkeyed_src2     = Get-ClipKeyFault (Clip $n '' '' '' 'b') $false
}
$out | ConvertTo-Json -Depth 6 -Compress
"""


@unittest.skipUnless(SHELL, "no PowerShell on PATH")
class ClipKeyRuleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with tempfile.TemporaryDirectory() as d:
            harness = os.path.join(d, "harness.ps1")
            with open(harness, "w", encoding="ascii") as f:
                f.write(HARNESS)
            p = subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", harness,
                                "-Script", SCRIPT], capture_output=True, text=True, timeout=180)
        if p.returncode != 0:
            raise AssertionError("harness failed (%d): %s %s" % (p.returncode, p.stdout, p.stderr))
        cls.r = json.loads(p.stdout.strip().splitlines()[-1])

    def _list(self, v):
        return v if isinstance(v, list) else ([] if v is None else [v])

    def test_the_rule_is_three_functions_in_the_script(self):
        self.assertEqual(sorted(self._list(self.r["found"])), ["Get-ClipKeyFault", "Set-ClipKey", "Test-KeyedTemplate"])

    def test_both_write_paths_and_both_verify_modes_use_the_rule(self):
        calls = self.r["calls"]
        self.assertEqual(calls["Set-ClipKey"], 2, "the -CloneByName path and the Fab path")
        self.assertEqual(calls["Get-ClipKeyFault"], 2, "the -CloneByName -Verify and the Fab -Verify")
        self.assertGreaterEqual(calls["Test-KeyedTemplate"], 1)

    def test_every_clone_name_is_length_checked_before_it_is_keyed(self):
        # the -CloneByName path and the Fab path both refuse a name over 63 characters before Set-ClipKey, which only
        # sets fields: a self-key is the name, so the name's own limit covers it (the header promises it for every name)
        self.assertEqual(self._list(self.r["name_guarded"]), [True, True])

    def test_nothing_else_writes_the_key_fields(self):
        # the old lines 270 and 436 blanked the key on every clone
        self.assertEqual(self._list(self.r["stray_writes"]), [])

    def test_a_template_is_keyed_by_its_own_name_a_twin_or_a_parent(self):
        self.assertEqual(self.r["keyed"], {"self": True, "twin": True, "child": True, "plain": False})

    def test_a_keyed_template_makes_a_self_keyed_clone(self):
        s = self.r["set"]["keyed"]
        self.assertEqual(s["out"], 0, "Set-ClipKey only sets fields")
        self.assertEqual(s["clip"], {"BlendWithAnimation": "anim_hill_troll_release_overswing_2h", "BlendsWithAction": "",
                                     "ClipSource1Name": "", "ClipSource2Name": "", "GeneratedIndex": -1})
        self.assertEqual(s["fault"], "", "a fresh clone passes -Verify")

    def test_a_generated_child_template_loses_its_parents_and_index(self):
        s = self.r["set"]["keyed_child"]
        self.assertEqual(s["out"], 0)
        self.assertEqual(s["clip"], {"BlendWithAnimation": "anim_hill_troll_child", "BlendsWithAction": "",
                                     "ClipSource1Name": "", "ClipSource2Name": "", "GeneratedIndex": -1})

    def test_an_unkeyed_template_keeps_todays_blank_names(self):
        s = self.r["set"]["unkeyed"]
        self.assertEqual(s["out"], 0)
        self.assertEqual(s["clip"], {"BlendWithAnimation": "", "BlendsWithAction": "act_walk",
                                     "ClipSource1Name": "", "ClipSource2Name": "", "GeneratedIndex": -1})
        self.assertEqual(s["fault"], "")

    def test_verify_passes_the_right_shapes_only(self):
        f = self.r["fault"]
        self.assertEqual((f["keyed_self"], f["unkeyed_blank"]), ("", ""))
        for bad in ("keyed_blank", "keyed_other", "keyed_action", "keyed_child", "keyed_case", "unkeyed_key",
                    "unkeyed_src2"):
            self.assertTrue(f[bad], bad)


RESTYLE_HARNESS = r"""
param([string]$Script, [string]$Cases)
$ErrorActionPreference = 'Stop'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$null)
$want = @('Read-StepPoints', 'Resolve-TravelScale', 'Get-LoopFault', 'Get-StepCoverageFault', 'Get-StepFault')
$defs = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true) |
  Where-Object { $want -contains $_.Name })
$out = [ordered]@{ found = @($defs | ForEach-Object { $_.Name }) }
$params = @($ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
$out.params = $params
$calls = [ordered]@{}
foreach ($n in $want) {
  $calls[$n] = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.CommandAst] -and $a.GetCommandName() -eq $n }, $true)).Count
}
$out.calls = $calls
if ($defs.Count -ne $want.Count) { $out | ConvertTo-Json -Depth 8 -Compress; exit 0 }
foreach ($d in $defs) { . ([scriptblock]::Create($d.Extent.Text)) }
$spec = Get-Content $Cases -Raw | ConvertFrom-Json  # not $cases: PowerShell names are case-insensitive and that is the typed param
$res = [ordered]@{}
foreach ($c in $spec.read) {
  try {
    $r = Read-StepPoints $c.report $c.prefix @($c.clips)
    $pts = [ordered]@{}
    foreach ($k in ($r.Points.Keys | Sort-Object)) { $pts[$k] = @($r.Points[$k] | ForEach-Object { [double]$_ }) }
    $res[$c.name] = [ordered]@{ ok = $true; stride = [double]$r.Stride; points = $pts; gait = @($r.Gait.Keys | Sort-Object) }
  } catch { $res[$c.name] = [ordered]@{ ok = $false; error = $_.Exception.Message } }
}
$out.read = $res
$tr = [ordered]@{}
foreach ($c in $spec.travel) {
  try { $tr[$c.name] = [ordered]@{ ok = $true; value = [double](Resolve-TravelScale ([double]$c.typed) ([double]$c.report) 'the report') } }
  catch { $tr[$c.name] = [ordered]@{ ok = $false; error = $_.Exception.Message } }
}
$out.travel = $tr
$lp = [ordered]@{}
foreach ($c in $spec.loop) { $lp[$c.name] = Get-LoopFault ([double]$c.vanilla) ([double]$c.stride) ([double]$c.actual) }
$out.loop = $lp
$cv = [ordered]@{}
foreach ($c in $spec.coverage) { $cv[$c.name] = Get-StepCoverageFault ([bool]$c.restyle) ([int]$c.checked) }
$out.coverage = $cv
$sf = [ordered]@{}
foreach ($c in $spec.stepfault) { $sf[$c.name] = Get-StepFault ([double]$c.x) ([double]$c.y) @([double]$c.want[0], [double]$c.want[1]) }
$out.stepfault = $sf
$out | ConvertTo-Json -Depth 8 -Compress
"""

WALK = ["walk_forward_unarmed", "walk_forward_1h", "walk_forward_1h_left_stance", "walk_forward_1h_with_shield",
        "walk_forward_1h_with_shield_left_stance"]
RUN = [w.replace("walk", "run") for w in WALK]
IDLES = ["stand_1h", "stand_non_combat"]
# The restyle's provenance (restyle_human_clip.py): the top-level spec_sha is the whole loaded spec's; each entry has its
# own spec_sha (the spec merged for that clip plus its kind) and a loaded_spec_sha that equals the top-level one
SHA = "5c1e0f3a9b7d"
CLIP_SHA = "a0a0a0a0a0a0"


def gait_entry(stride=0.85, points=(0.48912, 0.92563), **extra):
    e = {"clip": "x", "kind": "gait", "stride_scale": stride, "spec_sha": CLIP_SHA, "loaded_spec_sha": SHA,
         "export": {"ok": True, "err": None, "bytes": 1}}
    if points is not None:
        e["step_points"] = list(points)
    e.update(extra)
    return e


def idle_entry():
    return {"clip": "x", "kind": "idle", "stride_scale": 1.0, "spec_sha": CLIP_SHA, "loaded_spec_sha": SHA,
            "export": {"ok": True, "err": None, "bytes": 1}}


def good_report(prefix="taom_orc_", stride=0.85, with_idles=False):
    clips = {c: gait_entry(stride, (0.48912, 0.92563)) for c in WALK}
    clips.update({c: gait_entry(stride, (0.5, 0.9396)) for c in RUN})
    if with_idles:
        clips.update({c: idle_entry() for c in IDLES})
    return {"variant": "orc", "spec": {"clip_prefix": prefix, "stride_scale": stride}, "spec_sha": SHA, "clips": clips}


@unittest.skipUnless(SHELL, "no PowerShell on PATH")
class RestyleReportTests(unittest.TestCase):
    """-RestyleReport: the report's contract is checked before a clip is written, and the report's stride sets
    -TravelScale. Each rejected shape below was a silent 0 clips with a green -Verify before."""

    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        d = cls.tmp.name
        cls.index = WALK + RUN
        cls.n = 0
        cases = {"read": [], "travel": [], "loop": [], "coverage": [], "stepfault": []}

        def add(name, report, prefix="taom_orc_", clips=None):
            path = os.path.join(d, "r%d.json" % len(cases["read"]))
            with open(path, "w", encoding="utf-8") as f:
                json.dump(report, f)
            cases["read"].append({"name": name, "report": path, "prefix": prefix, "clips": clips or cls.index})

        add("good10", good_report())
        add("good_with_idles", good_report(with_idles=True), clips=cls.index + IDLES)
        r = good_report()
        r["clips"]["walk_forward_1h"]["step_points"] = [0.9, 0.4]
        add("descending_order_kept", r)
        add("no_clips_key", {"spec": {"clip_prefix": "taom_orc_"}})
        add("empty_clips", {"spec": {"clip_prefix": "taom_orc_"}, "clips": {}})
        r = good_report()
        del r["clips"]["run_forward_1h"]["step_points"]
        r["clips"]["run_forward_1h"]["step_points_gap"] = "no vanilla step points were given"
        add("step_points_gap", r)
        r = good_report()
        for e in r["clips"].values():   # the stale 2026-10-14 orc report: gait clips, no step point fields at all
            del e["step_points"]
        add("gait_without_step_points", r)
        r = good_report()
        r["clips"]["run_forward_1h"]["step_points"] = [0.5]
        add("one_step_point", r)
        r = good_report()
        r["clips"]["walk_forward_1h"] = {"clip": "walk_forward_1h", "error": "Traceback ..."}
        add("error_entry", r)
        r = good_report()
        r["clips"]["walk_forward_1h"]["export"] = {"ok": False, "err": "boom"}
        add("export_failed", r)
        add("prefix_mismatch", good_report(prefix="taom_orc_b_"))
        r = good_report()
        del r["spec"]
        add("no_spec", r)
        r = good_report()
        r["clips"]["run_forward_1h"]["stride_scale"] = 0.95
        add("two_strides", r)
        r = good_report()
        for e in r["clips"].values():
            e["stride_scale"] = 0.85 + 0.00001   # the same stride to 4 places
        r["clips"]["run_forward_1h"]["stride_scale"] = 0.85
        add("same_stride_rounded", r)
        r = good_report()
        del r["clips"]["run_forward_1h"]
        add("index_clip_without_entry", r)
        add("only_idles", {"spec": {"clip_prefix": "taom_orc_"}, "clips": {c: idle_entry() for c in IDLES},
                           "spec_sha": SHA}, clips=IDLES)

        # F4 (a): the restyle drops a failed clip from clips_index.json but keeps its report entry, so a batch whose index
        # holds only the walks used to pass with a failed run in the report
        r = good_report()
        r["clips"]["run_forward_1h"] = {"clip": "run_forward_1h", "error": "Traceback ..."}
        add("failed_run_not_indexed", r, clips=WALK)
        r = good_report()
        r["clips"]["run_forward_1h"]["export"] = {"ok": False, "err": "boom"}
        add("export_failed_not_indexed", r, clips=WALK)
        r = good_report()
        del r["clips"]["run_forward_1h"]["export"]
        add("no_export_not_indexed", r, clips=WALK)
        # F4 (b): the class is the clip NAME's (walk_ or run_ is a gait), never the report's kind
        r = good_report()
        e = r["clips"]["run_forward_unarmed"]
        e["kind"], e["stride_scale"] = "idle", 0.95
        del e["step_points"]
        add("run_marked_idle", r)
        r = good_report()
        del r["clips"]["walk_forward_1h"]["kind"]
        add("gait_without_kind", r)
        r = good_report(with_idles=True)
        r["clips"]["stand_1h"]["kind"] = "gait"
        add("stand_marked_gait", r, clips=cls.index + IDLES)
        # F4 (c): provenance. The restyle writes spec_sha at the top and on every entry; a partial run keeps older entries
        add("no_top_spec_sha", {k: v for k, v in good_report().items() if k != "spec_sha"})
        r = good_report()
        r["spec_sha"] = ""
        add("empty_top_spec_sha", r)
        r = good_report()
        del r["clips"]["run_forward_1h"]["spec_sha"]
        add("no_entry_spec_sha", r)
        r = good_report()
        del r["clips"]["run_forward_1h"]["loaded_spec_sha"]
        add("no_entry_loaded_spec_sha", r)
        r = good_report()
        r["clips"]["run_forward_1h"]["loaded_spec_sha"] = ""
        add("empty_entry_loaded_spec_sha", r)
        r = good_report()
        r["clips"]["run_forward_1h"]["loaded_spec_sha"] = 12345
        add("non_string_entry_loaded_spec_sha", r)
        # an --only gait re-run after a spec change leaves the idle entries (and their FBX) on the old spec
        r = good_report(with_idles=True)
        r["clips"]["stand_1h"]["loaded_spec_sha"] = "0000deadbeef"
        add("stale_idle", r, clips=cls.index + IDLES)
        r = good_report(with_idles=True)
        r["clips"]["stand_1h"]["loaded_spec_sha"] = "0000deadbeef"
        add("stale_idle_not_indexed", r)   # the batch index holds gaits only
        r = good_report(with_idles=True)
        del r["clips"]["stand_non_combat"]["loaded_spec_sha"]
        add("idle_without_loaded_spec_sha", r, clips=cls.index + IDLES)
        r = good_report(with_idles=True)
        del r["clips"]["stand_non_combat"]["spec_sha"]
        add("idle_without_spec_sha", r, clips=cls.index + IDLES)
        r = good_report(with_idles=True)
        for k in ("spec_sha", "loaded_spec_sha"):
            del r["clips"]["stand_1h"][k]
        add("idle_without_provenance", r, clips=cls.index + IDLES)
        r = good_report()
        r["clips"]["walk_forward_1h"]["loaded_spec_sha"] = "0000deadbeef"
        add("mixed_provenance", r)
        r = good_report()
        for c in RUN:
            r["clips"][c]["loaded_spec_sha"] = "0000deadbeef"
        add("mixed_provenance_not_indexed", r, clips=WALK)
        r = good_report()
        r["spec_sha"] = r["spec_sha"].upper()
        add("spec_sha_case_differs", r)
        r = good_report()   # a run override: the merged spec, so the per-clip spec_sha, differs by clip. That is legitimate
        for c in RUN:
            r["clips"][c]["spec_sha"] = "b1b1b1b1b1b1"
        r["clips"]["walk_forward_1h"]["spec_sha"] = SHA   # even equal to the top-level one, it is never compared
        add("per_clip_spec_sha_differs_ok", r)
        # F5: numbers are checked before any [double] conversion
        for name, val in (("nan_string", "NaN"), ("zero", 0), ("negative", -0.85), ("true", True), ("null", None),
                          ("numeric_string", "0.85"), ("list", [0.85])):
            r = good_report()
            r["clips"]["walk_forward_1h"]["stride_scale"] = val
            add("stride_" + name, r)
        r = good_report()
        r["clips"]["walk_forward_1h"]["stride_scale"] = float("nan")   # json.dump writes a bare NaN
        add("stride_nan_literal", r)
        r = good_report()
        r["clips"]["walk_forward_1h"]["stride_scale"] = float("inf")
        add("stride_infinity_literal", r)
        for name, pts in (("null_first", [None, 2]), ("true", [True, 0.9]), ("string", ["0.4", 0.9]),
                          ("nan_string", ["NaN", 0.9]), ("negative", [-0.1, 0.9]), ("past_one", [0.4, 1.5]),
                          ("bad_third", [0.4, 0.9, "x"]), ("list", [[0.4], 0.9])):
            r = good_report()
            r["clips"]["walk_forward_1h"]["step_points"] = pts
            add("points_" + name, r)
        r = good_report()
        r["clips"]["walk_forward_1h"]["step_points"] = [0.4, float("nan")]
        add("points_nan_literal", r)
        r = good_report()
        r["clips"]["walk_forward_1h"]["step_points"] = [0.0, 1.0]   # the restyle rounds to 5 places: 0.999996 is 1.0
        add("points_bounds_ok", r)
        r = good_report()
        e = r["clips"]["walk_forward_1h"]    # Codex's executed counterexample
        e["stride_scale"], e["step_points"] = "NaN", [None, 2]
        add("codex_nan_and_null_points", r)
        r = good_report(with_idles=True)
        r["clips"]["stand_1h"]["step_points"] = ["NaN", 0.5]
        add("idle_bad_points", r, clips=cls.index + IDLES)
        cases["travel"] += [
            {"name": "unset", "typed": 0, "report": 0.85},
            {"name": "typed_matches", "typed": 0.8504, "report": 0.85},
            {"name": "typed_differs", "typed": 1.377, "report": 1.5578},
            {"name": "report_nan", "typed": 1.377, "report": "NaN"},
            {"name": "report_nan_unset", "typed": 0, "report": "NaN"},
            {"name": "typed_nan", "typed": "NaN", "report": 0.85},
            {"name": "report_infinity", "typed": 0, "report": "Infinity"},
            {"name": "report_zero", "typed": 0, "report": 0},
            {"name": "report_negative", "typed": 0, "report": -0.85},
        ]
        cases["loop"] += [
            {"name": "exact", "vanilla": 1.7, "stride": 0.85, "actual": 1.445},
            {"name": "float_noise", "vanilla": 1.7, "stride": 0.85, "actual": 1.4450001},
            {"name": "unscaled", "vanilla": 1.7, "stride": 0.85, "actual": 1.7},
            {"name": "wrong_scale", "vanilla": 1.7, "stride": 1.5578, "actual": 1.7 * 1.377},
            {"name": "nan_stride", "vanilla": 1.8, "stride": "NaN", "actual": 123},   # Codex F5: used to return ''
            {"name": "nan_vanilla", "vanilla": "NaN", "stride": 0.85, "actual": 1.445},
            {"name": "nan_actual", "vanilla": 1.7, "stride": 0.85, "actual": "NaN"},
            {"name": "infinite_stride", "vanilla": 1.7, "stride": "Infinity", "actual": "Infinity"},
        ]
        cases["coverage"] += [
            {"name": "off", "restyle": False, "checked": 0},
            {"name": "none_checked", "restyle": True, "checked": 0},
            {"name": "some", "restyle": True, "checked": 10},
        ]
        cases["stepfault"] += [
            {"name": "exact", "x": 0.4, "y": 0.9, "want": [0.4, 0.9]},
            {"name": "within_tolerance", "x": 0.4015, "y": 0.8985, "want": [0.4, 0.9]},
            {"name": "x_off", "x": 0.45, "y": 0.9, "want": [0.4, 0.9]},
            {"name": "y_off", "x": 0.4, "y": 0.95, "want": [0.4, 0.9]},
            {"name": "nan_x", "x": "NaN", "y": 0.9, "want": [0.4, 0.9]},   # NaN compares false against any limit
            {"name": "nan_y", "x": 0.4, "y": "NaN", "want": [0.4, 0.9]},
        ]
        harness = os.path.join(d, "harness.ps1")
        with open(harness, "w", encoding="ascii") as f:
            f.write(RESTYLE_HARNESS)
        cf = os.path.join(d, "cases.json")
        with open(cf, "w", encoding="utf-8") as f:
            json.dump(cases, f)
        p = subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", harness,
                            "-Script", SCRIPT, "-Cases", cf], capture_output=True, text=True, timeout=180)
        if p.returncode != 0:
            raise AssertionError("harness failed (%d): %s %s" % (p.returncode, p.stdout, p.stderr))
        cls.r = json.loads(p.stdout.strip().splitlines()[-1])

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def _list(self, v):
        return v if isinstance(v, list) else ([] if v is None else [v])

    def refused(self, name, *needles):
        c = self.r["read"][name]
        self.assertFalse(c["ok"], name + " must be refused")
        for n in needles:
            self.assertIn(n, c["error"], name)

    def test_the_contract_is_five_functions_and_the_switch_is_renamed(self):
        self.assertEqual(sorted(self._list(self.r["found"])),
                         ["Get-LoopFault", "Get-StepCoverageFault", "Get-StepFault", "Read-StepPoints",
                          "Resolve-TravelScale"])
        params = self._list(self.r["params"])
        self.assertIn("RestyleReport", params)
        self.assertNotIn("StepPointsFrom", params)

    def test_the_script_calls_each_function(self):
        calls = self.r["calls"]
        self.assertEqual(calls["Read-StepPoints"], 1)
        self.assertEqual(calls["Resolve-TravelScale"], 2, "the retarget report and the restyle report share one rule")
        self.assertGreaterEqual(calls["Get-LoopFault"], 1)
        self.assertEqual(calls["Get-StepCoverageFault"], 1)
        self.assertEqual(calls["Get-StepFault"], 1, "-Verify's STEP check")

    def test_a_good_report_gives_ten_entries_and_its_stride(self):
        c = self.r["read"]["good10"]
        self.assertTrue(c["ok"], c)
        self.assertEqual(len(c["points"]), 10)
        self.assertAlmostEqual(c["stride"], 0.85)
        self.assertEqual(c["points"]["walk_forward_1h"], [0.48912, 0.92563])
        self.assertEqual(c["points"]["run_forward_1h"], [0.5, 0.9396])
        self.assertEqual(len(c["gait"]), 10)

    def test_idle_entries_are_neither_gait_nor_a_second_stride(self):
        c = self.r["read"]["good_with_idles"]
        self.assertTrue(c["ok"], c)
        self.assertEqual(len(c["points"]), 10, "an idle has stride_scale 1.0 and no step points")
        self.assertAlmostEqual(c["stride"], 0.85)

    def test_step_point_order_is_the_reports(self):
        c = self.r["read"]["descending_order_kept"]
        self.assertEqual(c["points"]["walk_forward_1h"], [0.9, 0.4], "the restyle no longer sorts; neither does the reader")

    def test_a_report_without_clips_is_refused(self):
        self.refused("no_clips_key", "no clips")
        self.refused("empty_clips", "no clips")

    def test_a_gait_clip_without_two_step_points_is_refused(self):
        self.refused("step_points_gap", "run_forward_1h", "step points")
        self.refused("gait_without_step_points", "step points")   # the stale orc report shape
        self.refused("one_step_point", "run_forward_1h", "step points")

    def test_an_error_or_failed_export_entry_is_refused(self):
        self.refused("error_entry", "walk_forward_1h", "error")
        self.refused("export_failed", "walk_forward_1h", "export")

    def test_a_report_of_another_prefix_is_refused(self):
        self.refused("prefix_mismatch", "clip_prefix", "taom_orc_b_")
        self.refused("no_spec", "clip_prefix")

    def test_two_strides_among_gait_clips_are_refused_but_rounding_is_not(self):
        self.refused("two_strides", "stride")
        self.assertTrue(self.r["read"]["same_stride_rounded"]["ok"])

    def test_an_index_clip_with_no_entry_is_refused(self):
        self.refused("index_clip_without_entry", "run_forward_1h")

    def test_a_report_with_no_gait_clip_is_refused(self):
        self.refused("only_idles", "gait")

    def test_a_failed_entry_is_refused_though_the_index_dropped_it(self):
        # F4: restyle_human_clip.py pops a failed clip from clips_index.json and keeps its report entry. A walk-only index
        # used to pass, and -Verify then counted the old run tpac as another pipeline's
        self.refused("failed_run_not_indexed", "run_forward_1h", "error")
        self.refused("export_failed_not_indexed", "run_forward_1h", "export")
        self.refused("no_export_not_indexed", "run_forward_1h", "export")

    def test_a_gait_is_what_its_name_says_not_what_its_kind_says(self):
        # F4: a run labelled idle at stride 0.95 escaped stride agreement and the gait STEP and LOOP checks
        self.refused("run_marked_idle", "run_forward_unarmed", "kind")
        self.refused("gait_without_kind", "walk_forward_1h", "kind")
        self.refused("stand_marked_gait", "stand_1h", "kind")

    def test_a_report_without_provenance_is_refused_and_says_to_regenerate(self):
        self.refused("no_top_spec_sha", "spec_sha", "regenerated")
        self.refused("empty_top_spec_sha", "spec_sha", "regenerated")
        self.refused("no_entry_spec_sha", "run_forward_1h", "spec_sha", "regenerated")
        self.refused("no_entry_loaded_spec_sha", "run_forward_1h", "loaded_spec_sha", "regenerated")
        self.refused("empty_entry_loaded_spec_sha", "run_forward_1h", "loaded_spec_sha", "regenerated")
        self.refused("non_string_entry_loaded_spec_sha", "run_forward_1h", "loaded_spec_sha")

    def test_mixed_provenance_is_refused(self):
        self.refused("mixed_provenance", "walk_forward_1h", "loaded_spec_sha")
        self.refused("mixed_provenance_not_indexed", "loaded_spec_sha")   # a stale gait record the index no longer lists
        self.refused("spec_sha_case_differs", "loaded_spec_sha")

    def test_idle_entries_need_the_same_provenance_as_gaits(self):
        self.refused("stale_idle", "stand_1h", "loaded_spec_sha")
        self.refused("stale_idle_not_indexed", "stand_1h", "loaded_spec_sha")
        self.refused("idle_without_loaded_spec_sha", "stand_non_combat", "loaded_spec_sha", "regenerated")
        self.refused("idle_without_spec_sha", "stand_non_combat", "spec_sha", "regenerated")
        self.refused("idle_without_provenance", "stand_1h", "spec_sha", "regenerated")

    def test_the_per_clip_spec_sha_is_required_but_never_compared(self):
        c = self.r["read"]["per_clip_spec_sha_differs_ok"]
        self.assertTrue(c["ok"], c)

    def test_a_good_report_passes_with_provenance(self):
        self.assertTrue(self.r["read"]["good10"]["ok"])
        self.assertTrue(self.r["read"]["good_with_idles"]["ok"], "idle entries carry the same spec_sha")

    def test_a_bad_stride_is_refused_before_any_conversion(self):
        for name in ("nan_string", "zero", "negative", "true", "null", "numeric_string", "list", "nan_literal",
                     "infinity_literal"):
            self.refused("stride_" + name)
        self.refused("stride_nan_string", "stride_scale", "walk_forward_1h")
        self.refused("stride_zero", "stride_scale")

    def test_a_bad_step_point_is_refused_before_any_conversion(self):
        for name in ("null_first", "true", "string", "nan_string", "negative", "past_one", "bad_third", "list",
                     "nan_literal"):
            self.refused("points_" + name)
        self.refused("points_null_first", "step point", "walk_forward_1h")
        self.refused("idle_bad_points", "stand_1h", "step point")

    def test_step_points_zero_and_one_are_in_range(self):
        # the restyle rounds a point to 5 places, so a phase of 0.999996 is written as 1.0
        c = self.r["read"]["points_bounds_ok"]
        self.assertTrue(c["ok"], c)
        self.assertEqual(c["points"]["walk_forward_1h"], [0.0, 1.0])

    def test_codexs_nan_stride_with_null_step_points_is_refused(self):
        self.refused("codex_nan_and_null_points", "walk_forward_1h")

    def test_travel_scale_is_the_reports_unless_a_typed_one_differs(self):
        t = self.r["travel"]
        self.assertEqual((t["unset"]["ok"], t["unset"]["value"]), (True, 0.85))
        self.assertTrue(t["typed_matches"]["ok"])
        self.assertFalse(t["typed_differs"]["ok"], "the hand-typed 1.377 against 1.5578 skated the troll's feet 12%")
        self.assertIn("1.377", t["typed_differs"]["error"])

    def test_travel_scale_fails_closed_on_a_non_finite_or_non_positive_value(self):
        t = self.r["travel"]
        for name in ("report_nan", "report_nan_unset", "typed_nan", "report_infinity", "report_zero", "report_negative"):
            self.assertFalse(t[name]["ok"], name + ": a NaN compares false, so the old disagreement test passed it")

    def test_loop_displacement_must_be_vanilla_times_stride(self):
        lp = self.r["loop"]
        self.assertEqual((lp["exact"], lp["float_noise"]), ("", ""))
        self.assertTrue(lp["unscaled"])
        self.assertTrue(lp["wrong_scale"])

    def test_loop_displacement_fails_closed_on_non_finite_values(self):
        lp = self.r["loop"]
        for name in ("nan_stride", "nan_vanilla", "nan_actual", "infinite_stride"):
            self.assertTrue(lp[name], name + ": `Get-LoopFault 1.8 NaN 123` used to return ''")

    def test_step_fault_is_the_verify_tolerance_and_fails_closed(self):
        sf = self.r["stepfault"]
        self.assertEqual((sf["exact"], sf["within_tolerance"]), ("", ""))
        for name in ("x_off", "y_off", "nan_x", "nan_y"):
            self.assertTrue(sf[name], name)

    def test_verify_fails_when_no_step_point_was_checked(self):
        cv = self.r["coverage"]
        self.assertEqual((cv["off"], cv["some"]), ("", ""))
        self.assertTrue(cv["none_checked"])

    def test_restyle_report_without_clone_by_name_is_refused(self):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "r.json")
            with open(path, "w", encoding="utf-8") as f:
                json.dump(good_report(), f)
            p = subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", SCRIPT,
                                "-RestyleReport", path], capture_output=True, text=True, timeout=180)
        self.assertNotEqual(p.returncode, 0)
        self.assertIn("-CloneByName", p.stdout + p.stderr)


TOOLS_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SAVE_CALL = re.compile(r"\.Save\(", re.I)   # PowerShell binds .NET method names in any case
COMMENT_OR_STRING = re.compile(r"(\"[^\"]*\"|'[^']*')|#.*")


def strip_comment(line):
    """The line without its `#` comment; a `#` inside a quoted string stays."""
    return COMMENT_OR_STRING.sub(lambda m: m.group(1) or "", line)


def save_call_arg_counts(line):
    """Argument count of every `.Save(` call on one line: top-level commas, so `Save($a, (f 1, 2), 2)` is 3."""
    counts = []
    for m in SAVE_CALL.finditer(line):
        depth, args, quote, seen = 1, 1, None, False
        for ch in line[m.end():]:
            if quote:
                quote = None if ch == quote else quote
                continue
            if ch in "'\"":
                quote, seen = ch, True
            elif ch in "([{":
                depth, seen = depth + 1, True
            elif ch in ")]}":
                depth -= 1
                if depth == 0:
                    break
            elif ch == "," and depth == 1:
                args += 1
            elif not ch.isspace():
                seen = True
        counts.append(args if seen else 0)
    return counts


class TestTpacSaveCallsPassThreeArguments(unittest.TestCase):
    """TpacTool v0.5.1 is AssetPackage.Save(string newFilePath = null, bool useOriginalChecksum = false,
    int tpacVersion = 2). A two-argument `Save($out, 2)` binds useOriginalChecksum = $true and writes the
    old checksum, silently. Every tools/*.ps1 package save must spell all three."""

    def test_the_matcher_flags_a_short_call(self):
        self.assertEqual(save_call_arg_counts("$pkg.Save($out, 2)"), [2])
        self.assertEqual(save_call_arg_counts("$pkg.Save($out)"), [1])
        self.assertEqual(save_call_arg_counts("$pkg.Save()"), [0])
        self.assertEqual(save_call_arg_counts("$pkg.Save($out, $false, 2)   # path, flag, version"), [3])
        self.assertEqual(save_call_arg_counts("$p.Save((Join-Path $a 'x,y'), $false, 2)"), [3])
        # PowerShell binds .NET method names in any case, and a `#` inside a string is not a comment
        self.assertEqual(save_call_arg_counts(strip_comment("$pkg.save($out, 2)")), [2])
        self.assertEqual(save_call_arg_counts(strip_comment('$tag = "#1"; $pkg.Save($out, 2)')), [2])
        self.assertEqual(save_call_arg_counts(strip_comment("# $pkg.Save($out, 2)")), [])

    def test_every_package_save_in_tools_passes_three_arguments(self):
        bad, seen = [], 0
        for path in sorted(glob.glob(os.path.join(TOOLS_DIR, "*.ps1"))):
            with open(path, encoding="utf-8-sig") as fh:
                for no, line in enumerate(fh, 1):
                    for n in save_call_arg_counts(strip_comment(line)):
                        seen += 1
                        if n != 3:
                            bad.append(f"{os.path.basename(path)}:{no} passes {n} argument(s)")
        self.assertGreater(seen, 0, "no .Save( call found: the scan checks nothing")
        self.assertEqual(bad, [])


if __name__ == "__main__":
    unittest.main()
