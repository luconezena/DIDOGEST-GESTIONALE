$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$uiProj = Join-Path $repoRoot 'DidoGest.UI\DidoGest.UI.csproj'
$outApp = Join-Path $repoRoot 'portable\app'
$outRoot = Join-Path $repoRoot 'portable'
$distRoot = Join-Path $repoRoot 'portable\dist'
$distDir = Join-Path $distRoot 'DidoGest-Portable'
$zipPath = Join-Path $distRoot 'DidoGest-Portable.zip'

dotnet publish $uiProj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o $outApp

if (Test-Path $distDir) { Remove-Item -Recurse -Force $distDir }
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Copy-Item -Recurse -Force (Join-Path $outApp '*') $distDir

# Comodita': EXE e HELP.md anche in portable\ (root)
Copy-Item -Force (Join-Path $distDir 'DidoGest.exe') (Join-Path $outRoot 'DidoGest.exe')
if (Test-Path (Join-Path $distDir 'HELP.md'))
{
    Copy-Item -Force (Join-Path $distDir 'HELP.md') (Join-Path $outRoot 'HELP.md')
}

if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $distDir '*') -DestinationPath $zipPath -Force

Write-Host "OK dist: $distDir"
Write-Host "OK zip : $zipPath"
Write-Host "OK root exe :" (Test-Path (Join-Path $outRoot 'DidoGest.exe'))
Write-Host "OK root help:" (Test-Path (Join-Path $outRoot 'HELP.md'))
