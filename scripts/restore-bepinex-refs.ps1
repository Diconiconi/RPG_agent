$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$zipPath = Join-Path $repoRoot ".tools\bepinex\BepInEx_x64_5.4.17.0.zip"
$extractPath = Join-Path $repoRoot ".tools\bepinex\5.4.17"
$downloadUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.17/BepInEx_x64_5.4.17.0.zip"

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $zipPath) | Out-Null

if (-not (Test-Path -LiteralPath $zipPath)) {
    Write-Host "Downloading BepInEx 5.4.17 compile refs..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath
}

if (Test-Path -LiteralPath $extractPath) {
    $resolvedExtractPath = (Resolve-Path -LiteralPath $extractPath).Path
    if (-not $resolvedExtractPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe extract path: $resolvedExtractPath"
    }

    Remove-Item -LiteralPath $resolvedExtractPath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $extractPath | Out-Null
Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force

$bepInExDll = Join-Path $extractPath "BepInEx\core\BepInEx.dll"
$harmonyDll = Join-Path $extractPath "BepInEx\core\0Harmony.dll"

if (-not (Test-Path -LiteralPath $bepInExDll)) {
    throw "Missing BepInEx.dll after extraction."
}

if (-not (Test-Path -LiteralPath $harmonyDll)) {
    throw "Missing 0Harmony.dll after extraction."
}

$bepInExVersion = [System.Reflection.AssemblyName]::GetAssemblyName($bepInExDll).Version.ToString()
$harmonyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($harmonyDll).Version.ToString()

if ($bepInExVersion -ne "5.4.17.0") {
    throw "Unexpected BepInEx version: $bepInExVersion"
}

if ($harmonyVersion -ne "2.5.5.0") {
    throw "Unexpected 0Harmony version: $harmonyVersion"
}

Write-Host "BepInEx compile refs ready: BepInEx $bepInExVersion, 0Harmony $harmonyVersion"
