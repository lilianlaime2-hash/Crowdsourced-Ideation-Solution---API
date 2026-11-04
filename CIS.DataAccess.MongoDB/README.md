# Usar MongoDB en local

Requiere Docker Desktop y la configuración común del [README principal](../README.md). Users API sigue necesitando su MySQL, aunque CIS use MongoDB.

## Base nueva

Desde la raíz, crea el contenedor una vez:

~~~powershell
docker run -d --name mongo_cis -p 127.0.0.1:27017:27017 mongo:8.0
~~~

Si ya existe, usa docker start mongo_cis.

Para una base vacía llamada cis_local, crea los índices únicos:

~~~powershell
docker exec mongo_cis mongosh cis_local --quiet --eval "db.tags.createIndex({normalizedName:1},{unique:true,name:'ux_tags_normalizedName'})"
docker exec mongo_cis mongosh cis_local --quiet --eval "db.votes.createIndex({ideaId:1,userId:1},{unique:true,name:'ux_votes_ideaId_userId'})"
~~~

Los índices evitan etiquetas normalizadas repetidas y votos duplicados del mismo usuario. Los contadores se crean cuando la API genera sus primeros IDs.

## Configurar CIS

~~~powershell
dotnet user-secrets set "Persistence:Provider" "MongoDB" --project CIS.API
dotnet user-secrets set "ConnectionStrings:MongoDatabase" "mongodb://127.0.0.1:27017/cis_local" --project CIS.API
dotnet run --project CIS.API --launch-profile http
~~~

La URI debe incluir el nombre de la base. Si tu servidor requiere autenticación, agrega el usuario, contraseña y authSource correspondientes.

## Base migrada

Para copiar datos desde MySQL sigue [la herramienta de migración](../tools/CIS.Migration/README.md). Después apunta MongoDatabase a ese destino. La copia local original del proyecto usa mongodb://127.0.0.1:27017/sd3db_mongo_perf.

El [post-migration](../scripts/migration/README.md) está preparado para ese dataset concreto; no se aplica a la base vacía cis_local.
