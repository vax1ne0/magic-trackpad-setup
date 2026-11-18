# Reuse the application icon for native Windows Installer artwork.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$icon = [Drawing.Icon]::new((Join-Path $PSScriptRoot '../panel/Resources/MagicTrackpad.ico'), 128, 128)
try {
    foreach ($kind in @('Banner', 'Dialog')) {
        $width = 493
        $height = if ($kind -eq 'Banner') { 58 } else { 312 }
        $bitmap = [Drawing.Bitmap]::new($width, $height)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([Drawing.Color]::White)
            if ($kind -eq 'Banner') {
                $graphics.DrawIcon($icon, [Drawing.Rectangle]::new(440, 7, 44, 44))
            } else {
                $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(241, 245, 250))
                try { $graphics.FillRectangle($brush, 0, 0, 164, 312) } finally { $brush.Dispose() }
                $graphics.DrawIcon($icon, [Drawing.Rectangle]::new(40, 40, 84, 84))
            }
            $bitmap.Save((Join-Path $PSScriptRoot "../payload/Setup$kind.bmp"), [Drawing.Imaging.ImageFormat]::Bmp)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $icon.Dispose() }
