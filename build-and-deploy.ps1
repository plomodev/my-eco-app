# Script de build et de déploiement automatique de GameEcoBot
Set-Location $PSScriptRoot

Write-Host "Arrêt des processus GameEcoBot en cours si nécessaire..."
Get-Process -Name "GameEcoBot" -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Compilation et publication du nouvel exécutable autonome..."
dotnet publish "$PSScriptRoot\GameEcoBot.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "$PSScriptRoot\bin\PublishRelease"

Write-Host "Mise à jour du raccourci sur le Bureau..."
$sh = New-Object -ComObject WScript.Shell
$desktopOk = "C:\Users\HOME\Desktop\OK"
if (-not (Test-Path $desktopOk)) { New-Item -ItemType Directory -Path $desktopOk -Force | Out-Null }

$sc = $sh.CreateShortcut('C:\Users\HOME\Desktop\OK\GameEcoBot - Raccourci.lnk')
$sc.TargetPath = 'C:\Users\HOME\Desktop\OK\GameEcoBot.exe'
$sc.WorkingDirectory = 'C:\Users\HOME\Desktop\OK'
$sc.Description = 'GameEcoBot - Optimiseur et Mode Eco'
$sc.Save()

# Copier également le fichier presets json à côté de l'exécutable
if (Test-Path "$PSScriptRoot\crosshair_presets.json") {
    Copy-Item "$PSScriptRoot\crosshair_presets.json" "$PSScriptRoot\bin\PublishRelease\crosshair_presets.json" -Force -ErrorAction SilentlyContinue
    Copy-Item "$PSScriptRoot\crosshair_presets.json" "$desktopOk\crosshair_presets.json" -Force -ErrorAction SilentlyContinue
}

Write-Host "Déploiement terminé avec succès ! Exécutables à jour :"
Get-Item 'C:\Users\HOME\Desktop\my-eco-app-main\GameEcoBot.exe' -ErrorAction SilentlyContinue | Select-Object FullName, Length, LastWriteTime
Get-Item 'C:\Users\HOME\Desktop\OK\GameEcoBot.exe' -ErrorAction SilentlyContinue | Select-Object FullName, Length, LastWriteTime
