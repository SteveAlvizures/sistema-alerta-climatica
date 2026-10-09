# Paginación del historial

Vigía Rural utiliza **Offset-Based Pagination** para el historial de un sensor.

```http
GET /api/sensors/{id}/readings?page=2&pageSize=20
```

## Parámetros

| Parámetro | Regla |
|---|---|
| `page` | Índice iniciado en 1. |
| `pageSize` | Registros solicitados por página; mínimo 1 y máximo 100. |

Un valor fuera del rango produce `400 Bad Request`. El parámetro anterior `limit` puede actuar como alias de `pageSize` para compatibilidad si no se envía `pageSize`.

## Implementación con EF Core

La capa Infrastructure construye primero un `IQueryable` filtrado por sensor:

```csharp
int totalCount = await query.CountAsync(cancellationToken);

var items = await query
    .OrderByDescending(reading => reading.MeasuredAt)
    .ThenByDescending(reading => reading.Id)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(cancellationToken);
```

`CountAsync()` obtiene el total para calcular metadata. `Skip()` descarta el desplazamiento y `Take()` limita la página. El orden por fecha y luego por ID evita resultados inestables cuando dos lecturas comparten fecha.

Estas operaciones ocurren sobre `IQueryable` **antes** de `ToListAsync()`. EF Core traduce el patrón para SQL Server mediante `OFFSET/FETCH` o SQL equivalente. El backend no descarga todo el historial para recortarlo en memoria.

## Envelope

```json
{
  "data": [],
  "pageIndex": 2,
  "pageSize": 20,
  "totalPages": 10,
  "totalCount": 200,
  "hasPrevious": true,
  "hasNext": true
}
```

| Campo | Significado |
|---|---|
| `data` | Lecturas de la página actual. |
| `pageIndex` | Página solicitada. |
| `pageSize` | Tamaño aplicado. |
| `totalPages` | Páginas calculadas. |
| `totalCount` | Registros que cumplen el filtro. |
| `hasPrevious` | Existe una página anterior. |
| `hasNext` | Existe una página siguiente. |

## Beneficio

El costo de memoria y transferencia depende del tamaño de página, no del historial completo. Angular cambia la query string al avanzar y muestra la metadata devuelta por la API.

La **Cursor-Based Pagination** usa un valor estable del último registro como punto de continuación y puede convenir en flujos de datos que cambian intensamente. Vigía Rural emplea Offset-Based Pagination porque facilita navegar por número de página y explicar el cálculo con `Skip/Take`.
