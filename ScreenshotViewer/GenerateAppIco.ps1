Add-Type -AssemblyName System.Drawing;

# Custom function to draw a rounded rectangle
function Draw-RoundedRectangle {
    param (
        [System.Drawing.Graphics]$graphics,
        [System.Drawing.Pen]$pen,
        [int]$x,
        [int]$y,
        [int]$width,
        [int]$height,
        [int]$radius
    )
    $graphics.DrawArc($pen, $x, $y, $radius * 2, $radius * 2, 180, 90)
    $graphics.DrawArc($pen, $x + $width - ($radius * 2), $y, $radius * 2, $radius * 2, 270, 90)
    $graphics.DrawArc($pen, $x + $width - ($radius * 2), $y + $height - ($radius * 2), $radius * 2, $radius * 2, 0, 90)
    $graphics.DrawArc($pen, $x, $y + $height - ($radius * 2), $radius * 2, $radius * 2, 90, 90)
    $graphics.DrawLine($pen, $x + $radius, $y, $x + $width - $radius, $y)
    $graphics.DrawLine($pen, $x + $width, $y + $radius, $x + $width, $y + $height - $radius)
    $graphics.DrawLine($pen, $x + $radius, $y + $height, $x + $width - $radius, $y + $height)
    $graphics.DrawLine($pen, $x, $y + $radius, $x, $y + $height - $radius)
}


$bmp = New-Object System.Drawing.Bitmap(256, 256)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.Color]::FromArgb(64, 64, 64))
$whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$barWidth = 15
$barSpacing = 8

for ($i = 0; $i -lt 7; $i++) {
    $x = 20 + ($i * ($barWidth + $barSpacing))
    # Vary the opacity of the rectangle between 50% and 100% using a repeatable random pattern
    $opacity = [int](128 + ($i * 25) % 128)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($opacity, 255, 255, 255))
    $g.FillRectangle($brush, $x, 60, $barWidth, 140)
}

$yellowX = (7 * ($barWidth + $barSpacing))

$yellowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(96, 0, 0, 0))
$g.FillRectangle($yellowBrush, ($yellowX - 10), 75, 50, 110)

$yellowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 0))
$pen = New-Object System.Drawing.Pen($yellowBrush, 15)

# Call the custom function to draw the rounded rectangle
Draw-RoundedRectangle $g $pen ($yellowX - 10) 75 50 110 10

$font = New-Object System.Drawing.Font('Arial', 28, [System.Drawing.FontStyle]::Bold)
$timeText = '09:00'
$textSize = $g.MeasureString($timeText, $font)
$textX = (256 - $textSize.Width) / 2
$textY = 210
$g.DrawString($timeText, $font, $whiteBrush, $textX, $textY)

$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$fs = [System.IO.File]::OpenWrite('app.ico')
$icon.Save($fs)
$fs.Close()

$g.Dispose()
$bmp.Dispose()

Write-Host 'Icon created successfully'
