# Vigía Rural

Frontend del Sistema Web de Monitoreo y Alerta Temprana para Riesgos Climáticos. Presenta el estado de comunidades, indicadores ambientales, sensores, alertas e historial reciente mediante una interfaz responsive.

## Base técnica

El proyecto utiliza Angular 20, TypeScript, componentes standalone, routing y SCSS. La aplicación admite modo claro y oscuro, respeta la preferencia del sistema y permite guardar la selección del tema en el navegador.

La organización principal de `src/app` es:

- `core`: modelos y servicios compartidos por toda la aplicación.
- `shared`: componentes visuales reutilizables.
- `layout`: encabezado, navegación y estructura principal.
- `features`: módulos funcionales organizados por dominio.

El dashboard puede consultar comunidades, sensores y últimas lecturas desde la API, o utilizar el servicio temporal de simulación. El modo activo se identifica y puede cambiarse desde el encabezado.

Las solicitudes utilizan la ruta relativa `/api`. En desarrollo, `proxy.conf.json` redirige esa ruta hacia `http://localhost:5152`; `npm start` aplica el proxy automáticamente. Para probar la integración local, inicia primero la API con su perfil HTTP y después ejecuta el frontend.

En producción, el frontend no depende de una dirección local ni del nombre de un contenedor. Nginx debe servir la aplicación y redirigir `/api` hacia el contenedor de la API dentro de la red de Docker. SignalR se incorporará en una etapa posterior.

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
