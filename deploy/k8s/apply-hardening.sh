#!/bin/bash
set -e

NAMESPACE="vigiarural"

kubectl patch deployment auth-service -n "$NAMESPACE" --type='strategic' -p='
spec:
  template:
    spec:
      containers:
      - name: auth-service
        securityContext:
          allowPrivilegeEscalation: false
          runAsNonRoot: true
          runAsUser: 1000
          runAsGroup: 1000
          capabilities:
            drop:
            - ALL
          seccompProfile:
            type: RuntimeDefault
        resources:
          requests:
            cpu: 50m
            memory: 96Mi
          limits:
            cpu: 500m
            memory: 256Mi
'

kubectl patch deployment community-service -n "$NAMESPACE" --type='strategic' -p='
spec:
  template:
    spec:
      containers:
      - name: community-service
        securityContext:
          allowPrivilegeEscalation: false
          capabilities:
            drop:
            - ALL
          seccompProfile:
            type: RuntimeDefault
        resources:
          requests:
            cpu: 50m
            memory: 128Mi
          limits:
            cpu: 500m
            memory: 384Mi
'

kubectl patch deployment alert-service -n "$NAMESPACE" --type='strategic' -p='
spec:
  template:
    spec:
      containers:
      - name: alert-service
        securityContext:
          allowPrivilegeEscalation: false
          capabilities:
            drop:
            - ALL
          seccompProfile:
            type: RuntimeDefault
        resources:
          requests:
            cpu: 100m
            memory: 256Mi
          limits:
            cpu: "1"
            memory: 640Mi
'

kubectl patch deployment sensor-service -n "$NAMESPACE" --type='strategic' -p='
spec:
  template:
    spec:
      containers:
      - name: sensor-service
        securityContext:
          allowPrivilegeEscalation: false
          capabilities:
            drop:
            - ALL
          seccompProfile:
            type: RuntimeDefault
        resources:
          requests:
            cpu: 50m
            memory: 128Mi
          limits:
            cpu: 500m
            memory: 384Mi
'

kubectl patch deployment report-service -n "$NAMESPACE" --type='strategic' -p='
spec:
  template:
    spec:
      containers:
      - name: report-service
        securityContext:
          allowPrivilegeEscalation: false
          capabilities:
            drop:
            - ALL
          seccompProfile:
            type: RuntimeDefault
        resources:
          requests:
            cpu: 50m
            memory: 96Mi
          limits:
            cpu: 500m
            memory: 256Mi
'

kubectl patch deployment audit-service -n "$NAMESPACE" --type='strategic' -p='
spec:
  template:
    spec:
      containers:
      - name: audit-service
        securityContext:
          allowPrivilegeEscalation: false
          capabilities:
            drop:
            - ALL
          seccompProfile:
            type: RuntimeDefault
        resources:
          requests:
            cpu: 50m
            memory: 64Mi
          limits:
            cpu: 500m
            memory: 192Mi
'

kubectl patch deployment gateway -n "$NAMESPACE" --type='strategic' -p='
spec:
  template:
    spec:
      containers:
      - name: gateway
        securityContext:
          allowPrivilegeEscalation: false
          runAsNonRoot: true
          capabilities:
            drop:
            - ALL
          seccompProfile:
            type: RuntimeDefault
        resources:
          requests:
            cpu: 50m
            memory: 96Mi
          limits:
            cpu: 500m
            memory: 256Mi
'

echo "Hardening aplicado correctamente."
