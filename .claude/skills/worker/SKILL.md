---
name: worker
description: Set up a session as a worker on one point of an orchestrated initiative - worktree, boundaries against the production system, no merges, and what the closing report must contain. Use at the start of a session that received a task from an orchestrator session.
---

Du bearbeitest **einen** Punkt eines orchestrierten Vorhabens. Der eigentliche Auftrag
steht in der Nachricht, die dir Thomas gibt; hier steht nur, was **immer** gilt.

## Arbeitsweise

- **Eigener Worktree.** Leg ihn mit `EnterWorktree` unter dem Namen an, den der Auftrag
  nennt. Arbeite ausschließlich darin.
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
  `ProjectReference`
- **wo der Auftrag nicht gestimmt hat.** Das ist der wertvollste Teil
- bei sichtbaren Änderungen: **worauf Thomas nach dem Rollout schauen soll.** Er ist der
  Einzige, der das Ergebnis auf dem echten Gerät sieht

Melde auch, was du **nicht** prüfen konntest. Eine offen genannte Lücke ist brauchbar, eine
verschwiegene macht den ganzen Bericht wertlos.
