param([string]$Guid)
$ErrorActionPreference = 'Stop'
foreach ($scene in @('Menu','Gameplay')) {
 $path = Join-Path $PWD "Assets/Scenes/$scene.unity"
 $s = [IO.File]::ReadAllText($path)
 $blocks = @{}; foreach ($m in [regex]::Matches($s, '(?ms)^--- !u!\d+ &(-?\d+).*?(?=^--- !u!|\z)')) { $blocks[$m.Groups[1].Value] = $m.Value }
 $demo = @($blocks.Values | Where-Object { $_ -match '(?m)^  m_Name: Demo\r?$' })
 if ($demo.Count -ne 1) { throw "$scene Demo count $($demo.Count)" }
 $goId = [regex]::Match($demo[0], '&(\d+)').Groups[1].Value
 $componentId = '9027400001'
 if ($blocks.ContainsKey($componentId)) { throw 'Component ID already exists' }
 $updated = $demo[0].Replace('  m_Layer:', "  - component: {fileID: $componentId}`r`n  m_Layer:")
 $s = $s.Replace($demo[0], $updated)
 $rectId = [regex]::Match($demo[0], 'component: \{fileID: (\d+)').Groups[1].Value
 $buttonIds = @{}
 function FindButtons($id) {
  $b = $blocks[$id]; $go = [regex]::Match($b, 'm_GameObject: \{fileID: (\d+)').Groups[1].Value
  $gb = $blocks[$go]; $name = [regex]::Match($gb, 'm_Name: (.+)').Groups[1].Value.Trim()
  if ($name -in @('ConfirmNewGame','CancelNewGame')) {
   foreach ($c in [regex]::Matches($gb, 'component: \{fileID: (\d+)')) {
    $cid = $c.Groups[1].Value
    if ($blocks[$cid] -match 'guid: 4e29b1a8efbd4b44bb3f3716e73f07ff') { $buttonIds[$name] = $cid }
   }
  }
  $children = [regex]::Match($b, '(?s)m_Children:(.*?)m_Father:').Groups[1].Value
  foreach ($c in [regex]::Matches($children, 'fileID: (\d+)')) { FindButtons $c.Groups[1].Value }
 }
 FindButtons $rectId
 if ($buttonIds.Count -ne 2) { throw 'Expected two demo buttons' }
 foreach ($name in $buttonIds.Keys) {
  $method = if ($name -eq 'ConfirmNewGame') {'Yes'} else {'No'}
  $old = $blocks[$buttonIds[$name]]
  $event = @"
  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {fileID: $componentId}
        m_TargetAssemblyTypeName: DemoPopupButtons, Assembly-CSharp
        m_MethodName: $method
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {fileID: 0}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument:
          m_BoolArgument: 0
        m_CallState: 2
"@
  $new = [regex]::Replace($old, '(?s)  m_OnClick:.*\z', $event+"`r`n")
  if ($old -eq $new) { throw 'Missing event' }
  $s = $s.Replace($old,$new)
  "$scene $name -> DemoPopupButtons.$method"
 }
 $s += @"
--- !u!114 &$componentId
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goId}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $Guid, type: 3}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::DemoPopupButtons

"@
 [IO.File]::WriteAllText($path,$s)
}
