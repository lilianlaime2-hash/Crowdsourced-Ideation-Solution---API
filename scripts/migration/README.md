# Validación posterior a la migración

post-migration-sd3-perf.js revisa el dataset de cuatro usuarios y prepara índices únicos y contadores. Se usa después de copiar los datos.

Es específico de sd3db_mongo_perf: espera 4 usuarios, 200 etiquetas, 500 temas, 2000 ideas y 5000 votos, con las cuotas y distribuciones del fixture. No se aplica a una base vacía ni a datos que ya modificó la API.

## Requisitos

- MongoDB iniciado y con el dataset migrado.
- Docker para la receta siguiente; mongosh viene dentro de la imagen MongoDB.
- Haber detenido las escrituras sobre ese dataset durante la comprobación.

Para preparar datos nuevos sigue [el migrador](../../tools/CIS.Migration/README.md). Para hacerlo automáticamente sigue [el ejecutor](../phase2/README.md).

## Ejecutar en PowerShell

Desde la raíz, usando el contenedor local mongo_cis:

~~~powershell
docker cp ./scripts/migration/post-migration-sd3-perf.js mongo_cis:/tmp/post-migration.js
docker exec mongo_cis mongosh sd3db_mongo_perf --quiet --file /tmp/post-migration.js
~~~

Si seguiste la receta manual del migrador, reemplaza mongo_cis por cis_migration_mongo en ambos comandos.

Con mongosh instalado en la máquina también puedes usar:

~~~powershell
mongosh "mongodb://127.0.0.1:27017/sd3db_mongo_perf" --quiet --file ./scripts/migration/post-migration-sd3-perf.js
~~~

Ajusta la URI y autenticación a tu servidor. El nombre de base esperado por este script es fijo.

## Resultado

Al terminar debe mostrar POSTMIGRACION COMPLETADA. Primero valida datos y relaciones; si fallan esas validaciones no escribe. Después crea índices únicos de etiquetas/votos y ajusta los contadores sin reducirlos.

Este archivo no copia datos ni configura la conexión de CIS. Para apuntar la API al destino sigue [MongoDB](../../CIS.DataAccess.MongoDB/README.md).

## Pruebas

Requiere Node.js 22 y Docker:

~~~powershell
node --test tests/migration/post-migration-sd3-perf.test.cjs
~~~

La suite usa otro contenedor temporal y tiene 11 casos.
