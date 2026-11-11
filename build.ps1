param(
    [ValidateSet("x86", "x64", "any", "all")]
    [string]$Platform = "all",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

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

# Any CPU 在项目中仍固定为 x86；x86/x64 为显式平台。
$platforms = switch ($Platform) {
    "x86" { @("x86") }
    "x64" { @("x64") }
    "any" { @("Any CPU") }
    "all" { @("x86", "x64") }
}

Write-Host "使用 MSBuild: $msbuild"
Write-Host "配置: $Configuration  平台: $($platforms -join ', ')"

$failed = @()
foreach ($p in $platforms) {
    Write-Host ""
    Write-Host ">>> 编译 $Configuration|$p" -ForegroundColor Cyan
    & $msbuild $sln /t:Rebuild /p:Configuration=$Configuration "/p:Platform=$p" /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { $failed += $p }
}

# 按平台报告输出路径
Write-Host ""
$map = @{ "Any CPU" = "bin\Release"; "x86" = "bin\x86"; "x64" = "bin\x64" }
if ($Configuration -eq "Debug") {
    $map = @{ "Any CPU" = "bin\Debug"; "x86" = "bin\x86"; "x64" = "bin\x64" }
}

foreach ($p in $platforms) {
    $exe = Join-Path $root ("IEBrowser\" + $map[$p] + "\IEBrowser.exe")
    if ($failed -notcontains $p -and (Test-Path $exe)) {
        $ver = "未知位数"
        try {
            $bytes = [System.IO.File]::ReadAllBytes($exe)
            $pe = [BitConverter]::ToInt32($bytes, 0x3C)
            $machine = [BitConverter]::ToUInt16($bytes, $pe + 4)
            $ver = switch ($machine) {
                0x014c { "32 位 (x86)" }
                0x8664 { "64 位 (x64)" }
                default { "类型 0x{0:X}" -f $machine }
            }
        } catch { }
        Write-Host "编译完成 [$p / $ver]: $exe" -ForegroundColor Green
    }
}

if ($failed.Count -gt 0) {
    throw "编译失败: $($failed -join ', ')"
}
