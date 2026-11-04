# Postman CLI

La colección hace 32 peticiones de lectura, CRUD y errores esperados. El resultado verificado es 49 aserciones correctas.

## Requisitos

- Postman CLI disponible en el PATH; se probó con 1.74.0.
- Users API en http://localhost:8080.
- CIS en http://localhost:5039, con MySQL o MongoDB configurado.
- Una cuenta válida y al menos un tema con una idea.

La [guía de las APIs](../../CIS.API/README.md) muestra cómo levantar los servicios, registrar devlocal y crear esos datos. Si falta el CLI, usa la [instalación oficial](https://learning.postman.com/docs/postman-cli/postman-cli-installation/).

Para ejecutar todo con bases y usuarios temporales, usa directamente [el ejecutor](../../scripts/phase2/README.md).

## Configurar el entorno local

Desde la raíz:

~~~powershell
postman --version
New-Item -ItemType Directory -Force TestResults/PostmanLocal | Out-Null
~~~

Crea TestResults/PostmanLocal/local.environment.json con este contenido y reemplaza las credenciales por las de tu cuenta:

~~~json
{
  "name": "CIS local",
  "values": [
    { "key": "baseUrl", "value": "http://localhost:5039", "enabled": true },
    { "key": "usersBaseUrl", "value": "http://localhost:8080", "enabled": true },
    { "key": "login", "value": "devlocal", "enabled": true },
    { "key": "password", "value": "clave-local", "enabled": true }
  ]
}
~~~

El archivo queda dentro de TestResults, que está ignorado por Git. No agregues token ni los IDs al entorno: los scripts los guardan en las variables de la colección.

## Ejecutar la colección

~~~powershell
postman collection run tests/postman/CIS-API-funcional.postman_collection.json -e TestResults/PostmanLocal/local.environment.json --no-report-events -r cli,junit --reporter-junit-export TestResults/PostmanLocal/postman.junit.xml
~~~

La primera petición obtiene el JWT. Las siguientes leen datos y crean sus propios recursos para actualizar, votar y eliminar. Los temas e ideas quedan eliminados lógicamente al terminar.

El reporte aparece en consola y en postman.junit.xml. Si falla el login, revisa la cuenta y la clave JWT compartida; si fallan las lecturas iniciales, comprueba que haya un tema con ideas.

## Usar la aplicación Postman

Importa el JSON de la colección y el entorno local, selecciona ese entorno y ejecuta las carpetas en orden. Las URLs y bodies ya están incluidos.

El [detalle por endpoint](../../docs/phase2/endpoint-matrix.md) explica los casos HTTP. postman.cjs, dentro de scripts/phase2, es el módulo que valida y limpia los reportes del flujo automático; se utiliza desde run-workflow.cjs.
