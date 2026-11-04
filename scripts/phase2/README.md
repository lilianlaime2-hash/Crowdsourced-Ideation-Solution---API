# Migración y Postman con datos temporales

Este ejecutor crea sus propios contenedores, migra el dataset, compara SHA-256 y ejecuta Postman con CIS sobre MySQL y MongoDB. El servicio de usuarios es de prueba.

## Requisitos

- .NET SDK 10.
- Node.js 22.
- Docker Desktop iniciado con contenedores Linux.
- Postman CLI; la versión usada en la verificación fue 1.74.0.

Comprueba las herramientas desde la raíz:

~~~powershell
dotnet --version
node --version
docker info
postman --version
~~~

Si falta Postman CLI, sigue su [instalación oficial](https://learning.postman.com/docs/postman-cli/postman-cli-installation/). El instalador oficial para Windows se ejecuta así:

~~~powershell
powershell -NoP -NonI -c "irm https://dl-cli.pstmn.io/install/win64.ps1 | iex"
~~~

Abre otra terminal y comprueba postman --version.

## Ejecutar

Desde la raíz de CIS-API:

~~~powershell
node scripts/phase2/run-workflow.cjs --mode full
~~~

Para hacer solo la migración y el checksum:

~~~powershell
node scripts/phase2/run-workflow.cjs --mode migration
~~~

El modo functional realiza la migración y Postman, igual que full en el alcance actual. No se ejecuta k6.

No necesitas iniciar las APIs manualmente ni configurar User Secrets, contraseñas o conexiones: el ejecutor genera los valores temporales.

## Reportes

Al finalizar imprime la carpeta de resultados y PASS. Por defecto guarda en TestResults/Phase2/Workflow-fecha. Puedes indicar otra:

~~~powershell
node scripts/phase2/run-workflow.cjs --mode full --output TestResults/LocalSmoke
~~~

Conserva workflow.json, reportes SHA-256, logs y postman-MySQL/postman.junit.xml junto al de MongoDB. Usa un nombre de carpeta distinto para cada ejecución.

## Archivos auxiliares

- infrastructure.cjs: crea y retira contenedores y procesos de CIS.
- users-fixture.cjs: atiende login y consultas de usuarios de prueba.
- postman.cjs: ejecuta la colección, valida sus reportes y limpia los archivos temporales.
- runtime.cjs: funciones compartidas de procesos, rutas y limpieza.

Esos archivos se importan desde run-workflow.cjs. Para usar Postman con tus servicios reales sigue [esta guía](../../tests/postman/README.md).
