# Script build & dong goi tu dong cho LMScreenShot
$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " 1. BAT DAU BUILD BAN PORTABLE SINGLE-FILE " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$publishDir = "publish\portable"
$outputZip = "publish\LMScreenShot_v1.0.0_Portable.zip"

dotnet publish src\LMScreenShot\LMScreenShot.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $publishDir

Write-Host "[OK] Build Single-File thanh cong tai $publishDir\LMScreenShot.exe" -ForegroundColor Green

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " 2. TAO FILE NEN PORTABLE .ZIP            " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

Compress-Archive -Path "$publishDir\LMScreenShot.exe" -DestinationPath $outputZip -Force
Write-Host "[OK] Da tao file zip tai $outputZip" -ForegroundColor Green

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " 3. DONG GOI INSTALLER SETUP.EXE          " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$innoPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$iscc = $null
foreach ($p in $innoPaths) {
    if (Test-Path $p) {
        $iscc = $p
        break
    }
}

if ($iscc) {
    & $iscc "installer.iss"
    Write-Host "[OK] Tao Installer Setup.exe thanh cong tai publish\installer\LMScreenShot_Setup_v1.0.0.exe" -ForegroundColor Green
} else {
    Write-Host "[NOTE] Inno Setup chua duoc cai dat. Ban co the tai mien phi tai https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "Sau khi cai Inno Setup, chi can bam chuot phai vao file 'installer.iss' chon 'Compile' la xong!" -ForegroundColor Yellow
}

Write-Host "==========================================" -ForegroundColor Green
Write-Host " HOAN TAT DONG GOI BAN PHAT HANH!         " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
