# Builds bin\Batereai.exe with the C# compiler that ships with .NET Framework 4.x.
# No SDK or NuGet is needed; any Windows 10/11 machine (and GitHub's windows-latest runner) has it.
param([string]$Out = (Join-Path $PSScriptRoot '..\bin'))
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Out = [IO.Path]::GetFullPath($Out)
$version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "VERSION must be MAJOR.MINOR.PATCH, got '$version'" }

$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fx 'csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc (.NET Framework 4.x is required)" }
$wpf = Join-Path $fx 'WPF'

# Stamp the version into generated copies so src\AssemblyInfo.cs and assets\app.manifest stay stable defaults.
$gen = Join-Path $Out 'obj'
New-Item -ItemType Directory -Force $Out, $gen | Out-Null
$info = Get-Content (Join-Path $root 'src\AssemblyInfo.cs') -Raw
$info = $info -replace 'Version\("[0-9.]+"\)', ('Version("' + $version + '.0")') -replace 'InformationalVersion\("[0-9.]+"\)', ('InformationalVersion("' + $version + '")')
$infoGen = Join-Path $gen 'AssemblyInfo.g.cs'
[IO.File]::WriteAllText($infoGen, $info, (New-Object Text.UTF8Encoding($true)))
$manifest = Get-Content (Join-Path $root 'assets\app.manifest') -Raw
$manifest = $manifest -replace '(<assemblyIdentity\b[^>]*\bversion=")[0-9.]+(")', ('${1}' + $version + '.0${2}')
$manifestGen = Join-Path $gen 'app.manifest'
[IO.File]::WriteAllText($manifestGen, $manifest, (New-Object Text.UTF8Encoding($false)))

$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','System.Management.dll') | ForEach-Object { "/reference:$fx\$_" }
$refs += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','UIAutomationClient.dll','UIAutomationTypes.dll','UIAutomationProvider.dll') | ForEach-Object { "/reference:$wpf\$_" }
$src = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs -Recurse | Where-Object Name -ne 'AssemblyInfo.cs' | ForEach-Object FullName) + $infoGen

& $csc /nologo /target:winexe /platform:x64 /optimize+ /langversion:5 /utf8output `
    "/win32manifest:$manifestGen" "/win32icon:$root\assets\batereai.ico" "/out:$Out\Batereai.exe" $refs $src
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force (Join-Path $Out 'fonts') | Out-Null
Copy-Item (Join-Path $root 'assets\fonts\*') (Join-Path $Out 'fonts') -Force
Copy-Item (Join-Path $root 'assets\batereai.ico') $Out -Force
Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD_PARTY_NOTICES.md') $Out -Force
Remove-Item $gen -Recurse -Force
Write-Output "Built $Out\Batereai.exe ($version)"
