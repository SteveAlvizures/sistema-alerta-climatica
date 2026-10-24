# Vigía Rural - Preparación para Kubernetes

Estos manifiestos corresponden a la preparación solicitada para la Fase 3.

No se realiza despliegue real en Kubernetes durante esta fase.

## Contenido

- namespace.yaml
- configmap.yaml
- secret.example.yaml
- apps.yaml
- services.yaml

## Servicios preparados

- Auth Service - puerto 3001
- Community Service - puerto 3002
- Alert Service - puerto 3003
- Sensor Service - puerto 3004
- Report Service - puerto 3005
- Audit Service - puerto 3006
- API Gateway - puerto 8080

## Preparación incluida

Los servicios están preparados para:

- configuración mediante variables de entorno;
- secretos mediante Kubernetes Secrets;
- configuración no sensible mediante ConfigMaps;
- readiness probes;
- liveness probes;
- terminación con período de gracia;
- comunicación interna mediante Kubernetes Services;
- ejecución stateless de los microservicios.

## Bases de datos

Las bases de datos no se incluyen como Deployments en estos manifiestos.

En un despliegue Kubernetes real se recomienda utilizar servicios administrados o StatefulSets con PersistentVolumeClaims para:

- PostgreSQL;
- SQL Server;
- MongoDB.

Cada microservicio mantiene su propia base de datos y no accede directamente a la base de datos de otro servicio.

## Report Service

Report Service utiliza:

- Python;
- FastAPI;
- MongoDB;
- Jinja2;
- WeasyPrint.

Consume Community Service, Sensor Service y Alert Service mediante sus APIs.

No consulta directamente las bases de datos de esos servicios.

## Secrets

`secret.example.yaml` contiene únicamente valores de ejemplo.

No deben almacenarse contraseñas reales ni JWT_SECRET reales en Git.

Antes de un despliegue real debe crearse un Secret con los valores correspondientes al entorno.

## Fase 3

Estos archivos demuestran preparación para Kubernetes.

No ejecutar `kubectl apply` como parte de la Fase 3 salvo que posteriormente se solicite de forma explícita.
