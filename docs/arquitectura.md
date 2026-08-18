# Arquitectura del sistema

## Propósito

El Sistema de Monitoreo y Alerta Climática permitirá recopilar y procesar lecturas relacionadas con riesgos climáticos en comunidades. En la primera etapa trabajará con sensores simulados y quedará preparado para integrar dispositivos físicos posteriormente.

## Componentes previstos

- El frontend se desarrollará con Angular.
- El backend se desarrollará con ASP.NET Core y se organizará mediante Clean Architecture.
- La persistencia de datos utilizará SQL Server.
- Los servicios se ejecutarán mediante Docker.

El backend separará las responsabilidades de dominio, aplicación, infraestructura y presentación. Esta organización permitirá cambiar el mecanismo de captura de lecturas sin duplicar las reglas de procesamiento.

## Lecturas de sensores

Al inicio, el sistema generará lecturas simuladas. En una etapa futura podrá recibir lecturas de sensores físicos conectados a Arduino o ESP32 mediante una API HTTP o un adaptador MQTT.

Las lecturas simuladas y físicas pasarán por la misma capa de aplicación. Cada lectura identificará su origen como `Simulated` o `Physical`. Para los dispositivos físicos se registrará el código del dispositivo y la fecha y hora de su última comunicación.

Los dispositivos nunca accederán directamente a SQL Server. Toda lectura física ingresará por el backend, donde podrá validarse y procesarse antes de persistirse.

## Estructura prevista del repositorio

```text
frontend/
  angular-app/
backend/
tests/
docker/
docs/
docker-compose.yml
.env.example
.gitignore
README.md
```

Esta estructura representa la organización prevista; los proyectos de frontend y backend se incorporarán en etapas posteriores.

## Documentación relacionada

- [Modelo conceptual del dominio](modelo-dominio.md)
- [Reglas de alertas climáticas](reglas-alertas.md)
- [Ejecución local con Docker](ejecucion-local.md)
