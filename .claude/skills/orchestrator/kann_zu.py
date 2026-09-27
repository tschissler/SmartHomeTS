#!/usr/bin/env python3
"""Sessions, die Thomas schließen kann – gesetzt von der Orchestrierung, gezeigt in der Übersicht.

  kann_zu.py setzen <Session> "<Grund>"   z. B. "Punkt gemergt, Retro verwertet"
  kann_zu.py zuruecknehmen <Session>
  kann_zu.py liste

Die Markierung hängt an der laufenden Session (ihrer Id), nicht am Namen: Wird die Session
geschlossen, verschwindet sie; eine neue Session gleichen Namens erbt sie nicht.
Sie liegt in derselben Datei wie die Fragen (fragen.py).
"""

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fragen  # noqa: E402


def laufende_sessions():
    """Name → Session-Eintrag aller laufenden interaktiven Sessions."""
    ergebnis = {}
    try:
        dateien = [d for d in os.listdir(fragen.SESSIONS) if d.endswith(".json")]
    except FileNotFoundError:
        return ergebnis
    for datei in dateien:
        try:
            with open(os.path.join(fragen.SESSIONS, datei), encoding="utf-8") as f:
                s = json.load(f)
        except (OSError, ValueError):
            continue
        if s.get("kind") != "interactive" or not s.get("name"):
            continue
        try:
            os.kill(s.get("pid", 0), 0)
        except ProcessLookupError:
            continue
        except PermissionError:
            pass
        ergebnis[s["name"]] = s
    return ergebnis


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="befehl", required=True)
    s = sub.add_parser("setzen")
    s.add_argument("session")
    s.add_argument("grund")
    z = sub.add_parser("zuruecknehmen")
    z.add_argument("session")
    sub.add_parser("liste")
    args = p.parse_args()

    laufend = laufende_sessions()
    ids = {e.get("sessionId") for e in laufend.values()}

    if args.befehl == "liste":
        with fragen.liste(schreiben=False) as daten:
            eintraege = [k for k in daten.get("kann_zu", []) if k["session_id"] in ids]
        print("\n".join(f"{k['name']}: {k['grund']}" for k in eintraege) or "keine")
        return

    with fragen.liste(schreiben=True) as daten:
        # Geschlossene Sessions fallen bei jedem Schreiben heraus.
        eintraege = [k for k in daten.get("kann_zu", [])
                     if k["session_id"] in ids and k["name"] != args.session]
        if args.befehl == "setzen":
            session = laufend.get(args.session)
            if not session:
                sys.exit(f"Keine laufende Session „{args.session}“ – Namen wie in der Übersicht angeben.")
            eintraege.append({"name": args.session, "session_id": session.get("sessionId"),
                              "grund": args.grund, "seit": fragen.jetzt()})
        daten["kann_zu"] = eintraege
    print(f"{args.session} {'kann zu' if args.befehl == 'setzen' else 'nicht mehr als schließbar markiert'}")


if __name__ == "__main__":
    main()
