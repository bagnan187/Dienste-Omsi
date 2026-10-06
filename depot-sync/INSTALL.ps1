param(
  [Parameter(Mandatory=$true)][string]$OmsiRoot
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$srcObj = Join-Path $here "Sceneryobjects\ROGIS_DepotSlots"
$dstObj = Join-Path $OmsiRoot "Sceneryobjects\ROGIS_DepotSlots"
New-Item -ItemType Directory -Force -Path $dstObj | Out-Null
Copy-Item -Recurse -Force (Join-Path $srcObj "*") $dstObj

$srcLua = Join-Path $here "plugins\ROGIS_DepotSync.lua"
$dstPlugins = Join-Path $OmsiRoot "plugins"
New-Item -ItemType Directory -Force -Path $dstPlugins | Out-Null
Copy-Item -Force $srcLua (Join-Path $dstPlugins "ROGIS_DepotSync.lua")

if (!(Test-Path (Join-Path $here "config.json"))) {
  Copy-Item (Join-Path $here "config.example.json") (Join-Path $here "config.json")
  Write-Host "config.json wurde angelegt. Bitte URL, Token, OMSI-Pfad und Mapordner eintragen." -ForegroundColor Yellow
}

Write-Host "ROGIS Depot Slots + openOMSI Plugin installiert." -ForegroundColor Green
Write-Host "Naechster Schritt: Slotobjekte im Editor setzen und z.B. M001 / S001 / H001 beschriften."
