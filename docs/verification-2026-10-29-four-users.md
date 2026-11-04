# Auditoria del dataset de cuatro usuarios — 29 de octubre de 2026

La redistribucion y eliminacion de usuarios en MySQL son correctas. Persisten dos asuntos de datos antes de probar el dataset completo desde la API: normalizar 198 etiquetas y migrar los datos actualizados a MongoDB.

## MySQL sd3

| Entidad | Actual | Esperado |
| --- | ---: | ---: |
| Usuarios | 4 | 4 |
| Etiquetas | 200 | 200 |
| Temas | 500 | 500 |
| Relaciones tema-etiqueta | 1800 | 1800 |
| Ideas | 2000 | 2000 |
| Votos | 5000 | 5000 |

| Login | Temas | Ideas | Votos |
| --- | ---: | ---: | ---: |
| AdrianCis | 350 | 1400 | 1800 |
| ARuiz | 50 | 200 | 1067 |
| AdrianRuiz | 50 | 200 | 1067 |
| jroca2 | 50 | 200 | 1066 |

Todos los chequeos de integridad dieron cero incidencias: temas/ideas/votos sin autor, ideas sin tema, votos sin idea, asociaciones de etiquetas huerfanas o repetidas, votos duplicados, usuarios fuera de las cuatro cuentas, UUIDs invalidos, titulos/descripciones invalidos, estados invalidos y fechas incoherentes.

La distribucion de votos es 200 ideas sin votos, 200 con uno, 400 con dos, 800 con tres y 400 con cuatro. No hay ideas con mas de cuatro votos. Las distribuciones de etiquetas e ideas por tema conservan las cuotas del seed.

Los AUTO_INCREMENT son mayores al ID maximo: temas 517 > 500; ideas 2050 > 2000; votos 8192 > 5000; etiquetas 258 > 200. Los saltos son validos. Se consultaron estadisticas actualizadas de la sesion para evitar valores antiguos de information_schema.

## Pendiente: normalizacion de etiquetas

198 registros tienen, por ejemplo, `name = Perf Educacion 001` y `normalized_name = PERF_TAG_001`. La API transforma el nombre consultado en `PERF EDUCACION 001` al buscarlo, por lo que esas etiquetas pueden no aparecer mediante el filtro `tag`. Los 198 nombres actuales del seed tienen formato simple y no se detectaron colisiones al calcular `UPPER(TRIM(name))`. No se modificaron sus valores durante la auditoria.

## Pendiente: migracion de los datos actualizados

La unica base de aplicacion detectada en MongoDB fue `sd3db_mongo`, con 4 usuarios, 2 etiquetas, 5 temas, 2 ideas y 0 votos. Sus contadores son 2/5/2/0 para etiquetas/temas/ideas/votos y corresponden a ese dataset pequeno. Conserva los indices unicos de etiquetas y votos.

La configuracion local en User Secrets indica proveedor `MongoDb` y base `sd3db_mongo`. Por ello, iniciar CIS API con esa configuracion no expone el dataset grande de MySQL. Se requiere una migracion posterior a la copia elegida y sus correspondientes contadores/indices.

Actualizacion posterior: `scripts/migration/post-migration-sd3-perf.js` ya valida cuatro usuarios, las cuotas de autores y el reparto 0/1/2/3/4 de votos. Tambien detecta las etiquetas mal normalizadas antes de crear indices o actualizar contadores. Se verifico con 11 pruebas en un MongoDB temporal aislado. El cambio del validador no realiza la migracion ni corrige los datos de etiquetas pendientes de esta auditoria.

## Pruebas del proyecto

Se ejecuto `dotnet test CIS-WebAPI.slnx --no-restore --logger "console;verbosity=minimal" --logger "trx;LogFilePrefix=audit" --results-directory TestResults/Audit`:

- 134 pruebas unitarias aprobadas.
- 14 pruebas de integracion aprobadas.
- 148 en total, sin fallos ni omisiones.

Las pruebas de integracion usan MongoDB temporal aislado; su resultado no significa que la base MongoDB de la aplicacion ya este migrada. Esta auditoria no verifico credenciales de login ni ejecuto la coleccion de Postman contra servicios en vivo.

Se preparo y ejecuto tambien `scripts/migration/verify-sd3-four-users.sql`, reutilizable en Workbench. La auditoria no modifico tablas de MySQL ni documentos de MongoDB.
