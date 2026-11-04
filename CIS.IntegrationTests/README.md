# Pruebas de integración

Requiere .NET SDK 10, Docker Desktop y contenedores Linux. No hace falta iniciar las APIs ni configurar conexiones locales.

Desde la raíz del repositorio:

~~~powershell
docker info
dotnet restore CIS-WebAPI.slnx
dotnet test CIS.IntegrationTests/CIS.IntegrationTests.csproj -m:1
~~~

Testcontainers descarga MySQL y MongoDB si hacen falta, crea bases temporales y las retira al terminar. La primera ejecución demora más por las descargas.

## Ejecutar un grupo

~~~powershell
dotnet test CIS.IntegrationTests/CIS.IntegrationTests.csproj --filter 'FullyQualifiedName~CIS.IntegrationTests.Http'
dotnet test CIS.IntegrationTests/CIS.IntegrationTests.csproj --filter 'FullyQualifiedName~MongoSequenceIntegrationTests'
~~~

El proyecto tiene 99 casos HTTP, 18 de repositorios MySQL y 14 originales de MongoDB: 131 en total. Los casos por endpoint están en [la matriz](../docs/phase2/endpoint-matrix.md).

## Guardar resultados

~~~powershell
dotnet test CIS.IntegrationTests/CIS.IntegrationTests.csproj --logger 'trx;LogFileName=integration.trx' --results-directory TestResults/Integration
~~~

La API HTTP y el JWT son reales; la consulta a Users API se sustituye por datos de prueba. Las pruebas de concurrencia mantienen 50 operaciones simultáneas.

Si Docker no responde, revisa docker info antes de repetir. La [guía de cobertura](../scripts/coverage/README.md) explica cómo combinar estos resultados con los unitarios.
