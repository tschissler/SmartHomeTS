#!/usr/bin/env python3
"""Übersicht für Thomas: welche Session was tut und wo seine Antwort fehlt.

  uebersicht.py            zeichnet sich alle zwei Sekunden neu (Strg+C beendet)
  uebersicht.py --einmal   einmal ausgeben
  uebersicht.py --oeffnen  in herdr einen Bereich rechts abteilen und die Übersicht dort starten

Den Stand der Sessions liest sie aus herdr (erkennt auch Freigabe-Dialoge) und aus
~/.claude/sessions, die offenen Fragen aus der Liste von fragen.py, die schließbaren
Sessions aus kann_zu.py.
"""

import json
import os
import shutil
import subprocess
import sys
import textwrap
import time
from datetime import datetime

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fragen  # noqa: E402

SKRIPT = os.path.abspath(__file__)
# Aus einem Worktree heraus gilt dasselbe Repo: alles vor /.claude/ ist die Wurzel.
REPO = SKRIPT.split("/.claude/")[0]

ROT, GELB, GRUEN, GRAU, FETT, AUS = "\033[31m", "\033[33m", "\033[32m", "\033[90m", "\033[1m", "\033[0m"

# herdr-Zustand → (Rang für die Sortierung, Symbol, Text, Farbe)
ZUSTAENDE = {
    "blocked": (0, "⚠", "braucht Freigabe", ROT),
    "done": (2, "✔", "fertig", GRUEN),
    "idle": (3, "○", "bereit", GRAU),
    "unknown": (3, "?", "unklar", GRAU),
    "working": (4, "◐", "arbeitet", ""),
    # Von der Orchestrierung gesetzt (kann_zu.py), nicht von herdr – steht zuletzt.
    "kann_zu": (5, "✕", "kann zu", GRUEN),
}
# Fallback ohne herdr: der Zustand, den Claude Code selbst mitschreibt.
CLAUDE_ZUSTAND = {"busy": "working", "idle": "idle", "waiting": "blocked"}


def lebt(pid):
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True


def herdr_zustaende():
    if not shutil.which("herdr"):
        return {}
    try:
        aus = subprocess.run(["herdr", "agent", "list"], capture_output=True, text=True, timeout=3).stdout
        agenten = json.loads(aus)["result"]["agents"]
    except (subprocess.SubprocessError, ValueError, KeyError, OSError):
        return {}
    return {
        (a.get("agent_session") or {}).get("value"): a.get("agent_status")
        for a in agenten
    }


def sessions(bekannte=()):
    """bekannte: Session-IDs, die die Orchestrierung führt (kann_zu) — sie zählen auch
    dann mit, wenn ihr Worktree hier schon abgeräumt ist und ihr cwd woanders liegt."""
    herdr = herdr_zustaende()
    ergebnis = []
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
        # Eine Session, die in einem zweiten Repo arbeitet (etwa im Deployments-Repo), steht
        # mit ihrem cwd außerhalb — sie zählt trotzdem mit, wenn sie hier einen Worktree hat.
        eigener_worktree = os.path.isdir(os.path.join(REPO, ".claude", "worktrees", s.get("name") or "-"))
        dazugehoerig = (s.get("cwd", "").startswith(REPO) or eigener_worktree
                        or s.get("sessionId") in bekannte)
        if s.get("kind") != "interactive" or not dazugehoerig:
            continue
        if not lebt(s.get("pid", 0)):
            continue
        zustand = herdr.get(s.get("sessionId")) or CLAUDE_ZUSTAND.get(s.get("status"), "unknown")
        ergebnis.append({"name": s.get("name") or s.get("sessionId", "")[:8], "zustand": zustand,
                         "session_id": s.get("sessionId"),
                         "worktree": "/.claude/worktrees/" in s.get("cwd", "")})
    return ergebnis


def zeichnen(breite):
    with fragen.liste(schreiben=False) as daten:
        alle = daten["fragen"]
        kann_zu = {k["session_id"]: k for k in daten.get("kann_zu", [])}
    offen = [q for q in alle if q["status"] == "offen"]
    erledigt = sorted((q for q in alle if q["status"] != "offen"), key=lambda q: q["erledigt"])[-3:]
    liste = sessions(bekannte=kann_zu.keys())
    namen = {s["name"] for s in liste}
    for s in liste:
        if s["session_id"] in kann_zu:
            s["zustand"] = "kann_zu"

    def rang(s):
        eigene = [q for q in offen if q["von"] == s["name"]]
        r = ZUSTAENDE.get(s["zustand"], ZUSTAENDE["unknown"])[0]
        return (min(r, 1) if eigene else r, s["name"])

    zeilen = [f"{FETT}SmartHome-Sessions{AUS}".ljust(breite - 5 + len(FETT) + len(AUS)) + datetime.now().strftime("%H:%M")]
    for s in sorted(liste, key=rang):
        _, symbol, text, farbe = ZUSTAENDE.get(s["zustand"], ZUSTAENDE["unknown"])
        nummern = " ".join(q["nr"] for q in offen if q["von"] == s["name"])
        name = s["name"] + (" ⎇" if s["worktree"] else "")
        sichtbar = f"{symbol} {name}  {text}" + (f"  {nummern}" if nummern else "")
        zeile = (f"{farbe}{symbol} {name}{AUS}  {farbe}{text}{AUS}"
                 + (f"  {GELB}{nummern}{AUS}" if nummern else ""))
        if s["zustand"] == "kann_zu":
            platz = breite - len(sichtbar) - 3
            grund = kann_zu[s["session_id"]]["grund"]
            if platz > 5:
                grund = grund if len(grund) <= platz else grund[:platz - 1] + "…"
                zeile += f"  {GRAU}{grund}{AUS}"
        zeilen.append(zeile)
    if not liste:
        zeilen.append(f"{GRAU}keine laufende Session{AUS}")

    zeilen.append("")
    if offen:
        zeilen.append(f"{FETT}{GELB}Offen bei Thomas ({len(offen)}){AUS}")
        for q in offen:
            weg = "" if q["von"] in namen else f" {GRAU}(Session beendet){AUS}"
            zeilen.append(f"{GELB}{FETT}{q['nr']}{AUS} {GRAU}{q['von']} · {q['gestellt'][11:16]}{AUS}{weg}")
            zeilen += textwrap.wrap(q["text"], breite - 2, initial_indent="  ", subsequent_indent="  ")
    else:
        zeilen.append(f"{GRUEN}Nichts offen bei Thomas{AUS}")

    if erledigt:
        zeilen.append("")
        zeilen.append(f"{GRAU}Zuletzt zugeordnet{AUS}")
        for q in reversed(erledigt):
            wie = f"→ {q['antwort']}" if q["status"] == "beantwortet" else "zurückgezogen"
            zeilen += textwrap.wrap(f"{q['nr']} {wie}", breite - 2, initial_indent="  ", subsequent_indent="    ")
    return "\n".join(zeilen)


def laeuft_schon():
    for pid in filter(str.isdigit, os.listdir("/proc")):
        if int(pid) == os.getpid():
            continue
        try:
            with open(f"/proc/{pid}/cmdline", "rb") as f:
                teile = f.read().decode(errors="replace").split("\0")
        except OSError:
            continue
        if any(t.endswith("uebersicht.py") for t in teile) and "--oeffnen" not in teile:
            return True
    return False


def oeffnen():
    if laeuft_schon():
        print("Die Übersicht läuft schon.")
        return
    if os.environ.get("HERDR_ENV") != "1" or not shutil.which("herdr"):
        sys.exit(f"Nicht in herdr – von Hand starten: python3 {SKRIPT}")
    aus = subprocess.run(
        ["herdr", "pane", "split", "--current", "--direction", "right", "--cwd", REPO, "--no-focus"],
        capture_output=True, text=True, timeout=5,
    ).stdout
    try:
        bereich = json.loads(aus)["result"]["pane"]["pane_id"]
    except (ValueError, KeyError):
        sys.exit(f"herdr hat keinen Bereich geliefert: {aus.strip()}")
    subprocess.run(["herdr", "pane", "rename", bereich, "Übersicht"],
                   capture_output=True, timeout=5, check=False)
    subprocess.run(["herdr", "pane", "run", bereich, f"python3 {SKRIPT}"],
                   capture_output=True, timeout=5, check=False)
    print(f"Übersicht in Bereich {bereich} gestartet.")


def main():
    if "--oeffnen" in sys.argv:
        oeffnen()
        return
    if "--einmal" in sys.argv:
        print(zeichnen(shutil.get_terminal_size((60, 20)).columns))
        return
    sys.stdout.write("\033[?25l")
    try:
        while True:
            bild = zeichnen(shutil.get_terminal_size((60, 20)).columns)
            sys.stdout.write("\033[H\033[J" + bild)
            sys.stdout.flush()
            time.sleep(2)
    except KeyboardInterrupt:
        pass
    finally:
        sys.stdout.write("\033[?25h\n")


if __name__ == "__main__":
    main()
