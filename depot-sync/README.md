# ROGIS Depot Sync + Static-Busse

Dieser Stand ist fuer den bestehenden ROGIS Live Client gedacht. Die Wagennummer ist ueberall derselbe Schluessel.

## Was du im OMSI-Editor setzt

Es gibt nur vier Stellplatzobjekte:

- `ROGIS_DepotSlot_DS.sco` = Diesel Solo, 12,5 m
- `ROGIS_DepotSlot_DG.sco` = Diesel Gelenk, 18,5 m
- `ROGIS_DepotSlot_ES.sco` = Elektro Solo, 12,5 m
- `ROGIS_DepotSlot_EG.sco` = Elektro Gelenk, 18,5 m

Im Editor unter **Beschriftung** bekommt jede Instanz ihre eindeutige Nummer:
`M001`..., `S001`..., `H001`...

Der Hof wird aus M/S/H erkannt. Die Position und Ausrichtung kommen direkt aus der .map.

## Static-Busse

Standardname:
`Sceneryobjects\\ROGISstatic\\ROGIS_<Wagennummer>.sco`

Beispiel:
`KOM 1516 -> ROGIS_1516.sco`

Die Originaldateien werden NICHT veraendert. Beim Start erzeugt der Client kleine Runtime-SCOs im selben Ordner, die auf dieselben vorhandenen Meshes verweisen.

- IDLE: Wagen ohne Umlauf bleibt am Hof sichtbar.
- START: Wagen ist am Abholstellplatz sichtbar bis zur Abfahrtszeit.
- END: Wagen wird am Rueckgabestellplatz ab der Einrueckzeit sichtbar.

Dafuer wird automatisch `script\\ROGIS_DepotVisibility.osc` erzeugt und per `[visible]` an die Runtime-SCO gekoppelt.

Vor der ersten Map-Aenderung legt der Client fuer jede betroffene Kachel eine `.rogis.bak`-Sicherung an.

## Verbindung zur Website

- Slots: POST `/api/openomsi/depot-slots`
- Tagesbelegung: GET `/api/openomsi/depot-day?date=YYYY-MM-DD`
- gleicher Schluessel wie bei KI-Zuordnung: `vehicleNumber`

Die Website liefert damit z. B.:
`1516 -> M037 -> Umlauf -> M114`

## Installation

1. Den Inhalt von `Sceneryobjects/ROGIS_DepotSlots` nach `<OMSI>\\Sceneryobjects\\ROGIS_DepotSlots` kopieren.
2. `plugins/ROGIS_DepotSync.lua` nach `<OMSI>\\plugins` kopieren.
3. `config.example.json` als `config.json` kopieren und URL, Token, OMSI-Pfad und Mapordner eintragen.
4. Sicherstellen, dass deine Static-Busse als `ROGIS_<Wagennummer>.sco` im konfigurierten Static-Ordner liegen.
5. Im Editor die vier Slottypen setzen und beschriften.
6. Depot-Sync/Live-Client starten, danach openOMSI starten.

Wichtig: Die Runtime-Hofbelegung wird beim Clientstart erzeugt. Aendert sich der Tagesplan grundlegend, Live-Client/openOMSI einmal neu starten, damit die Mapobjekte neu aufgebaut werden.
