#!/bin/bash
set -e
# Usage: ./run-on-prod.sh [playbook] [ansible-args...]
# Or:    ./run-on-prod.sh [ansible-args...]  (uses default playbook cluster.yml)
if [[ "${1:-}" == -* ]]; then
	EXTRA_ARGS="$*"
	PLAYBOOK=cluster.yml
else
	PLAYBOOK=${1:-cluster.yml}
	shift || true
	EXTRA_ARGS="$*"
fi
cd "$(dirname "$0")"
ansible-playbook "$PLAYBOOK" -i inventory.ini --limit prodservers $EXTRA_ARGS
