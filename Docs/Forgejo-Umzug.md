# Umzug von GitHub nach Forgejo

**Stand 2026-09-27. Bestandsaufnahme und Plan, noch nichts umgestellt.** Erhoben gegen
`origin/main` = `15b82ab` dieses Repos (bis `fa0a022` keine Änderung an Workflows oder
Firmware), gegen `origin/main` = `ba7edfb` des
Deployments-Repos und gegen den laufenden Cluster, nur lesend.

**Fortgeschrieben 2026-09-27:** `thomas/SmartHomeTS` liegt auf Forgejo, `main` per
Fast-Forward auf `47e152e` (Nachzügler, Schritt 1a). Thomas hat **neu geklont** statt nur
`origin` umzustellen (Abschnitt 6 „Arbeitsplatz", Abschnitt 12): Neuer Klon
`~/Repos/Forgejo.intern/SmartHomeTS`, der alte unter `~/Repos/GitHub/SmartHomeTS` bleibt
übergangsweise. Workflows und Registry sind noch unverändert. U01 ist entschieden
(`forgejo-pull-secret` mitnehmen, keine zusätzliche Absicherung gegen einen
Forgejo-Ausfall), beides in Abschnitt 12.

Entschieden hat Thomas bisher:

| | Entscheidung |
|---|---|
| **F1** | Die Images ziehen von Docker Hub in die Forgejo-Registry (`forgejo.intern`) um |
| **F2** | GitHub bleibt vorerst bestehen und wird von Zeit zu Zeit von Forgejo aus gespiegelt |
| **F3** | Die Workflows ziehen schrittweise um, Dienst für Dienst |
| OTA | Die Firmware-Updates kommen künftig nicht mehr aus Azure Blob Storage |
| Bibliotheken | Die acht eigenen ESP32-Bibliotheken ziehen mit um |
| **F5** | Nur Code mit Git-Historie und die Pipelines ziehen um; keine Issues, PRs, Meilensteine. `thomas/SmartHomeTS` wird **leer und öffentlich** angelegt (korrigiert von „privat": die Instanz ist nur intern erreichbar), **Actions im Repo abgeschaltet**, danach Push von `main`. Bis zur Umstellung committet niemand auf Forgejo, das Nachziehen ist jeweils ein Fast-Forward |
| **F7** | Build-Muster A: Dienste nur für arm64, nativ auf den arm64-Runnern (Muster PV-Prognose), siehe Abschnitt 2 |
| **F8** | Tag-Schema: Images `1.1.<run>`, Firmware `0.1.<run>`, siehe Abschnitt 4 |

Die Schritte dieses Plans sind Issues in [thomas/SmartHomeTS](https://forgejo.intern/thomas/SmartHomeTS/issues), je Schritt eines; die Nummer `Uxx` steht im Issue-Titel. **Die Issues sind die Quelle für den Arbeitsstand**, dieses Dokument für Begründungen und Belege.

Was noch offen ist, steht als **Offen (Thomas)** im Text und gesammelt am Ende.

## Die fünf Befunde, die den Plan bestimmen

1. **Die Versionsfalle der Images gibt es nicht in der befürchteten Form.** Alle neun
   Dienste laufen mit der Strategie `newest-build`. Die sortiert nach dem **Build-Datum**
   im Image, nicht nach der Tag-Nummer. Ein `1.0.1` aus Forgejo würde also nicht
   ignoriert, sondern genommen. Mit dem neuen Image-Namen beginnt die Tag-Historie
   ohnehin neu. Die echte Falle sitzt woanders (Punkt 2).
2. **Die Falle sitzt bei der Firmware.** Die Geräte vergleichen die Version nur auf
   **Gleichheit** (`strcmp`). Forgejo zählt `github.run_number` **pro Repo**, nicht pro
   Workflow, und fängt bei 1 an. Die Geräte stehen heute auf `0.0.7` bis `0.0.79`. Trifft
   ein Forgejo-Lauf zufällig die Nummer, auf der ein Gerätetyp gerade steht, meldet das
   Gerät „Firmware is up to date" und übernimmt das Update **still nicht**. Deshalb
   braucht die Firmware ein neues Versionsschema, siehe [Versionsschema](#versionsschema).
3. **Forgejo nimmt `.forgejo/workflows/` und ignoriert dann `.github/workflows/`
   vollständig.** Damit ist der schrittweise Umzug einfach: Ein Workflow gehört genau dem
   System, in dessen Verzeichnis er liegt. Ein `git mv` pro Dienst ist der ganze
   Wechsel, und doppeltes Bauen ist ausgeschlossen, solange die Datei an genau einer
   Stelle liegt.
4. **Kurzform-Actions werden gegen `https://data.forgejo.org` aufgelöst.**
   `actions/setup-dotnet` gibt es dort nicht. Fünf Workflows scheitern daran, alle
   anderen benutzten Actions sind dort gespiegelt.
5. **Es gibt schon Vorbilder in Forgejo.** `thomas/PV-Prognose` baut `pv-forecast` auf dem
   arm64-Runner und pusht nach `forgejo.intern`. Der Cluster zieht das Image seitdem
   stündlich. `thomas/Einrohrheizung` baut Firmware mit PlatformIO in Forgejo. Beide
   haben Fallen schon ausgemessen, die hier sonst neu gelernt würden.

---

## 1. Inventar der 19 Workflows

Alle Workflows lösen auf `push` nach `main` aus, gefiltert über `paths:`. Die Filter
nennen immer die eigene Workflow-Datei und das Projektverzeichnis, dazu die geteilten
Bibliotheken, die das Projekt referenziert.

### Dienste (9) — Ziel Docker Hub `tschissler/<image>:1.0.<run>` plus `latest`

| Workflow | zusätzliche `paths:` | Dispatch | Jobs (`runs-on`) | Plattformen |
|---|---|---|---|---|
| `bmwconnector.yml` | `SharedContracts`, `Libs/HeartbeatLib` | ja | 1 Job, Test und Build zusammen (`ubuntu-latest`) | amd64+arm64 |
| `ChargingController.yml` | `SharedContracts`, `Libs/HeartbeatLib` | ja | `test` → `build` (`ubuntu-latest`) | amd64+arm64 |
| `EnphaseConnector.yml` | `Libs/HeartbeatLib`, `MQTTClient`, `SharedContracts`, `SmartHomeHelpers` | **nein** | 1 Job, kein Test | amd64+arm64 |
| `KebaConnector.yml` | wie Enphase, dazu `Libs/HelpersLib` | ja | `test` → `build` | amd64+arm64 |
| `RulesEngine.yml` | `MQTTClient`, `SmartHomeHelpers`, `SharedContracts`, `Libs/HeartbeatLib` | ja | `test` → `build` | **nur arm64** |
| `ShellyConnector.yml` | wie Enphase | ja | 1 Job, kein Test | amd64+arm64 |
| `SmartHome.DataHub.yml` | wie Enphase | ja | `test` → `build` | amd64+arm64 |
| `Smarthome.Web.yml` | `!SmartHome.Web/SmartHomeBlazorApp/**`, `SharedContracts`, `SmartHomeHelpers` | ja | 1 Job, kein Test | amd64+arm64 |
| `vwconnector.yml` | — | ja | 1 Job, kein Test, Kontext `./VWConnector` | amd64+arm64 |

Gemeinsam für alle neun:

- **Actions:** `actions/checkout@v4` (Enphase `@v3`), `docker/setup-qemu-action@v3`,
  `docker/setup-buildx-action@v3`, `docker/login-action@v3`, `docker/build-push-action@v6`.
  Die fünf Workflows mit Test benutzen zusätzlich `actions/setup-dotnet@v4`
  (BMW, ChargingController, Keba, RulesEngine, DataHub).
- **Secrets:** `DOCKER_HUB_USERNAME`, `DOCKER_HUB_ACCESS_TOKEN`.
- **Kontexte:** `github.run_number` (Tag und `BUILD_NUMBER`), `github.sha` (Kurz-SHA als
  `GIT_COMMIT`), `github.workflow` und `github.ref` (Gruppe von `concurrency`),
  `$GITHUB_OUTPUT`.
- **Cache:** `cache-from/cache-to: type=gha`, dazu `provenance: false`, `sbom: false`.

### Firmware (10) — Ziel Azure Blob plus retained MQTT-Nachricht

Jeder Firmware-Workflow hat zwei Jobs. `build` läuft auf `ubuntu-latest`, also auf
GitHub-Hardware: Er baut mit PlatformIO, prüft mit `check_firmware_size.py` und lädt mit
`az storage blob upload` nach `smarthomestorageprod/firmwareupdates` hoch.
`triggerupdate` läuft auf `[self-hosted, linux]`, also auf dem `github-runner` im Cluster,
und setzt `mosquitto_pub -h mosquitto.intern -p 1883 -t OTAUpdate/<Typ> -r` mit der
Blob-URL ab. Version `0.0.<run>`.

| Workflow | OTA-Topic | heute angeboten | Besonderheit |
|---|---|---|---|
| `HeatingFanControllerFirmware.yml` | `OTAUpdate/HeatingFanController` | `0.0.31` | |
| `HeatMeterSensorFirmware.yml` | `OTAUpdate/HeatMeter` | `0.0.11` | |
| `KellerdeviceFirmware.yml` | `OTAUpdate/KellerDevice` | `0.0.33` | Publish ohne `-q 2` (QoS 0) |
| `LEDStripeFirmware.yml` | `OTAUpdate/LEDStripe` | `0.0.59` | |
| `MixerControllerFirmware.yml` | `OTAUpdate/MixerController` | `0.0.7` | |
| `RelaisBoardFirmware.yml` | `OTAUpdate/Relaismodule` | `0.0.28` | |
| `SMLSensorFirmware.yml` | `OTAUpdate/SMLSensor` | `0.0.56` | zusätzlich Secret `METER_PINS` |
| `TemperatureDisplayFirmware.yml` | `OTAUpdate/TemperatureDisplay` | `0.0.31` | |
| `TemperatureSensor2Firmware.yml` | `OTAUpdate/TemperaturSensor2` | `0.0.56` | zwei Build-Jobs (esp32-c6, devkit-v4), URL mit Platzhalter `---board---` |
| `TemperatureSensorFirmware.yml` | `OTAUpdate/TemperaturSensor` | `0.0.79` | **kein** `workflow_dispatch` |

„Heute angeboten" ist der retained Wert auf dem Broker, gelesen am 2026-09-27 mit
`mosquitto_sub -t 'OTAUpdate/#' -W 4`.

Gemeinsam für alle zehn:

- **Actions:** `actions/checkout@v3`, `actions/setup-python@v5`.
- **Secrets:** `WIFI_PASSWORDS`, `AZURE_STORAGE_KEY`, bei SMLSensor zusätzlich `METER_PINS`.
- **Werkzeuge aus dem Runner-Image:** `az` (auf GitHub vorinstalliert), `mosquitto_pub`
  (installiert der `github-runner` beim Start per `postStart`).
- **Kontexte:** `github.run_number`, `$GITHUB_ENV`, `$GITHUB_OUTPUT`, `needs.<job>.outputs`.

### Warum die Firmware den `github-runner` braucht

Bestätigt: **nur wegen des MQTT-Publish ins LAN.** `mosquitto.intern` ist von
GitHub-Hardware aus nicht erreichbar. Der Build läuft auf GitHub, nur der Einzeiler mit
`mosquitto_pub` auf dem Runner im Cluster (`github-runner/smarthome-runner`, Image
`myoung34/github-runner:latest`, Labels `self-hosted,linux,kubernetes,smarthome`, läuft
auf `k3snode6`). Die README dort sagt es ausdrücklich, und das `postStart` installiert
genau `mosquitto-clients`.

---

## 2. Kompatibilität mit Forgejo Actions

Forgejo **15.0.9** (`/api/v1/version`: `15.0.9+gitea-1.22.0`), Runner
**v12.10.1** (aus einem Job-Log). Die Aussagen unten sind gegen den Quelltext von
`v15.0.9` und gegen echte Läufe dieser Instanz geprüft, nicht gegen die allgemeine Doku.

### Kurzform-Actions

`DEFAULT_ACTIONS_URL` ist in `Forgejo.yaml` nicht gesetzt, und der Pod hat keine
`GITEA__ACTIONS__*`-Variable. Der Default in `modules/setting/actions.go` (v15.0.9) ist
`https://data.forgejo.org`. **Belegt am laufenden System:** Das Log von
`PV-Prognose` Run 7 zeigt `git fetch 'https://data.forgejo.org/actions/checkout' # ref=v4`.

Auf `data.forgejo.org` gespiegelt (API, 2026-09-27):

| Action | Tag vorhanden | Ergebnis |
|---|---|---|
| `actions/checkout` | v3, v4 | läuft |
| `actions/setup-python` | v5 | läuft |
| `docker/setup-qemu-action` | v3 | läuft |
| `docker/setup-buildx-action` | v3 | läuft |
| `docker/login-action` | v3 | läuft |
| `docker/build-push-action` | v6 | läuft |
| **`actions/setup-dotnet`** | **Repo fehlt (404)** | **scheitert** |

Drei Auswege für `setup-dotnet`. Der erste ist der sauberste:

- **Job-Container `mcr.microsoft.com/dotnet/sdk:10.0`** statt `setup-dotnet`. Keine
  Action, kein Download pro Lauf, dieselbe SDK-Version wie im Dockerfile.
- Volle URL: `uses: https://github.com/actions/setup-dotnet@v4`. Forgejo erlaubt das. Der
  Preis ist eine Laufzeitabhängigkeit von github.com.
- `DEFAULT_ACTIONS_URL = github` instanzweit. Das ändert das Verhalten auch für
  `PV-Prognose` und `Einrohrheizung`, deshalb nicht empfohlen.

### Kontexte und Variablen

| | Forgejo 15 | Folge |
|---|---|---|
| `github.sha`, `github.ref`, `github.workflow` | vorhanden | keine |
| `$GITHUB_OUTPUT`, `$GITHUB_ENV`, `needs.*.outputs` | vorhanden (Einrohrheizung benutzt sie) | keine |
| `github.event.head_commit.message` | vorhanden (Einrohrheizung benutzt es) | keine |
| **`github.run_number`** | vorhanden, **aber pro Repo gezählt**, nicht pro Workflow | Versionen springen und beginnen bei 1, siehe [Versionsschema](#versionsschema) |
| `github.server_url` | die **Registrierungs-URL des Runners**, `http://forgejo-http.forgejo.svc.cluster.local:3000` | `checkout` klont clusterintern über HTTP, ganz ohne TLS (Log Run 7, Zeile 38). Deshalb braucht der Job-Container für den Checkout die interne CA nicht |
| `concurrency:` | unterstützt (Test `TestCancelPreviousWithConcurrencyGroup` in v15.0.9) | keine |
| `workflow_dispatch` | unterstützt | keine |
| `paths:` mit `!`-Negation | unterstützt (act `workflowpattern`) | unsicher, siehe [Unsichere Stellen](#unsichere-stellen) |
| `upload-/download-artifact@v4` | laut Kommentar in Einrohrheizung **nicht unterstützt** | hier nicht benutzt, keine Folge |

**`run_number` pro Repo ist belegt:** In `Einrohrheizung` tragen verschiedene Workflows
Läufe aus einer gemeinsamen Folge (69 CI-Image, 71 PlatformIO, 72 Docker-Build).

### Labels und Job-Images

| Label im Workflow | Forgejo-Runner heute | Ergebnis |
|---|---|---|
| `ubuntu-latest` | nur `act-runner-amd64`, Image `catthehacker/ubuntu:act-22.04` | läuft, aber alle Jobs auf **einem** Runner |
| `[self-hosted, linux]` | **kein Runner trägt `self-hosted`** | der Job bliebe für immer `waiting` |
| `arm64` | `act-runner-arm64` (2 Replicas), Image `catthehacker/ubuntu:act-22.04` | läuft (PV-Prognose) |
| `amd64` | `act-runner-amd64` | läuft |

`catthehacker/ubuntu:act-22.04` bringt Node, Python, git und Docker-CLI mit, aber **weder
`az` noch `mosquitto_pub`**. Beides muss der Workflow installieren oder ein eigenes
CI-Image mitbringen (Vorbild: `einrohr-pio-ci` in Einrohrheizung, spart laut Kommentar
etwa 1 GB Toolchain-Download pro Lauf).

### QEMU, Buildx und der Push nach `forgejo.intern`

Der Runner-Pod hat einen privilegierten `docker:dind`-Sidecar. Der Daemon vertraut der
Cluster-CA: Er hängt sie beim Start an `/etc/ssl/certs/ca-certificates.crt` an.

- **`docker build` + `docker push` über den Daemon funktioniert.** Das ist das Muster von
  PV-Prognose und Einrohrheizung.
- **`docker/build-push-action` mit `setup-buildx-action` scheitert voraussichtlich beim
  Push.** `setup-buildx-action` legt standardmäßig einen `docker-container`-Builder an. Der
  bringt einen eigenen BuildKit mit eigenem Push-Client, und der vertraut der CA **nicht**.
  So steht es wörtlich im Workflow `firmware-ci-image.yml` von Einrohrheizung: „der
  Daemon vertraut der self-signed *.intern-CA bereits; Buildkits eigener Push-Client
  nicht." Auswege: `driver: docker` (dann aber kein Multi-Arch ohne containerd-Store),
  oder die CA per `config-inline`/`buildkitd-config` in den Builder geben. Dafür muss sie
  erst in den Job-Container.
- **QEMU:** `setup-qemu-action` startet `tonistiigi/binfmt` privilegiert im dind. Das
  registriert die Emulatoren im **Kernel des Nodes** (`k3snode1/2`), nicht nur im Pod.
  Technisch geht das, ist aber ein Eingriff am Host, und es gibt nur einen amd64-Runner.
- **`cache-from/to: type=gha`** braucht den Cache-Dienst des Runners **und** muss ihn aus
  dem BuildKit-Container heraus erreichen. Das ist hier nicht erprobt. Mit `docker build`
  auf dem Daemon entfällt die Frage: Die Runner haben persistente Docker-Volumes (60 Gi
  amd64, 20 Gi arm64), der Layer-Cache bleibt also über Läufe hinweg erhalten.

### Build-Muster: entschieden A (F7)

**Alle neun Dienste laufen nur auf arm64.** Jede `values.yaml` setzt
`nodeSelector: kubernetes.io/arch: arm64`, und alle Pods stehen auf `k3snode4` bis `6`.
Das amd64-Image wird heute gebaut, aber nie gezogen.

| Variante | Preis |
|---|---|
| **A: nur arm64, nativ auf den arm64-Runnern, `docker build`/`push`** (Muster PV-Prognose und RulesEngine) — **gewählt** | kein amd64-Image mehr. Die Annotation `platforms` wird `linux/arm64`. Die arm64-Runner haben nur 1 CPU im dind-Limit, der Build ist dort langsamer als auf GitHub |
| B: Multi-Arch per QEMU auf dem amd64-Runner, Buildx mit CA-Konfiguration | CA in den Builder, binfmt im Node-Kernel, ein Runner für alle Builds. Emuliertes `dotnet publish` ist langsam |
| C: je Architektur nativ bauen, danach `docker buildx imagetools create` | zwei Jobs pro Dienst, Manifest zusammensetzen. Am meisten Arbeit |

**Warum A:** Sie ist auf dieser Instanz erprobt (PV-Prognose, MailSync). Sie umgeht alle
drei offenen Fragen aus dem Abschnitt davor: BuildKits Push-Client, binfmt im
Node-Kernel und den `gha`-Cache. Und sie baut nur, was tatsächlich gezogen wird. B und C
lösen ein Problem, das heute niemand hat.

**Was A kostet:**
- **Soll ein Dienst je auf `k3snode1/2` (amd64) laufen**, etwa weil die Pis ausfallen
  oder ein Dienst mehr Leistung braucht, gibt es dafür kein Image. Dann muss zuerst der
  Workflow auf B oder C umgebaut werden, dann `platforms` in der Annotation, dann der
  `nodeSelector`. Ein kurzfristiges Ausweichen auf die amd64-Nodes bei einem Ausfall
  der Pis ist damit nicht möglich. Heute ginge es theoretisch, weil das Image existiert,
  praktisch verhindert es aber schon der `nodeSelector`.
- Die Builds laufen auf den Pis langsamer (Messung beim Probelauf, [U20](https://forgejo.intern/thomas/SmartHomeTS/issues/12)).
- Die Docker-Hub-Images bleiben multi-arch. Der Rückweg eines Dienstes auf Docker Hub
  (Revert im Deployments-Repo) hat also weiter beide Architekturen.

### Wird der amd64-Runner noch gebraucht?

**Für die Images der Dienste nicht mehr.** Er bleibt aber nötig:

- **Firmware-Builds** sind auf `ubuntu-latest` (amd64) geplant, siehe
  [Abschnitt 8](#8-ota-weg-der-firmware). Ob PlatformIO mit den ESP32-Toolchains auch auf
  arm64 baut, habe ich nicht geprüft. Die Einrohrheizung baut ihre Firmware ebenfalls auf
  amd64.
- **Andere Repos:** `Einrohrheizung` (alle 7 Workflows) und `Grafana` (2 Workflows)
  laufen auf `ubuntu-latest`, also nur auf diesem Runner. `PV-Prognose` und `MailSync`
  laufen auf `arm64`. Gelesen über die API, 2026-09-27.
- **Test-Jobs der Dienste, optional:** `dotnet test` ist architekturunabhängig. Auf dem
  amd64-Runner (2 CPU statt 1) läuft er schneller und belegt keinen der zwei
  arm64-Plätze. Weil `build` mit `needs: test` ohnehin wartet, gewinnt man Zeit nur
  durch die schnellere Maschine. Vorschlag: Test-Job auf `ubuntu-latest` im Container
  `mcr.microsoft.com/dotnet/sdk:10.0`, Build-Job auf `arm64`. Nötig ist das nicht, der
  Test kann auch im arm64-Job laufen.

---

## 3. Runner

### Heute (`forgejo/runners.yaml` im Deployments-Repo, gehört gerade `runner-01-hostpath`)

| StatefulSet | Replicas | Nodes | Labels | dind-Limit | Docker-Volume |
|---|---|---|---|---|---|
| `act-runner-amd64` | 1 | `node-type=power` (k3snode1/2) | `amd64`, `ubuntu-latest` | 2 CPU / 4 Gi | 60 Gi `longhorn-single` |
| `act-runner-arm64` | 2 | arm64, nicht power (k3snode3–6) | `arm64` | 1 CPU / 2 Gi | 20 Gi `longhorn` |

`act-runner-config`: `container.network: host`, `default_image: catthehacker/ubuntu:act-22.04`,
`DOCKER_HOST=tcp://localhost:2375`. Keine `runner.capacity`, also der Default **1 Job pro
Runner**: insgesamt drei Jobs gleichzeitig.

### Kann ein Forgejo-Runner nach `mosquitto.intern` publizieren?

**Sehr wahrscheinlich ja, aber nicht getestet.**

- `network: host` setzt die Job-Container in den Netz-Namespace des dind, also des Pods.
  Der Pod erreicht das LAN, wie jeder andere Pod auch.
- Namensauflösung: CoreDNS hat eine Zone `intern:53 { forward . 10.43.11.255 }`
  (`coredns-custom`). Ein Pod löst `mosquitto.intern` also auf. Der Job-Container erbt die
  `resolv.conf` des dind-Containers.
- Es fehlt nur `mosquitto_pub` im Image (`apt-get install mosquitto-clients` im Job oder
  ein eigenes Image).

### Was der Umzug an den Runnern zusätzlich braucht (Vorschläge, nicht umgesetzt)

1. **Keins der Labels `self-hosted` nachbauen.** Die Firmware-Workflows bekommen beim
   Umzug ohnehin neue `runs-on`. Ein `self-hosted`-Label würde nur verschleiern, welcher
   Runner gemeint ist.
2. **Kapazität.** Eine Änderung an `SharedContracts` löst heute acht Dienst-Workflows
   aus, auf GitHub parallel. Auf drei Runnern mit je einem Platz stehen sie
   hintereinander. Entweder `runner.capacity: 2` auf den arm64-Runnern (dind-Limit
   beachten) oder hinnehmen. **Messen, bevor man es ändert:** Dauer eines Dienst-Builds
   auf dem arm64-Runner, beim Probelauf.
3. **Die CA im Job-Container**, falls ein Job selbst mit `forgejo.intern` über HTTPS redet
   (Bibliotheken holen, Firmware hochladen). Einfacher ist der clusterinterne Weg über
   HTTP, den auch `checkout` nimmt, siehe [Abschnitt 9](#9-die-esp32-bibliotheken).
4. **Beobachtung am Rande, nicht Teil dieses Umzugs:** `runner-data` ist ein `emptyDir`.
   Die Datei `.runner` überlebt also keinen Pod-Neustart, und `register` meldet den Runner
   bei jedem Neustart neu an. In Forgejo dürften sich dadurch verwaiste Runner-Einträge
   sammeln. Das betrifft `runner-01-hostpath`.

---

## 4. Tags, Image Updater und das Versionsschema

### Was der Image Updater tatsächlich tut

`argocd-image-updater` **v0.18.0**, Konfiguration über Annotations an den Applications.
Für alle neun Dienste gleich:

```yaml
argocd-image-updater.argoproj.io/image-list: <app>=tschissler/<image>
argocd-image-updater.argoproj.io/<app>.update-strategy: newest-build
argocd-image-updater.argoproj.io/<app>.allow-tags: regexp:^1\.[0-9]+\.[0-9]+$
argocd-image-updater.argoproj.io/<app>.platforms: linux/amd64,linux/arm64   # RulesEngine: linux/arm64
argocd-image-updater.argoproj.io/<app>.pull-secret: pullsecret:argocd/dockerhub-credentials
argocd-image-updater.argoproj.io/write-back-method: git
argocd-image-updater.argoproj.io/write-back-target: helmvalues:/<Verzeichnis>/values.yaml
```

`newest-build` wählt laut Doku von v0.18.0 das Image mit dem **jüngsten Build-Datum** aus
den Metadaten, „not the date of when the image was tagged or pushed". Bei gleichem Datum
entscheidet die lexikalische Sortierung. Die Nummer im Tag spielt für die Auswahl also
**keine Rolle**. Nur `allow-tags` muss passen.

**Daraus folgt:**

- Ein Forgejo-Tag `1.0.1` würde **nicht** ignoriert. Er wäre der jüngste Build und würde
  ausgerollt. Die befürchtete Falle („kleinere Nummer wird übersehen") tritt nicht ein.
- Mit F1 wechselt ohnehin der Image-Name (`forgejo.intern/thomas/<image>`). Der Updater
  beobachtet pro App genau einen Namen und sieht die Docker-Hub-Historie gar nicht mehr.
- Die Doku von v0.18.0 rät sogar **ab** von `newest-build` mit Docker Hub (Pull-Limits,
  weil für jeden Tag das Manifest geholt wird). Der Umzug beseitigt das nebenbei.

### Höchste Tags auf Docker Hub (API, 2026-09-27)

| Image | höchster Tag | Tags gesamt | in `values.yaml` |
|---|---|---|---|
| bmwconnector | 1.0.101 | 29 | 1.0.101 |
| chargingcontroller | 1.0.58 | 27 | |
| enphaseconnector | 1.0.52 | 24 | |
| kebaconnector | 1.0.42 | 26 | |
| rulesengine | 1.0.13 | 14 | 1.0.13 |
| shellyconnector | 1.0.50 | 23 | |
| smarthomedatahub | 1.0.90 | 33 | 1.0.90 |
| smarthomeweb | 1.0.189 | 39 | 1.0.189 |
| vwconnector | 1.0.41 | 20 | |

Die Zahl der Tags ist viel kleiner als die höchste Nummer. Alte Tags werden also schon
heute gelöscht, von wem, ist nicht erhoben.

### Versionsschema (entschieden, F8)

**Images: `1.1.<run>` (F8).** Für die Auswahl ist es gleichgültig (siehe oben).
Es spricht trotzdem einiges dafür:

- Es bleibt innerhalb von `allow-tags ^1\.[0-9]+\.[0-9]+$`. Die Grenze gegen einen Major
  aus `docs/update-strategie.md` muss also nicht mitgezogen werden. Ein `2.0.x` würde
  genau diese Grenze reißen und wäre falsch.
- Es sortiert nach semver über allen `1.0.x`. Wer die Heartbeat-Version liest oder im
  Deployments-Repo `git log` sieht, erkennt sofort, dass `1.1.7` neuer ist als `1.0.189`
  und aus Forgejo kommt.
- Es ist das Schema, das Einrohrheizung für Forgejo-Builds schon benutzt
  (`FIRMWARE_VERSION: 1.1.${{ github.run_number }}`).
- Der Preis: Die Nummern springen, weil Forgejo pro Repo zählt. `1.1.3` bei Web und
  `1.1.4` bei DataHub können aus zwei verschiedenen Pushes stammen. Nichts wertet
  Lücken aus.

**Firmware: `0.1.<run>` statt `0.0.<run>` — hier ist es Pflicht, keine Kosmetik.**

- Alle zehn Firmwares vergleichen die angebotene Version nur auf Gleichheit
  (`strcmp(version, updateVersion.c_str())`, LEDStripe mit `==`). Die Webseite `Devices`
  macht es genauso (`offered != card.Version`).
- Die Geräte stehen auf `0.0.7` bis `0.0.79`. Ein Forgejo-Lauf mit derselben Nummer würde
  vom Gerät als „up to date" verworfen. Weil Forgejo pro Repo zählt, landet jede Nummer
  zwischen 7 und 79 irgendwann bei irgendeinem Workflow.
- Ein neuer Präfix schließt das für immer aus. Ob `0.1`, `1.0` oder `1.1`, ist egal,
  solange er noch nie vergeben war.
- Die URL muss die Form `…_<version>.bin` behalten. Das Gerät zieht die Version mit
  `lastIndexOf('_')` und `lastIndexOf('.')` heraus, die Webseite mit
  `_([0-9]+(?:\.[0-9]+)+)\.bin`.

---

## 5. Registry-Umzug nach `forgejo.intern` (F1)

### Pull auf den Nodes

- **Es gibt keine `registries.yaml`.** Das Vertrauen kommt aus dem OS-Trust-Store:
  `Kubernetes/k3s/ansible/plays/trust-cluster-ca.yml` legt die Cluster-CA nach
  `/usr/local/share/ca-certificates/`, ruft `update-ca-certificates` und startet k3s neu.
  containerd nimmt die System-Roots. Die README nennt als Zweck ausdrücklich „Required for
  image pulls from `forgejo.intern`".
- **Nachgewiesen auf `k3snode5` und `k3snode6`** (arm64): `pv-forecast:1.0.7`
  (`pullPolicy: Always`, stündlich) und `graph-sync-service` liegen dort im Image-Cache.
  Der CronJob läuft seit Monaten grün.
- **Nicht nachgewiesen auf `k3snode3`, `k3snode4`** (arm64, dort laufen Dienste!) und auf
  den amd64-Nodes. Die Image-Liste im Node-Status ist auf 50 Einträge gekappt. Ihr
  Fehlen dort beweist also nichts. Test siehe [Unsichere Stellen](#unsichere-stellen).
- **Multi-Arch-Manifeste:** Die Forgejo-Registry nimmt OCI-Indizes an. Mit Muster A
  (nur arm64, F7) stellt sich die Frage nicht.

### Pull-Secrets

- **Die Pakete von `thomas` sind anonym lesbar.** Der anonyme Token-Flow
  (`/v2/token?scope=repository:thomas/<image>:pull`) liefert einen Token, mit dem
  `tags/list` für `pv-forecast` und `einrohrheizung-service` mit 200 antwortet. Ein
  `imagePullSecret` ist damit nicht nötig.
- Trotzdem liegt `forgejo-pull-secret` (`dockerconfigjson`) schon in den Namespaces
  `smarthome` und `argocd`, und `PVForecast/values.yaml` benutzt es. Es mitzunehmen kostet
  eine Zeile pro `values.yaml` und hält die Dienste lauffähig, falls die Sichtbarkeit von
  `thomas` einmal auf privat geht. **Entschieden (Thomas, U01): mitnehmen.** Die Charts
  aller neun Dienste reichen `.Values.imagePullSecrets` schon durch und stehen heute auf
  `imagePullSecrets: []`; daraus wird `imagePullSecrets: [{name: forgejo-pull-secret}]`.

### Image Updater

- **`registries.conf` kennt `forgejo.intern` schon**, mit `insecure: true` (keine
  TLS-Prüfung). **Dieser Inhalt steht nicht in Git.** Die ConfigMap
  `argocd-image-updater-config` selbst kommt leer aus dem Upstream-`install.yaml`, das
  `Kubernetes/k3s/ansible/plays/install-argocd.yml` anwendet (managedFields:
  `kubectl-client-side-apply` 2026-01-05, ohne `data`). Nur `data.registries.conf` kam von
  Hand per `kubectl patch` dazu (2026-05-10). Bei einem Neuaufbau fehlt also genau dieser
  Eintrag. **Entschieden (U02):** Er kommt als Patch-Datei plus Patch-Task in
  `install-argocd.yml` nach Ansible, nicht ins Deployments-Repo. Grund: `00-bootstrap` hat
  `prune: true`, ein Revert dort würde die ConfigMap löschen, und neben dem Upstream-apply
  gäbe es einen zweiten Besitzer.
- Credentials: `pullsecret:argocd/forgejo-pull-secret` existiert und wird von `pv-forecast`
  benutzt.
- **Umstellung je Dienst** in `<Dienst>.yaml` im Deployments-Repo:
  `image-list: <app>=forgejo.intern/thomas/<image>`,
  `pull-secret: pullsecret:argocd/forgejo-pull-secret`,
  `platforms: linux/arm64` (Muster A, F7). `allow-tags` bleibt.
- **Umstellung je Dienst** in `<Dienst>/values.yaml`: `image.repository` und
  `image.tag` auf den ersten Forgejo-Tag, dazu
  `imagePullSecrets: [{name: forgejo-pull-secret}]` (U01, siehe Pull-Secrets). **Der Tag
  muss existieren, bevor der Commit landet**, sonst geht der Pod in `ImagePullBackOff`.
- Falle aus `docs/update-strategie.md`: **Eine geänderte Root-`*.yaml` braucht einen
  Refresh von `00-bootstrap`**, sonst handelt der Updater weiter nach der alten
  Annotation. Er würde dann `tschissler/<image>`-Tags in eine `values.yaml` schreiben,
  deren `repository` schon auf Forgejo zeigt. Deshalb Annotation und `values.yaml` in
  **einem** Commit ändern und danach `00-bootstrap` synchronisieren.

### Henne-Ei: was nicht aus der Forgejo-Registry kommen darf

Forgejo braucht zum Starten Longhorn (PVC), `forgejo-postgres`, Traefik (Ingress),
cert-manager (Zertifikat `forgejo-tls`), CoreDNS und AdGuard (Auflösung von
`forgejo.intern`) und kube-vip. **Nichts davon und nichts von Forgejo selbst darf aus
`forgejo.intern` kommen.** Heute gilt das:

| Komponente | Image-Quelle heute |
|---|---|
| Forgejo | `codeberg.org/forgejo/forgejo:15.0.9` |
| Runner | `data.forgejo.org/forgejo/runner:12`, `docker:dind`, `node:22-bookworm` |
| Job-Default | `catthehacker/ubuntu:act-22.04` (Docker Hub) |
| ArgoCD, Image Updater | `quay.io/…` |
| kube-vip | `ghcr.io/kube-vip/…` |
| AdGuard, Mosquitto, Longhorn, Traefik, cert-manager | Docker Hub bzw. Upstream-Registries |

**Regel für die Zukunft:** Nur eigene Anwendungs-Images gehören nach `forgejo.intern`. Die
neun Dienste sind keine Voraussetzung für Forgejo, sie dürfen also umziehen. Ein eigenes
CI-Image für die Runner (etwa mit `mosquitto_pub` und PlatformIO) dürfte dort liegen. Es
wird nur gebraucht, wenn Forgejo läuft.

### Was bei einem Forgejo-Ausfall mit neu startenden Pods passiert

- Alle neun Dienste stehen auf `pullPolicy: IfNotPresent` mit exaktem Tag. **Startet ein
  Pod auf einem Node neu, der das Image im Cache hat, läuft er an.** Das gilt auch nach
  einem Stromausfall, der containerd-Cache liegt auf der Platte.
- **Wird ein Pod auf einen Node verschoben, der das Image nicht hat, bleibt er in
  `ImagePullBackOff`, bis Forgejo wieder steht.** Heute gilt dasselbe bei einem
  Docker-Hub-Ausfall. Docker Hub fällt aber seltener aus als ein Dienst im eigenen
  Cluster, der auf einem einzigen Node (`k3snode1`) mit Longhorn-Volume läuft.
- `pv-forecast` hat `pullPolicy: Always` und scheitert schon heute bei jedem
  Forgejo-Ausfall stündlich.
- Ein Kaltstart des ganzen Clusters verlängert sich um die Zeit, bis Forgejo samt
  Postgres und Longhorn steht. Für den ChargingController heißt das: keine Regelung in
  dieser Zeit, sofern er auf einem Node ohne Image landet.
- Abhilfe, falls nötig: Die Dienste nach dem Umzug einmal auf jedem arm64-Node ziehen
  lassen (der Cache wärmt sich dann über die Zeit von allein), oder die kritischen
  Dienste (ChargingController) zusätzlich weiter auf Docker Hub spiegeln. **Entschieden
  (Thomas, U01): vorerst nein**, keine zusätzliche Spiegelung, auch nicht für den
  ChargingController. Das Risiko oben wird bewusst getragen.

### Platz

Das Forgejo-Volume (`gitea-shared-storage`, 50 Gi) meldet in Longhorn 22 GB
`actualSize`. Das ist eine obere Schranke, Snapshots zählen mit. Ein Dienst-Image ist auf
Docker Hub etwa 95–110 MB groß (pro Architektur, komprimiert). Die Basis-Layer teilen sich
die .NET-Dienste, und die Forgejo-Registry speichert Blobs nach Hash nur einmal. Pro
Build kommen also nur die geänderten Layer dazu. **Nötig ist trotzdem eine
Aufräumregel** (Forgejo: Einstellungen → Pakete → Bereinigungsregeln, z. B. „die letzten
10 Versionen behalten"), und das Volume wächst im nächtlichen Velero-Backup mit.

---

## 6. Weitere Abhängigkeiten von GitHub

Grep über das ganze Repo ohne `Depricated/`, `.pio/`, `bin/`, `obj/`, dazu das
Deployments-Repo und die anderen Forgejo-Repos.

| Fundstelle | Art | Folge beim Umzug |
|---|---|---|
| `README.md` Z. 3–7 | fünf Workflow-Badges `github.com/…/actions/workflows/*.yml/badge.svg` | zeigen nach dem Umzug alte Stände; Forgejo-Badges wären nur im LAN sichtbar. Entfernen oder stehen lassen, **Offen (Thomas)** |
| `README.md` Z. 55, 73, 94; `RelaisBoard.Firmware/README.md` Z. 79 | Links `github.com/tschissler/SmartHomeTS/tree/main…` | funktionieren weiter, solange GitHub gespiegelt wird |
| `README.md` Z. 57, 69 | Bilder unter `github.com/user-attachments/…` | liegen bei GitHub, funktionieren weiter, solange das Repo existiert |
| 10 × `platformio.ini` | eigene Bibliotheken `github.com/tschissler/ESP32_*.git`, **unversioniert** | siehe [Abschnitt 9](#9-die-esp32-bibliotheken) |
| 6 × `platformio.ini` | `platform = https://github.com/pioarduino/…/platform-espressif32.zip` | fremd, bleibt, braucht Internet im Runner |
| `TemperatureDisplay.Firmware/platformio.ini` | `arduino-esp32.git`, `lvgl.git#v9.3.0` u. a. | fremd, bleibt |
| 10 Firmware-Workflows | `smarthomestorageprod.blob.core.windows.net` | siehe [Abschnitt 8](#8-ota-weg-der-firmware) |
| Deployments: `GitHubRunner.yaml`, `github-runner/` | der Runner selbst, mit PAT (`github-runner-secret`) | abbauen, wenn die letzte Firmware umgezogen ist |
| Deployments: `github-runner/runner-secret.yaml` | Secret-Manifest im Repo | Inhalt **nicht angesehen** (Secrets suche ich nicht). Prüfen, ob dort ein echter PAT steht; das Repo ist anonym lesbar |
| Deployments: `SmartHomeDeployments.code-workspace`, `SmartHomeTS.code-workspace` | Pfade `../../../GitHub/tschissler/SmartHomeTS/…` (Windows-Arbeitsplatz, auf diesem Rechner schon heute tot) | Entschieden (Thomas, F34): **löschen**. Das geschieht im Deployments-Repo im Zuge von umzug-01 (`e67abc8`, Stand 2026-09-27 noch nicht gepusht) |
| Deployments: `CLAUDE.md`, `docs/gitops-grenze.md`, `docs/update-strategie.md` | Pfad `~/Repos/GitHub/SmartHomeTS/Kubernetes/k3s/ansible/` | Der Klon ist umgezogen (Abschnitt 12). Die drei Pfade werden im Deployments-Repo umgestellt, im Zuge von umzug-01 (Branch dort, Stand 2026-09-27 noch nicht gepusht) |
| **Forgejo `thomas/Einrohrheizung`** | klont `github.com/tschissler/SmartHomeTS` im Firmware-Workflow; pinnt `ESP32_ESP32Helpers` und `ESP32_WifiLib` per SHA **auf GitHub** | Fremdes Repo, hängt an beiden GitHub-Quellen. Der Klon von SmartHomeTS ist tot (`SHARED_LIBS_PATH` zeigt auf `ESP32Firmwares/SharedLibs`, das es nicht mehr gibt), die zwei Bibliotheken aber nicht. Die GitHub-Kopien der Bibliotheken dürfen erst weg, wenn Einrohrheizung umgestellt ist |
| `Kubernetes/k3s/ansible/plays/install-argocd.yml` | `raw.githubusercontent.com/argoproj/…` | fremd, bleibt |
| Dependabot | **keins** (`.github/` enthält nur `workflows/` und `copilot-instructions.md`) | keine |
| ArgoCD / Image Updater | **kein Bezug auf GitHub**; alle `repoURL` des Deployments-Repos zeigen schon auf Forgejo | keine |
| GitHub-Repo selbst | öffentlich, 15 offene Issues, 0 offene PRs, 1 Fork, 16 Sterne | Issues bleiben auf GitHub (F5) |
| **Arbeitsplatz** | Klon unter `~/Repos/GitHub/SmartHomeTS`. **Das Memory-Verzeichnis von Claude Code hängt am Pfad** (`~/.claude/projects/-home-thomas-Repos-GitHub-SmartHomeTS/`), ebenso `.claude/settings.local.json` und diese Skills | Ursprüngliche Empfehlung: Verzeichnis nicht umbenennen, nur `origin` umstellen. **Entschieden anders (Thomas, 2026-09-27): neu geklont** nach `~/Repos/Forgejo.intern/SmartHomeTS`, `origin` = Forgejo. Gründe: Der alte Pfad wäre irreführend, und beide Klone sollen übergangsweise nutzbar sein. Memory und die ignorierten Dateien (SMLSensor-`.env`, drei `settings.local.json` in Unterordnern) sind kopiert. Regel für die zwei Klone in [Abschnitt 12](#12-entscheidungen) |
| `CLAUDE.md`, `Docs/Parallel-Arbeiten.md`, Skills `worker` und `orchestrator` | beschreiben CI als GitHub, zählen `paths:` in `.github/workflows` | Während des Übergangs gelten **zwei** Verzeichnisse. Die Rollout-Zählung muss beide durchsehen (U12) |

---

## 7. Secrets

**Nur Namen, keine Werte.**

| Secret (GitHub, Repo-Ebene) | benutzt von | in Forgejo | nach dem Umzug auf GitHub löschbar |
|---|---|---|---|
| `DOCKER_HUB_USERNAME`, `DOCKER_HUB_ACCESS_TOKEN` | 9 Dienst-Workflows | **nicht nötig** (F1) | ja, sobald der letzte Dienst umgezogen ist. Danach den Token auch auf Docker Hub widerrufen |
| `AZURE_STORAGE_KEY` | 10 Firmware-Workflows | **nicht nötig**, wenn der OTA-Weg vor dem Firmware-Umzug wechselt (siehe [Abschnitt 8](#8-ota-weg-der-firmware)) | ja, sobald die letzte Firmware umgezogen ist; Schlüssel in Azure rotieren |
| `WIFI_PASSWORDS` | 10 Firmware-Workflows | **Repo** `thomas/SmartHomeTS` | ja, nach der letzten Firmware |
| `METER_PINS` | SMLSensor | **Repo** `thomas/SmartHomeTS` | ja, nach SMLSensor |
| (neu) `REGISTRY_USERNAME`, `REGISTRY_TOKEN` | Push nach `forgejo.intern` | **Benutzer-Ebene** `thomas`: dieselben Werte brauchen PV-Prognose und Einrohrheizung schon (dort unter denselben Namen) | — |
| (neu) Token für Paket-Upload der Firmware | Upload in die Generic-Registry | derselbe `REGISTRY_TOKEN`, Scope `write:package` | — |
| (neu) GitHub-PAT für den Push-Spiegel | Forgejo → GitHub | liegt in der Spiegel-Konfiguration des Repos, nicht als Actions-Secret | — |
| `github-runner-secret` (K8s, Namespace `github-runner`) | GitHub-PAT des Runners | — | mit dem Runner abbauen, PAT auf GitHub widerrufen |
| `dockerhub-credentials` (K8s, Namespace `argocd`) | Image Updater | — | nach dem letzten Dienst entfernbar |

Instanz-Ebene braucht nichts. Organisationen gibt es auf dieser Instanz nicht, alles liegt
unter dem Benutzer `thomas`.

**Die Werte ziehen nicht mit.** GitHub gibt Secret-Werte über keine Schnittstelle heraus.
Für `WIFI_PASSWORDS` und `METER_PINS` braucht Thomas die Originale, `REGISTRY_TOKEN` gibt
es schon (PV-Prognose, Einrohrheizung), die Docker-Hub- und Azure-Werte braucht Forgejo
nicht.

### Was sonst am GitHub-Repo hängt und nicht im Git steht

Ohne Token nicht einsehbar (die API antwortet auf `actions/runners` mit 401). Deshalb
Fragen an Thomas (U07), beantwortet am 2026-09-27, soweit nicht als offen markiert:

| Was | Frage | Antwort |
|---|---|---|
| Secret-Werte | Liegen die Originale von `WIFI_PASSWORDS` und `METER_PINS` außerhalb von GitHub vor? | **Offen (Thomas)** |
| Webhooks | Gibt es Webhooks am GitHub-Repo (Settings → Webhooks), etwa zu Docker Hub, Azure oder einem Chat? | keine |
| Branch-Schutz | Ist `main` auf GitHub geschützt? Ein Schutz gegen Force-Push lässt den Push-Spiegel scheitern | keiner — der `--mirror`-Spiegel kann pushen |
| Deploy-Keys | Gibt es Deploy-Keys (Settings → Deploy keys)? Wer benutzt sie? | keine |
| Actions-Variablen | Gibt es neben den Secrets auch `vars.*`? In den Workflows wird keine benutzt | keine |
| Registrierte Runner | Außer `smarthome-runner` weitere Self-hosted-Runner? | nur `smarthome-runner` |
| GitHub-Apps / Integrationen | Copilot (`.github/copilot-instructions.md` liegt im Repo), sonst etwas? | nicht gefragt worden, offen |
| Umgebungen | Environments mit eigenen Secrets? In den Workflows steht kein `environment:` | keine |
| Runner-Secret im Deployments-Repo | Enthält `github-runner/runner-secret.yaml` einen echten PAT? (siehe Abschnitt 6) | **Offen (Thomas)** |

**Externe Verweise auf die GitHub-URL**, soweit ohne Token sichtbar: der Workflow von
`thomas/Einrohrheizung` (klont SmartHomeTS, zieht zwei Bibliotheken), `REPO_URL` des
`github-runner`, die README-Links, ein Fork, die `lib_deps` in anderen Projekten
(nur Einrohrheizung gefunden, andere Repos von Thomas nicht durchsucht).

**Sicherheitshinweis, unabhängig vom Umzug:** `WIFI_PASSWORDS` wird per
`-D WIFI_PASSWORDS=\"${sysenv.WIFI_PASSWORDS}\"` als String in die Firmware kompiliert. Die
`.bin`-Dateien liegen in Azure **anonym abrufbar** (die Geräte laden ohne Anmeldung,
`HEAD` auf `SMLSensorFirmware_0.0.56.bin` liefert 200), und die URLs sind vorhersagbar.
Die WLAN-Passwörter stehen damit sehr wahrscheinlich öffentlich im Internet. Nachgesehen
habe ich das bewusst nicht. Der OTA-Umzug nach `forgejo.intern` (nur LAN) schließt das
für neue Builds. Die alten Blobs bleiben liegen, bis jemand sie löscht.

---

## 8. OTA-Weg der Firmware

### Heute

1. Der Workflow lädt `<Name>_0.0.<run>.bin` mit `az storage blob upload` hoch.
2. `mosquitto_pub -r` setzt die Blob-URL retained auf `OTAUpdate/<Typ>`.
3. Das Gerät vergleicht die Version aus der URL mit der eigenen. Bei Ungleichheit ruft
   es `AzureOTAUpdater::UpdateFirmwareFromUrl` → `HttpsOTA.begin(url, server_certificate)`.
4. `server_certificate` ist **genau ein** Root-Zertifikat: DigiCert Global Root G2, fest in
   der Bibliothek `ESP32_OTAUpdate` (`src/AzureOTAUpdater.cpp`).

### Welche Firmware welche Fassung der OTA-Bibliothek benutzt

- **Alle zehn** holen `https://github.com/tschissler/ESP32_OTAUpdate.git` über `lib_deps`,
  unversioniert. Das Repo hat einen einzigen Commit (`67d8973`, 2026-02-15) und keine Tags.
- **`HeatmeterSensor.Firmware/lib/OTAUpdater/` ist eine tote Kopie.** Sie kennt weder
  `ExtractVersionFromUrl` noch ein `int` als Rückgabe von `CheckUpdateStatus`. Beides
  benutzt `main.cpp` aber. Die `firmware.map` des letzten lokalen Builds (2026-08-30)
  enthält `ExtractVersionFromUrl`, gebaut wurde also die Fassung aus `lib_deps`. Die
  Kopie sollte weg, damit niemand an ihr die Zertifikatsänderung macht.
- `HeatmeterSensor.Firmware/lib/MQTTClientLib/` ist eine zweite lokale Kopie neben
  `lib_deps`. Welche gebaut wird, habe ich nicht belegt.
- `Kellerdevice.Firmware/src/AzureRootCert.h` enthält ebenfalls ein Zertifikat, wird aber
  nirgends eingebunden (nur `Template.Firmware` bindet seine eigene Kopie ein).
- Sonst gibt es keine lokalen Bibliothekskopien in `lib/`: nur
  `TemperatureDisplay.Firmware/lib/{lvgl_port,temperature_display,ui}` und
  `TemperatureMiniDisplay.Firmware/lib/Display_ST7789`, alle projekteigen.

### Die Hauptfalle: das eingebaute Zertifikat

Ein Gerät, das nur DigiCert vertraut, kann von `forgejo.intern` (Cluster-CA) nicht laden.
**Das Update, das der Firmware die Cluster-CA beibringt, muss deshalb noch über Azure
kommen.**

- `HttpsOTA.begin` reicht den PEM-String an `esp_https_ota` weiter. mbedTLS
  (`mbedtls_x509_crt_parse`) liest mehrere hintereinandergehängte PEM-Blöcke. **Zwei
  CAs in einem String** sind also der einfachste Weg: DigiCert G2 + „SmartHome Cluster CA"
  (RSA 4096, gültig bis 2036-02-04). Kostet rund 2 KB Flash.
- `check_firmware_size.py` entscheidet, ob das bei jedem Gerät passt. LEDStripe hatte
  schon einmal 108 % (FastLED-Falle in `CLAUDE.md`), also vor dem Rollout für alle zehn
  bauen und die Reserve ansehen.
- Die Geräte lösen `.intern` schon heute auf (`mosquitto.intern`). Für `forgejo.intern`
  braucht es nichts Neues.

### Ablageort in Forgejo

| | Generic Package Registry | Release-Assets |
|---|---|---|
| URL-Form | `https://forgejo.intern/api/packages/thomas/generic/<Paket>/<Version>/<Name>_<Version>.bin` | `https://forgejo.intern/thomas/<Repo>/releases/download/<Tag>/<Name>_<Version>.bin` |
| `…_<version>.bin` am Ende | ja | ja |
| anonym ladbar | **ja, geprüft U05** (siehe unten). Die Doku-Aussage „Downloads require … authentication" trifft auf dieser Instanz nicht zu | ja: das Repo ist öffentlich (F5) |
| Hochladen | ein `curl -X PUT` mit Token | Release anlegen, dann Asset hochladen; ein Tag pro Firmware-Version |
| Überschreiben | 409, gleicher Name zweimal geht nicht | Asset löschen und neu |
| Aufräumen | Bereinigungsregeln für Pakete (z. B. letzte 5 behalten) | von Hand oder per API |
| TemperatureSensor2 | ein Paket pro Board, `---board---` im Paketnamen erlaubt (`-` ist zulässig) | Asset-Name mit Board |

**Bewertung, neu nach der F5-Korrektur (Repo öffentlich).** `HttpsOTA` kann keine
Anmeldedaten mitschicken, beide Wege müssen also anonym ladbar sein.

- **Anonym ladbar:** Release-Assets eines öffentlichen Repos sicher, die Generic Registry
  ebenfalls, nachgewiesen in [U05](https://forgejo.intern/thomas/SmartHomeTS/issues/5)
  (Ergebnis unten).
- **URL-Form:** Beide enden auf `…_<version>.bin`, das Gerät und die Webseite lesen die
  Version also richtig. Release-Assets brauchen einen Tag je Firmware und Version
  (z. B. `fw/SMLSensor/0.1.123`). Bei zehn Gerätetypen wächst die Tag-Liste des
  Hauptrepos schnell, und jeder Tag wird über den Push-Spiegel auch auf GitHub sichtbar.
  Die Generic Registry braucht weder Tags noch Releases.
- **Aufräumen:** Die Generic Registry hat Bereinigungsregeln (z. B. „letzte 5 behalten").
  Release-Assets muss man per API oder von Hand löschen, samt Tags.
- **Hochladen:** Generic ist ein `curl -X PUT`. Release heißt: Tag anlegen, Release anlegen,
  Asset hochladen, also drei Aufrufe.

**Entschieden: Generic Registry** (nach U05), wegen Aufräumen und sauberer Tag-Liste.
Release-Assets bleiben der Ausweg, falls sich an der Sichtbarkeit etwas ändert.

**Ergebnis U05** (Test-Paket `ota-test`, umzug-02-proben, 2026-09-27):

- **Anonym ladbar:** 200, keine Weiterleitung, `Content-Length` gesetzt. Ein nicht
  vorhandener Pfad liefert 404, nicht 401.
- **Die Sichtbarkeit hängt am Benutzer, nicht am Repo** (`repository: null`, Owner
  `public`). **Auflage: `thomas` bleibt öffentlich**, sonst verlangt jeder OTA-Download
  eine Anmeldung, die `HttpsOTA` nicht schicken kann.
- **Kein 302**, weil kein S3-Store mit `SERVE_DIRECT` konfiguriert ist. Wird der
  Paketspeicher je auf MinIO mit Direktauslieferung umgestellt, **muss U05 neu laufen**:
  Dann leitet Forgejo auf eine andere Adresse mit anderem Zertifikat um.
- **TLS-Kette:** Der Server liefert nur das Leaf (SAN `forgejo.intern`, Issuer „SmartHome
  Cluster CA", 90 Tage Laufzeit). Das Gerät braucht die **CA als Root**, nicht das Leaf,
  sonst scheitert es nach jeder Zertifikatserneuerung.
- **Dieselbe Version zweimal hochladen ergibt 409.** Die CI braucht eine eindeutige
  Version je Lauf, `run_number` leistet das.

**HTTP statt HTTPS im LAN?** Bewertet, nicht empfohlen. Es entfiele die ganze CA-Frage,
aber:

- `CONFIG_ESP_HTTPS_OTA_ALLOW_HTTP` ist in den vorkompilierten Arduino-Bibliotheken
  **nicht gesetzt** (geprüft in `framework-arduinoespressif32-libs/esp32` und `esp32c6`,
  `sdkconfig`). `HttpsOTA` lehnt `http://` also ab. HTTP hieße: eine andere OTA-Bibliothek
  (etwa `HTTPUpdate`), also ein Umbau aller zehn Firmwares, nicht nur eine andere URL.
- Jeder im WLAN könnte dann eine Firmware unterschieben, die das Gerät ungeprüft
  flasht.
- Das Zwischen-Update über Azure bräuchte es trotzdem, nur mit anderem Inhalt.

### Reihenfolge des OTA-Wechsels

Diese Reihenfolge ist zwingend. Ein Gerät, das Schritt O2 verpasst, ist nach dem
Abschalten von Azure nur noch per Kabel erreichbar.

| Schritt | Was | löst aus | Rückweg |
|---|---|---|---|
| O1 | `ESP32_OTAUpdate`: zweite CA an den PEM-String hängen, **Tag setzen** (noch auf GitHub, siehe [Abschnitt 9](#9-die-esp32-bibliotheken)) | nichts | Revert |
| O2 | Alle zehn Firmwares pinnen `ESP32_OTAUpdate` auf diesen Tag. Sie bauen **noch auf GitHub** und laden **noch nach Azure** | 10 OTA-Rollouts über Azure, wie heute | Vorversion retained nachsenden |
| O3 | **Nachweis, dass jedes Gerät die O2-Fassung fährt** (siehe unten), außer den fünf Ausnahmen nach F40. Erst danach weiter | nichts | — |
| O4 | Ein Gerätetyp als Probe: Workflow nach `.forgejo/workflows/`, Upload in die Generic-Registry, retained URL auf `forgejo.intern`. Ein Gerät dieses Typs liegt dabei am Tisch am Seriell-Monitor | 1 OTA-Rollout über Forgejo | alte Azure-URL retained nachsenden; das Gerät vertraut beiden CAs |
| O5 | Die übrigen neun Gerätetypen, einzeln | je 1 OTA-Rollout | wie O4 |
| O6 | Azure: Blobs löschen, Speicherkonto kündigen, Schlüssel und Secrets entfernen. **Erst nach einer Karenzzeit**, in der kein Rückweg mehr gebraucht wurde | nichts | keiner mehr |

### Woran man den Firmware-Stand jedes Geräts erkennt

- **Heartbeat** auf `status/<Ort>/<Geraetetyp>/<Name>`, Feld `version`
  (`Docs/Service-Heartbeat.md`). Die Seite **Devices** in SmartHome.Web vergleicht genau
  das mit dem retained Angebot auf `OTAUpdate/<Typ>` und zählt „veraltet"
  (`IsOutdated`: `offered != card.Version`).
- Geräte mit altem Format melden die Version auf `meta/…` (von der Web-App als
  `LegacyDevices` geführt).
- **Die Lücke:** Ein Gerät, das gerade aus ist oder keinen Heartbeat sendet, taucht dort
  nicht als „veraltet" auf, sondern gar nicht. Für O3 braucht es deshalb eine **Soll-Liste
  aller Geräte** (Typ, Ort, Name), gegen die abgehakt wird. Nicht nur „keins meldet sich
  veraltet". Die Liste ist mit U08 aufgestellt und liegt in
  [Issue #8](https://forgejo.intern/thomas/SmartHomeTS/issues/8) (nicht hier: sie wird
  gepflegt, und das Repo wird öffentlich gespiegelt).

**Ausnahmen vom O3-Nachweis (U33), entschieden F40.** Fünf Geräte sind schon heute nicht
auf der angebotenen Fassung und werden nicht abgewartet: `Heatmeter_M1` (Typ HeatMeter,
U41), Heizkörperlüfter Esszimmer M3 und Kinderzimmer M1 (HeatingFanController, U43),
`Brutkasten` (TemperaturSensor, U39) und `Relaismodule_M1_OG` (Relaismodule,
Brownout-Schleife, Issue #44). Jedes bekommt ein eigenes Reparatur-Issue für die Zeit
nach dem Umzug.

**Der Preis:** Diese Geräte fahren eine Firmware, die nur DigiCert vertraut. Sobald
`OTAUpdate/<Typ>` ihres Typs auf `forgejo.intern` zeigt, bekommen sie **per OTA keine neue
Fassung mehr**, sondern nur noch **per Kabel**. Bis O6 gäbe es theoretisch den Rückweg,
die Azure-URL für den Typ noch einmal retained nachzusenden — das stuft aber alle anderen
Geräte des Typs mit zurück, weil die Firmware nur auf Ungleichheit prüft. Praktisch gilt
also: Kabel ab der Umstellung des Typs, spätestens ab O6 ausschließlich.

**Solange ihr Typ noch auf Azure zeigt, heilen sie sich selbst:** Wird ein solches Gerät
zwischen U32 und der Umstellung seines Typs eingeschaltet oder neu gestartet, holt es
sich die Fassung mit beiden CAs von allein. Das betrifft den Brutkasten (nur offline,
F39) und genauso Heatmeter und Heizkörperlüfter, denen laut Stand 2026-08-30 ein
Stromreset fehlt. Für diese vier lohnt der Handgriff **vor** U39, U41 bzw. U43.

### Laufzeitabhängigkeit nach dem Wechsel

- Im Normalbetrieb braucht ein Gerät Forgejo **nicht**. Es lädt nur, wenn die angebotene
  Version von der eigenen abweicht.
- Startet ein Gerät während eines Forgejo-Ausfalls neu, bekommt es die retained URL. Steht
  es schon auf dieser Version, passiert nichts. Steht es darunter, schlägt der Download
  fehl, `esp_https_ota` schreibt nur in die inaktive Partition, das Gerät läuft auf der
  alten Firmware weiter. Nach einem Fehlschlag verhalten sich die Firmwares
  unterschiedlich (RelaisBoard setzt `otaStarted` zurück, SMLSensor liest den Status
  jede Runde). Einen neuen Versuch gibt es erst, wenn die retained Nachricht erneut
  ankommt, also beim nächsten Reconnect. **Nicht pro Firmware geprüft.**
- Gegenüber heute tauscht man die Abhängigkeit von Azure gegen die von einem Dienst im
  eigenen Cluster. Der ist öfter weg, aber nur im LAN sichtbar, und ohne laufende Kosten.

---

## 9. Die ESP32-Bibliotheken

### Bestand (nachgezählt über alle `platformio.ini`, die Zahlen der Orchestrierung stimmen)

| Bibliothek | Firmwares | Tags auf GitHub | zuletzt gepusht |
|---|---|---|---|
| `ESP32_WifiLib` | 10 | 0 | 2026-06-21 |
| `ESP32_MQTTClientLib` | 10 | 0 | 2026-08-30 |
| `ESP32_OTAUpdate` | 10 | 0 | 2026-02-15 |
| `ESP32_ESP32Helpers` | 9 (alle außer HeatmeterSensor) | 0 | 2026-02-12 |
| `ESP32_Colors` | 2 (TemperatureSensor2, Kellerdevice) | 0 | 2026-02-15 |
| `ESP32_Sensors` | 2 (HeatingFanController, TemperatureSensor2) | 0 | 2026-02-15 |
| `ESP32_LEDLib` | 1 (HeatmeterSensor) | 0 | 2026-02-15 |
| `ESP32_TFTDisplay` | 1 (TemperatureSensor) | 0 | 2026-02-15 |

Alle acht sind öffentlich. **Ein weiterer Nutzer außerhalb dieses Repos:**
`thomas/Einrohrheizung` pinnt `ESP32_ESP32Helpers` und `ESP32_WifiLib` per SHA auf
GitHub. Das ist schon das Muster, das hier vorgeschlagen wird, samt Preis: „Update = SHA
bumpen" steht dort im Kommentar.

`DS18B20Monitor`, `TemperatureMiniDisplay`, `Template`, `TemperatureCalibrator` und
`IR-Tester` benutzen keine der acht und haben keinen Workflow. **`WebRadio` ist deprecated
(F38) und liegt jetzt unter `Depricated/WebRadio`.** Es bindet zwar `AzureOTAUpdater.h`
ein, zieht die Bibliothek aber nicht per `lib_deps` (Überrest der früheren
`SharedLibs`). Die Soll-Liste für U33 (U08, Issue #8) zählt es nicht.

### Holen im CI

- Der lokale Build auf diesem Rechner holt von `https://forgejo.intern` ohne Zutun: Die
  Cluster-CA steht im System-Trust-Store (`curl https://forgejo.intern/…` klappt hier ohne
  `-k`), und PlatformIO klont Git-Bibliotheken mit dem System-`git`. Auf dem
  Windows-Arbeitsplatz (Pfade `C:\Users\ThomasSchissler\…` im Repo) muss die CA im
  Windows-Store bzw. in `git config http.sslCAInfo` stehen. **Nicht geprüft.**
- Im Job-Container (`catthehacker/ubuntu:act-22.04`) fehlt die CA. Zwei Wege:
  - **clusterintern über HTTP**, wie `checkout` es schon tut:
    `git config --global url."http://forgejo-http.forgejo.svc.cluster.local:3000/".insteadOf "https://forgejo.intern/"`
    als erster Schritt im Job. `platformio.ini` behält die `https://forgejo.intern/…`-URL
    für den lokalen Build.
  - die CA in den Job-Container legen und `GIT_SSL_CAINFO` setzen.
- Öffentliche Repos brauchen keine Anmeldung. Private bräuchten ein Token im
  `insteadOf`, damit landet es in der Git-Konfiguration des Jobs.

### Versionen pinnen

Heute ist alles unversioniert. Jeder CI-Lauf nimmt den aktuellen HEAD jeder Bibliothek,
das ist dieselbe offene Spanne wie bei der FastLED-Falle. Lokal kommt noch hinzu:
PlatformIO aktualisiert eine einmal installierte Git-Bibliothek nicht von selbst. Der
lokale Build kann also auf einem älteren Stand bauen als die CI.

Vorschlag: **Tags** in den Bibliotheken (`v1.0.0` …) und `…/ESP32_OTAUpdate.git#v1.0.0`
in `lib_deps`. Tags statt SHAs, weil man sie im Diff lesen kann.

Preis: Jede Bibliotheksänderung braucht einen Bump in bis zu **zehn** `platformio.ini`.
Jeder Bump löst den Workflow der Firmware aus, also einen OTA-Rollout pro Gerätetyp. Das
ist gewollt, heute rollt eine Bibliotheksänderung dagegen **gar nicht** aus, bis die
Firmware aus anderem Grund neu baut.

### Verlauf und Spiegel

Forgejo migriert Repos mit Historie und Tags (Neues Repository → Migration → GitHub).
Varianten für danach, **Offen (Thomas):**

- zurückspiegeln nach GitHub (Push-Spiegel wie beim Hauptrepo). Einrohrheizung kann dann
  unverändert weiterbauen.
- GitHub-Repos archivieren. Einrohrheizung muss vorher umgestellt sein, sonst bricht
  dort ein künftiger Build, sobald die SHAs nicht mehr erreichbar sind. Bei einem
  archivierten Repo bleiben sie erreichbar.
- GitHub-Repos löschen. Bricht Einrohrheizung und jeden fremden Nutzer.

### Reihenfolge im Zusammenspiel mit dem OTA-Wechsel

Die Zwischenfassung mit zwei CAs (O1) wird **von GitHub gebaut** (O2), denn dort bauen
die Firmwares noch. GitHub-Runner erreichen `forgejo.intern` nicht. Also:

1. **O1 auf GitHub**: zwei CAs in `ESP32_OTAUpdate`, erster Tag.
2. **O2/O3**: alle Firmwares pinnen diesen Tag (GitHub-URL), Rollout über Azure,
   Nachweis.
3. **Bibliotheken migrieren** (mit Historie und Tags) nach `forgejo.intern`.
4. **O4/O5**: je Firmware `lib_deps` auf die Forgejo-URLs mit Tag umstellen, im selben
   Commit wie der Workflow-Umzug.

---

## 10. Umzugsreihenfolge (F2 + F3)

### Die tragfähige Variante für den Zwischenzustand

Während des Übergangs liegt der Code auf Forgejo. Die noch nicht umgezogenen Workflows
laufen weiter auf GitHub, dafür müssen sie jeden Commit sehen.

| Variante | Preis |
|---|---|
| **A: Push-Spiegel „bei jedem Commit" nur für die Übergangszeit**, Workflows dienstweise per `git mv` nach `.forgejo/workflows/` | GitHub bleibt bis zum letzten Schritt ein aktives Build-System, mit seinen Secrets und dem `github-runner`. Das ist genau der heutige Zustand, nichts kommt hinzu |
| B: alle 19 Workflows sofort nach Forgejo, nur die Registry dienstweise | Forgejo bräuchte Docker-Hub-Secrets, Multi-Arch per QEMU und **für die Firmware vom ersten Tag an `az` und `AZURE_STORAGE_KEY`**. Das Riskanteste (Firmware-OTA) zieht zuerst um. Widerspricht „nicht mit der Firmware anfangen" |

**Empfehlung A.** Warum sie kein doppeltes Bauen kennt: Forgejo liest nur
`.forgejo/workflows/`, sobald es existiert, GitHub nur `.github/workflows/`. Liegt ein
Workflow in genau einem der beiden Verzeichnisse, baut genau ein System. Der Commit, der
ihn verschiebt, löscht ihn aus `.github/workflows/`. GitHub wertet die Workflows aus dem
gepushten Commit aus, sieht ihn nicht mehr und baut ihn nicht. **Beide Systeme beschreiben
nie dasselbe Ziel:** GitHub pusht nach `tschissler/<image>`, Forgejo nach
`forgejo.intern/thomas/<image>`, und der Updater einer App beobachtet genau eins davon.

**Die Falle im Zwischenzustand:** Zwischen dem Workflow-Umzug eines Dienstes (Schritt a)
und der Umstellung seiner App im Deployments-Repo (Schritt b) baut Forgejo Images, die
niemand beobachtet. Eine Code-Änderung am Dienst in dieser Lücke rollt **still nirgendwo
aus**, dieselbe Fehlerklasse wie ein fehlender Pfadfilter. Deshalb enthält Schritt a
**keine** Code-Änderung, und b folgt direkt danach.

### Schritt 0 — Vorbereitung (löst nichts aus)

- Tag-Schema (`1.1.<run>`, Firmware `0.1.<run>`, F8) und Build-Muster (A, F7) sind
  entschieden. Offen sind noch die Punkte aus Abschnitt 12.
- `REGISTRY_USERNAME`/`REGISTRY_TOKEN` als Benutzer-Secrets anlegen.
- `registries.conf` der `argocd-image-updater-config` in die Ansible-Installation holen
  (Patch-Task in `install-argocd.yml`, U02; heute nur im Cluster, siehe Abschnitt 5).
- Pull von `forgejo.intern` auf `k3snode3` und `k3snode4` nachweisen.
- Soll-Liste aller ESP32-Geräte aufstellen (für O3).

### Schritt 1 — Repo nach Forgejo

Nach F5 in zwei Teilen: erst läuft Forgejo als Nachzügler mit, dann kippt die Richtung.

**1a — Forgejo als Nachzügler.**

1. `thomas/SmartHomeTS` **leer und öffentlich** anlegen, **Actions im Repo abgeschaltet**.
2. `main` pushen (erledigt: aus dem alten Klon per URL, `fa0a022..47e152e`). Bis zur
   Umstellung bleibt GitHub die Quelle. Forgejo wird **nicht laufend nachgezogen**, es darf
   hinterherhinken; niemand committet dort. Keiner der beiden Klone braucht ein
   `forgejo`-Remote.

**1b — Umstellung: Forgejo wird `origin`.**

1. Auf GitHub einen letzten Stand abwarten, dann Forgejo aus dem **neuen Klon** per
   Fast-Forward nachziehen:
   `git fetch https://github.com/tschissler/SmartHomeTS.git main`, danach
   `git push origin FETCH_HEAD:main` (nur Fast-Forward, nie erzwungen). Nachweis:
   `git ls-remote origin main` zeigt den erwarteten Commit.
2. Commit **`.forgejo/workflows/.gitkeep`**. `ListWorkflows` nimmt das erste vorhandene
   Verzeichnis, „no matter whether it contains workflows or not". Ab dann ignoriert Forgejo
   die 19 Dateien in `.github/workflows/`. **Erst danach Actions im Repo einschalten.**
   Umgekehrt würde Forgejo beim nächsten Push **alle 19** starten: Die Docker-Jobs
   scheitern ohne Secrets, die MQTT-Jobs hängen ewig auf `self-hosted`, die
   Firmware-Builds scheitern an `az`.
3. Push-Spiegel nach GitHub einrichten, „bei jedem Commit synchronisieren" an. Der
   Spiegel pusht mit `--mirror`, also erzwungen und mit Löschen. **Auf GitHub darf danach
   niemand mehr direkt pushen**, sonst überschreibt der nächste Spiegellauf den Commit.
4. Arbeitsplatz: **Ab hier wird nur noch im neuen Klon** `~/Repos/Forgejo.intern/SmartHomeTS`
   committet und gepusht; sein `origin` zeigt schon auf Forgejo. Der alte Klon
   `~/Repos/GitHub/SmartHomeTS` dient nur noch zum Lesen — ein Push von dort nach GitHub
   würde beim nächsten Spiegellauf überschrieben (Punkt 3). Laufende Worktrees und
   Sessions der Orchestrierung im alten Klon vorher abschließen, nicht mitnehmen.
   (Ursprünglich geplant war `git remote set-url origin` im alten Verzeichnis; siehe
   Abschnitt 12, warum neu geklont wurde.)

Löst aus: auf Forgejo nichts (`.gitkeep`). Auf GitHub kommt der `.gitkeep`-Commit über
den Spiegel an. Er berührt keinen `paths:`-Filter, also baut GitHub nichts.
Rückweg: Spiegel löschen, wieder im alten Klon (`origin` = GitHub) arbeiten. Solange kein Workflow umgezogen ist,
hat GitHub alle Commits und baut wie bisher.

### Schritt 2 — Probelauf mit einem Dienst: **VWConnector**

Warum dieser:

- Laut Memory gehen die VW-Daten in keine Regelung ein. Ein Ausfall kostet nur die
  Anzeige.
- Python, kein Test-Job, also keine `setup-dotnet`-Frage. Keine geteilten Pfade, der
  Kontext ist nur `./VWConnector`.
- Er hat einen Heartbeat mit Version (`status/Cluster/Dienst/VWConnector`).

a) SmartHomeTS: `git mv .github/workflows/vwconnector.yml .forgejo/workflows/` und
   anpassen: `runs-on: arm64`, `docker build --platform linux/arm64` + `docker push`
   nach `forgejo.intern/thomas/vwconnector:1.1.<run>`, Login mit `REGISTRY_*`. Keine
   Code-Änderung am Dienst.
   → Forgejo baut, pusht `1.1.N`. GitHub baut nichts.
b) Deployments: `VWConnector.yaml` (`image-list`, `pull-secret`, `platforms`) und
   `VWConnector/values.yaml` (`repository`, `tag: 1.1.N`, `imagePullSecrets`) in einem
   Commit, danach
   `00-bootstrap` synchronisieren.
   → ArgoCD rollt `1.1.N` aus.

**Woran man sieht, dass es wirkt:**

1. Forgejo: Lauf grün, Paket `vwconnector` mit Tag `1.1.N` unter `thomas`.
2. Pod: `kubectl -n smarthome get pod -l … -o jsonpath='{..image}'` zeigt
   `forgejo.intern/thomas/vwconnector:1.1.N`, `Running`, keine Restarts.
3. Heartbeat: `status/Cluster/Dienst/VWConnector` trägt `"version": "1.1.N"`.
4. **Der eigentliche Test:** Eine zweite, harmlose Änderung unter `VWConnector/`
   pushen. Erscheint innerhalb von ~2 min ein Commit `build: automatic update of
   vwconnector` auf `1.1.M` im Deployments-Repo, arbeitet der Updater gegen die
   Forgejo-Registry. Erst das beweist den ganzen Weg, nicht der von Hand gesetzte Tag.

Rückweg: (b) revertieren → zurück auf `tschissler/vwconnector:1.0.41`, das auf Docker Hub
liegen bleibt. (a) revertieren → GitHub baut wieder. Beide Schritte sind einzeln
rücknehmbar.

### Schritt 3 — die übrigen Dienste, einzeln, je wie Schritt 2

Reihenfolge nach Risiko:

1. **EnphaseConnector, ShellyConnector**: .NET ohne Test-Job, das Build-Muster wird ohne
   `setup-dotnet` erprobt. Energiedaten, also danach Grafana kurz ansehen.
2. **BMWConnector, KebaConnector, RulesEngine, SmartHome.DataHub**: mit Test-Job, hier
   wird der Ersatz für `setup-dotnet` erprobt (Job-Container `dotnet/sdk:10.0`,
   Test-Job auf `ubuntu-latest`, Build-Job auf `arm64`, siehe „Wird der amd64-Runner noch
   gebraucht?" in Abschnitt 2). Keba und RulesEngine gehören zur Lade-Kette, also nicht
   beide am selben Abend.
3. **SmartHome.Web**: der größte Build, der erste echte Messpunkt für die Dauer auf dem
   arm64-Runner.
4. **ChargingController** zuletzt.

Je Dienst angefasst: eine Workflow-Datei (verschoben), eine `<Dienst>.yaml`, eine
`values.yaml`. Rollouts je Schritt: einer (der Dienst selbst).

### Schritt 4 — Firmware

In der Reihenfolge aus [Abschnitt 8](#8-ota-weg-der-firmware) (O1–O5) und
[Abschnitt 9](#9-die-esp32-bibliotheken). Pro Gerätetyp: Workflow per `git mv`, neue
`runs-on` (`ubuntu-latest` bzw. `amd64`), `mosquitto_pub` im Job installieren oder
eigenes Image, Upload per `curl -X PUT` in die Generic-Registry, `lib_deps` auf Forgejo mit
Tag, Version `0.1.<run>`. Kein `az`, kein Azure-Schlüssel in Forgejo.

Erster Gerätetyp: einer, bei dem ein Fehlschlag nicht heizt oder misst. Kandidaten sind
**LEDStripe** oder **TemperatureDisplay**. Nicht MixerController, RelaisBoard,
HeatingFanController oder SMLSensor. **Offen (Thomas):** welcher, und welches Gerät
dafür am Tisch liegt.

### Schritt 5 — Aufräumen, wenn `.github/workflows/` leer ist

Einzeln betrachtet, was davon **allein** reicht, damit ein Rückspiegeln auf GitHub nichts
mehr auslöst:

| Maßnahme | allein ausreichend? |
|---|---|
| `.github/workflows/` leer (alle Dateien per `git mv` verschoben) | **Ja, für Pushes nach `main`.** Alle Trigger sind `push: branches: [main]`, und GitHub wertet die Workflows aus dem gepushten Commit aus. Alte Branches oder Tags auf GitHub lösen nichts aus. Lücke: wer die Dateien je wieder anlegt |
| Actions im GitHub-Repo abschalten (Settings → Actions → Disable) | **Ja, vollständig**, auch für `workflow_dispatch` und künftige Dateien. Ein Schalter, jederzeit umkehrbar |
| GitHub-Secrets löschen | nein. Ein Lauf scheitert dann, aber er läuft. Begrenzt den Schaden, falls die anderen beiden versagen |
| `github-runner` abbauen | nein. Er ist der einzige Weg ins LAN, also kein MQTT-Publish mehr. **Der Azure-Upload läuft aber auf GitHub-Hardware und bräuchte den Runner nicht** |

Empfehlung: **alle vier**, in dieser Reihenfolge. Die ersten beiden verhindern den Lauf,
die anderen beiden nehmen ihm die Wirkung. Danach den Push-Spiegel von „bei jedem Commit"
auf den gewünschten Takt stellen (siehe unten). Dazu: Docker-Hub-Token widerrufen,
`dockerhub-credentials` entfernen, `CLAUDE.md` (CI/CD), `Docs/Parallel-Arbeiten.md`
(Pfadfilter) und den Skill `worker` (Rollout-Zählung) auf `.forgejo/workflows/`
umschreiben, README-Badges.

### Mechanik des Spiegels nach GitHub (F2), **Offen (Thomas)**

| Variante | was sie auf GitHub auslöst |
|---|---|
| Forgejo-Push-Spiegel, „bei jedem Commit synchronisieren" | einen Push pro Forgejo-Push. Während des Übergangs gewollt (Variante A), danach nichts mehr, wenn Schritt 5 erledigt ist |
| Forgejo-Push-Spiegel mit Intervall (z. B. 24 h) | einen gesammelten Push pro Intervall. GitHub wertet `paths:` über den ganzen Bereich aus, würde also alle betroffenen Workflows auf einmal starten, solange es welche gibt |
| Push-Spiegel ohne Intervall, nur der Knopf „Jetzt synchronisieren" | nur, wenn Thomas drückt |
| manuell `git push github main` vom Arbeitsplatz | dasselbe, aber ohne das erzwungene `--mirror`: Branches und Tags nur, wenn ausdrücklich mitgegeben |

Alle Varianten pushen erzwungen bzw. überschreiben. Auf GitHub entstandene Commits gehen
verloren, auch die eines Forks oder PRs, der dort gemergt würde.

---

## 11. Unsichere Stellen und der Test, der sie klärt

| Behauptung | warum unsicher | Test |
|---|---|---|
| Ein Forgejo-Job kann nach `mosquitto.intern` publizieren | Netz und DNS aus der Konfiguration abgeleitet, nicht ausgeführt | Test-Lauf mit `mosquitto_pub -t test/forgejo-runner -m x` **ohne `-r`**, nicht auf `OTAUpdate/`, auf einem Test-Topic. Braucht Thomas' Freigabe, weil es den Produktivbroker berührt (erteilt, F26). **Läuft erst nach U11** als Test-Workflow auf einem Branch von SmartHomeTS, nicht in einem Wegwerf-Repo: Vor der `.gitkeep` würde das Einschalten der Actions alle 19 Workflows aus `.github/workflows` starten |
| Pull von `forgejo.intern` auf `k3snode3`, `k3snode4` (und amd64) | nur 5 und 6 belegt; Node-Status auf 50 Images gekappt | `ansible prodservers -m command -a "curl -sS -o /dev/null -w '%{http_code}' https://forgejo.intern/v2/"`: erwartet 401 statt TLS-Fehler. Liest nur |
| Generic-Pakete sind anonym ladbar | **geklärt (U05): ja**, Ergebnis in Abschnitt 8 | die U30-Test-`.bin` als Generic-Paket `ota-test` unter `thomas` laden, anonym `curl -I`, Paket löschen. Schreibt in Forgejo, Freigabe erteilt (F26) |
| Zwei PEM-Blöcke in `server_certificate` funktionieren mit `HttpsOTA` | aus mbedTLS abgeleitet, nicht auf dem Gerät gesehen | ein nacktes ESP32 devkit-v4 am Notebook per USB mit einem eigenen Test-Sketch (O1-Fassung, ohne MQTT) flashen, einmal von Azure, einmal von `forgejo.intern` laden lassen (U30) |
| `paths:` mit `!`-Negation (Smarthome.Web) wird von Forgejo wie von GitHub ausgewertet | nicht an dieser Instanz beobachtet | beim Umzug von Web: eine Änderung nur unter `SmartHome.Web/SmartHomeBlazorApp/` pushen, es darf kein Lauf entstehen |
| Build-Dauer eines .NET-Dienstes auf dem arm64-Runner | nicht gemessen | ergibt sich aus dem Probelauf (Schritt 2) und aus Web (Schritt 3.3) |
| `HeatmeterSensor.Firmware/lib/MQTTClientLib` wird nicht gebaut | für `OTAUpdater` belegt, für diese Kopie nicht | `pio run -v` und nachsehen, aus welchem Pfad `MQTTClientLib.cpp` übersetzt wird |
| `github-runner/runner-secret.yaml` enthält keinen echten PAT | nicht angesehen | Thomas sieht nach. Wenn ja: PAT widerrufen, das Repo ist anonym lesbar |
| `DEFAULT_ACTIONS_URL` steht nicht in der `app.ini` auf dem PVC | Values und Env geprüft, die Datei nicht; das Log zeigt aber `data.forgejo.org` | nicht nötig, das Log belegt das Verhalten |

## 12. Entscheidungen

**Entschieden:**

- **F7 Build-Muster:** A, nur arm64 nativ auf den Pi-Runnern. Begründung und Preis in
  Abschnitt 2.
- **F8 Tag-Schema:** Images `1.1.<run>`, Firmware `0.1.<run>`. Begründung in Abschnitt 4.
- **Arbeitsplatz: neu klonen statt `origin` umstellen** (2026-09-27). Neuer Klon
  `~/Repos/Forgejo.intern/SmartHomeTS` mit `origin` = Forgejo; der alte
  `~/Repos/GitHub/SmartHomeTS` (`origin` = GitHub) bleibt übergangsweise. Gründe: Der Pfad
  `…/GitHub/…` wäre nach dem Umzug irreführend, und Thomas will beide Klone eine Weile
  nebeneinander nutzen. Das kippt die Empfehlung aus Abschnitt 6; Memory und ignorierte
  Dateien wurden deshalb von Hand kopiert. **Regel für die zwei Klone** (von Thomas
  bestätigt, F24):
  - **Bis U11** wird aus dem neuen Klon **nichts gepusht** — Forgejo ist bis dahin
    Nachzügler, auf dem niemand committet (F5).
  - **Ab U11** wird **nur im neuen Klon** committet; der alte dient nur zum Lesen, seine
    Push-URL wird bei U11 gesperrt. Grund:
    Der `--mirror`-Spiegel überschreibt auf GitHub alles, was direkt dort gepusht wurde.
- **`forgejo-pull-secret` mitnehmen** (U01, 2026-09-27): eine Zeile je `values.yaml`.
  Begründung in Abschnitt 5, „Pull-Secrets".
- **Keine zusätzliche Absicherung gegen einen Forgejo-Ausfall** (U01, 2026-09-27),
  vorerst auch nicht für den ChargingController. Das Risiko steht in Abschnitt 5, „Was bei
  einem Forgejo-Ausfall …".
- **Testgerät für U30** (2026-09-27): ein nacktes ESP32 devkit-v4 am Notebook, per USB
  geflasht, mit eigenem Test-Sketch ohne MQTT (Abschnitt 11, „Zwei PEM-Blöcke").
- **U04 und U05 ohne Wegwerf-Repo** (F26): U05 lädt ein Test-Paket `ota-test`, U04 läuft
  nach U11 als Test-Workflow auf einem Branch von SmartHomeTS (Abschnitt 11).
- **Ablage der Firmware: Generic Registry** (nach U05). Auflage: Der Benutzer `thomas`
  bleibt öffentlich. Ergebnis und Folgen in Abschnitt 8, „Ablageort in Forgejo".
- **Soll-Liste der Geräte** (U08): aufgestellt, liegt in Issue #8.
- **Fünf Geräte aus dem O3-Nachweis ausgenommen** (F40): Heatmeter_M1, beide
  Heizkörperlüfter, Brutkasten (nur offline, bleibt in der Liste, F39) und
  Relaismodule_M1_OG. Je ein Reparatur-Issue nach dem Umzug; Preis und Selbstheilung in
  Abschnitt 8, „Woran man den Firmware-Stand …".

**Offen für Thomas:**

6. Erster Firmware-Gerätetyp für U36 (das Testgerät für U30 ist entschieden, siehe oben)
8. Spiegel-Mechanik nach dem Übergang (Intervall, Knopf, manuell)
9. Bibliotheks-Repos auf GitHub: spiegeln, archivieren oder löschen, und wann
   Einrohrheizung umgestellt wird
10. README-Badges entfernen oder stehen lassen
11. Aus [Abschnitt 7](#was-sonst-am-github-repo-hängt-und-nicht-im-git-steht) noch offen:
    Originale von `WIFI_PASSWORDS`/`METER_PINS` (F32) und ob
    `github-runner/runner-secret.yaml` einen echten PAT enthält (F33). Webhooks,
    Branch-Schutz, Deploy-Keys, Runner, Variablen und Environments sind beantwortet
12. Sichtbarkeit der Bibliotheks-Repos auf Forgejo: öffentlich (kein Token im CI) oder
    privat (Token im `insteadOf`)

## 13. Issues

Angelegt am 2026-09-27 in `thomas/SmartHomeTS`. Abhängigkeiten sind als Forgejo-Abhängigkeiten gesetzt.
Die U-Nummern haben Lücken (U09, U13–U19 …), die Issue-Nummern nicht.

| U | Issue | Phase | Titel |
|---|---|---|---|
| U01 | [#1](https://forgejo.intern/thomas/SmartHomeTS/issues/1) | Vorbereitung | Offene Entscheidungen vor dem ersten Dienst |
| U02 | [#2](https://forgejo.intern/thomas/SmartHomeTS/issues/2) | Vorbereitung | `argocd-image-updater-config` in die Ansible-Installation holen |
| U03 | [#3](https://forgejo.intern/thomas/SmartHomeTS/issues/3) | Vorbereitung | Pull von `forgejo.intern` auf allen Nodes nachweisen |
| U04 | [#4](https://forgejo.intern/thomas/SmartHomeTS/issues/4) | Vorbereitung | Test: MQTT-Publish aus einem Forgejo-Job |
| U05 | [#5](https://forgejo.intern/thomas/SmartHomeTS/issues/5) | Vorbereitung | Test: Generic-Paket anonym ladbar |
| U06 | [#6](https://forgejo.intern/thomas/SmartHomeTS/issues/6) | Vorbereitung | Secrets in Forgejo anlegen |
| U07 | [#7](https://forgejo.intern/thomas/SmartHomeTS/issues/7) | Vorbereitung | Fragen an Thomas zum GitHub-Repo |
| U08 | [#8](https://forgejo.intern/thomas/SmartHomeTS/issues/8) | Vorbereitung | Soll-Liste aller ESP32-Geräte |
| U10 | [#9](https://forgejo.intern/thomas/SmartHomeTS/issues/9) | Repo | Repo leer und öffentlich anlegen, `main` pushen (Nachzügler) |
| U11 | [#10](https://forgejo.intern/thomas/SmartHomeTS/issues/10) | Repo | Umstellung: Forgejo wird `origin` |
| U12 | [#11](https://forgejo.intern/thomas/SmartHomeTS/issues/11) | Repo | Doku und Skills auf zwei Workflow-Verzeichnisse umstellen |
| U20 | [#12](https://forgejo.intern/thomas/SmartHomeTS/issues/12) | Dienste | VWConnector umziehen (Probelauf) |
| U21 | [#13](https://forgejo.intern/thomas/SmartHomeTS/issues/13) | Dienste | EnphaseConnector umziehen |
| U22 | [#14](https://forgejo.intern/thomas/SmartHomeTS/issues/14) | Dienste | ShellyConnector umziehen |
| U23 | [#15](https://forgejo.intern/thomas/SmartHomeTS/issues/15) | Dienste | BMWConnector umziehen |
| U24 | [#16](https://forgejo.intern/thomas/SmartHomeTS/issues/16) | Dienste | KebaConnector umziehen |
| U25 | [#17](https://forgejo.intern/thomas/SmartHomeTS/issues/17) | Dienste | RulesEngine umziehen |
| U26 | [#18](https://forgejo.intern/thomas/SmartHomeTS/issues/18) | Dienste | SmartHome.DataHub umziehen |
| U27 | [#19](https://forgejo.intern/thomas/SmartHomeTS/issues/19) | Dienste | SmartHome.Web umziehen |
| U28 | [#20](https://forgejo.intern/thomas/SmartHomeTS/issues/20) | Dienste | ChargingController umziehen |
| U30 | [#21](https://forgejo.intern/thomas/SmartHomeTS/issues/21) | Firmware, OTA, Bibliotheken | `ESP32_OTAUpdate`: zweite CA und erster Tag (noch auf GitHub) |
| U31 | [#22](https://forgejo.intern/thomas/SmartHomeTS/issues/22) | Firmware, OTA, Bibliotheken | Tote Bibliothekskopien entfernen |
| U32 | [#23](https://forgejo.intern/thomas/SmartHomeTS/issues/23) | Firmware, OTA, Bibliotheken | Alle zehn Firmwares pinnen `ESP32_OTAUpdate#v1.0.0` (Rollout über Azure) |
| U33 | [#24](https://forgejo.intern/thomas/SmartHomeTS/issues/24) | Firmware, OTA, Bibliotheken | Nachweis: jedes Gerät fährt die Fassung aus U32 |
| U34 | [#25](https://forgejo.intern/thomas/SmartHomeTS/issues/25) | Firmware, OTA, Bibliotheken | Die acht ESP32-Bibliotheken nach Forgejo migrieren |
| U35 | [#26](https://forgejo.intern/thomas/SmartHomeTS/issues/26) | Firmware, OTA, Bibliotheken | Vorlage für Firmware-Workflows auf Forgejo |
| U36 | [#27](https://forgejo.intern/thomas/SmartHomeTS/issues/27) | Firmware, OTA, Bibliotheken | Erster Gerätetyp auf Forgejo (Probe) |
| U37 | [#28](https://forgejo.intern/thomas/SmartHomeTS/issues/28) | Firmware, OTA, Bibliotheken | Gerätetyp auf Forgejo: TemperatureDisplayFirmware.yml bzw. LEDStripeFirmware.yml (der, der nicht U36 war) |
| U38 | [#29](https://forgejo.intern/thomas/SmartHomeTS/issues/29) | Firmware, OTA, Bibliotheken | Gerätetyp auf Forgejo: KellerdeviceFirmware.yml |
| U39 | [#30](https://forgejo.intern/thomas/SmartHomeTS/issues/30) | Firmware, OTA, Bibliotheken | Gerätetyp auf Forgejo: TemperatureSensorFirmware.yml |
| U40 | [#31](https://forgejo.intern/thomas/SmartHomeTS/issues/31) | Firmware, OTA, Bibliotheken | Gerätetyp auf Forgejo: TemperatureSensor2Firmware.yml |
| U41 | [#32](https://forgejo.intern/thomas/SmartHomeTS/issues/32) | Firmware, OTA, Bibliotheken | Gerätetyp auf Forgejo: HeatMeterSensorFirmware.yml |
| U42 | [#33](https://forgejo.intern/thomas/SmartHomeTS/issues/33) | Firmware, OTA, Bibliotheken | Gerätetyp auf Forgejo: SMLSensorFirmware.yml |
| U43 | [#34](https://forgejo.intern/thomas/SmartHomeTS/issues/34) | Firmware, OTA, Bibliotheken | Gerätetyp auf Forgejo: HeatingFanControllerFirmware.yml |
| U44 | [#35](https://forgejo.intern/thomas/SmartHomeTS/issues/35) | Firmware, OTA, Bibliotheken | Gerätetyp auf Forgejo: RelaisBoardFirmware.yml |
| U45 | [#36](https://forgejo.intern/thomas/SmartHomeTS/issues/36) | Firmware, OTA, Bibliotheken | Gerätetyp auf Forgejo: MixerControllerFirmware.yml |
| U50 | [#37](https://forgejo.intern/thomas/SmartHomeTS/issues/37) | Aufräumen | GitHub Actions abschalten und Secrets löschen |
| U51 | [#38](https://forgejo.intern/thomas/SmartHomeTS/issues/38) | Aufräumen | `github-runner` abbauen |
| U52 | [#39](https://forgejo.intern/thomas/SmartHomeTS/issues/39) | Aufräumen | Docker Hub stilllegen |
| U53 | [#40](https://forgejo.intern/thomas/SmartHomeTS/issues/40) | Aufräumen | Azure stilllegen |
| U54 | [#41](https://forgejo.intern/thomas/SmartHomeTS/issues/41) | Aufräumen | Spiegeltakt nach GitHub festlegen |
| U55 | [#42](https://forgejo.intern/thomas/SmartHomeTS/issues/42) | Aufräumen | Einrohrheizung und die GitHub-Kopien der Bibliotheken |
| U56 | [#43](https://forgejo.intern/thomas/SmartHomeTS/issues/43) | Aufräumen | README und Verweise |
