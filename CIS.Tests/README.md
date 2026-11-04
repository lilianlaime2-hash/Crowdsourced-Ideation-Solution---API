# Pruebas unitarias

Requiere .NET SDK 10. No hace falta iniciar Docker ni las APIs.

Desde la raíz del repositorio:

~~~powershell
dotnet restore CIS-WebAPI.slnx
dotnet test CIS.Tests/CIS.Tests.csproj
~~~

Para ejecutar un grupo:

~~~powershell
dotnet test CIS.Tests/CIS.Tests.csproj --filter 'FullyQualifiedName~StatisticsServiceTests'
~~~

Para guardar el resultado:

~~~powershell
dotnet test CIS.Tests/CIS.Tests.csproj --logger 'trx;LogFileName=unit.trx' --results-directory TestResults/Unit
~~~

Para generar el HTML de cobertura, sigue [estos pasos](../scripts/coverage/README.md).
