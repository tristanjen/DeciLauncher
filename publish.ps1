# DeciLauncher 发布脚本
# 用法: .\publish.ps1 [-Version <version>] [-Configuration <Release|Debug>]
param(
    [string]$version = "1.0.0-beta.1",
    [string]$config = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$rids = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
$releaseDir = Join-Path $root "Release"

Write-Host "=== DeciLauncher 发布构建 v$version ===" -ForegroundColor Cyan
Write-Host ""

# 1. 前端构建
Write-Host "[1/3] 构建前端..." -ForegroundColor Yellow
$uiDir = Join-Path $root "UserInterface"
Push-Location $uiDir
try {
    pnpm install --frozen-lockfile
    pnpm build
    if ($LASTEXITCODE -ne 0) { throw "前端构建失败" }
}
finally {
    Pop-Location
}
Write-Host "      前端构建完成 ✓" -ForegroundColor Green

# 2. dotnet publish
Write-Host ""
Write-Host "[2/3] 发布后端..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

foreach ($rid in $rids) {
    $publishDir = Join-Path $root "bin\$config\net10.0\$rid\publish"

    if ($rid -match "^(linux|osx)" -and -not $IsLinux -and -not $IsMacOS) {
        Write-Host "      跳过 $rid（需要对应平台的原生库，请在目标系统上构建）" -ForegroundColor DarkYellow
        continue
    }

    Write-Host "      构建 $rid..." -ForegroundColor White
    dotnet publish -c $config -r $rid -p:Version=$version
    if ($LASTEXITCODE -ne 0) { throw "发布 $rid 失败" }

    Write-Host "      已输出到: $publishDir" -ForegroundColor Gray
}

# 3. 打包 ZIP
Write-Host ""
Write-Host "[3/3] 打包 ZIP..." -ForegroundColor Yellow

foreach ($rid in $rids) {
    $publishDir = Join-Path $root "bin\$config\net10.0\$rid\publish"
    if (-not (Test-Path $publishDir)) { continue }

    $zipName = "DeciLauncher-$version-$rid.zip"
    $zipPath = Join-Path $releaseDir $zipName

    Write-Host "      打包 $zipName..." -ForegroundColor White
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath
    $size = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host "      $zipName ($size MB) ✓" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== 发布完成 ===" -ForegroundColor Cyan
Write-Host "ZIP 文件已输出到: $releaseDir" -ForegroundColor White
