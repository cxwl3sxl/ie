$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sln = Join-Path $root "IEBrowser.sln"

# 优先使用 Visual Studio 自带的 MSBuild
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = $null

if (Test-Path $vswhere) {
    $install = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if ($install) {
        $candidate = Join-Path $install "MSBuild\Current\Bin\MSBuild.exe"
        if (Test-Path $candidate) { $msbuild = $candidate }
    }
}

if (-not $msbuild) {
    $candidate = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
    if (Test-Path $candidate) { $msbuild = $candidate }
}

if (-not $msbuild) {
    throw "未找到 MSBuild，请安装 Visual Studio 或 .NET Framework SDK。"
}

Write-Host "使用 MSBuild: $msbuild"
& $msbuild $sln /p:Configuration=Release /v:minimal /nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "编译完成: $root\IEBrowser\bin\Release\IEBrowser.exe" -ForegroundColor Green
}
