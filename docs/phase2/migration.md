# Migracion reproducible MySQL a MongoDB

La herramienta independiente `tools/CIS.Migration` usa .NET 10. No modifica los repositorios de la API ni necesita ejecutar las APIs. Requiere una base MySQL existente y una URI MongoDB que incluya el nombre de la base. Las credenciales se reciben solamente mediante variables de entorno; no se escriben en el informe.

```powershell
$env:CIS_MYSQL_CONNECTION = 'server=localhost;port=3306;database=cis_phase2_source;user=phase2;password=<local-secret>'
$env:CIS_MONGO_CONNECTION = 'mongodb://localhost:27017/sd3db_mongo_perf'
dotnet run --project tools/CIS.Migration -- seed
dotnet run --project tools/CIS.Migration -- migrate
dotnet run --project tools/CIS.Migration -- verify --report TestResults/phase2/migration.json
```

Si MongoDB requiere autenticacion en `admin`, incluir `?authSource=admin`. El nombre `sd3db_mongo_perf` permite reutilizar el validador legado `scripts/migration/post-migration-sd3-perf.js`; la herramienta acepta otros nombres de destino explicitos. Utilizar contenedores nuevos para las pruebas, nunca las bases locales existentes `sd3` o `sd3db_mongo_perf` del usuario.

`seed` exige que el nombre MySQL empiece exactamente por `cis_phase2_` y que el esquema no tenga tablas. No crea ni elimina bases y nunca borra tablas. Crea el modelo de negocio mediante EF `EnsureCreated` y la tabla `users` con columnas `id`, `name`, `login`, `password`. Las inserciones se realizan en una transaccion; el DDL de MySQL no es transaccional. Si una creacion falla parcialmente, preparar otra base vacia en lugar de reintentar sobre ese esquema.

El fixture es determinista, con fechas UTC a precision de milisegundos, identificadores positivos de 64 bits y texto fijo. Contiene 4 usuarios, 200 etiquetas, 500 topicos, 2.000 ideas, 5.000 votos y 1.800 relaciones `topic_tags`. Las etiquetas cumplen `TagNamePolicy` (NFKC, espacios y mayusculas invariantes). Los UUID y logins son los cuatro del validador existente; nombres y contrasenas del fixture son sinteticos. La unica contrasena sembrada es `phase2-test-password`, exclusiva de pruebas.

| Login | Topicos | Ideas | Votos |
| --- | ---: | ---: | ---: |
| AdrianCis | 350 | 1.400 | 1.800 |
| ARuiz | 50 | 200 | 1.067 |
| AdrianRuiz | 50 | 200 | 1.067 |
| jroca2 | 50 | 200 | 1.066 |

Las distribuciones son 100 topicos sin etiquetas, 200 con cuatro y 200 con cinco; 100 topicos para cada cantidad de ideas 0/1/2/5/12; y 200/200/400/800/400 ideas con 0/1/2/3/4 votos respectivamente.

`migrate` obtiene una instantanea MySQL con aislamiento `REPEATABLE READ`; las seis tablas deben ser InnoDB para compartir esa instantanea. Solo lee las columnas de negocio indicadas abajo y nunca consulta `users.password`. Rechaza estados desconocidos, valores nulos requeridos, etiquetas inconsistentes, duplicados, relaciones inexistentes y fechas con microsegundos que BSON DateTime no pueda conservar. No convierte silenciosamente datos invalidos. El destino debe estar vacio, incluso sus colecciones ajenas: no elimina, reemplaza ni actualiza documentos existentes. La carga no es una transaccion entre bases; una interrupcion puede dejar un destino parcial. Preparar un destino nuevo vacio y conservar el origen para repetirla. Detener las escrituras de las aplicaciones durante migracion y verificacion; las lecturas MongoDB no representan una instantanea entre colecciones.

MongoDB recibe IDs de negocio, referencias, `tagIds` y `counters.seq` como BSON Long; UUID como strings; estados con los nombres exactos de los enums; fechas como BSON DateTime UTC. `topic_tags` se integra en `topics.tagIds`. Se crean indices unicos de `tags.normalizedName` y `(votes.ideaId, votes.userId)`. Los contadores de `tags`, `topics`, `ideas` y `votes` empiezan en el maximo ID migrado. La verificacion acepta secuencias adelantadas que sean BSON Long y al menos ese maximo; una secuencia inferior falla porque podria reutilizar un ID. No reduce secuencias existentes.

`verify` solo lee las bases. Compara los campos requeridos sin arreglar el destino:

| Entidad | Proyeccion canonica |
| --- | --- |
| users | id, name, login |
| tags | id, name, normalizedName |
| topics | id, title, description, status, userId, createdAt, updatedAt, tagIds ordenados |
| ideas | id, topicId, userId, title, description, status, createdAt, updatedAt |
| votes | id, ideaId, userId, createdAt |
| topic_tags | topicId, tagId; derivada de tagIds en MongoDB |

La comparacion de usuarios cubre el perfil, **no las credenciales ni la autenticacion**. No migra contrasenas a MongoDB; la Users API conserva su almacenamiento MySQL. Las contrasenas no forman parte del canon ni del informe. La verificacion exige los campos de negocio y sus tipos exactos, rechaza campos MongoDB desconocidos y permite ignorar explicitamente `users.password`. Conserva texto, UUID y estados tal como aparecen, sin trim, conversion numerica aproximada o normalizacion de valores del destino.

Cada fila se serializa como un array JSON con nombre de entidad y campos en el orden de la tabla; fechas como milisegundos Unix y numeros como enteros de 64 bits. Las filas se ordenan por clave ordinal (ID decimal/string; `topicId/tagId` para relaciones), se unen con LF y se agrega un LF final. SHA256 se calcula sobre esos bytes UTF-8, en hexadecimal minusculo. El informe incluye `Passed`, `Entities` con cantidades y hashes de cada lado, hasta 20 claves diferentes por entidad y `Errors` de integridad. No incluye valores de campos, contrasenas ni conexiones. Tambien valida unicidad, claves foraneas y los cuatro contadores. Las cantidades iguales no bastan para aprobar.

Codigos de salida: `0` exito; `1` diferencias, esquema invalido o fallo de operacion; `2` argumentos o variables ausentes. Con `verify` y datos invalidos se genera un informe fallido; un problema de conectividad o de permisos puede impedir generar el informe. Conservar unicamente informes de fixtures o informes autorizados; contienen identificadores de registros.

```powershell
dotnet test tools/CIS.Migration.Tests
```

Las pruebas usan MySQL 8.4 y MongoDB 8.0 con Testcontainers, crean bases con nombres aleatorios dentro de esos contenedores y los eliminan al finalizar. Comprueban el fixture completo, determinismo, distribuciones/cuotas, preservacion del origen, indices unicos, IDs mayores que `2^53`, cambios con cantidades iguales en todas las entidades/relaciones, campos ausentes/nulos, estados y precision de fechas invalidos, asi como rechazo de origen/destino no vacio. Requieren Docker disponible y permiso para iniciar contenedores; no usan las conexiones del entorno de la aplicacion.
