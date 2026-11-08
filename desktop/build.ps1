param([string]$Compiler = "$PSScriptRoot\.tools\InnoSetup\ISCC.exe")
$ErrorActionPreference = 'Stop'
dotnet run --project "$PSScriptRoot\CoinWars.Checks" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Checks failed' }
dotnet publish "$PSScriptRoot\CoinWars" -c Release -r win-x64 --self-contained true -o "$PSScriptRoot\artifacts\app"
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
if (!(Test-Path -LiteralPath $Compiler)) { throw 'Inno Setup ISCC.exe is required. Pass -Compiler with its absolute path.' }
& $Compiler "$PSScriptRoot\installer.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer failed' }
