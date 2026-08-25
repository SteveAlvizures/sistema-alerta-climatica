# Integración continua y validación

## Estado actual

El repositorio no contiene actualmente un workflow remoto de GitHub Actions u otra plataforma de CI. Por tanto, no se afirma que exista una canalización automática publicada.

Sí existe un proceso de validación local reproducible antes de integrar cambios:

```text
Restore → Build Release → pruebas backend → npm ci → pruebas Angular
→ build Angular de producción → validación Docker → git diff --check
```

## Resultados comprobados más recientes

| Validación | Resultado |
|---|---:|
| Domain | 26/26 |
| Application | 52/52 |
| Infrastructure | 14/14 |
| Backend total | 92/92 |
| Angular | 47/47 |
| Backend Release | 0 errores, 0 advertencias |
| Angular producción | Correcto |

Los comandos completos se encuentran en [Ejecución local](ejecucion-local.md).

## Propuesta futura

Una CI futura podría ejecutar restore, builds y pruebas en cada Pull Request, sin incorporar `.env` ni secretos. La publicación automática o el despliegue a VPS no están implementados actualmente.
