# Documentación oficial de Fase 3

Estado del repositorio: `steve-fase3`, base `764ac02` y ajustes locales posteriores.
Este índice delimita el entregable técnico y de usuario. El material personal de
estudio está separado del directorio oficial y no forma parte de este índice.

| Documento | Público y contenido |
|---|---|
| [Manual de usuario](manual-usuario.md) | Uso de sesión, navegación, filtros, historial y descargas; capturas por completar |
| [Manual técnico](manual-tecnico.md) | Requisitos, instalación, configuración, componentes, operación y recuperación |
| [Arquitectura](arquitectura.md) | Estado actual y objetivo, flujos, propiedad, dependencias y decisiones |
| [Guía de desarrollo interno](desarrollo-interno.md) | Módulos, comentarios de diseño y procedimiento para trabajar sin romper contratos |
| [API](api.md) | Entrada pública, contratos Auth/Report y baseline del monolito |
| [Seguridad](seguridad.md) | JWT, permisos, identidad vigente, cabeceras y persistencia |
| [Despliegue](despliegue.md) | Compose, importación de identidad, producción y rollback |
| [Pruebas](pruebas.md) | Única tabla vigente de resultados, comandos y límites de evidencia |
| [Troubleshooting](troubleshooting.md) | Síntomas comprobados, diagnóstico y recuperación |
| [Integración pendiente](integracion-pendiente.md) | Contratos requeridos de Community/Sensor/Alert/Audit y cambio de destinos |
| [Gateway](gateway.md) | Enrutamiento YARP y operación del componente |
| [Auth Service](auth-service.md) | Compatibilidad de credenciales, persistencia, outbox e importación |
| [Report Service](report-service.md) | Tipos PDF, semántica, límites, MongoDB/GridFS y Angular |
| [Revisión de cierre](revision-final-steve.md) | Correcciones verificadas, hallazgos y limitaciones |

Los documentos `validacion-gateway.md`, `validacion-auth-service.md` y
`validacion-reportes.md` se mantienen como puntos de entrada por componente y
remiten al registro vigente de pruebas. No contienen conteos de incrementos obsoletos.

## Estados usados

- **IMPLEMENTADO:** existe código/configuración en esta rama.
- **PROBADO LOCALMENTE:** hay evidencia de ejecución en el entorno aislado descrito
  en [pruebas](pruebas.md); no implica producción ni carga sostenida.
- **PENDIENTE:** no implementado o no validado; se identifica expresamente.

Auth y Report están extraídos. Community, Sensor, Alert y Audit **siguen en el
monolito**, junto con dashboard, reglas y eventos. No hay OAuth/OIDC ni despliegue
Kubernetes/K3s ejecutado. La documentación se revisó contra el código; esta revisión
documental no vuelve a ejecutar las suites funcionales anteriores.
