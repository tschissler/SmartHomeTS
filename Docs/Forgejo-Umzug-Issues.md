# Issue-Entwürfe für den Forgejo-Umzug

Ein Abschnitt je Issue, zum Anlegen in `thomas/SmartHomeTS` auf Forgejo. Begründungen und
Belege stehen in `Docs/Forgejo-Umzug.md`, auf das die Issues mit „§" verweisen. Die
Nummern `Uxx` sind Arbeitsnamen; beim Anlegen durch die echten Issue-Nummern ersetzen.

Jedes Issue hat dieselben Felder: **Ziel**, **Abhängig von**, **Löst aus** (Rollouts,
OTA, Builds), **Rückweg**, **Nachweis**. „Löst aus: nichts" heißt nachgeprüft: kein
Workflow, kein ArgoCD-Sync, kein OTA.

Übersicht der Abhängigkeiten:

```
U01 ─┬─ U10 ── U11 ── U12
U02 ─┤          │
U03 ─┤          └── U20 (VW) ── U21 ── U22 ── U23 ── U24 ── U25 ── U26 ── U27 ── U28
U06 ─┘                                                                         │
U04, U05 ──────────────────────────────────────────────┐                       │
U07, U08 ─── U30 ── U31 ── U32 ── U33 ── U34 ── U35 ── U36 ── U37…U45 ──────────┤
                                                                               └── U50 … U56
```

---

## Vorbereitung

### U01 — Offene Entscheidungen vor dem ersten Dienst

- **Ziel:** Thomas entscheidet, was vor dem ersten Workflow-Umzug noch offen ist (§12):
  `forgejo-pull-secret` in den `values.yaml` ja/nein; Absicherung kritischer Dienste
  (ChargingController) gegen einen Forgejo-Ausfall ja/nein.
  Schon entschieden und hier nur zur Orientierung: **F7** Build-Muster A (nur arm64,
  nativ auf den Pi-Runnern), **F8** Tags `1.1.<run>` (Images) und `0.1.<run>` (Firmware).
- **Abhängig von:** —
- **Löst aus:** nichts
- **Rückweg:** —
- **Nachweis:** Entscheidungen im Issue festgehalten und in `Docs/Forgejo-Umzug.md`
  nachgetragen.

### U02 — `argocd-image-updater-config` ins Deployments-Repo holen

- **Ziel:** Die ConfigMap mit `registries.conf` (Eintrag `forgejo.intern`,
  `insecure: true`) liegt heute nur im Cluster (von Hand per `kubectl apply`). Sie gehört
  in Git, bevor neun Dienste von ihr abhängen (§5).
- **Abhängig von:** —
- **Löst aus:** einen ArgoCD-Sync der Stelle, die sie künftig verwaltet; inhaltlich keine
  Änderung, wenn der Inhalt 1:1 übernommen wird.
- **Rückweg:** Commit revertieren; die ConfigMap bleibt im Cluster stehen (vorher
  prüfen, ob die verwaltende App `prune` hat).
- **Nachweis:** `kubectl -n argocd get cm argocd-image-updater-config -o yaml` identisch
  vorher/nachher; Image-Updater-Log ohne Registry-Fehler; `pv-forecast` wird weiter
  gefunden.

### U03 — Pull von `forgejo.intern` auf allen Nodes nachweisen

- **Ziel:** Belegen, dass `k3snode3` und `k3snode4` (dort laufen Dienste) sowie die
  amd64-Nodes der Cluster-CA vertrauen und `forgejo.intern` auflösen. Belegt ist es bisher
  nur für `k3snode5/6` (§5).
- **Abhängig von:** —
- **Löst aus:** nichts (nur lesend)
- **Rückweg:** —
- **Nachweis:** `ansible prodservers -m command -a "curl -sS -o /dev/null -w '%{http_code}' https://forgejo.intern/v2/"`
  liefert auf jedem Node `401`, keinen TLS- oder DNS-Fehler. Fehlt es irgendwo:
  `plays/trust-cluster-ca.yml` für diesen Node laufen lassen.

### U04 — Test: MQTT-Publish aus einem Forgejo-Job

- **Ziel:** Belegen, dass ein Job auf einem Forgejo-Runner `mosquitto.intern` erreicht
  (§3). Heute nur aus der Konfiguration abgeleitet.
- **Abhängig von:** Freigabe durch Thomas (berührt den Produktivbroker)
- **Löst aus:** eine Nachricht auf einem **Test-Topic**, z. B. `test/forgejo-runner`,
  **ohne `-r`**. Nicht auf `OTAUpdate/`.
- **Rückweg:** —
- **Nachweis:** `mosquitto_sub -t test/forgejo-runner` sieht die Nachricht; Job-Log grün.
  Einmal auf `ubuntu-latest` (amd64), einmal auf `arm64`.

### U05 — Test: Generic-Paket anonym ladbar

- **Ziel:** Klären, ob ein ESP32 ohne Anmeldung aus der Generic Registry laden kann
  (§8). Die Doku sagt nein, die Container-Pakete von `thomas` sind aber anonym lesbar.
  Hängt an diesem Test, ob die Firmware-Ablage wie geplant geht.
- **Abhängig von:** Freigabe durch Thomas (schreibt in Forgejo)
- **Löst aus:** ein Wegwerf-Paket, danach gelöscht
- **Rückweg:** Paket löschen
- **Nachweis:** `curl -I https://forgejo.intern/api/packages/thomas/generic/<test>/0.0.0/test_0.0.0.bin`
  ohne Anmeldung liefert `200`. Bei `401`: Release-Assets des öffentlichen Repos nehmen
  (§8, Bewertung nach der F5-Korrektur). Das kostet einen Tag je Firmware und Version
  und Aufräumen per API statt über Bereinigungsregeln.

### U06 — Secrets in Forgejo anlegen

- **Ziel:** `REGISTRY_USERNAME`, `REGISTRY_TOKEN` auf **Benutzer-Ebene** (dieselben Werte
  wie in PV-Prognose und Einrohrheizung); `WIFI_PASSWORDS`, `METER_PINS` auf
  **Repo-Ebene** `thomas/SmartHomeTS` (§7). Die Werte kommen von Thomas, nicht aus GitHub
  (GitHub gibt sie nicht heraus).
- **Abhängig von:** U10 (für die Repo-Secrets)
- **Löst aus:** nichts
- **Rückweg:** Secrets löschen
- **Nachweis:** Namen in den Einstellungen sichtbar. Wirksam erst belegt durch U20
  (Registry) und U36 (WLAN).

### U07 — Fragen an Thomas zum GitHub-Repo

- **Ziel:** Klären, was am GitHub-Repo hängt und nicht im Git steht (§7): Webhooks,
  Branch-Schutz auf `main` (blockiert er den erzwungenen Spiegel-Push?), Deploy-Keys,
  Actions-Variablen, weitere Self-hosted-Runner, Apps/Integrationen, Environments; ob
  die Originale von `WIFI_PASSWORDS` und `METER_PINS` außerhalb von GitHub vorliegen;
  ob `github-runner/runner-secret.yaml` im Deployments-Repo einen echten PAT enthält
  (das Repo ist anonym lesbar).
- **Abhängig von:** —
- **Löst aus:** nichts
- **Rückweg:** —
- **Nachweis:** Antworten im Issue.

### U08 — Soll-Liste aller ESP32-Geräte

- **Ziel:** Eine Liste jedes Geräts mit Typ, Ort und Name. Ohne sie lässt sich der
  Nachweis in U33 nicht führen: Ein ausgeschaltetes Gerät erscheint auf der Seite
  `Devices` nicht als „veraltet", sondern gar nicht (§8).
- **Abhängig von:** —
- **Löst aus:** nichts
- **Rückweg:** —
- **Nachweis:** Liste im Issue; jeder Eintrag einmal mit Heartbeat oder `meta/…/version`
  gesehen.

---

## Repo

### U10 — Repo leer und öffentlich anlegen, `main` pushen (Nachzügler)

- **Ziel:** `thomas/SmartHomeTS` auf Forgejo: leer, öffentlich, **Actions im Repo
  abgeschaltet**; danach `main` pushen (F5, §10 Schritt 1a). GitHub bleibt `origin`,
  Forgejo wird bis U11 nur per Fast-Forward nachgezogen.
- **Abhängig von:** —
- **Löst aus:** nichts. Actions sind aus. **Nicht einschalten, bevor U11 den
  `.gitkeep` gelegt hat**, sonst starten alle 19 Workflows beim nächsten Push.
- **Rückweg:** Repo löschen
- **Nachweis:** `git ls-remote forgejo main` = `git ls-remote origin main`; Actions-Tab
  des Repos zeigt „deaktiviert".

### U11 — Umstellung: Forgejo wird `origin`

- **Ziel:** §10 Schritt 1b.
  1. Letzten Stand von GitHub per Fast-Forward nachziehen.
  2. Commit `.forgejo/workflows/.gitkeep`.
  3. Erst jetzt Actions im Repo einschalten.
  4. Push-Spiegel nach GitHub, „bei jedem Commit synchronisieren".
  5. `origin` auf dem Arbeitsplatz umstellen; **Verzeichnis nicht umbenennen** (Memory
     und Freigaben hängen am Pfad). Worktrees und laufende Sessions informieren.
- **Abhängig von:** U10, U07 (Branch-Schutz), U12 bereit zum Merge
- **Löst aus:** auf Forgejo nichts (`.gitkeep` enthält keinen Workflow); auf GitHub kommt
  der Commit über den Spiegel an und trifft keinen `paths:`-Filter.
- **Rückweg:** Remote zurück auf GitHub, Spiegel löschen, Actions im Forgejo-Repo aus.
  Solange kein Workflow umgezogen ist, baut GitHub alles wie bisher.
- **Nachweis:** Forgejo-Actions-Tab leer (kein Lauf); GitHub-`main` = Forgejo-`main`
  nach dem Spiegellauf, geprüft per `git ls-remote`, nicht am Exit-Code des Push.

### U12 — Doku und Skills auf zwei Workflow-Verzeichnisse umstellen

- **Ziel:** `CLAUDE.md` (Abschnitt CI/CD), `Docs/Parallel-Arbeiten.md` (Pfadfilter) und
  der Skill `worker` (Rollout-Zählung an den `paths:`-Blöcken) nennen während des
  Übergangs **beide** Verzeichnisse: `.forgejo/workflows/` baut auf Forgejo,
  `.github/workflows/` über den Spiegel auf GitHub. Dazu `ESP32Firmwares/SharedLibs/` aus
  `CLAUDE.md` streichen (existiert nicht).
- **Abhängig von:** —
- **Löst aus:** nichts (nur Doku/Skills, per Zählung der `paths:`-Blöcke nachweisen)
- **Rückweg:** Revert
- **Nachweis:** Zählung der `paths:`-Blöcke im Commit-Text.

---

## Dienste

Jeder Dienst ist ein Issue mit denselben zwei Commits. Die Vorlage steht bei U20, die
übrigen nennen nur, was abweicht.

### U20 — VWConnector umziehen (Probelauf)

- **Ziel:** Der erste Dienst baut auf Forgejo, liegt in `forgejo.intern` und wird vom
  Image Updater von dort ausgerollt (§10 Schritt 2). Gewählt, weil die VW-Daten in keine
  Regelung eingehen, er Python ohne Test-Job ist und einen Heartbeat mit Version hat.
- **Abhängig von:** U01, U02, U03, U06, U11
- **Commit a (SmartHomeTS), ohne Code-Änderung am Dienst:**
  `git mv .github/workflows/vwconnector.yml .forgejo/workflows/`; nach Muster A (F7)
  anpassen: `runs-on: arm64`, `docker build --platform linux/arm64`,
  `docker login forgejo.intern` mit `REGISTRY_*`, `docker push`; kein QEMU, kein Buildx.
  Tag `1.1.${{ github.run_number }}` (F8).
- **Commit b (Deployments), direkt danach:** `VWConnector.yaml`:
  `image-list: vwconnector=forgejo.intern/thomas/vwconnector`,
  `pull-secret: pullsecret:argocd/forgejo-pull-secret`, `platforms: linux/arm64`;
  `VWConnector/values.yaml`: `repository`, `tag` auf den Tag aus a. **In einem Commit**,
  danach `00-bootstrap` synchronisieren (sonst schreibt der Updater weiter
  Docker-Hub-Tags).
- **Löst aus:** a) ein Forgejo-Build; GitHub baut nichts mehr. b) ein Rollout
  (vwconnector).
- **Rückweg:** b revertieren (zurück auf `tschissler/vwconnector:1.0.41`, liegt weiter auf
  Docker Hub), a revertieren (GitHub baut wieder). Einzeln möglich.
- **Nachweis:**
  1. Forgejo-Lauf grün, Paket `vwconnector` mit dem Tag unter `thomas`.
  2. Pod zeigt `forgejo.intern/thomas/vwconnector:<tag>`, `Running`, keine Restarts.
  3. `status/Cluster/Dienst/VWConnector` meldet `"version": "<tag>"`.
  4. **Entscheidend:** eine zweite harmlose Änderung unter `VWConnector/` → binnen ~2 min
     Commit `build: automatic update of vwconnector` im Deployments-Repo. Erst das
     belegt den Updater gegen die Forgejo-Registry.
  5. Build-Dauer notieren (Kapazitätsfrage §3).

### U21 — EnphaseConnector umziehen

- **Wie U20.** Abweichend: .NET, kein Test-Job; Workflow benutzt `checkout@v3` und hat
  kein `workflow_dispatch` (beim Umzug ergänzen). Pfade: `Libs/HeartbeatLib`,
  `MQTTClient`, `SharedContracts`, `SmartHomeHelpers`.
- **Abhängig von:** U20
- **Nachweis:** wie U20; zusätzlich Enphase-Werte in Grafana laufen weiter
  (Heartbeat `status/Cluster/Dienst/EnphaseConnector`).

### U22 — ShellyConnector umziehen

- **Wie U21.** Pfade wie Enphase.
- **Abhängig von:** U21

### U23 — BMWConnector umziehen

- **Wie U20.** Abweichend: Test und Build in einem Job, `.slnx`; **Ersatz für
  `actions/setup-dotnet`** wird hier erprobt (gibt es auf `data.forgejo.org` nicht, §2),
  vorgeschlagen Job-Container `mcr.microsoft.com/dotnet/sdk:10.0`; Test-Job auf
  `ubuntu-latest` (amd64, 2 CPU), Build-Job auf `arm64` mit `needs: test`. Pfade:
  `SharedContracts`, `Libs/HeartbeatLib`.
- **Abhängig von:** U22
- **Nachweis:** wie U20; zusätzlich ein absichtlich roter Test im Branch verhindert das
  Image (Test-Job greift).

### U24 — KebaConnector umziehen

- **Wie U23**, getrennter `test`-Job. Pfade zusätzlich `Libs/HelpersLib`. Gehört zur
  Lade-Kette: nicht am selben Abend wie U25 oder U28.
- **Abhängig von:** U23

### U25 — RulesEngine umziehen

- **Wie U23.** Baut heute schon nur arm64. Gehört zur Lade-Kette.
- **Abhängig von:** U24

### U26 — SmartHome.DataHub umziehen

- **Wie U23.**
- **Abhängig von:** U25

### U27 — SmartHome.Web umziehen

- **Wie U20**, kein Test-Job. Abweichend: `paths:` mit Negation
  `!SmartHome.Web/SmartHomeBlazorApp/**`.
- **Abhängig von:** U26
- **Nachweis:** wie U20; zusätzlich eine Änderung nur unter
  `SmartHome.Web/SmartHomeBlazorApp/` erzeugt **keinen** Lauf (Negation greift auf
  Forgejo); Build-Dauer notieren (größter Build).

### U28 — ChargingController umziehen

- **Wie U23.** Zuletzt, weil er die Ladung regelt.
- **Abhängig von:** U27
- **Nachweis:** wie U20; zusätzlich der Regelkreis läuft (`Docs/ChargingController-Regelkreis.md`,
  Diagnose), während ein Fahrzeug lädt oder ein Ladevorgang simuliert wird.

---

## Firmware, OTA und Bibliotheken

Die Reihenfolge ist zwingend (§8, §9): Die Firmware muss der Cluster-CA vertrauen,
**bevor** die erste URL auf `forgejo.intern` zeigt, und dieses Update kommt noch über
Azure und wird noch auf GitHub gebaut.

### U30 — `ESP32_OTAUpdate`: zweite CA und erster Tag (noch auf GitHub)

- **Ziel:** Den PEM-String `server_certificate` in `src/AzureOTAUpdater.cpp` um die
  „SmartHome Cluster CA" ergänzen (DigiCert Global Root G2 bleibt). Tag `v1.0.0` setzen.
- **Abhängig von:** —
- **Löst aus:** nichts (die Firmwares ziehen die Bibliothek unversioniert, bauen aber erst
  bei der nächsten Änderung in ihrem Pfad; **Achtung:** jede Firmware, die bis U32 aus
  anderem Grund baut, nimmt diesen Stand schon mit)
- **Rückweg:** Revert, Tag löschen
- **Nachweis:** ein Gerät am Tisch mit dieser Fassung flashen und einmal von Azure, einmal
  von `forgejo.intern` laden lassen (§11). Erst dann weiter.

### U31 — Tote Bibliothekskopien entfernen

- **Ziel:** `HeatmeterSensor.Firmware/lib/OTAUpdater/` (nachweislich nicht gebaut, §8)
  und `Kellerdevice.Firmware/src/AzureRootCert.h` (nirgends eingebunden) löschen, damit
  niemand an ihnen die CA-Änderung macht. `HeatmeterSensor.Firmware/lib/MQTTClientLib/`
  erst prüfen (`pio run -v`), dann ggf. mit löschen.
- **Abhängig von:** —
- **Löst aus:** die Workflows von HeatMeterSensor und Kellerdevice → **je ein
  OTA-Rollout über Azure.** Am besten mit U32 zusammenlegen.
- **Rückweg:** Revert
- **Nachweis:** Build grün, `firmware.map` unverändert bis auf Zeitstempel.

### U32 — Alle zehn Firmwares pinnen `ESP32_OTAUpdate#v1.0.0` (Rollout über Azure)

- **Ziel:** In allen zehn `platformio.ini` `…/ESP32_OTAUpdate.git#v1.0.0`. Gebaut noch
  auf GitHub, ausgeliefert noch über Azure.
- **Abhängig von:** U30 (Nachweis am Tisch), U08
- **Löst aus:** **zehn OTA-Rollouts über Azure**, einer je Gerätetyp. Vorher je Firmware
  `check_firmware_size.py` lokal mit frischem `PLATFORMIO_CORE_DIR` (CLAUDE.md) — die CA
  kostet rund 2 KB.
- **Rückweg:** Vorversion retained auf `OTAUpdate/<Typ>` nachsenden (die alte URL steht
  in §1)
- **Nachweis:** siehe U33.

### U33 — Nachweis: jedes Gerät fährt die Fassung aus U32

- **Ziel:** Gegen die Soll-Liste aus U08 für **jedes** Gerät belegen, dass sein
  Heartbeat (`status/<Ort>/<Typ>/<Name>`, Feld `version`) oder `meta/…/version` der
  angebotenen Version aus U32 entspricht. Die Seite `Devices` zeigt 0 „veraltet" —
  **und** jedes Gerät der Liste ist dort zu sehen.
- **Abhängig von:** U32
- **Löst aus:** nichts
- **Rückweg:** —
- **Nachweis:** abgehakte Liste im Issue. **Ohne diesen Haken kein U36**: Ein Gerät, das
  die Fassung verpasst, ist nach U53 nur noch per Kabel erreichbar.

### U34 — Die acht ESP32-Bibliotheken nach Forgejo migrieren

- **Ziel:** `ESP32_WifiLib`, `ESP32_MQTTClientLib`, `ESP32_OTAUpdate`,
  `ESP32_ESP32Helpers`, `ESP32_Colors`, `ESP32_Sensors`, `ESP32_LEDLib`,
  `ESP32_TFTDisplay` mit Historie und Tags nach `forgejo.intern/thomas/`. Je Bibliothek
  einen ersten Tag setzen (nur `ESP32_OTAUpdate` hat einen aus U30). Sichtbarkeit nach
  §12 Punkt 12. GitHub-Kopien **bleiben** (Einrohrheizung pinnt zwei davon, §9).
- **Abhängig von:** U33
- **Löst aus:** nichts
- **Rückweg:** Repos auf Forgejo löschen
- **Nachweis:** Tags auf Forgejo = Tags auf GitHub; lokaler `pio run` einer Firmware mit
  Forgejo-URLs grün.

### U35 — Vorlage für Firmware-Workflows auf Forgejo

- **Ziel:** Das Muster, das U36–U45 übernehmen: `runs-on: ubuntu-latest` (amd64);
  Bibliotheken über `git config --global url."http://forgejo-http.forgejo.svc.cluster.local:3000/".insteadOf "https://forgejo.intern/"`
  (§9); PlatformIO-Build, `check_firmware_size.py`; Upload per
  `curl -X PUT … /api/packages/thomas/generic/<Paket>/<Version>/<Name>_<Version>.bin`;
  `mosquitto_pub -r` (Client im Job installieren oder eigenes CI-Image, Vorbild
  `einrohr-pio-ci`); Version `0.1.<run>` (F8). Kein `az`, kein
  Azure-Schlüssel in Forgejo.
- **Abhängig von:** U04, U05, U34
- **Löst aus:** nichts (Vorlage, noch keinem Gerätetyp zugeordnet)
- **Rückweg:** —
- **Nachweis:** Review.

### U36 — Erster Gerätetyp auf Forgejo (Probe)

- **Ziel:** Ein Gerätetyp, bei dem ein Fehlschlag nicht heizt oder misst — Vorschlag
  **LEDStripe** oder **TemperatureDisplay** (§10 Schritt 4). Ein Gerät dieses Typs am
  Tisch am Seriell-Monitor.
  Commit: Workflow per `git mv` nach `.forgejo/workflows/`, nach U35 umbauen;
  `platformio.ini` → alle Bibliotheken auf Forgejo-URL mit Tag.
- **Abhängig von:** U35, U06
- **Löst aus:** ein Forgejo-Build, **ein OTA-Rollout über Forgejo** (retained URL auf
  `forgejo.intern`). GitHub baut diesen Typ nicht mehr.
- **Rückweg:** alte Azure-URL retained auf `OTAUpdate/<Typ>` nachsenden (die Geräte
  vertrauen beiden CAs); Commit revertieren.
- **Nachweis:** Seriell-Log zeigt Download von `forgejo.intern` und Neustart; Heartbeat
  aller Geräte des Typs meldet `0.1.<run>`; `Devices` zeigt 0 „veraltet" für den Typ.

### U37–U45 — Die übrigen neun Gerätetypen, einzeln

Je ein Issue, jeweils **wie U36**, abhängig vom vorigen. Rückweg und Nachweis wie U36.

| Issue | Workflow | Topic | Abweichung |
|---|---|---|---|
| U37 | `TemperatureDisplayFirmware.yml` bzw. `LEDStripeFirmware.yml` (der, der nicht U36 war) | | |
| U38 | `KellerdeviceFirmware.yml` | `OTAUpdate/KellerDevice` | Publish heute ohne `-q 2`; beim Umzug angleichen |
| U39 | `TemperatureSensorFirmware.yml` | `OTAUpdate/TemperaturSensor` | kein `workflow_dispatch`, beim Umzug ergänzen |
| U40 | `TemperatureSensor2Firmware.yml` | `OTAUpdate/TemperaturSensor2` | zwei Boards; ein Paket je Board, URL mit `---board---` |
| U41 | `HeatMeterSensorFirmware.yml` | `OTAUpdate/HeatMeter` | misst Wärme; nach U31 |
| U42 | `SMLSensorFirmware.yml` | `OTAUpdate/SMLSensor` | Secret `METER_PINS`; misst Strom |
| U43 | `HeatingFanControllerFirmware.yml` | `OTAUpdate/HeatingFanController` | steuert Heizung |
| U44 | `RelaisBoardFirmware.yml` | `OTAUpdate/Relaismodule` | steuert Heizung; bekannter MQTT-Robustheitsfix (Memory) vorher geklärt |
| U45 | `MixerControllerFirmware.yml` | `OTAUpdate/MixerController` | steuert Mischer, zuletzt |

---

## Aufräumen (wenn `.github/workflows/` leer ist)

### U50 — GitHub Actions abschalten und Secrets löschen

- **Ziel:** Actions im GitHub-Repo abschalten (Settings → Actions → Disable); danach die
  Secrets `DOCKER_HUB_USERNAME`, `DOCKER_HUB_ACCESS_TOKEN`, `AZURE_STORAGE_KEY`,
  `WIFI_PASSWORDS`, `METER_PINS` löschen (§7, §10 Schritt 5).
- **Abhängig von:** U28, U45 (`.github/workflows/` leer)
- **Löst aus:** nichts
- **Rückweg:** Actions wieder einschalten (Secrets müssten neu eingetragen werden)
- **Nachweis:** Actions-Tab auf GitHub zeigt „disabled"; Secret-Liste leer; der nächste
  Spiegel-Push startet nichts.

### U51 — `github-runner` abbauen

- **Ziel:** `GitHubRunner.yaml` und `github-runner/` im Deployments-Repo entfernen; den
  PAT des Runners auf GitHub widerrufen.
- **Abhängig von:** U45
- **Löst aus:** ArgoCD entfernt den Namespace-Inhalt (Prune)
- **Rückweg:** Revert (braucht einen neuen PAT)
- **Nachweis:** `kubectl get ns github-runner` leer bzw. weg; Runner auf GitHub offline.

### U52 — Docker Hub stilllegen

- **Ziel:** Token auf Docker Hub widerrufen, `dockerhub-credentials` in `argocd`
  entfernen. Die Images auf Docker Hub bleiben als Rückweg liegen, bis Thomas anders
  entscheidet.
- **Abhängig von:** U28, Karenzzeit ohne Rückgriff
- **Löst aus:** nichts
- **Rückweg:** neuen Token anlegen
- **Nachweis:** Image-Updater-Log ohne Verweis auf Docker Hub.

### U53 — Azure stilllegen

- **Ziel:** Firmware-Blobs löschen (sie enthalten sehr wahrscheinlich die
  WLAN-Passwörter und sind öffentlich, §7), Speicherkonto kündigen bzw. Schlüssel
  rotieren.
- **Abhängig von:** U45, Karenzzeit, in der keine Azure-URL mehr gebraucht wurde
- **Löst aus:** nichts auf den Geräten, solange kein retained Topic mehr auf Azure zeigt
  — vorher `mosquitto_sub -t 'OTAUpdate/#' -W 4` prüfen
- **Rückweg:** keiner mehr (danach führt der Weg zurück nur über Forgejo oder das Kabel)
- **Nachweis:** keine retained URL auf `blob.core.windows.net`; Blob-URLs liefern 404.

### U54 — Spiegeltakt nach GitHub festlegen

- **Ziel:** Den Push-Spiegel von „bei jedem Commit" auf den Takt stellen, den Thomas
  wählt (Intervall, nur Knopf oder manuell, §10).
- **Abhängig von:** U50
- **Löst aus:** nichts
- **Rückweg:** Einstellung zurück
- **Nachweis:** Einstellung im Repo.

### U55 — Einrohrheizung und die GitHub-Kopien der Bibliotheken

- **Ziel:** `thomas/Einrohrheizung` pinnt `ESP32_ESP32Helpers` und `ESP32_WifiLib` auf
  GitHub und klont SmartHomeTS von GitHub (toter Schritt, `SharedLibs` gibt es nicht
  mehr). Auf die Forgejo-Bibliotheken umstellen; danach die GitHub-Kopien nach Thomas'
  Entscheidung spiegeln, archivieren oder löschen.
- **Abhängig von:** U34
- **Löst aus:** in Einrohrheizung nichts automatisch (Firmware-Build dort nur per
  `workflow_dispatch`)
- **Rückweg:** Revert in Einrohrheizung
- **Nachweis:** manueller Build in Einrohrheizung grün.

### U56 — README und Verweise

- **Ziel:** Die fünf Workflow-Badges in `README.md` entfernen oder ersetzen; README-Links
  auf GitHub prüfen (funktionieren, solange gespiegelt wird).
- **Abhängig von:** U50
- **Löst aus:** nichts (per Zählung der `paths:`-Blöcke nachweisen)
- **Rückweg:** Revert
- **Nachweis:** README auf Forgejo und GitHub angesehen.
