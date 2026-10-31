param([string]$Command = 'script run', [string]$File, [string]$Parameters = '{}', [string]$ParametersFile)
$connection = Get-Content 'Library/Pipeline/.unity-pipeline-port' -Raw | ConvertFrom-Json
$arguments = if ($ParametersFile) { Get-Content $ParametersFile -Raw | ConvertFrom-Json } else { $Parameters | ConvertFrom-Json }
if ($File) { $arguments = @{file=$File} }
$body = @{command=$Command; parameters=$arguments; timeout=60000} | ConvertTo-Json -Depth 16
Invoke-RestMethod "http://127.0.0.1:$($connection.port)/api/exec" -Method Post -Headers @{Authorization='Bearer '+$connection.evalToken} -ContentType 'application/json' -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 65 | ConvertTo-Json -Depth 24
