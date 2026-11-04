# Comandos de los cinco controles

Desde la raíz de CIS-API, en PowerShell, con .NET 10, Node 22, Docker Desktop y Postman CLI 1.74.0 disponibles:

~~~powershell
# Unitarias, integración y pruebas del migrador.
dotnet test CIS-WebAPI.slnx -m:1

# Migración, checksum y Postman CLI contra ambos proveedores.
node scripts/phase2/run-workflow.cjs --mode full

# Ejecutar solo migración y checksum.
node scripts/phase2/run-workflow.cjs --mode migration

# Controles del gate, ejecutor, fixture y cliente CLI.
node --test tests/coverage/*.test.cjs tests/phase2/*.test.cjs

# Validación posterior a migración, con MongoDB temporal.
node --test tests/migration/post-migration-sd3-perf.test.cjs
~~~

El modo full inicia copias nuevas MySQL/MongoDB, genera el fixture de cuatro usuarios, migra, compara seis proyecciones SHA-256, ejecuta el post-migration y corre la colección Postman con ambos proveedores. functional realiza ese mismo flujo; migration termina tras validar los datos. No se ejecuta k6 ni se necesita instalarlo.

El comando de solución ejecuta todos los tests. Para consolidar cobertura con reportes separados por proyecto seguir [coverage.md](coverage.md); evitar un nombre TRX único para toda la solución, que sobrescribiría resultados entre proyectos. Usar carpetas nuevas para no mezclar mediciones.

~~~powershell
dotnet format whitespace CIS-WebAPI.slnx --verify-no-changes --no-restore
dotnet publish CIS.API/CIS.API.csproj --configuration Release --output TestResults/Phase3/publish -m:1
~~~

El ejecutor retira sus procesos y contenedores y conserva los reportes en TestResults. Usa un servicio de autenticación de fixture; la colección se puede apuntar a la Users API Java real como explica [Postman](../../tests/postman/README.md). Los datos reales de sd3 y mongo_cis no se usan en este flujo.

