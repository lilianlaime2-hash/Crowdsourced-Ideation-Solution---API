# Preparar MySQL

Requiere .NET SDK 10 y Docker Desktop. Ejecuta desde la raíz del repositorio. Configura primero la clave JWT y la dirección de Users API del [README principal](../README.md).

## Crear la base local

Una sola vez:

~~~powershell
docker run -d --name sd3db -e MYSQL_ROOT_PASSWORD=sd5 -e MYSQL_ROOT_HOST=% -e MYSQL_DATABASE=sd3 -p 127.0.0.1:3307:3306 mysql:8.4
docker logs sd3db --tail 20
~~~

Espera a que MySQL termine de iniciar. Si el contenedor ya existe, usa docker start sd3db.

Guarda la configuración de CIS:

~~~powershell
dotnet user-secrets set "Persistence:Provider" "MySQL" --project CIS.API
dotnet user-secrets set "ConnectionStrings:LegacyDatabase" "server=127.0.0.1;port=3307;database=sd3;user=root;password=sd5;SslMode=Disabled;AllowPublicKeyRetrieval=True" --project CIS.API
~~~

sd5 es la contraseña del ejemplo local. Si cambias puerto, base o contraseña, actualiza también esta conexión y la de Users API.

## Crear las tablas de CIS

Instala EF una vez por máquina:

~~~powershell
dotnet tool install --global dotnet-ef --version 10.0.12
dotnet restore CIS-WebAPI.slnx
dotnet ef database update --project CIS.DataAccess --startup-project CIS.API -- --environment Development
~~~

Si ya tienes otra versión, usa dotnet tool update --global dotnet-ef --version 10.0.12. Las migraciones crean las tablas de temas, etiquetas, ideas y votos. [Referencia de EF](https://learn.microsoft.com/ef/core/cli/dotnet).

## Tabla de Users API

CIS no administra esta tabla. En una base nueva, abre MySQL e ingresa sd5 cuando solicite la contraseña:

~~~powershell
docker exec -it sd3db mysql -uroot -p
~~~

Ejecuta el SQL una vez:

~~~sql
USE sd3;
CREATE TABLE IF NOT EXISTS users (
  id VARCHAR(36) NOT NULL PRIMARY KEY,
  name VARCHAR(200) NOT NULL,
  login VARCHAR(20) NOT NULL,
  password VARCHAR(100) NOT NULL
);
exit;
~~~

Después inicia [Users API y CIS](../CIS.API/README.md). En las siguientes sesiones basta con iniciar el contenedor y las APIs.
