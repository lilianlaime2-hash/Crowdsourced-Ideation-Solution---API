# Pruebas de scripts y colección HTTP

Ejecuta desde la raíz del repositorio. Los tests de C# están en [CIS.Tests](../CIS.Tests/README.md) y [CIS.IntegrationTests](../CIS.IntegrationTests/README.md).

## Controles de Node

Requieren Node.js 22:

~~~powershell
node --test tests/coverage/*.test.cjs tests/phase2/*.test.cjs
~~~

Comprueban el control de cobertura, el ejecutor, el servicio de usuarios de prueba y los reportes Postman. Son 56 casos; no llaman a tus APIs o bases locales.

## Post-migration

Requiere Node.js 22 y Docker Desktop:

~~~powershell
node --test tests/migration/post-migration-sd3-perf.test.cjs
~~~

Tiene 11 casos y usa su propio MongoDB temporal.

## Colección HTTP

La configuración de servicios, cuenta y comandos de consola está en [postman/README.md](postman/README.md).

performance conserva el trabajo previo de k6 y tiene su propia guía. Está fuera de los cinco controles del pipeline actual.
