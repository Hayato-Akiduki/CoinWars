param([string]$Compiler = "$PSScriptRoot\.tools\InnoSetup\ISCC.exe")
$ErrorActionPreference = 'Stop'
dotnet run --project "$PSScriptRoot\CoinWars.Checks" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Checks failed' }
$reviewedPacks = Get-Content "$PSScriptRoot/CoinWars/Licenses/reviewed-packs.json" -Raw | ConvertFrom-Json
$runtimeVersion = $reviewedPacks.'Microsoft.NETCore.App.Runtime.win-x64'
dotnet publish "$PSScriptRoot\CoinWars" -c Release -r win-x64 --self-contained true "-p:RuntimeFrameworkVersion=$runtimeVersion" -o "$PSScriptRoot\artifacts\app"
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
# Do not silently ship stale license documents when a runtime pack changes.
$releaseDeps = Get-Content "$PSScriptRoot/artifacts/app/CoinWars.deps.json" -Raw | ConvertFrom-Json
foreach ($pack in $reviewedPacks.PSObject.Properties) {
    $expected = "runtimepack.$($pack.Name)/$($pack.Value)"
    if ($expected -notin $releaseDeps.libraries.PSObject.Properties.Name) { throw "Update license documents for changed runtime pack: $($pack.Name)" }
}
foreach ($notice in Get-ChildItem "$PSScriptRoot/CoinWars/Licenses" -File) {
    $publishedNotice = Join-Path "$PSScriptRoot/artifacts/app/Licenses" $notice.Name
    if (!(Test-Path -LiteralPath $publishedNotice) -or (Get-FileHash $notice.FullName).Hash -ne (Get-FileHash $publishedNotice).Hash) { throw "License document missing or changed: $($notice.Name)" }
}
if (!(Test-Path -LiteralPath $Compiler)) { throw 'Inno Setup ISCC.exe is required. Pass -Compiler with its absolute path.' }
& $Compiler "$PSScriptRoot\installer.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer failed' }
