#Requires -Version 5.1
<#
.SYNOPSIS
    انتشار نسخه جدید برنامه روی GitHub Releases (سیستم به‌روزرسانی خودکار).

.DESCRIPTION
    این اسکریپت:
      1. نسخه در csproj را افزایش می‌دهد (ApplicationDisplayVersion و ApplicationVersion)
      2. APK امضاشده Release را می‌سازد
      3. یک Release در GitHub با تگ v{نسخه} می‌سازد و APK را آپلود می‌کند

    روش‌های انتشار Release:
      - اگر gh CLI نصب و لاگین باشد: خودکار (پیشنهادی)
      - در غیر این صورت: با پارامتر -Token (یک GitHub Personal Access Token با دسترسی repo)

.EXAMPLE
    .\scripts\publish-update.ps1 -Version 1.1.1 -Notes "رفع مشکل جستجوی قبض"

.EXAMPLE
    .\scripts\publish-update.ps1 -Version 1.2.0 -Notes "ویژگی جدید" -Token ghp_xxxxx

.EXAMPLE
    .\scripts\publish-update.ps1 -Version 1.2.1 -SkipBuild   # فقط انتشار (APK قبلاً ساخته شده)
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Notes = "",
    [string]$Owner = "JaberAsadiM",
    [string]$Repo = "BillPayment",
    [string]$Token = "",
    [switch]$SkipVersionBump,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Csproj = Join-Path $Root "src\Amard.App\Amard.App.csproj"

# ---------- اعتبارسنجی نسخه ----------
if ($Version -notmatch '^\d+\.\d+(\.\d+)?$') {
    throw "نسخه باید به شکل «1.2.3» یا «1.2» باشد (مثال: -Version 1.1.1)"
}
$parts = $Version.Split('.') | ForEach-Object { [int]$_ }
while ($parts.Count -lt 3) { $parts += 0 }
$versionCode = $parts[0] * 10000 + $parts[1] * 100 + $parts[2]

Write-Host "==> نسخه: $Version  (VersionCode: $versionCode)" -ForegroundColor Cyan

# ---------- 1) افزایش نسخه در csproj ----------
if (-not $SkipVersionBump) {
    $content = Get-Content $Csproj -Raw -Encoding UTF8
    $content = $content -replace '<ApplicationDisplayVersion>[^<]*</ApplicationDisplayVersion>', "<ApplicationDisplayVersion>$Version</ApplicationDisplayVersion>"
    $content = $content -replace '<ApplicationVersion>\d+</ApplicationVersion>', "<ApplicationVersion>$versionCode</ApplicationVersion>"
    [System.IO.File]::WriteAllText($Csproj, $content, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "==> نسخه در csproj به‌روزرسانی شد" -ForegroundColor Green
}

# ---------- 2) بیلد APK امضاشده ----------
if (-not $SkipBuild) {
    Write-Host "==> در حال بیلد Release (چند دقیقه طول می‌کشد)..." -ForegroundColor Cyan
    Push-Location $Root
    dotnet publish $Csproj -c Release -f net10.0-android
    $publishExit = $LASTEXITCODE
    Pop-Location
    if ($publishExit -ne 0) { throw "بیلد ناموفق بود." }
}

# ---------- 3) یافتن APK ساخته‌شده ----------
$publishDir = Join-Path $Root "src\Amard.App\bin\Release\net10.0-android\publish"
$apk = Get-ChildItem $publishDir -Filter *.apk | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) { throw "فایل APK در $publishDir یافت نشد." }

$releaseDir = Join-Path $Root "releases"
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
$targetApk = Join-Path $releaseDir "Amard-v$Version.apk"
Copy-Item $apk.FullName $targetApk -Force
Write-Host "==> APK کپی شد: $targetApk" -ForegroundColor Green

# ---------- 4) ساخت Release در GitHub ----------
$tag = "v$Version"
$title = "نسخه $Version"
if (-not $Notes) { $Notes = "نسخه $Version" }

# سازگاری TLS با PowerShell 5.1
if ($PSVersionTable.PSVersion.Major -lt 6) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}

$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($gh) {
    Write-Host "==> انتشار با gh CLI (تگ $tag)..." -ForegroundColor Cyan
    Push-Location $Root
    gh release create $tag $targetApk --title $title --notes $Notes
    $ghExit = $LASTEXITCODE
    Pop-Location
    if ($ghExit -ne 0) { throw "انتشار با gh ناموفق بود." }
}
else {
    if (-not $Token) {
        Write-Warning "gh CLI نصب نیست."
        throw "یا gh CLI را نصب و با 'gh auth login' وارد شوید، یا با -Token یک Personal Access Token بدهید."
    }

    Write-Host "==> انتشار با GitHub API (تگ $tag)..." -ForegroundColor Cyan
    $headers = @{
        Authorization = "Bearer $Token"
        Accept        = "application/vnd.github+json"
    }

    $body = @{ tag_name = $tag; name = $title; body = $Notes; draft = $false; prerelease = $false } | ConvertTo-Json
    $release = Invoke-RestMethod -Method Post `
        -Uri "https://api.github.com/repos/$Owner/$Repo/releases" `
        -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) `
        -ContentType "application/json; charset=utf-8"

    $uploadUrl = $release.upload_url -replace '\{\?name,label\}', "?name=$([uri]::EscapeDataString((Split-Path $targetApk -Leaf)))"
    $apkBytes = [System.IO.File]::ReadAllBytes($targetApk)
    Invoke-RestMethod -Method Post -Uri $uploadUrl -Headers $headers `
        -ContentType "application/vnd.android.package-archive" -Body $apkBytes | Out-Null

    Write-Host "==> Release منتشر شد: $($release.html_url)" -ForegroundColor Green
}

Write-Host ""
Write-Host "✅ نسخه $Version منتشر شد. دستگاه‌ها هنگام شروع برنامه یا ورود به تنظیمات، آن را دریافت خواهند کرد." -ForegroundColor Green
