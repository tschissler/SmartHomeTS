# InfluxDB-Modellierung

Wie Messwerte in InfluxDB 3 abgelegt werden: welche Tabelle, Feld oder Tag, und warum.
Verbindlich für alles, was neu dazukommt.

> ## ⚠️ Bevor du diese Regeln anwendest
>
> **Passt eine Regel auf deinen Fall nicht, oder beantwortet sie ihn nicht eindeutig:
> nicht selbst entscheiden — mit Thomas besprechen.**
>
> Diese Regeln sind aus mehreren verworfenen Ansätzen entstanden und beschreiben, was sich
> bewährt hat. Sie sind keine vollständige Theorie. Ein Fall, der sich nicht sauber
> einordnen lässt, ist deshalb kein Anlass, die Regel kreativ zu dehnen, sondern ein
> Hinweis darauf, dass sie ergänzt werden muss.
>
> Das ist nicht formal gemeint. Eine Tabelle, die einmal Daten trägt, lässt sich nur noch
> unter Verlust ändern: Die Historie bleibt zurück, und eine über zwei Tabellen geteilte
> Reihe braucht künftig ein `UNION` in jeder Abfrage. Eine halbe Stunde Abstimmung vorher
> ist billiger als jede Migration danach.

## Die Trennachse: die Einheit, nicht der Datentyp

**Tabellen werden nach der Einheit geschnitten.** Der Speichertyp folgt daraus, er begründet
keine Tabelle.

Der Bestand ist faktisch schon so aufgebaut:

| Tabelle | Feld(er) | Typ |
|---|---|---|
| `energy_values` | `value_kwh`, `value_cumulated_kwh`, `value_delta_kwh` | Float64 |
| `power_values` | `value_watt` | Float64 |
| `temperature_values` | `value_temp` | Float64 |
| `percent_values` | `value_percent` | Float64 |
| `voltage_values` | `value_volt` | Float64 |
| `volume_values` | `value_volume` | Float64 |
| `distance_values` | `value_km` | Float64 |
| `position_values` | `value_latitude`, `value_longitude` | Float64 |
| `counter_values` | `value_counter` | Int64 |
| `status_values` | `value_status` | Int64 |

Sechs der ursprünglich acht Tabellen sind `Float64` — der Typ trennt dort gar nichts. Was
sie trennt, ist die Einheit, und das ist der Gewinn: **Wer `power_values` abfragt, weiß ohne
Nachschlagen, dass Watt herauskommen.** Eine Tabelle ohne Einheit zwingt jeden Leser, erst
`measurement` zu lesen und zu wissen, was dieser Name bedeutet.

Deshalb ist der Datentyp eine Speicherentscheidung, keine Modellierungsentscheidung. Wer aus
einem zu schmalen Feldtyp auf eine Modellgrenze schließt, verwechselt beides — genau das ist
bei Punkt 17 des Lade-Backlogs passiert.

## Feld oder `measurement`-Tag?

> **Feld, wenn die Werte ohne einander unvollständig sind.
> `measurement`-Tag, wenn jeder für sich eine Aussage ist.**

| Fall | Form | warum |
|---|---|---|
| `value_latitude` + `value_longitude` | Felder | Eine Breite ohne Länge ist keine halbe Position, sondern keine |
| die drei kWh-Aspekte | Felder | Dieselbe Messung, gemeinsam erhoben |
| `LadeleistungPv` / `-Batterie` / `-Netz` | Tags | Einzeln sinnvoll, einzeln abgefragt |
| `remainingRange` / `mileage` | Tags | Unabhängig voneinander, teilen nur die Einheit |

Der praktische Grund: Ein Feld erzeugt in jeder Zeile der Tabelle eine Spalte, auch wenn nur
einer der Werte geschrieben wird. Drei unabhängige Größen als Felder hießen zwei `NULL` je
Zeile. Ein Tag kostet das nicht.

## Fehlende Werte

**Ein fehlender Wert wird nicht geschrieben.** Eine `0` in der Historie ist eine Messung, die
nie stattgefunden hat — und sie ist von einer echten Null nicht zu unterscheiden.

## Der Zeitstempel

**Der Zeitstempel ist die Messzeit, nicht die Ankunftszeit.** Ein gesunder Connector kann
wochenalte Werte publizieren: Der BMW trug am 2026-09-20 einen `lastUpdate` vom 2026-08-16,
weil CarData nur bei Fahrzeugereignissen sendet. Mit der Ankunftszeit geschrieben, entstünde
bei jedem Neustart eine Linie frisch aussehender Punkte mit einem uralten Wert — schlimmer
als keine Daten, weil sie glaubwürdig aussieht.

Daraus folgt die Entdopplung von selbst: Der Primärschlüssel ist Tabelle + Tag-Satz + Zeit.
Ein wiederholt empfangener retained Payload erzeugt dieselbe Zeile, keine neue.

## Bekannte Ausnahme: `counter_values`

`counter_values` trägt `Betriebsstunden_Waermeerzeuger` (Stunden) und
`Schaltzyklen_Waermeerzeuger` (dimensionslos) nebeneinander — zwei Größen, zusammengehalten
allein vom Datentyp. Das widerspricht der Trennachse.

**Es wird trotzdem nicht migriert**, und das ist eine bewusste Entscheidung: Der einzige
reale Schaden wäre ein Überlauf, und den behebt das Streichen von `Convert.ToInt16` im
`InfluxDB3Connector` vollständig, ohne eine Zeile Bestandsdaten anzufassen. Eine über zwei
Tabellen geteilte Historie bräuchte dagegen in jeder Abfrage ein `UNION`.

**Nichts Neues kommt in `counter_values`.**

## Offen

**`energy_values` trägt drei Felder.** `value_kwh`, `value_cumulated_kwh` und
`value_delta_kwh` fallen gemeinsam an, tragen also die Regel — aber `value_kwh` wird soweit
erkennbar von niemandem mehr geschrieben. Schreibt künftig jemand nur den Zählerstand ohne
Delta, steht in jeder Zeile ein `NULL`, und die Begründung „fallen gemeinsam an" stimmt nicht
mehr. Dann ist entweder die Ausnahme „ein Feld darf leer bleiben, wenn die Größe es zulässt"
nachzutragen oder die Tabelle zu bereinigen. **Noch nicht entschieden.**

Ebenfalls nie geprüft: die sieben Tags, die jede Tabelle trägt (`measurement_id`, `category`,
`sub_category`, `sensor_type`, `location`, `device`, `measurement`) — ob alle tragen, was
`measurement_id` leistet, wenn die übrigen sechs schon eindeutig sind, und wie sich die
Kardinalität mit wachsender Gerätezahl entwickelt.

---

*Festgelegt am 2026-09-20. Verbindungsdetails, Datenbanknamen und Abfragemuster stehen nicht
hier, sondern in `docs/influxdb-reference.md` des Repos `forgejo.intern/thomas/Grafana`.*
