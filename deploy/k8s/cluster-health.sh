#!/bin/bash

NAMESPACE="vigiarural"
DOMAIN="https://vigiarural.site"

GREEN="\033[0;32m"
YELLOW="\033[1;33m"
RED="\033[0;31m"
CYAN="\033[0;36m"
NC="\033[0m"

echo
echo -e "${CYAN}====================================================${NC}"
echo -e "${CYAN}      VIGÍA RURAL - MONITOR DE SALUD K3S${NC}"
echo -e "${CYAN}====================================================${NC}"
echo "Fecha: $(date)"
echo

echo -e "${CYAN}[1] ESTADO DE PODS${NC}"
kubectl get pods -n "$NAMESPACE"
echo

NOT_READY=$(kubectl get pods -n "$NAMESPACE" --no-headers 2>/dev/null | \
awk '{
  split($2, ready, "/");
  if (ready[1] != ready[2] || $3 != "Running") {
    count++
  }
}
END {
  print count+0
}')

if [ "$NOT_READY" -eq 0 ]; then
    echo -e "${GREEN}OK: Todos los Pods están saludables.${NC}"
else
    echo -e "${RED}ADVERTENCIA: Hay $NOT_READY Pod(s) con problemas.${NC}"
fi

echo
echo -e "${CYAN}[2] USO DE CPU Y MEMORIA${NC}"
kubectl top pods -n "$NAMESPACE" 2>/dev/null || \
echo -e "${YELLOW}Metrics Server no disponible.${NC}"

echo
echo -e "${CYAN}[3] ESCALABILIDAD HPA${NC}"
kubectl get hpa -n "$NAMESPACE"

echo
echo -e "${CYAN}[4] ALMACENAMIENTO PERSISTENTE${NC}"
kubectl get pvc -n "$NAMESPACE"

PVC_PENDING=$(kubectl get pvc -n "$NAMESPACE" --no-headers 2>/dev/null | \
awk '$2 != "Bound" {count++} END {print count+0}')

if [ "$PVC_PENDING" -eq 0 ]; then
    echo -e "${GREEN}OK: Todos los PVC están enlazados.${NC}"
else
    echo -e "${RED}ADVERTENCIA: Hay $PVC_PENDING PVC sin enlazar.${NC}"
fi

echo
echo -e "${CYAN}[5] SERVICES${NC}"
kubectl get svc -n "$NAMESPACE"

echo
echo -e "${CYAN}[6] INGRESS${NC}"
kubectl get ingress -n "$NAMESPACE"

echo
echo -e "${CYAN}[7] HTTPS PÚBLICO${NC}"

HTTP_CODE=$(curl -k -s -o /dev/null -w "%{http_code}" "$DOMAIN")

if [ "$HTTP_CODE" = "200" ]; then
    echo -e "${GREEN}OK: $DOMAIN responde HTTP $HTTP_CODE.${NC}"
else
    echo -e "${RED}ADVERTENCIA: $DOMAIN responde HTTP $HTTP_CODE.${NC}"
fi

echo
echo -e "${CYAN}[8] OAUTH GOOGLE${NC}"

GOOGLE_CODE=$(curl -k -s -o /dev/null -w "%{http_code}" \
"$DOMAIN/api/auth/oauth/google")

if [ "$GOOGLE_CODE" = "302" ]; then
    echo -e "${GREEN}OK: OAuth Google responde correctamente con 302.${NC}"
else
    echo -e "${YELLOW}ADVERTENCIA: OAuth Google respondió $GOOGLE_CODE.${NC}"
fi

echo
echo -e "${CYAN}[9] OAUTH GITHUB${NC}"

GITHUB_CODE=$(curl -k -s -o /dev/null -w "%{http_code}" \
"$DOMAIN/api/auth/oauth/github")

if [ "$GITHUB_CODE" = "302" ]; then
    echo -e "${GREEN}OK: OAuth GitHub responde correctamente con 302.${NC}"
else
    echo -e "${YELLOW}ADVERTENCIA: OAuth GitHub respondió $GITHUB_CODE.${NC}"
fi

echo
echo -e "${CYAN}====================================================${NC}"

if [ "$NOT_READY" -eq 0 ] && \
   [ "$PVC_PENDING" -eq 0 ] && \
   [ "$HTTP_CODE" = "200" ] && \
   [ "$GOOGLE_CODE" = "302" ] && \
   [ "$GITHUB_CODE" = "302" ]; then
    echo -e "${GREEN}ESTADO GENERAL: SISTEMA SALUDABLE${NC}"
else
    echo -e "${YELLOW}ESTADO GENERAL: REVISAR ADVERTENCIAS${NC}"
fi

echo -e "${CYAN}====================================================${NC}"
echo
