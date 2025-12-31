#!/bin/bash
set -e

# Runs cluster add-ons for the production cluster (kube-vip, later more).
# Usage: ./run-addons-prod.sh [playbook] [ansible-args...]
# Or:    ./run-addons-prod.sh [ansible-args...]  (uses default playbook configure-cluster.yml)

if [[ "${1:-}" == -* ]]; then
	EXTRA_ARGS="$*"
	PLAYBOOK=configure-cluster.yml
else
	PLAYBOOK=${1:-configure-cluster.yml}
	shift || true
	EXTRA_ARGS="$*"
fi

cd "$(dirname "$0")"
ansible-playbook "$PLAYBOOK" -i inventory.ini --limit prodservers $EXTRA_ARGS
