# Vigía Rural

Frontend del Sistema Web de Monitoreo y Alerta Temprana para Riesgos Climáticos. Presenta el estado de comunidades, indicadores ambientales, sensores, alertas e historial reciente mediante una interfaz responsive.

## Base técnica

El proyecto utiliza Angular 20, TypeScript, componentes standalone, routing y SCSS. La aplicación admite modo claro y oscuro, respeta la preferencia del sistema y permite guardar la selección del tema en el navegador.

La organización principal de `src/app` es:

- `core`: modelos y servicios compartidos por toda la aplicación.
- `shared`: componentes visuales reutilizables.
- `layout`: encabezado, navegación y estructura principal.
- `features`: módulos funcionales organizados por dominio.

Actualmente, un servicio temporal proporciona lecturas simuladas y mantiene un historial corto para las tendencias climáticas. Más adelante este servicio se reemplazará por la integración con la API y SignalR.

## Comandos

Iniciar el entorno local:

```bash
npm start
```

Generar la compilación de producción:

```bash
npm run build
```

Ejecutar las pruebas una vez, sin modo de observación:

```bash
npm test -- --watch=false
```
