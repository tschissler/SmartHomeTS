#!/bin/bash
set -e
PLAYBOOK=${1:-cluster.yml}
cd "$(dirname "$0")"
if [[ "${1:-}" == -* ]]; then
	EXTRA_ARGS="$*"
	PLAYBOOK=cluster.yml
else
	PLAYBOOK=${1:-cluster.yml}
	shift || true
	EXTRA_ARGS="$*"
fi
ansible-playbook "$PLAYBOOK" -i inventory.ini --limit testservers $EXTRA_ARGS
