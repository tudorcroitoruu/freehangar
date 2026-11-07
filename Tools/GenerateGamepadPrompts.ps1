# Rebuild the runtime TMP atlas from the game's authored icons. Keep this order
# aligned with GamepadPromptSprites.Names. LT-1 is the authored RT icon.
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path $PSScriptRoot -Parent
$names = @('Y','A','X','B','menu','view','LB','RB','LT','LT-1','L3','R3','LeftStick','RightStick','dPad_All','dPad_Up','dPad_Down','dPad_Left','dPad_Right','Home')
$atlas = New-Object System.Drawing.Bitmap(1024,384)
$graphics = [System.Drawing.Graphics]::FromImage($atlas)
try {
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    for ($i=0; $i -lt $names.Count; $i++) {
        $source = [System.Drawing.Image]::FromFile((Join-Path $projectRoot ('Assets/_UI/Joystick_Buttons/' + $names[$i] + '.png')))
        try {
            $scale = 120.0 / [Math]::Max($source.Width,$source.Height)
            $width = [int]($source.Width*$scale); $height = [int]($source.Height*$scale)
            $x = ($i%8)*128 + [int]((128-$width)/2)
            $y = [int][Math]::Floor($i/8)*128 + [int]((128-$height)/2)
            $graphics.DrawImage($source,$x,$y,$width,$height)
        } finally { $source.Dispose() }
    }
    $atlas.Save((Join-Path $projectRoot 'Assets/Resources/GamepadPrompts.png'),[System.Drawing.Imaging.ImageFormat]::Png)
} finally { $graphics.Dispose(); $atlas.Dispose() }
