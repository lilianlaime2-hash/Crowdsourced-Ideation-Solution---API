# Ejecutar las APIs en local

Completa primero los User Secrets y la base del [README principal](../README.md). Para levantar Users API necesitas Java 17, Maven y su repositorio.

## Users API

En una terminal ubicada en la carpeta de Users API Java:

~~~powershell
$env:DB_URL = 'jdbc:mysql://127.0.0.1:3307/sd3?useSSL=false&allowPublicKeyRetrieval=true&serverTimezone=UTC'
$env:DB_USERNAME = 'root'
$env:DB_PASSWORD = 'sd5'
mvn spring-boot:run
~~~

Las variables duran mientras esa terminal esté abierta. La base sd3 y la tabla users deben existir; se preparan en [esta guía](../CIS.DataAccess/README.md). Si la versión de Users API del equipo usa otra configuración, sigue la de ese repositorio.

Users API debe escuchar en http://localhost:8080 y compartir la clave JWT configurada en CIS.

## CIS API

En otra terminal, desde la raíz de CIS-API:

~~~powershell
dotnet run --project CIS.API --launch-profile http
~~~

Swagger: http://localhost:5039/swagger. Con el perfil HTTP no hace falta configurar certificados HTTPS.

## Crear una cuenta e iniciar sesión

Crea una cuenta local una sola vez:

~~~powershell
$usuario = @{ name = 'Dev Local'; login = 'devlocal'; password = 'clave-local' } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:8080/auth/register' -ContentType 'application/json' -Body $usuario
~~~

Para iniciar sesión y consultar CIS:

~~~powershell
$credenciales = @{ login = 'devlocal'; password = 'clave-local' } | ConvertTo-Json
$sesion = Invoke-RestMethod -Method Post -Uri 'http://localhost:8080/auth/login' -ContentType 'application/json' -Body $credenciales
$headers = @{ Authorization = "Bearer $($sesion.token)" }
Invoke-RestMethod -Uri 'http://localhost:5039/api/topics' -Headers $headers
~~~

Una base nueva devuelve una página sin temas. Para crear los datos mínimos que usa la colección Postman:

~~~powershell
$body = @{ title = 'Tema local'; description = 'Tema para probar la API'; tags = @() } | ConvertTo-Json
$tema = Invoke-RestMethod -Method Post -Uri 'http://localhost:5039/api/topics' -Headers $headers -ContentType 'application/json' -Body $body
$body = @{ title = 'Idea local'; description = 'Idea de prueba' } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:5039/api/topics/$($tema.id)/ideas" -Headers $headers -ContentType 'application/json' -Body $body
~~~

Usa esa cuenta para [Postman CLI](../tests/postman/README.md). Detén las APIs con Ctrl+C en sus terminales.
