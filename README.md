# CIS API

API de temas, ideas, votos y estadísticas. Usa .NET 10 con MySQL o MongoDB. El login lo hace la Users API Java; CIS valida su JWT.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0).
- Docker Desktop iniciado, usando contenedores Linux.
- El proyecto Users API del equipo para levantar los servicios manualmente.
- Node.js 22 y Postman CLI para los scripts de pruebas.

Todos los comandos de estas guías se ejecutan en PowerShell, desde la carpeta que contiene CIS-WebAPI.slnx.

## Preparar el proyecto

~~~powershell
dotnet --version
docker info
dotnet restore CIS-WebAPI.slnx
dotnet tool restore
~~~

El último comando instala la versión de ReportGenerator del repositorio. Después de instalar una herramienta, abre otra terminal si no aparece en el PATH.

## Configuración local

Guarda la dirección de Users API y la clave JWT:

~~~powershell
dotnet user-secrets set "UsersApi:BaseUrl" "http://localhost:8080" --project CIS.API
dotnet user-secrets set "Jwt:SigningKey" "this-is-a-secret-key-of-at-least-32-characters" --project CIS.API
~~~

La clave del ejemplo es el valor de desarrollo de la Users API revisada. Si el equipo usa otra, coloca exactamente la misma en ambos servicios; debe tener al menos 32 bytes. Los User Secrets se configuran una vez en cada máquina, por usuario.

1. Prepara MySQL siguiendo [esta guía](CIS.DataAccess/README.md).
2. Inicia Users API y crea una cuenta siguiendo [la guía de ejecución](CIS.API/README.md).
3. Si vas a usar MongoDB para CIS, configura [ese proveedor](CIS.DataAccess.MongoDB/README.md).

## Ejecutar

Con las bases y Users API iniciadas:

~~~powershell
dotnet run --project CIS.API --launch-profile http
~~~

API: http://localhost:5039. Swagger: http://localhost:5039/swagger.

Los endpoints necesitan Authorization: Bearer seguido del token de POST http://localhost:8080/auth/login. Un GET sin token devuelve 401.

## Pruebas

~~~powershell
dotnet test CIS-WebAPI.slnx -m:1
~~~

La solución completa necesita Docker. Los tests crean bases temporales y no usan los User Secrets de tu API.

Para ejecutar migración, checksum y Postman juntos:

~~~powershell
node --version
postman --version
node scripts/phase2/run-workflow.cjs --mode full
~~~

Ese flujo usa un servicio de usuarios de prueba y guarda reportes en la carpeta que imprime al terminar. Requisitos e instalación de Postman: [guía del ejecutor](scripts/phase2/README.md).

## Guías por tarea

- [Pruebas unitarias](CIS.Tests/README.md).
- [Pruebas de integración](CIS.IntegrationTests/README.md).
- [Generar y abrir cobertura](scripts/coverage/README.md).
- [Migrar y comparar datos](tools/CIS.Migration/README.md).
- [Validación posterior a migración](scripts/migration/README.md).
- [Postman CLI contra tus servicios](tests/postman/README.md).
- [Scripts de GitLab CI](scripts/ci/README.md).
- [Estado de los cinco requisitos](docs/phase2/status.md).

Las rutas phase2 conservan el nombre del trabajo anterior. El alcance actual corresponde a los cinco pendientes de Fase 3.

## Problemas comunes

- Docker no responde: abre Docker Desktop y comprueba docker info.
- CIS no inicia: revisa proveedor, conexión, dirección de Users API y clave JWT.
- Todos los requests dan 401: inicia sesión nuevamente y verifica la clave de ambas APIs.
- Puerto ocupado: cambia el puerto publicado y actualiza la conexión.

Equipo: Imagine Devs — Lilian Laime, Josefina Carabajal y Alessandro Ruiz.
