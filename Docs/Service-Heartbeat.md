# Service-Heartbeat auf `status/`

Warum die .NET- und Python-Dienste dasselbe Heartbeat-Format senden wie die ESP32-Geräte,
welche Felder sie füllen und welche nicht, und warum ihr Alter anders gemessen wird.

Verbindlich für jeden neuen Dienst. Die Topic-Regel selbst steht in
`MQTT-Topic-Konvention.md`, Abschnitt „Ausnahme `status/`".

## Das Problem

Auf `status/#` lagen 17 ESP32-Geräte und kein einziger Dienst. Ein toter Connector war auf
MQTT unsichtbar: Der Ausfall des BMWConnectors blieb 35 Tage unbemerkt (siehe
`Archiv/Backlog-Laden-2026-09.md`, Punkt 1/2), und die Geräteseite der Weboberfläche wusste nicht einmal,
dass es den Dienst gibt. Ein Dienst, der schweigt, muss genauso auffallen wie ein Sensor,
der schweigt — und zwar an derselben Stelle.

## Topic

```
status/Cluster/Dienst/<Name>
```

Das ist das **Bestandsformat der Geräte**, `status/<Ort>/<Geraetetyp>/<Name>`, nicht der
Schnitt der Topic-Konvention. Die Begründung steht dort: Im `status`-Namensraum sollen nicht
zwei Formate nebeneinanderstehen, und die Migration lohnt nur gemeinsam mit der Flotte.

- **Ort `Cluster`** — die Dienste sind ortslos. Die Ebene lässt sich trotzdem nicht
  weglassen: Das Bestandsformat hat feste Tiefe, und die Geräteseite gruppiert danach.
- **Geraetetyp `Dienst`** — für alle gleich, damit die Dienste auf der Geräteseite **eine**
  Gruppe bilden statt sechs Gruppen mit je einer Karte.
- **Name** — der Dienstname, wie er im Repo heißt. Alle acht aus diesem Repo deployten
  Dienste senden: `BMWConnector`, `VWConnector`, `KebaConnector`, `EnphaseConnector`,
  `ShellyConnector`, `ChargingController`, `DataHub`, `RulesEngine`. (`SmartHome.Web` sendet
  nicht — es ist der Verbraucher; `Thermostat` wird nicht aus diesem Repo deployt.)

Retained, QoS 1, alle **60 s** — dieselbe Taktung wie die Firmwares
(`HEARTBEAT_INTERVAL_MS`). Die Geräteseite erklärt einen Sender nach 3 Minuten für „stumm"
und nach 10 für tot; 60 s lässt also zwei Ausfälle zu, bevor etwas gemeldet wird.

## Payload

```json
{
  "location": "Cluster",
  "deviceType": "Dienst",
  "deviceName": "ChargingController",
  "version": "1.0.482",
  "uptimeSeconds": 3600,
  "lastDataSecondsAgo": 4,
  "Zeitpunkt": "2026-09-20T14:17:18.388Z",
  "Zustand": "Healthy",
  "ZustandText": "Last successful read: 4 seconds ago"
}
```

Die kleingeschriebenen englischen Felder sind das Bestandsformat und heißen **wortwörtlich**
so, wie `MQTTClientLib::publishStatus()` sie schreibt. Die drei großgeschriebenen deutschen
sind neu und folgen den Payload-Regeln der Konvention. Diese Mischung ist gewollt: Jede
Hälfte ist mit ihrer Herkunft konsistent, und `Zeitpunkt` genau so zu schreiben wie überall
sonst im System ist mehr wert als eine einheitliche Schreibweise innerhalb dieses einen
Payloads.

### Was ein Dienst nicht füllt

| ESP32-Feld | Dienst | Warum |
|---|---|---|
| `mac`, `chipModel`, `ip` | **fehlt** | Beschreibt Hardware. Die Pod-IP wechselt bei jedem Neustart und sagt nichts |
| `rssi` | **fehlt** | Kein WLAN |
| `freeHeap` | **fehlt** | „Freier Speicher" ist bei einem 320-KB-Gerät eine Aussage, bei einem Pod mit GC keine |
| `resetReason` | **fehlt** | Kein Reset-Grund; die Neustart-Warnung der Karte greift ohnehin nur bei belegtem Feld |
| `mqttConnects` | **fehlt** | Wäre füllbar, aber kein Dienst zählt das heute. Lieber weglassen als erfinden |
| `timestamp` | **fehlt** | Ersetzt durch `Zeitpunkt` — siehe unten |
| `lastDataSecondsAgo` | **gefüllt** | Alter der letzten erfolgreichen fachlichen Aktion |

**Ein Feld, das fehlt, muss als „nicht vorhanden" ankommen, nicht als 0.** Deshalb sind
`rssi`, `freeHeap` und `mqttConnects` in `SmartHome.Web/Services/DeviceStatus.cs` nullable
und werden in `Devices.razor` nur gezeigt, wenn sie da sind. Ohne das hätte jeder Dienst
„-0 dBm" mit vollem Empfangsbalken und „0 KB frei" angezeigt — eine Messung, die es nie gab.

## Zeitpunkt: warum Dienste ihr Alter anders messen

`DeviceStatus.Age` nimmt `Zeitpunkt`, wenn er da ist, sonst die **Empfangszeit**.

Die Empfangszeit ist für die **Geräte** die richtige Wahl und bleibt es: Die Firmwares
konfigurieren verschiedene NTP-Offsets, ein Gerät auf UTC sähe eine Stunde älter aus als es
ist und würde für tot erklärt. Der Preis ist bekannt: Heartbeats sind retained, beim
Verbinden liefert der Broker alle sofort, und nach einem Neustart der Weboberfläche sieht
ein längst totes Gerät drei Minuten lang gesund aus.

Für die **Dienste** gilt der Grund nicht — sie laufen im Cluster auf korrekter UTC-Zeit —,
und die Konvention verlangt in jedem retained Payload ohnehin ein `Zeitpunkt`-Feld, genau
damit ein Verbraucher einen frischen Wert von einem wiedergegebenen unterscheiden kann. Also
tragen sie ihn, und die Geräteseite benutzt ihn, wenn er da ist.

Damit sind die 17 Geräte **unverändert** behandelt und die Dienste ehrlich: Ein seit 42
Minuten toter Dienst ist auch direkt nach einem Neustart der Weboberfläche sofort als tot
erkennbar. Die Geräte auf `Zeitpunkt` umzustellen ist eine Entscheidung über die ganze
Flotte samt OTA-Rollout und gehört nicht auf diese Seite.

## Fachlicher Zustand: nur eine Quelle

`Zustand` und `ZustandText` kommen aus **denselben** Health-Checks, aus denen die
Readiness-Probe antwortet — `HealthCheckService`, mit dem `ready`-Tag dort, wo es eines gibt
(DataHub, BMWConnector). Kein zweiter Gesundheitsbegriff:

- Die Alters-Warnung des BMW-Tokens geht **nicht** ein. Sie ist bewusst nicht Teil der
  Readiness — ein gewarnter, aber funktionierender Connector ist betriebsbereit, und der
  Heartbeat darf nichts anderes behaupten.
- Beim DataHub zählen die `ready`-Checks, nicht alle: Die Liveness ignoriert absichtlich
  Ausfälle nachgelagerter Systeme und würde den Dienst gesund nennen, während nichts
  geschrieben wird.
- `lastDataSecondsAgo` kommt aus **demselben** Feld, nach dem der Health-Check urteilt
  (`ChargingControllerHealthCheck.LastSuccessfulRead`, `HealthRegistry.LastPublishedAt`, …).
  Ein zweiter Zähler ließe Readiness und Geräteseite auseinanderlaufen.

`Zustand` färbt auf der Karte einen eigenen Chip und geht **nicht** in den Kartenzustand
(ok / stumm / tot) ein: Der Kartenzustand handelt vom Schweigen, und ein Dienst, der
antwortet und dabei Ärger meldet, ist ein anderer Fehler als einer, der aufgehört hat zu
reden.

## Wo der Code liegt

| | |
|---|---|
| `Libs/HeartbeatLib` | Topic und Payload für die .NET-Dienste. `ServiceHeartbeat.BuildPayload()` für Dienste mit eigener Schleife, `ServiceHeartbeatWorker` für die am generischen Host |
| `VWConnector/vw_mqtt.py` | `build_heartbeat_payload()` — dasselbe Format, eigene Umsetzung. Python erreicht `Libs/` nicht |
| `SmartHome.Web/Services/DeviceStatus.cs` | Verbraucherseite: Parsen und Alter |
| `SmartHome.Web/Components/Pages/Devices.razor` | Die Karte |

**Vier Stellen, ein Format.** Die ESP32-Seite (`MQTTClientLib::publishStatus`) und die
Python-Seite können den .NET-Typ nicht teilen, der Verbraucher muss ohnehin alle drei
Sender lesen. Bei Änderungen am Feldsatz alle vier nachziehen — ein Feld, das nur einer
schreibt, fällt nicht auf, die Karte zeigt es einfach nicht.

**`HeartbeatLib` bringt bewusst kein MQTTnet mit.** Die Dienste laufen mit zwei
Hauptversionen davon (BMWConnector 4.3, alle anderen 5.0), deshalb nimmt die Bibliothek ein
Publish-Delegate statt eines Clients. Die Health-Typen kommen über
`FrameworkReference Microsoft.AspNetCore.App` aus dem Shared Framework, nicht aus NuGet.

## Pfadfilter nicht vergessen

`Libs/**` löst ohne Eintrag in `paths:` **keinen** Build aus — kein Fehler, nur kein
Deployment — die Regel und ihre Begründung stehen in `Parallel-Arbeiten.md`. `Libs/HeartbeatLib/**` steht deshalb in den
`paths:`-Blöcken von sieben Workflows: `bmwconnector.yml`, `ChargingController.yml`,
`EnphaseConnector.yml`, `KebaConnector.yml`, `RulesEngine.yml`, `ShellyConnector.yml`,
`SmartHome.DataHub.yml`. Der VWConnector braucht keinen — er ist Python und hat keine
`ProjectReference`; `Smarthome.Web.yml` auch nicht, die Weboberfläche liest das Format,
referenziert die Bibliothek aber nicht.

## Was damit entfällt

`meta/RulesEngine/version` war das einzige Versions-Topic eines Dienstes und trug nichts als
eine Nummer. Es geht im Heartbeat auf. Die RulesEngine **löscht** das retained Topic beim
Start mit leerem Payload, statt es einfach liegen zu lassen — sonst nennt es auf dem Broker
für immer eine Version, die niemand mehr pflegt.

Die `meta/…/version`-Topics der **Geräte** bleiben, wie sie sind: Die Geräteseite baut aus
ihnen die Karten der Firmwares, die noch keinen Heartbeat senden.
