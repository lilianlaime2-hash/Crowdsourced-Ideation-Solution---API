# Pruebas del migrador

Requiere .NET SDK 10 y Docker Desktop con contenedores Linux. No necesitas configurar CIS_MYSQL_CONNECTION ni CIS_MONGO_CONNECTION.

Desde la raíz:

~~~powershell
docker info
dotnet test tools/CIS.Migration.Tests/CIS.Migration.Tests.csproj -m:1
~~~

Las pruebas preparan sus propios contenedores MySQL/MongoDB y los eliminan al terminar. El resultado actual es 19 pruebas correctas.

Para guardar un reporte:

~~~powershell
dotnet test tools/CIS.Migration.Tests/CIS.Migration.Tests.csproj --logger 'trx;LogFileName=migration.trx' --results-directory TestResults/MigrationTests
~~~

Puede aparecer Verification failed, see report durante un caso negativo. Esa prueba altera datos a propósito y espera detectar la diferencia. Revisa el resumen final de xUnit para saber si hubo errores.
