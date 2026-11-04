# Generar cobertura

Requiere .NET SDK 10, Node.js 22 y Docker Desktop. Ejecuta los bloques en la misma terminal PowerShell, desde la raíz.

## Recoger los datos

Usa una carpeta nueva en cada ejecución:

~~~powershell
dotnet tool restore
$coverageDir = "TestResults/Coverage-$(Get-Date -Format yyyyMMdd-HHmmss)"
dotnet test CIS.Tests/CIS.Tests.csproj -m:1 --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory "$coverageDir/unit"
dotnet test CIS.IntegrationTests/CIS.IntegrationTests.csproj -m:1 --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory "$coverageDir/integration"
~~~

El segundo proyecto necesita Docker. El migrador tiene sus propias pruebas, pero el porcentaje corresponde a los cinco proyectos productivos de la API.

## Crear y abrir el HTML

~~~powershell
dotnet tool run reportgenerator "-reports:$coverageDir/**/coverage.cobertura.xml" "-targetdir:$coverageDir/merged" "-reporttypes:Html;Cobertura;TextSummary"
Start-Process "$coverageDir/merged/index.html"
~~~

El navegador muestra el total y el detalle por proyecto, clase y línea.

## Comprobar el mínimo

~~~powershell
node scripts/coverage/check-coverage.cjs "$coverageDir/merged/Cobertura.xml" 80
~~~

El script devuelve error si falta el reporte, es inválido o la cobertura de líneas es menor al porcentaje indicado. Si omites el último argumento usa 80. Las ramas se informan sin otro umbral.

La configuración está en coverage.runsettings; excluye los archivos generados, no los servicios o repositorios. Para probar el propio control:

~~~powershell
node --test tests/coverage/check-coverage.test.cjs
~~~
