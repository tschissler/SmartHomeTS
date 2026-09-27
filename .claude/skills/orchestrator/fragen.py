#!/usr/bin/env python3
"""Gemeinsame Liste der Fragen an Thomas, über alle Sessions eines Vorhabens.

Jede Frage bekommt eine Nummer (F1, F2, …), die nie wiederverwendet wird – eine
Antwort, die in der Warteschlange hängt, trifft so immer die Frage, die gemeint war.

  fragen.py neu "<Frage>"             legt an, gibt die Nummer aus, meldet sie in herdr
  fragen.py offen                     alle offenen Fragen
  fragen.py beantwortet F7 "<Antwort>"
  fragen.py zurueckgezogen F7         überholt oder selbst entschieden

Der Absender ist die aufrufende Claude-Session (Name aus `claude -n`); `--von` überschreibt.
Die Liste liegt in ~/.cache/smarthome-orchestrierung/fragen.json (SMARTHOME_FRAGEN_DATEI
überschreibt) – getrennt von anderen Repos, damit deren Nummern sich nicht mischen.
"""

import argparse
import fcntl
import json
import os
import shutil
import subprocess
import sys
from contextlib import contextmanager
from datetime import datetime, timedelta

DATEI = os.environ.get(
    "SMARTHOME_FRAGEN_DATEI",
    os.path.expanduser("~/.cache/smarthome-orchestrierung/fragen.json"),
)
SESSIONS = os.path.expanduser("~/.claude/sessions")
# Erledigte Fragen bleiben einen Tag sichtbar, damit eine späte Antwort noch zuzuordnen ist.
AUFBEWAHRUNG = timedelta(hours=24)


def jetzt():
    return datetime.now().astimezone().isoformat(timespec="seconds")


def eigene_session():
    """Sucht in der Elternkette den Claude-Prozess und liest seinen Session-Eintrag."""
    pid = os.getppid()
    while pid > 1:
        pfad = os.path.join(SESSIONS, f"{pid}.json")
        if os.path.exists(pfad):
            try:
                with open(pfad, encoding="utf-8") as f:
                    return json.load(f)
            except (OSError, ValueError):
                return None
        try:
            with open(f"/proc/{pid}/stat", encoding="utf-8") as f:
                pid = int(f.read().rsplit(")", 1)[1].split()[1])
        except (OSError, ValueError, IndexError):
            return None
    return None


@contextmanager
def liste(schreiben):
    os.makedirs(os.path.dirname(DATEI), exist_ok=True)
    with open(DATEI + ".lock", "w") as sperre:
        fcntl.flock(sperre, fcntl.LOCK_EX if schreiben else fcntl.LOCK_SH)
        daten = lesen()
        yield daten
        if schreiben:
            aufraeumen(daten)
            tmp = DATEI + ".tmp"
            with open(tmp, "w", encoding="utf-8") as f:
                json.dump(daten, f, ensure_ascii=False, indent=2)
            os.replace(tmp, DATEI)


def lesen():
    try:
        with open(DATEI, encoding="utf-8") as f:
            return json.load(f)
    except FileNotFoundError:
        return {"naechste": 1, "fragen": []}


def aufraeumen(daten):
    grenze = datetime.now().astimezone() - AUFBEWAHRUNG
    daten["fragen"] = [
        q for q in daten["fragen"]
        if q["status"] == "offen" or datetime.fromisoformat(q["erledigt"]) > grenze
    ]


def finden(daten, nr):
    nr = nr.upper()
    if not nr.startswith("F"):
        nr = "F" + nr
    for q in daten["fragen"]:
        if q["nr"] == nr:
            return q
    sys.exit(f"{nr} steht nicht in der Liste (oder ist älter als einen Tag erledigt).")


def herdr_melden(frage):
    if os.environ.get("HERDR_ENV") != "1" or not shutil.which("herdr"):
        return
    subprocess.run(
        ["herdr", "notification", "show", f"{frage['nr']} von {frage['von']}",
         "--body", frage["text"][:200], "--sound", "request"],
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=5, check=False,
    )


def zeile(q):
    return f"{q['nr']} ({q['von']}, {q['gestellt'][11:16]}): {q['text']}"


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="befehl", required=True)
    neu = sub.add_parser("neu")
    neu.add_argument("text")
    neu.add_argument("--von")
    sub.add_parser("offen")
    b = sub.add_parser("beantwortet")
    b.add_argument("nr")
    b.add_argument("antwort")
    z = sub.add_parser("zurueckgezogen")
    z.add_argument("nr")
    args = p.parse_args()

    if args.befehl == "offen":
        with liste(schreiben=False) as daten:
            offen = [q for q in daten["fragen"] if q["status"] == "offen"]
        print("\n".join(zeile(q) for q in offen) if offen else "nichts offen")
        return

    with liste(schreiben=True) as daten:
        if args.befehl == "neu":
            session = eigene_session() or {}
            frage = {
                "nr": f"F{daten['naechste']}",
                "von": args.von or session.get("name") or "unbekannt",
                "session_id": session.get("sessionId"),
                "text": args.text,
                "gestellt": jetzt(),
                "status": "offen",
            }
            daten["naechste"] += 1
            daten["fragen"].append(frage)
            print(frage["nr"])
        else:
            q = finden(daten, args.nr)
            if q["status"] != "offen":
                sys.exit(f"{q['nr']} ist bereits {q['status']}: {q.get('antwort', '')}")
            q["status"] = args.befehl
            q["erledigt"] = jetzt()
            if args.befehl == "beantwortet":
                q["antwort"] = args.antwort
            print(f"{q['nr']} {args.befehl}")
            frage = None

    if args.befehl == "neu":
        herdr_melden(frage)


if __name__ == "__main__":
    main()
