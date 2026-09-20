# Parallel arbeiten: mehrere Sessions an einem Vorhaben

Entstanden am 2026-09-20, als zehn Sessions an einem Tag 24 Punkte des Lade-Vorhabens
abgearbeitet haben. Das Backlog dazu liegt im Archiv; **hier steht nur, was über jenes
Vorhaben hinaus trägt.**

Gilt für Vorhaben, die sich in **unabhängig mergebare Stücke** zerlegen lassen. Für
Einzelaufgaben ist der Aufwand nicht gerechtfertigt.

## Die Rollen

**Eine Integrator-Session** führt das Arbeitsdokument, schreibt die Prompts, prüft
Ergebnisse **unabhängig** nach und merged — aber nur auf ausdrückliche Freigabe.

**Arbeits-Sessions** bearbeiten je einen Punkt in einem eigenen git worktree. Sie fassen
das Arbeitsdokument **nicht** an; sonst wird es selbst zum Merge-Konflikt.

Sinnvoll sind zwei bis drei gleichzeitige Sessions. Mehr kostet mehr Koordination, als es
einbringt.

## Die Regeln

- **Ein Punkt, eine Session.** Kein Punkt wird von zweien gleichzeitig angefasst.
- **Ein Name für alles.** Session (`claude -n <Name>`), Worktree und Branch tragen
  denselben Namen. Ohne `-n` vergibt der CLI eine Nummer, die niemandem sagt, woran die
  Session arbeitet.
- **Kein Merge ohne ausdrückliche Freigabe.** In diesem Repo ist jeder Merge nach `main`
  binnen ~2 min ein Deployment ins laufende System.
- **Nummern bleiben stabil.** Neue Erkenntnisse werden hinten angehängt, nie eingeschoben —
  laufende Sessions verweisen auf diese Nummern.
- **Im Zweifel gewinnen die Konzeptdokumente** gegenüber dem Arbeitsdokument: Das Backlog
  wird nicht immer nachgezogen, wenn ein Konzept präzisiert wurde.

## Zwei Tabellen, nicht eine

**Die Abhängigkeitstabelle** sagt, was fachlich aufeinander aufbaut. Sie ergibt die
Reihenfolge.

**Die Kollisionstabelle** sagt, welche Punkte dieselben Dateien anfassen. Sie ergibt, was
*nicht* gleichzeitig laufen darf — und sie ist die wichtigere von beiden. Zwei Punkte
können fachlich völlig unabhängig sein und sich trotzdem in einer Datei treffen.

Führe sie nach **Bereich**, nicht nach Datei: „`SmartHome.Web`" ist grob genug, um sie
pflegbar zu halten, und fein genug, um Kollisionen zu sehen. Wo zwei Punkte denselben
Bereich, aber verschiedene Dateien anfassen, notiere die Dateien — das rettet die
Parallelität.

## Pfadfilter: der Fehler, der nicht wehtut

Wenn die CI nach geänderten Pfaden baut, muss **geteilter Code in den Pfadfiltern aller
Dienste stehen, die ihn referenzieren** — transitiv aufgelöst.

Fehlt ein Eintrag, passiert nichts Sichtbares: kein Fehler, kein roter Build. Die Änderung
deployt nur nirgendwohin. Das fällt erst auf, wenn jemand sich wundert, warum ein Fix nicht
wirkt.

**Zähl die betroffenen Workflows vor jedem Merge selbst nach**, statt eine Zahl aus einem
früheren Bericht zu übernehmen. Sie ändert sich, sobald ein Dienst eine `ProjectReference`
dazubekommt — genau das ist an einem Tag zweimal passiert.

## Vier Regeln zum Messen

Alle vier sind an einem Tag teuer gelernt worden.

**1. Der Exit-Code eines `git push` ist kein Nachweis.** Auf losgelöstem HEAD pusht
`git push origin main` die lokale Referenz `main` — „Everything up-to-date", Exit-Code 0,
Erfolgsmeldung, nichts passiert. Nachgewiesen ist ein Push erst durch `git fetch` und einen
Vergleich von `rev-parse origin/main` mit dem erwarteten Commit.

**2. Verglichene Warnungszahlen brauchen `--no-incremental` auf beiden Ständen.** Ein
inkrementeller Build meldet nur die Warnungen der Projekte, die er tatsächlich neu übersetzt
hat; was unverändert im `obj` liegt, schweigt. Eine Zahl, die zwischen zwei Messungen
schwankt, ist ein Hinweis auf die Messung, nicht auf den Code.

**3. Nachrechnen in einem fremden Arbeitsverzeichnis nur über einen eigenen Klon.** Lesen
ist harmlos, aber `checkout` und alles, was HEAD oder Refs bewegt, ist ein Eingriff in
fremde Arbeit — ein losgelöster HEAD lässt die Commits der anderen Session ins Leere
laufen, ohne dass sie es merkt.

**4. Wer einen Bereich zwischen zwei Markern ersetzt, muss wissen, was dazwischen steht.**
Ein Textabschnitt „von Marker A bis Marker B" verschluckt alles dazwischen. `git log -S`
nach dem Commit ist billiger als der Verlust.

## Was ein guter Prompt leistet

Der Prompt ist der Vertrag. Was nicht drinsteht, wird nicht geprüft.

- **Nenn die Fallen, die du kennst** — mit Datei und Zeile. Eine Session, die eine Falle
  selbst finden muss, findet sie vielleicht nicht.
- **Nenn die Grenzen**, besonders gegenüber produktiven Systemen: was gelesen, was nicht
  geschrieben werden darf, und was ausdrücklich einer anderen Session gehört.
- **Nenn die Rollout-Folgen.** Eine Session, die weiß, dass ihr Merge neun Dienste neu
  startet, entscheidet anders.
- **Verlang ausdrücklich Widerspruch.** Bitte um eine Liste der Stellen, an denen der
  Auftrag nicht gestimmt hat.

Der letzte Punkt ist der wichtigste, und er ist empirisch: **An einem Tag hat jede Session,
die dem Auftrag widersprochen hat, recht gehabt** — bei einer falsch herum beschriebenen
Konfiguration, bei einer Rollout-Zahl, bei einem angeblich verwaisten Typ, der in Benutzung
war, und bei einem Vorschlag, der eine negative Anzeige erzeugt hätte. Eine Session, die
einen Auftrag widerspruchslos ausführt, hat ihn möglicherweise nur nicht geprüft.

## Was die Integrator-Session nicht darf

- **Aus dem Gedächtnis berichten.** Der Abschlussbericht einer Session ist ihr letzter
  Stand, nicht der aktuelle. Wo der Mensch direkt mit einer Session geredet hat, steht das
  Ergebnis im Repo — nachsehen, nicht referieren.
- **Aus einer Teilprüfung schließen.** Sechs Trefferdateien und zwei davon geöffnet ergibt
  keine Aussage über alle sechs.
- **Eine Sichtprüfung durch einen grünen Build ersetzen.** Bei einer Umgestaltung ist das
  Aussehen der Gegenstand. Meldet eine Session, dass sie das Ergebnis nicht ansehen konnte,
  gehört der Blick des Menschen **vor** den Merge.

---

*Das zugehörige Vorhaben ist im Lade-Backlog protokolliert (Archiv). Dort stehen die
konkreten Wellen- und Kollisionstabellen als ausgearbeitetes Beispiel.*
