Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(100,100)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(100,150,200))
$bmp.Save('C:\temp_test.png', [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$g.Dispose()
Write-Host 'Created C:\temp_test.png'
