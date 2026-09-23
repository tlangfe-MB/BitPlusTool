$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$repoRoot = Split-Path $root -Parent
$now = Get-Date
$minor = [int]("{0}{1:00}" -f $now.Month, $now.Day)
$build = [int]("{0:00}{1:00}" -f $now.Hour, $now.Minute)
$version = "$($now.Year).$minor.$build.0"

Write-Host "Baue BitPlusTool Version $version ..."
Push-Location $root
try {
    Get-Process BitPlusTool -ErrorAction SilentlyContinue | Stop-Process -Force
    dotnet publish -c Release -p:Version=$version -v:q
    if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen (Exit-Code $LASTEXITCODE)." }

    $publishExe = Join-Path $root "bin\Release\net10.0-windows\win-x64\publish\BitPlusTool.exe"
    $desktopExe = Join-Path $env:USERPROFILE "Desktop\BitPlusTool.exe"
    Copy-Item $publishExe $desktopExe -Force

    Write-Host "Fertig: $desktopExe"
    Write-Host "Version: $version"
}
finally {
    Pop-Location
}

# Automatischer Versionsstand: jeder Update-Lauf wird als Commit + Tag festgehalten,
# damit jederzeit auf einen frueheren Stand zurueckgestellt werden kann.
Push-Location $repoRoot
try {
    git add -A | Out-Null
    git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        git commit -m "Update BitPlusTool v$version" | Out-Null
        Write-Host "Git-Commit erstellt."
    }
    else {
        Write-Host "Keine Code-Aenderungen seit letztem Commit."
    }
    git tag -a "v$version" -m "Deployed to Desktop" 2>$null
    Write-Host "Git-Tag: v$version"
}
finally {
    Pop-Location
}

