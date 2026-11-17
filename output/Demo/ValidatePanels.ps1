$ErrorActionPreference = 'Stop'
foreach ($scene in @('Menu','Gameplay')) {
 $s = [IO.File]::ReadAllText((Join-Path $PWD "Assets/Scenes/$scene.unity"))
 $blocks = @{}; foreach ($m in [regex]::Matches($s, '(?ms)^--- !u!\d+ &(-?\d+).*?(?=^--- !u!|\z)')) { $blocks[$m.Groups[1].Value] = $m.Value }
 $demo = @($blocks.Values | Where-Object { $_ -match '(?m)^  m_Name: Demo\r?$' })
 if ($demo.Count -ne 1) { throw "$scene Demo count $($demo.Count)" }
 $rectId = [regex]::Match($demo[0], 'component: \{fileID: (\d+)').Groups[1].Value
 $rect = $blocks[$rectId]; $parentId = [regex]::Match($rect, 'm_Father: \{fileID: (\d+)').Groups[1].Value
 $parentGo = [regex]::Match($blocks[$parentId], 'm_GameObject: \{fileID: (\d+)').Groups[1].Value
 "$scene parent: " + [regex]::Match($blocks[$parentGo], 'm_Name: (.+)').Groups[1].Value
 function Walk($id) {
  $b = $blocks[$id]; $go = [regex]::Match($b, 'm_GameObject: \{fileID: (\d+)').Groups[1].Value
  $gb = $blocks[$go]; $name = [regex]::Match($gb, 'm_Name: (.+)').Groups[1].Value.Trim()
  if ($name -match 'Confirm|Cancel') { "$scene button: $name"; if ($gb -notmatch 'm_IsActive: 1') { throw 'Button inactive' } }
  $children = [regex]::Match($b, '(?s)m_Children:(.*?)m_Father:').Groups[1].Value
  foreach ($c in [regex]::Matches($children, 'fileID: (\d+)')) { Walk $c.Groups[1].Value }
 }
 Walk $rectId
}
