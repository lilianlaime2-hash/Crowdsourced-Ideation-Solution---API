# Migrar MySQL a MongoDB

Requiere .NET SDK 10 y acceso a ambos motores. Esta receta usa Docker y bases nuevas de prueba.

Ejecuta los comandos desde la raíz del repositorio.

## Preparar las bases

Crea estos contenedores una sola vez:

~~~powershell
docker run -d --name cis_migration_mysql -e MYSQL_ROOT_PASSWORD=local-test -e MYSQL_ROOT_HOST=% -e MYSQL_DATABASE=cis_phase2_source -p 127.0.0.1:3308:3306 mysql:8.4
docker run -d --name cis_migration_mongo -p 127.0.0.1:27019:27017 mongo:8.0
docker logs cis_migration_mysql --tail 20
~~~

Espera a que MySQL termine de iniciar. En otra sesión puedes iniciarlos con docker start cis_migration_mysql cis_migration_mongo.

## Configurar conexiones

En la misma terminal donde ejecutarás la herramienta:

~~~powershell
$env:CIS_MYSQL_CONNECTION = 'Server=127.0.0.1;Port=3308;Database=cis_phase2_source;User ID=root;Password=local-test;SslMode=Disabled;AllowPublicKeyRetrieval=True'
$env:CIS_MONGO_CONNECTION = 'mongodb://127.0.0.1:27019/sd3db_mongo_perf'
~~~

Estas variables no son los User Secrets de la API y duran solo en esa terminal. Ajusta puertos y credenciales si tus servidores son distintos.

## Ejecutar

~~~powershell
dotnet run --project tools/CIS.Migration -- seed
dotnet run --project tools/CIS.Migration -- migrate
dotnet run --project tools/CIS.Migration -- verify --report TestResults/Migration/verification.json
~~~

- seed crea cuatro usuarios, 500 temas, 200 etiquetas, 2000 ideas y 5000 votos. Solo acepta una base vacía cuyo nombre empiece por cis_phase2_.
- migrate copia las tablas de negocio a un destino MongoDB vacío y crea índices únicos y contadores.
- verify compara los datos y sus relaciones mediante SHA-256. No modifica las bases.

Para un origen que ya tiene datos, omite seed. Durante la copia y comparación detén las escrituras de las APIs.

El resultado correcto es Verification passed y un reporte con Passed: true. Código de salida: 0 correcto, 1 fallo o diferencia, 2 configuración/argumentos incompletos.

Puedes repetir verify. Si una migración quedó a medias, prepara otro destino vacío; la herramienta no sobrescribe datos existentes. Las contraseñas de usuarios no se copian.

[Formato de datos y detalles](../../docs/phase2/migration.md). El [post-migration](../../scripts/migration/README.md) comprueba el dataset completo; si usaste esta receta, su contenedor Mongo se llama cis_migration_mongo.
