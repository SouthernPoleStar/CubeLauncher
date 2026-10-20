$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "找不到 .NET Framework 4.0 编译器: $csc" }

$dist = Join-Path $root 'dist'
$release = Join-Path $root 'release'
$research = Join-Path $root '_research'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force $dist, $release | Out-Null

$sources = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter '*.cs' | ForEach-Object { $_.FullName }
$references = @(
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Web.Extensions.dll',
    '/r:System.Security.dll',
    '/r:System.IO.Compression.dll',
    '/r:System.IO.Compression.FileSystem.dll',
    '/r:System.Xml.dll',
    '/r:System.Xml.Linq.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll'
)

$outExe = Join-Path $dist 'CubeLauncher.exe'
Write-Host "使用 $csc"
Write-Host "编译 $($sources.Count) 个源文件..."

& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    "/out:$outExe" `
    "/win32icon:$(Join-Path $root 'assets\app.ico')" `
    "/win32manifest:$(Join-Path $root 'src\app.manifest')" `
    $references $sources

if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }

Copy-Item (Join-Path $root 'README.md') $dist -Force
Copy-Item (Join-Path $root 'STATUS.md') $dist -Force
Copy-Item (Join-Path $root 'LICENSE') $dist -Force
New-Item -ItemType Directory -Force (Join-Path $dist 'assets') | Out-Null
Copy-Item (Join-Path $root 'assets\icon-preview.png') (Join-Path $dist 'assets') -Force
Copy-Item (Join-Path $root 'assets\screenshot-home.png') (Join-Path $dist 'assets') -Force
Copy-Item (Join-Path $root 'assets\screenshot-mods.png') (Join-Path $dist 'assets') -Force
Copy-Item (Join-Path $root 'assets\screenshot-settings.png') (Join-Path $dist 'assets') -Force
New-Item -ItemType Directory -Force (Join-Path $dist 'assets\fonts') | Out-Null
Copy-Item (Join-Path $root 'assets\fonts\ShangShouBiaoTiSong.ttf') (Join-Path $dist 'assets\fonts') -Force

$zip = Join-Path $release 'CubeLauncher-1.0.0-win64.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zip -CompressionLevel Optimal

Write-Host ""
Write-Host "构建完成:"
Write-Host "  $outExe"
Write-Host "  $zip"
Write-Host ("  EXE 大小: {0:N0} 字节" -f (Get-Item $outExe).Length)
