# Documentación oficial de Fase 3

Estado del repositorio: `steve-fase3`, rama de integración `integracion-final-fase3`, desde `steve-fase3` / `5bc6bce`.
Este índice delimita el entregable técnico y de usuario. El material personal de
estudio está separado del directorio oficial y no forma parte de este índice.

| Documento | Público y contenido |
|---|---|
| [Informe universitario final](INFORME_FINAL_FASE3.md) | Informe de entrega, arquitectura alcanzada, resultados y límites |
| [Manual de usuario](manual-usuario.md) | Uso de sesión, navegación, filtros, historial y descargas; capturas por completar |
| [Manual técnico](manual-tecnico.md) | Requisitos, instalación, configuración, componentes, operación y recuperación |
| [Arquitectura](arquitectura.md) | Estado actual y objetivo, flujos, propiedad, dependencias y decisiones |
| [Guía de desarrollo interno](desarrollo-interno.md) | Módulos, comentarios de diseño y procedimiento para trabajar sin romper contratos |
| [API](api.md) | Entrada pública, contratos Auth/Report y baseline del monolito |
| [Seguridad](seguridad.md) | JWT, permisos, identidad vigente, cabeceras y persistencia |
| [Despliegue](despliegue.md) | Compose, importación de identidad, producción y rollback |
| [Pruebas](pruebas.md) | Única tabla vigente de resultados, comandos y límites de evidencia |
| [Troubleshooting](troubleshooting.md) | Síntomas comprobados, diagnóstico y recuperación |
| [Integración pendiente](integracion-pendiente.md) | Paridad pendiente, contratos y criterios para el corte |
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

Auth y Report se conservan. Community, Sensor, Alert y Audit ya tienen implementaciones
separadas y persistencia propia en el overlay final. El modo base conserva rutas
legacy hasta el corte; la validación integrada activa los nuevos clusters después de
importar datos. Backend/SQL permanecen para composición HTTP, feed histórico y respaldo
de transición. La paridad completa, auditoría de todas las mutaciones, producción,
OAuth/OIDC y Kubernetes siguen pendientes. Ver el informe y pruebas para distinguir
implementación de evidencia ejecutada.
