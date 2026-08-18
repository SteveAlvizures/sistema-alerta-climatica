# Convenciones del proyecto

## Flujo de trabajo

- `main` es la rama estable.
- `steve`, `astrid` y `walter` son ramas personales de trabajo.
- Los cambios se incorporan a `main` mediante Pull Requests.
- Los commits deben ser cortos e indicar claramente el cambio realizado.

## Nombres y código

- Los archivos y carpetas de Angular usan kebab-case.
- Las clases e interfaces de .NET usan PascalCase.
- Las variables y los métodos deben tener nombres descriptivos.
- Los nombres deben relacionarse con comunidades, sensores, lecturas, alertas y eventos climáticos.
- No se deben usar ejemplos genéricos como `Products`, `Orders`, `Todo` o `WeatherForecast`.

## Comentarios

Los comentarios de código deben ser breves y naturales. Se utilizarán principalmente para explicar decisiones, validaciones o comportamientos poco evidentes. Deben evitarse comentarios que solo repitan el funcionamiento obvio de una línea.

## Seguridad

Nunca se almacenarán contraseñas, tokens ni secretos en Git. Los valores sensibles se configurarán fuera del repositorio y los archivos de ejemplo contendrán únicamente datos demostrativos.
