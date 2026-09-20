# ChargingController — Regelkreis und Trägheit

Beschreibt, wie der ChargingController aus den Messwerten den Ladestrom der beiden Keba-Wallboxen
ableitet, und warum der Regelkreis bewusst träge ausgelegt ist.

## Aufbau des Regelkreises

```
MQTT (Enphase 1 Hz, Keba alle 5 s)
        │  Handler aktualisieren nur ChargingSituation
        ▼
   ChargingSmoother        gleitender Mittelwert (EMA) über die Leistungsmesswerte
        ▼
 ChargingDecisionsMaker    „Was wäre jetzt ideal?" — reine Funktion, Excel-getestet
        ▼
  ChargingStabilizer       „Was kommandieren wir?" — Zeit-Hysterese, Totband, Schutzabschaltung
        ▼
MQTT commands/charging/Keba{Garage,Outside}
```

Der Zyklus läuft mit festem Takt (`controlCycleSeconds = 5`) in der Hauptschleife von
`Program.cs`. Die MQTT-Handler rechnen **nicht** mehr selbst — das war die Hauptursache für das
Aufschwingen (siehe unten). Eine Änderung an `config/charging/settings` stößt den Zyklus sofort an,
damit Bedienung am Web-UI unmittelbar wirkt.

## Warum Trägheit nötig ist

Drei Mechanismen führten dazu, dass der Ladevorgang bei kurzen Schwankungen ständig unterbrochen
wurde — jede Unterbrechung ist ein Schaltspiel des Wallbox-Schützes:

1. **Harte Schaltschwelle.** Die Ladeleistung springt zwischen 0 W und dem Minimum von
   4140 W (3 × 6 A). In Level 3 liegt die Grenze bei einer Verfügbarkeit von
   `MinimumChargingPower − BatteryDischargingMaxPower` = 640 W. Ein Wasserkocher reicht, um sie
   zu unterschreiten — und beim Abschalten des Verbrauchers wird sofort wieder eingeschaltet.
2. **Regeltakt schneller als die Streckentotzeit.** Der EnphaseConnector sendet im Sekundentakt,
   das Fahrzeug folgt einem neuen Sollstrom aber erst nach mehreren Sekunden und die Keba meldet
   alle 5 s zurück. Ein Regler, der schneller taktet als seine Totzeit, schwingt zwangsläufig auf.
3. **Kein Totband auf dem Sollwert.** Jede Änderung um wenige Watt erzeugte ein neues Kommando,
   obwohl die Keba in 100-mA-Schritten arbeitet.

Die Motivation für die schnelle Reaktion war minimaler Netzbezug. Der Kompromiss ist jetzt
umgekehrt gewichtet: etwas mehr Netz- bzw. Batteriebezug, dafür eine harte Obergrenze für die
Schaltspiele.

## Parameter

Alle Zeiten und Schwellen stehen in `ChargingStabilizerOptions` (Defaults = „vorsichtige"
Einstellung, Stand 2026-09-20). Einziger Ort zum Nachjustieren.

| Parameter | Default | Wirkung |
|---|---|---|
| `SmoothingTimeConstant` | 30 s | Zeitkonstante des EMA über Netz-, Batterie-, PV- und Ladeleistung |
| `StartDelay` | 30 s | Überschuss muss so lange reichen, bevor eingeschaltet wird |
| `StopDelay` | 90 s | Mangel muss so lange anhalten (Level 3 mit Batteriestütze) |
| `StopDelayWithoutBatterySupport` | 30 s | Verkürzt, wenn die Lücke aus dem Netz käme |
| `MinimumChargingDuration` | 5 min | Nach dem Start nicht abschalten |
| `MinimumPauseDuration` | 5 min | Nach dem Stopp nicht neu starten |
| `CurrentDeadbandmA` | 500 mA | Kleinere Sollwertänderungen werden verworfen |
| `MinimumCurrentChangeInterval` | 30 s | Mindestabstand zwischen zwei Sollwertänderungen |
| `GridProtectionLimitWatts` | 10000 W | Darüber sofort auf Minimalstrom drosseln (≈ 14,5 A je Phase) |
| `GridProtectionStopDelay` | 30 s | Hilft das Drosseln nicht, wird abgeschaltet |

Daraus folgt eine Obergrenze von **maximal 6 Schaltspielen pro Stunde** je Wallbox
(`MinimumChargingDuration + MinimumPauseDuration` = 10 min pro vollständigem Zyklus).

**Warum die Netzschutzgrenze 10.000 W ist und nicht 8.000 W.** Mit 8.000 W stand sie der
Stufe im Weg, die sie schützen sollte: Level 5 kommandiert allein 8.000 W, zusammen mit dem
Haus (Median 384 W, p90 933 W) lag der Bezug bei Dunkelheit über der Grenze — die Notbremse
hätte den Normalfall getroffen. Dass es nicht auffiel, liegt nur daran, dass Level 5 nachts
selten benutzt wird. In 90 Tagen überschritt der Netzbezug 8.000 W 37-mal (Maximum
10.613 W); die neue Grenze liegt also weiterhin über allem, was das Haus von selbst tut.

## Level 5: 8 kW oder 11 kW

Level 5 ist die einzige Stufe, deren Leistung **nicht** dem Überschuss folgt — sie
kommandiert einen festen Wert, und was fehlt, kommt aus dem Netz. Welcher der beiden Werte
gilt, entscheidet die Eigenleistung `PowerFromPV + PowerFromBattery` (Entladen positiv,
Laden negativ) auf den **geglätteten** Messwerten:

| Eigenleistung | Ladeleistung |
|---|---|
| ab 3.000 W | 11.000 W (`QuickChargingBoostPower`, = 3 × 16 A der Wallbox) |
| unter 2.000 W | 8.000 W (`QuickChargingBasePower`) |
| dazwischen | es bleibt beim zuletzt gewählten Wert |

Die Hysterese hat denselben Grund wie die von Level 3: Zwischen den beiden Werten liegen
4.348 mA, weit jenseits des Totbands von 500 mA — ohne sie verschöbe ein um die Schwelle
schwankender PV-Wert die Last alle 30 s um 3 kW. Deshalb wird seit 2026-09-20 auch
`PowerFromPV` mitgeglättet; die rohen Werte der `ChargingSituation` bleiben unberührt, sie
sind weiterhin das, was die UI zeigt und die Energieaufteilung verrechnet.

**Die entladende Hausbatterie zählt absichtlich mit**, obwohl sie nichts erzeugt. Die Folge
ist bekannt und gewollt: Nachts entlädt die Batterie *gerade deshalb*, weil das Auto lädt —
die Bedingung bleibt also erfüllt, bis die Batterie leer ist, und Level 5 füllt die
Hausbatterie ins Auto um, statt 8 kW aus dem Netz zu nehmen. Wird die Batterie dann leer,
springt der Netzbezug auf rund 11,4 kW, die Netzschutzgrenze greift, es wird 30 s auf
Minimalstrom gedrosselt und danach mit 8 kW weitergeladen. Selbstheilend, kein Schaltspiel.

Der Hysteresezustand liegt als statisches Feld im `ChargingDecisionsMaker`
(`QuickChargingBoostActive`) statt — wie bei Level 3 — in der `ChargingSituation`: Die liegt
in `SharedContracts`, und ein Feld dort baut und deployt jeden Dienst neu, der den Kontrakt
referenziert. Beim Neustart des Dienstes beginnt Level 5 deshalb bei 8 kW, bis die
Eigenleistung einmal 3 kW überschreitet.

## Verhalten in Sonderfällen

- **Kurzer Mangel während des Ladens:** Statt abzuschalten wird auf den Minimalstrom (6 A)
  gedrosselt und die Abschaltverzögerung abgewartet. Das hält die Kosten der Verzögerung gering,
  ohne den Schütz zu öffnen.
- **Stecker gezogen, Station deaktiviert, Level 0:** Sofortiger Stopp ohne Verzögerung. Es wird
  *keine* Mindestpause gesetzt, weil das kein Regelschwingen ist — eine neue Ladung kann nach der
  normalen Einschaltverzögerung beginnen.
- **Hausbatterie unter der Mindest-Ladegrenze (Level 3):** `BatterySupportedChargingActive` wird
  false, damit greift die kurze Abschaltverzögerung — ein Mangel würde sonst aus dem Netz gedeckt.
- **Netzbezug über dem Limit:** Sofortige Drosselung auf Minimalstrom unter Umgehung aller
  Verzögerungen; hält der hohe Bezug an, wird trotz Mindestladedauer abgeschaltet. Hierfür werden
  bewusst die **ungefilterten** Messwerte verwendet.
- **Neustart des Dienstes während einer laufenden Ladung:** Der erste Regelzyklus läuft erst,
  wenn beide Wallbox-Topics Daten geliefert haben (Timeout 30 s) — sonst würde der Regler 0 mA
  kommandieren, bevor er den Wallbox-Zustand kennt, und damit den Schütz öffnen. Eine laufende
  Session wird dann übernommen statt unterbrochen; erkannt wird sie an einer gemessenen
  Ladeleistung über 1000 W. Diese Schwelle darf **nicht** die nominellen 4140 W sein: eine
  dreiphasige 6-A-Ladung misst real nur rund 4000 W. Die Mindestladedauer wird beim Übernehmen
  nicht neu gestartet, damit ein Neustart keine Ladung künstlich verlängert.

  Jeder Pod-Wechsel ist damit für den Schütz folgenlos — und Pod-Wechsel gibt es bei jedem
  Deploy: Die GitHub-Action baut nur das Image, den Rollout macht der ArgoCD Image Updater
  (schreibt den Tag nach `SmartHomeDeployments`, ArgoCD synct). Die Deploy-Strategie ist dort
  in `docs/update-strategie.md` beschrieben.

## Diagnose

`data/charging/situation` (alle 5 s, retained) enthält zusätzlich:

- `AvailableChargingPowerWatts` — der geglättete Überschuss, auf dem die Entscheidung beruht
- `InsideSwitchCycles` / `OutsideSwitchCycles` — Schaltspiele seit Start des Dienstes

Die Schaltspielzähler sind das Maß dafür, ob die Trägheit ausreicht: steigen sie über wenige
Zyklen pro Ladevorgang, sind `StopDelay` und `MinimumChargingDuration` zu kurz. Im Log meldet
sich der Stabilizer mit dem Präfix `Stabilizer KebaGarage:` bzw. `Stabilizer KebaOutside:` bei
jedem Ein- und Ausschalten samt Begründung.

## Tests

- `ChargingControllerDecissionTests` — Excel-getriebene Fälle für die *stationäre* Entscheidung
  (`TestCases/ChargingDecisions.xlsx`). Der Stabilizer ist hier bewusst nicht beteiligt.
- `ChargingStabilizerTests` — Szenarien über simulierte Zeit (Zeitpunkt wird als Parameter
  übergeben, kein Zugriff auf die Systemuhr).
- `ChargingSmootherTests` — Dämpfungsverhalten des Filters.
