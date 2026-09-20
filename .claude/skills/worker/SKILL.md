---
name: worker
description: Set up a session as a worker on one point of an orchestrated initiative - worktree, boundaries against the production system, no merges, and what the closing report must contain. Use at the start of a session that received a task from an orchestrator session.
---

Du bearbeitest **einen** Punkt eines orchestrierten Vorhabens. Hier steht nur, was
**immer** gilt.

**Der eigentliche Auftrag kommt gleich per Nachricht von der Orchestrierungs-Session.**
Thomas hat dich nur gestartet; er wird dir nichts einfügen. Sag ihm in einem Satz, dass du
bereit bist und auf den Auftrag wartest, und **fang nichts an**, bis er da ist — auch dann
nicht, wenn du aus dem Sessionnamen erraten könntest, worum es geht.

## Arbeitsweise

- **Eigener Worktree.** Leg ihn mit `EnterWorktree` unter dem Namen an, den der Auftrag
  nennt. Arbeite ausschließlich darin. **`EnterWorktree` stellt dem Branch `worktree-`
  voran** — benenn ihn **nicht** um, sondern melde der Orchestrierung den tatsächlichen
  Namen. Ein `git branch -m` kostet zweimal: `ExitWorktree` kennt am Ende nur den alten
  Namen, sieht dort einen Commit, der scheinbar nirgends hinführt, und verweigert das
  Entfernen, bis der **Nutzer** es freigibt — eine Zusage der Orchestrierung reicht dafür
  nicht und darf es auch nicht.
- **Halt deine Shell-Befehle einfach.** Die Worktree-Isolation lehnt jeden Befehl ab, der
  ihr zu komplex wird, um zu belegen, dass er im Worktree bleibt — und sie schlägt schon
  auf die Zeichenfolge `git` an, die in diesem Repo in **jedem** absoluten Pfad steckt
  (`/home/thomas/Repos/GitHub/…`) und auch in `.github/workflows`. Einzelne, gerade Befehle
  laufen durch; `for`-Schleifen über Globs und lange `&&`-Ketten werden abgelehnt, auch
  wenn gar kein git darin vorkommt. Brauchst du eine Schleife, schreib sie in eine
  Skriptdatei und ruf die auf. **Und setz kein `cd` davor** — du stehst schon im Worktree,
  und der Reflex, den Pfad noch einmal abzusichern, macht aus einem geraden Befehl einen
  verschachtelten. Derselbe Befehl läuft ohne `cd` durch, der mit `cd` abgelehnt wird.
  **Dasselbe eine Ebene höher:** Wird eine lange
  `curl`-Abfrage abgelehnt, obwohl dieselbe Form vorher durchlief, leg den Rumpf als Datei
  im Scratchpad ab und ruf `curl --data-binary @datei` auf. Es ist die Verschachtelung, die
  anstößt, nicht der Inhalt. **Ein Token gehört trotzdem nie in eine Datei** — den holst du
  jedes Mal frisch in die Variable.
- **Nimm einen eigenen Port**, wenn du etwas startest (Web-App, Testserver): Zwei Sessions
  auf demselben Port kollidieren, und deine eigene Vorgängerinstanz auch. Bei .NET setzt
  du ihn mit `--urls` durch — ohne das nimmt `dotnet run` die `launchSettings.json` und
  ignoriert, was im Auftrag steht. Zum Aufräumen
  `ss -lptn 'sport = :<port>'` und `kill <pid>` — **nicht `pkill -f`**: Das Muster steht
  auch in deiner eigenen Kommandozeile, also greift es die eigene Shell mit und der Befehl
  endet mit Exit 144.
- **Die Shell hier ist fish.** Ungequotete Glob-Muster als Argument scheitern dort mit
  `no matches found`, wo bash sie durchreicht — `grep --include=*.razor` geht nicht,
  `grep --include='*.razor'` geht. Setz alles Glob-Ähnliche in Anführungszeichen.
- **Ein Punkt, eine Session.** Was der Auftrag nicht nennt, gehört jemand anderem — auch
  wenn es auf dem Weg liegt und klein aussieht. Im Zweifel melden statt anfassen.
- **Das Arbeitsdokument des Vorhabens fasst du nicht an.** Das führt die
  Orchestrierungs-Session; zwei Schreiber machen daraus einen Merge-Konflikt.

## Grenzen gegenüber dem laufenden System

- **Nichts gegen den Produktivbroker, die Wallboxen oder den Cluster schreiben.** Lesendes
  `kubectl` ist in Ordnung, schreibendes nicht. Zum Testen gehört ein lokaler Broker, kein
  produktiver.
- **Kein Merge nach `main`.** Das macht die Orchestrierungs-Session nach Thomas'
  ausdrücklicher Freigabe — jeder Merge ist binnen ~2 min ein Deployment.
  **Eine Ausnahme, und sie ist an einen Nachweis gebunden:** Änderungen, die *nachweislich*
  keinen Workflow auslösen — Dokumentation, Skills —, darfst du selbst pushen. Der Nachweis
  ist, dass du die `paths:`-Blöcke aller Workflows durchgesehen und keinen Treffer gefunden
  hast, und er gehört in den Bericht. Nicht „ich glaube, das löst nichts aus", sondern
  „ich habe nachgezählt, hier ist die Zählung". Bei allem anderen bleibt es beim Merge
  durch die Orchestrierung.
- **Secrets suchst du nicht.** Weder in Dateien noch in der Shell-Historie noch im Cluster.
  Brauchst du eines, sag es und warte. Ein Geheimnis, das eine Session sich zusammensucht,
  landet in einem Transkript.

## Glaub deinem Auftrag nicht

Er ist von jemandem geschrieben, der den Code nicht so genau gelesen hat wie du gleich.
**Prüf die Behauptungen darin nach, bevor du auf ihnen aufbaust** — besonders Dateinamen,
Zeilennummern, Zahlen und „X wird nirgends mehr benutzt".

Und wenn dir eine Prüfung möglich ist, **stell sie an, statt zu lesen**: Eine Datei
wegschieben und bauen ist ein Beweis, eine Suche über zwei von sechs Trefferdateien ist
keiner.

Findest du einen Fehler im Auftrag — melde ihn. Das ist kein Widerspruch, das ist die
Arbeit.

## Wenn du über den Auftrag hinausgehst

Manchmal ist es richtig. Dann gilt: **eigener Commit, der sich fallen lässt**, und im
Bericht die Begründung samt Kosten. So kann die Freigabe getrennt entschieden werden.

## Im Browser nachsehen

**Chrome steht dir zur Verfügung.** Die Werkzeuge heißen `mcp__claude-in-chrome__*` und
sind anfangs nicht geladen — hol sie mit `ToolSearch` in **einem** Aufruf, nicht einzeln:

```
select:mcp__claude-in-chrome__tabs_context_mcp,mcp__claude-in-chrome__navigate,mcp__claude-in-chrome__tabs_create_mcp,mcp__claude-in-chrome__resize_window,mcp__claude-in-chrome__javascript_tool,mcp__claude-in-chrome__computer,mcp__claude-in-chrome__tabs_close_mcp
```

Bei allem, was man **sieht** — Layout, Umbrüche, Breiten, Farben — gilt dieselbe Regel wie
sonst: stell die Prüfung an, statt sie zu rechnen. Eine im Browser gemessene Breite ist ein
Beweis, eine aus Schriftgröße mal Zeichenzahl geschätzte ist eine Vermutung. Miss im
Zweifel in der Seite selbst (`getBoundingClientRect`, `getComputedStyle`), statt einem
Screenshot anzusehen, ob etwas passt.

**`file://` nimmt die Erweiterung nicht an.** Für eine lokale Datei brauchst du einen
Server: `python3 -m http.server 8731 --bind 127.0.0.1` im Verzeichnis, dann
`http://127.0.0.1:8731/…`. Das kostet eine Minute, wenn man es weiß, und einen Fehlschlag,
wenn nicht. Lass ihn laufen, wenn Thomas selbst hinsehen soll — und nenn ihm die URL und
wie er ihn beendet.

Drei Auflagen:

- **Eigener Tab** (`tabs_create_mcp`), statt einen vorhandenen zu übernehmen. Thomas
  arbeitet in diesem Browser.
- **Keine JavaScript-Dialoge auslösen** (`alert`, `confirm`, `prompt`). Sie blockieren die
  Erweiterung, und danach nimmt sie keine Befehle mehr an.
- **Ein Screenshot ersetzt Thomas' Blick nicht.** Er sieht das Ergebnis auf dem echten
  Gerät; dein Bild sagt nur, dass es dort überhaupt ankommen kann.

**Ein Tab im Hintergrund sieht aus wie ein kaputtes Werkzeug.** Steht Chromes Fenster nicht
im Vordergrund, meldet der Tab `document.visibilityState: "hidden"`. Dann läuft jeder
Screenshot nach 30 s in einen Timeout — mit der Meldung „renderer may be frozen", die in
die falsche Richtung zeigt —, **und Anwendungen, die beim Rendern auf Sichtbarkeit achten,
zeigen gar nichts mehr**: Grafana liefert dort null Panels, auch bei einem fehlerfreien
Dashboard.

**Frag deshalb `document.visibilityState` ab, bevor du ein Rendering-Problem debuggst**, und
lade zur Gegenprobe etwas Bekanntes. Zwei Sessions haben je eine halbe Stunde die eigene
Datei verdächtigt. Den Fokus selbst zu übernehmen ist nichts, was du ungefragt tust —
Thomas arbeitet dort; sag ihm stattdessen, dass er das Fenster sichtbar lassen muss.

**Was auch im verborgenen Tab geht: messen und rechnen.** `getBoundingClientRect` und
`getComputedStyle` liefern für gewöhnliches Markup weiter, und die Funktionen der Anwendung
selbst lassen sich direkt aufrufen, statt über die Oberfläche zu gehen — bei Grafana etwa
`window.System.import('@grafana/data')`, um Wertformatierer, Sanitizing oder
Variablen-Interpolation zu prüfen. Das ist der billigste Weg, aus einer Annahme eine
Messung zu machen, und er braucht kein Bild.

Ein Behälter fester Breite ist dabei die verlässlichere Messgröße als die Fenstergröße —
`resize_window` greift nicht immer.

**Für ein Bild ohne Fenster: das Chromium, das schon da ist.** Playwright hat eines im
Cache, und es rendert unabhängig von Thomas' Browser — kein `visibilityState`, kein
Timeout, kein Systempaket nötig. Finden statt Pfad raten, die Versionsnummer wandert:

```bash
CHR=$(find ~/.cache/ms-playwright -maxdepth 3 -name chrome -type f | head -1)
"$CHR" --headless --disable-gpu --no-sandbox --screenshot=bild.png --window-size=400,800 "file://$PWD/seite.html"
"$CHR" --headless --disable-gpu --no-sandbox --print-to-pdf=aus.pdf "file://$PWD/seite.html"
```

Das ist der Weg für **lokale** Prüfstände und für alles, was als PDF gebraucht wird
(Firefox kann kein PDF). Für eine angemeldete Seite wie Grafana hilft es nicht — headless
hat Thomas' Sitzung nicht; dort bleibt es beim Messen im Tab.

**Es liegt nur die Binärdatei dort, keine Playwright-Bibliothek.** Wer eine Seite nicht nur
ablichten, sondern *bedienen* will, spricht das DevTools-Protokoll selbst (Node bringt
WebSocket mit). Zwei Fallen dabei, beide teuer gelernt:
`Emulation.setDeviceMetricsOverride` mit `mobile: true` verschluckt per CDP gesendete
Mausereignisse, und `Input.dispatchMouseEvent` braucht `buttons`, sonst kommt kein
`pointerup` an. Der Aufwand lohnt, sobald Ereignisse im Spiel sind: Ein Screenshot zeigt
nicht, dass ein Element den Klick schluckt.

## Der Abschlussbericht

Geht an die Orchestrierungs-Session. **Frag ihren Namen mit `ListAgents` ab** — er ist
nicht unbedingt der, den der Auftrag nennt.

Darin:

- was du geändert hast
- **Testergebnisse mit Zahlen.** Verglichene Warnungs- oder Fehlerzahlen nur aus Läufen mit
  `--no-incremental`, auf beiden Ständen — ein inkrementeller Build schweigt über alles,
  was er nicht neu übersetzt hat
- **wie viele Rollouts der Merge auslöst, selbst nachgezählt** an den `paths:`-Blöcken.
  Übernimm die Zahl nicht aus dem Auftrag, sie ändert sich mit jeder neuen
  `ProjectReference`. Zähl mit **einfachen Einzelbefehlen** — siehe unten, warum
- **wo der Auftrag nicht gestimmt hat.** Das ist der wertvollste Teil
- bei sichtbaren Änderungen: **worauf Thomas nach dem Rollout schauen soll.** Er ist der
  Einzige, der das Ergebnis auf dem echten Gerät sieht

Melde auch, was du **nicht** prüfen konntest. Eine offen genannte Lücke ist brauchbar, eine
verschwiegene macht den ganzen Bericht wertlos.

## Die Retro

Zum Schluss, **getrennt vom Bericht**, ein kurzer Blick auf die Zusammenarbeit — nicht auf
die Aufgabe. Der Widerspruchsteil oben sagt, wo der Auftrag *inhaltlich* danebenlag; die
Retro sagt, wo das *Verfahren* geklemmt hat.

Drei Fragen, ein paar Sätze:

- **Was hat dich aufgehalten, das mit der Aufgabe nichts zu tun hatte?** Ein Werkzeug, das
  du erst suchen musstest, eine Verweigerung, ein Umweg, den du bauen musstest, eine
  Annahme, die sich erst nach einer Stunde als falsch herausstellte.
- **Was im Auftrag war überflüssig, und was hättest du von Anfang an wissen wollen?** Beides
  ist wertvoll: ein Auftrag, der zu viel sagt, kostet genauso wie einer, der zu wenig sagt.
- **Trifft das die nächste Session genauso?** Nur Wiederkehrendes rechtfertigt eine Änderung
  an `/worker` oder an einem Dokument unter `Docs/`. Einmaliges nennst du trotzdem, aber
  sag dazu, dass es einmalig war.

**Kurz halten.** Eine Retro, die alles aufzählt, wird nicht gelesen. Zwei Punkte, die
wirklich wiederkehren, ändern das Verfahren — und genau dafür ist sie da.

Wenn nichts geklemmt hat, ist „nichts geklemmt" eine vollständige Retro. Erfinde nichts,
um das Feld zu füllen.
