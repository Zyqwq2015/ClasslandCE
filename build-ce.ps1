<#
.SYNOPSIS
    Classland CE 构建脚本 — 一键打包自包含便携版（免装 .NET 运行时）
.DESCRIPTION
    用法：
        ./build-ce.ps1                       # 标准版（需要 .NET 8 运行时）
        ./build-ce.ps1 -SelfContained         # 自包含版（免装运行时，体积 ~200MB）
        ./build-ce.ps1 -SelfContained -Arch x86   # x86 自包含版
        ./build-ce.ps1 -SelfContained -Arch arm64 # ARM64 自包含版
#>

param(
    [switch]$SelfContained,
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Arch = "x64",
    [string]$Configuration = "Release",
    [string]$OutputDir = "./out/ce"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$SolutionPath = Join-Path $ProjectRoot "ClassIsland.sln"
$ProjectPath = Join-Path $ProjectRoot "ClassIsland/ClassIsland.csproj"

Write-Host "=== Classland CE 构建脚本 ===" -ForegroundColor Cyan
Write-Host "项目根目录: $ProjectRoot"
Write-Host "配置: $Configuration"
Write-Host "架构: $Arch"
Write-Host "自包含: $SelfContained"
Write-Host "输出目录: $OutputDir"
Write-Host ""

# 检查 dotnet
$dotnet = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Error "未找到 dotnet CLI，请先安装 .NET 8 SDK。"
    exit 1
}

# MSBuild 版本检测
$sdkVersion = dotnet --version 2>$null
Write-Host "dotnet SDK 版本: $sdkVersion" -ForegroundColor Green

# 构建参数
$publishParams = @(
    "publish",
    $ProjectPath,
    "-c", $Configuration,
    "-o", $OutputDir,
    "--self-contained", $SelfContained.ToString().ToLower()
)

if ($SelfContained) {
    $runtimeId = switch ($Arch) {
        "x64"   { "win-x64" }
        "x86"   { "win-x86" }
        "arm64" { "win-arm64" }
    }
    $publishParams += "-r", $runtimeId
    $publishParams += "-p:ClassIsland_SelfContained=true"
    $outputName = "ClasslandCE_${Arch}_selfcontained"
} else {
    $publishParams += "--no-self-contained"
    $publishParams += "-p:ClassIsland_SelfContained=false"
    $outputName = "ClasslandCE_${Arch}_runtime"
}

# 添加 CE 版本常量
$publishParams += "-p:DefineConstants=CE_VERSION"

Write-Host "构建参数: $($publishParams -join ' ')" -ForegroundColor Yellow
Write-Host ""

# 开始构建
Write-Host "开始构建..." -ForegroundColor Cyan
& $dotnet $publishParams

if ($LASTEXITCODE -ne 0) {
    Write-Error "构建失败，退出代码: $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "构建成功！" -ForegroundColor Green
Write-Host "输出目录: $OutputDir" -ForegroundColor Green

# 打包为 zip
$zipPath = Join-Path $ProjectRoot "out/${outputName}.zip"
if (Get-Command "Compress-Archive" -ErrorAction SilentlyContinue) {
    Write-Host "正在打包为 ZIP: $zipPath" -ForegroundColor Cyan
    Compress-Archive -Path "$OutputDir/*" -DestinationPath $zipPath -Force
    Write-Host "打包完成: $zipPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== 构建完成 ===" -ForegroundColor Cyan
Write-Host "便携版使用方式：解压后直接运行 ClassIsland.exe"
Write-Host "（无需安装 .NET 运行时）"