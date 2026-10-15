"""gen_animalia_anim_clips.ps1: Get-MeasureProblem, the guard that decides whether a measurement may drive a clip.

The generator needs TpacTool, the Kit's loader and the install, so these tests lift Get-MeasureProblem (and the
frame-exemption table it reads) out of the script with the PowerShell parser and run them on plain objects shaped
like the measure JSON, the way test_gen_troll_anim_clips.py does. Windows PowerShell 5.1 first: the script's runtime.
"""
import json
import os
import shutil
import subprocess
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gen_animalia_anim_clips.ps1")
SHELL = shutil.which("powershell") or shutil.which("pwsh")

HARNESS = r"""
param([string]$Script)
$ErrorActionPreference = 'Stop'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$null)
$fn = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true) |
  Where-Object { @('Get-MeasureProblem', 'Get-FrameProblem') -contains $_.Name })
$ex = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.AssignmentStatementAst] -and
    $a.Left.Extent.Text -eq '$FRAME_EXEMPT' }, $true))
$out = [ordered]@{ found = @($fn | ForEach-Object { $_.Name }); exempt_found = $ex.Count }
if ($ex.Count -ne 1 -or ($fn | Where-Object { $_.Name -eq 'Get-MeasureProblem' }).Count -ne 1) {
  $out | ConvertTo-Json -Depth 6 -Compress; exit 0
}
$FPS = 30.0
. ([scriptblock]::Create($ex[0].Extent.Text))
foreach ($d in $fn) { . ([scriptblock]::Create($d.Extent.Text)) }
$out.exempt_names = @($FRAME_EXEMPT.Keys)
$out.exempt_reason_is_text = [bool](@($FRAME_EXEMPT.Values | Where-Object { $_ -is [string] -and $_.Length -gt 20 }).Count -eq $FRAME_EXEMPT.Count)

function J($s) { $s | ConvertFrom-Json }
function P($ms, $role, $name = 'wildmesh_x_walk', $frames = 37) {
  $r = Get-MeasureProblem (J $ms) $role $name $frames
  if ($null -eq $r) { '' } else { "$r" }
}
$gait = '{"n":36,"loop_displacement_m":1.5,"plants_sorted":[0.1,0.3,0.6,0.8]}'
$out.p = [ordered]@{
  null_record   = [string](Get-MeasureProblem $null 'walk' 'wildmesh_x_walk' 37)
  error_record  = P '{"error":"Traceback"}' 'walk'
  zero_travel   = P '{"n":36,"loop_displacement_m":0,"plants_sorted":[0.1,0.3,0.6,0.8]}' 'walk'
  few_plants    = P '{"n":36,"loop_displacement_m":1.5,"plants_sorted":[0.1,0.3,0.6]}' 'walk'
  canter_one    = P '{"n":36,"loop_displacement_m":1.5,"plants_sorted":[0.1]}' 'canter'
  good          = P $gait 'walk'
  fall_zero     = P '{"n":24,"fall_fraction":0}' 'fallL' 'x' 25
  fall_one      = P '{"n":24,"fall_fraction":1}' 'fallR' 'x' 25
  fall_ok       = P '{"n":24,"fall_fraction":0.4}' 'fallL' 'x' 25
  frames_n      = P $gait 'walk' 'wildmesh_x_walk' 36
  frames_n1     = P $gait 'walk' 'wildmesh_x_walk' 37
  frames_long   = P $gait 'walk' 'wildmesh_x_walk' 38
  fps25         = P '{"n":36,"fps":25.0,"loop_displacement_m":1.5,"plants_sorted":[0.1,0.3,0.6,0.8]}' 'walk'
  fps30         = P '{"n":36,"fps":30.0,"loop_displacement_m":1.5,"plants_sorted":[0.1,0.3,0.6,0.8]}' 'walk'
  fps_absent    = P $gait 'walk'
  n_absent      = P '{"loop_displacement_m":1.5,"plants_sorted":[0.1,0.3,0.6,0.8]}' 'walk' 'wildmesh_x_walk' 99
  exempt_short  = P '{"n":42,"loop_displacement_m":1.5,"plants_sorted":[0.1,0.3,0.6,0.8]}' 'walk' 'animalia_moose_loco_walk' 42
  exempt_other  = P '{"n":42,"loop_displacement_m":1.5,"plants_sorted":[0.1,0.3,0.6,0.8]}' 'walk' 'animalia_elk_loco_walk' 42
}
$out | ConvertTo-Json -Depth 6 -Compress
"""


@unittest.skipUnless(SHELL, "no PowerShell on PATH: the Get-MeasureProblem tests need powershell.exe or pwsh")
class MeasureProblemTests(unittest.TestCase):
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

    def _p(self, key):
        return self.r["p"][key]

    def test_the_guard_and_its_exemption_table_are_in_the_script(self):
        self.assertEqual(self.r["exempt_found"], 1)
        self.assertIn("Get-MeasureProblem", self.r["found"])
        self.assertIn("Get-FrameProblem", self.r["found"], "-Verify shares the frame rule")

    def test_characterization_a_missing_or_error_record_cannot_drive_a_clip(self):
        self.assertEqual(self._p("null_record"), "missing or failed in the measure run")
        self.assertEqual(self._p("error_record"), "missing or failed in the measure run")

    def test_characterization_zero_displacement(self):
        self.assertEqual(self._p("zero_travel"), "loop_displacement_m is not above 0")

    def test_characterization_too_few_plants(self):
        self.assertEqual(self._p("few_plants"), "fewer than 4 hoof plants")
        self.assertEqual(self._p("canter_one"), "")

    def test_characterization_fall_bounds(self):
        self.assertEqual(self._p("fall_zero"), "fall_fraction outside (0, 1)")
        self.assertEqual(self._p("fall_one"), "fall_fraction outside (0, 1)")
        self.assertEqual(self._p("fall_ok"), "")

    def test_a_sound_measure_with_its_rest_frame_passes(self):
        self.assertEqual(self._p("good"), "")
        self.assertEqual(self._p("frames_n1"), "")

    def test_a_master_with_the_measured_frames_but_no_rest_frame_is_a_problem(self):
        self.assertEqual(self._p("frames_n"), "master has 36 frames, measured 36 + rest")

    def test_a_master_with_extra_frames_is_a_problem_too(self):
        self.assertEqual(self._p("frames_long"), "master has 38 frames, measured 36 + rest")

    def test_a_measure_without_n_cannot_be_frame_checked(self):
        self.assertEqual(self._p("n_absent"), "")

    def test_a_measure_not_taken_at_30_fps_is_a_problem(self):
        self.assertEqual(self._p("fps25"), "measured at 25 fps")
        self.assertEqual(self._p("fps30"), "")

    def test_a_measure_without_fps_passes(self):
        self.assertEqual(self._p("fps_absent"), "")

    def test_the_moose_walk_master_is_exempt_from_the_frame_rule_only(self):
        self.assertEqual(self.r["exempt_names"], ["animalia_moose_loco_walk"])
        self.assertTrue(self.r["exempt_reason_is_text"])
        self.assertEqual(self._p("exempt_short"), "")
        self.assertEqual(self._p("exempt_other"), "master has 42 frames, measured 42 + rest")


READ_HARNESS = r"""
param([string]$Script, [string]$Dir)
$ErrorActionPreference = 'Stop'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$null)
$fn = @($ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
    $a.Name -eq 'Read-MeasureFile' }, $true))
if ($fn.Count -ne 1) { '{"found":0}'; exit 0 }
. ([scriptblock]::Create($fn[0].Extent.Text))
[IO.File]::WriteAllText("$Dir\bad.json", '{"clips": {oops')
[IO.File]::WriteAllText("$Dir\good.json", '{"clips": {"walk": {"n": 36}}}')
$missing = Read-MeasureFile "$Dir\nope.json"
$bad = Read-MeasureFile "$Dir\bad.json"
$good = Read-MeasureFile "$Dir\good.json"
[ordered]@{
  found = 1
  missing_is_string = ($missing -is [string])
  bad_is_string = ($bad -is [string])
  bad_text = [string]$bad
  good_is_string = ($good -is [string])
  good_n = [int]$good.walk.n
} | ConvertTo-Json -Compress
"""


@unittest.skipUnless(SHELL, "no PowerShell on PATH: the Read-MeasureFile tests need powershell.exe or pwsh")
class ReadMeasureFileTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with tempfile.TemporaryDirectory() as d:
            harness = os.path.join(d, "harness.ps1")
            with open(harness, "w", encoding="ascii") as f:
                f.write(READ_HARNESS)
            p = subprocess.run([SHELL, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", harness,
                                "-Script", SCRIPT, "-Dir", d], capture_output=True, text=True, timeout=180)
        if p.returncode != 0:
            raise AssertionError("harness failed (%d): %s %s" % (p.returncode, p.stdout, p.stderr))
        cls.r = json.loads(p.stdout.strip().splitlines()[-1])

    def test_the_loader_is_a_function_in_the_script(self):
        self.assertEqual(self.r["found"], 1)

    def test_a_missing_file_returns_a_reason(self):
        self.assertTrue(self.r["missing_is_string"])

    def test_a_malformed_file_returns_a_reason_instead_of_throwing(self):
        self.assertTrue(self.r["bad_is_string"])
        self.assertTrue(self.r["bad_text"])

    def test_a_good_file_returns_its_clips(self):
        self.assertFalse(self.r["good_is_string"])
        self.assertEqual(self.r["good_n"], 36)


if __name__ == "__main__":
    unittest.main()
