# ROGIS Depot Sync

Der Client verbindet die im OMSI-Editor gesetzten ROGIS-Stellplatzmarker mit der ROGIS-Dienstplan-API.

## Ablauf

1. Markerobjekte `ROGIS_DepotSlot_M_DS.sco`, `...M_DG`, `...M_ES`, `...M_EG` sowie entsprechend `S` und `H` im Editor setzen.
2. Client scannt die .map-Dateien und vergibt stabile IDs M001..., S001..., H001....
3. Die Slots werden an `/api/openomsi/depot-slots` synchronisiert.
4. Die Tagesbelegung wird aus `/api/openomsi/depot-day` gelesen und lokal als `depot-day.json` gespeichert.
5. Das openOMSI-Lua-Plugin meldet lokal Spielzustand/Dienstwechsel per UDP und stößt eine Aktualisierung an.

Die Static-Bus-Erzeugung kommt als nächster Baustein und nutzt dieselbe `depot-day.json`.

## Static-Bus-Verknüpfung

Die Wagennummer ist der gemeinsame Schlüssel. `static-models.json` ordnet z. B. `1516` einer Static-.sco zu. Die Tagesbelegung der Website liefert dieselbe `vehicleNumber`, sodass der Client daraus automatisch Wagen + Stellplatz + Modell zusammensetzt.

Beispiel:

```json
{"1516":"Sceneryobjects\\\\ROGISstatic\\\\1516.sco"}
```
