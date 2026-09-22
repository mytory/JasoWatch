param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\release\msix")
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$sdkBin = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.18362.0\x64"
$makeAppx = Join-Path $sdkBin "MakeAppx.exe"
if (-not (Test-Path -LiteralPath $makeAppx)) { throw "Windows SDK MakeAppx.exe를 찾을 수 없습니다." }

$layout = Join-Path $OutputDirectory "layout"
if (Test-Path -LiteralPath $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $layout "assets") -Force | Out-Null

& dotnet publish (Join-Path $root "JasoWatch.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $layout
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "AppxManifest.xml") -Destination (Join-Path $layout "AppxManifest.xml")
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "assets\Square150x150Logo.png") -Destination (Join-Path $layout "assets\Square150x150Logo.png")
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "assets\Square44x44Logo.png") -Destination (Join-Path $layout "assets\Square44x44Logo.png")
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "assets\StoreLogo.png") -Destination (Join-Path $layout "assets\StoreLogo.png")

$package = Join-Path $OutputDirectory "Mytory-Jaso-Watch-1.0.0-x64.msix"
& $makeAppx pack /o /d $layout /p $package
if ($LASTEXITCODE -ne 0) { throw "MSIX 패키지 생성에 실패했습니다." }
Write-Output $package
