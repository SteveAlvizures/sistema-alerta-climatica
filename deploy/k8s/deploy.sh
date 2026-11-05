#!/usr/bin/env bash
set -euo pipefail
# Explicit context and phase; no delete/prune/secret export and no implicit traffic cutover.
context="${1:?usage: deploy.sh CONTEXT storage|apps|ingress MANIFEST}"
phase="${2:?phase required}"
manifest="${3:?rendered release manifest required}"
namespace=vigiarural-phase4
k=(kubectl --context "$context")
test "$context" = "$("${k[@]}" config current-context)" || { echo 'Context mismatch'; exit 1; }
"${k[@]}" get nodes -o name
case "$phase" in
 storage)
  # This script is run on the target VPS. Refuse to overcommit the single-node migration host.
  test -r /proc/meminfo || { echo 'Run storage phase on the target Linux VPS after capacity review'; exit 1; }
  awk '/MemAvailable/ {exit ($2 < 7340032)}' /proc/meminfo || { echo 'Need at least 7 GiB available for the isolated migration environment. Review capacity; do not stop existing services blindly.'; exit 1; }
  "${k[@]}" apply --dry-run=server -f deploy/k8s/namespace.yaml >/dev/null
  "${k[@]}" apply -f deploy/k8s/namespace.yaml
  "${k[@]}" get storageclass local-path -o name
  "${k[@]}" -n "$namespace" get secret vigiarural-secrets -o name
  "${k[@]}" apply -f deploy/k8s/networkpolicy.yaml -f deploy/k8s/configmap.yaml -f deploy/k8s/mongo-init.yaml -f deploy/k8s/storage.yaml -f deploy/k8s/databases.yaml
  echo 'Restore and reconcile backups in these isolated PVCs before deploying applications. No production traffic changed.'
  ;;
 apps)
  # Created only after the operator has validated restore counts, IDs, credential hashes and backups.
  test "$("${k[@]}" -n "$namespace" get configmap migration-verification -o jsonpath='{.data.status}')" = verified
  "${k[@]}" -n "$namespace" get secret vigiarural-public-tls -o name
  "${k[@]}" apply --dry-run=server -f "$manifest" >/dev/null
  "${k[@]}" apply -f "$manifest"
  for app in auth-service community-service sensor-service alert-service audit-service report-service backend gateway frontend; do
   "${k[@]}" -n "$namespace" rollout status "deployment/$app" --timeout=300s
  done
  ;;
 ingress)
  test "$("${k[@]}" -n "$namespace" get configmap migration-verification -o jsonpath='{.data.status}')" = verified
  "${k[@]}" -n "$namespace" wait --for=condition=Available deployment --all --timeout=300s
  "${k[@]}" get crd serverstransports.traefik.io -o name
  # REVIEW all preexisting host rules before this phase; do not create a competing Ingress.
  "${k[@]}" get ingress -A
  "${k[@]}" apply --dry-run=server -f deploy/k8s/ingress.yaml >/dev/null
  "${k[@]}" apply -f deploy/k8s/ingress.yaml
  ;;
 *) echo 'Unknown phase'; exit 1;;
esac
