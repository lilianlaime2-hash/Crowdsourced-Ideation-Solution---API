# Pruebas de rendimiento de CIS API con k6

Hay cinco scripts de consulta y un flujo CRUD que mide las ocho operaciones de escritura. El login se ejecuta una vez en `setup()`; los tiempos de autenticacion y descubrimiento de IDs quedan fuera de los umbrales de CIS API.

| Archivo | Operacion medida |
| --- | --- |
| `get-topics-k6.js` | GET `/api/topics?page=1&pageSize=10` |
| `get-topic-k6.js` | GET `/api/topics/{topicId}` |
| `get-ideas-k6.js` | GET `/api/topics/{topicId}/ideas?page=1&pageSize=10` |
| `most-voted-ideas-k6.js` | GET `/api/statistics/ideas/most-voted?page=1&pageSize=10` |
| `most-active-topics-k6.js` | GET `/api/statistics/topics/most-active?page=1&pageSize=10` |
| `crud-k6.js` | POST tema, PUT tema, POST idea, PUT idea, POST voto, DELETE voto, DELETE idea y DELETE tema |

El test de tema existente ya no depende del titulo fijo `Programming Workshops`. Los IDs se descubren desde las respuestas o se toman de `PERF_TOPIC_ID`.

## Preparacion

1. Inicia las bases necesarias, Users API y CIS API. Ejecuta las pruebas desde la raiz del repositorio.
2. Comprueba que CIS API apunta al dataset que quieres medir. Para comparar MySQL con MongoDB, ambos deben contener los mismos datos actualizados y los mismos indices previstos. No confundas el dataset original pequeno con el de 500 temas, 2000 ideas y 5000 votos.
3. Configura una cuenta valida en PowerShell:

```powershell
$env:CIS_BASE_URL = "http://localhost:5039"
$env:USERS_BASE_URL = "http://localhost:8080"
$env:K6_LOGIN = "TU_USUARIO"
$env:K6_PASSWORD = "TU_CONTRASEÑA"
```

Tambien puedes usar un JWT obtenido en Postman:

```powershell
$env:K6_TOKEN = "TU_TOKEN_JWT"
```

Si `K6_TOKEN` tiene valor, se utiliza y no se llama al login. Para volver al login por credenciales:

```powershell
Remove-Item Env:K6_TOKEN -ErrorAction SilentlyContinue
```

Los 50 usuarios virtuales pueden compartir el JWT de AdrianCis; no necesitas crear 50 cuentas. Los usuarios virtuales representan ejecuciones concurrentes del script. El servicio Users API sigue participando cuando CIS API consulta nombres de autores; ese costo forma parte de la latencia real del endpoint.

## Primera comprobacion

```powershell
$env:PERF_PROFILE = "smoke"
k6 run .\tests\performance\get-topics-k6.js
k6 run .\tests\performance\get-topic-k6.js
k6 run .\tests\performance\get-ideas-k6.js
k6 run .\tests\performance\most-voted-ideas-k6.js
k6 run .\tests\performance\most-active-topics-k6.js
```

Cada script hace una iteracion con un usuario virtual. En consultas equivale a una peticion medida; en CRUD son ocho operaciones medidas por ciclo.

## Carga sostenida: 50 usuarios durante 30 segundos

```powershell
$env:PERF_PROFILE = "load"
$env:PERF_VUS = "50"
$env:PERF_DURATION = "30s"

k6 run .\tests\performance\get-topics-k6.js
k6 run .\tests\performance\get-topic-k6.js
k6 run .\tests\performance\get-ideas-k6.js
k6 run .\tests\performance\most-voted-ideas-k6.js
k6 run .\tests\performance\most-active-topics-k6.js
```

Se ejecutan por separado para comparar los endpoints bajo la misma concurrencia. El perfil de carga incluye una pausa de un segundo por iteracion. `PERF_VUS=50` no significa 50 peticiones por segundo: el ritmo depende del tiempo de respuesta y de esa pausa. Las iteraciones en curso tienen hasta dos minutos adicionales para terminar al finalizar la ventana de carga.

## Rafaga de 50 usuarios

```powershell
$env:PERF_PROFILE = "burst"
$env:PERF_VUS = "50"
k6 run .\tests\performance\get-topic-k6.js
```

Cada uno de los 50 usuarios ejecuta exactamente una iteracion, sin pausa por defecto: 50 peticiones medidas para este GET. El planificador de k6 inicia los usuarios concurrentemente, sin prometer que todas las solicitudes lleguen al servidor en el mismo instante. Puedes usar el mismo perfil con cualquiera de los otros archivos.

## POST, PUT y DELETE

```powershell
$env:PERF_PROFILE = "smoke"
k6 run .\tests\performance\crud-k6.js

$env:PERF_PROFILE = "burst"
$env:PERF_VUS = "50"
k6 run .\tests\performance\crud-k6.js
```

La rafaga completa realiza 50 ciclos, con 400 peticiones medidas: 50 para cada una de las ocho operaciones. Las operaciones de un ciclo son secuenciales porque necesitan los IDs creados; los 50 ciclos se ejecutan concurrentemente.

Para repetir ciclos durante 30 segundos:

```powershell
$env:PERF_PROFILE = "load"
$env:PERF_VUS = "50"
$env:PERF_DURATION = "30s"
k6 run .\tests\performance\crud-k6.js
```

Cada iteracion crea un tema y una idea propios, usando el usuario del token. No actualiza ni borra los registros originales del seed. No agrega etiquetas. Al terminar, elimina el voto y marca tema/idea como eliminados mediante los endpoints de la API.

Los temas e ideas eliminados logicamente permanecen en la base, y sus contadores avanzan. Por ello, ejecuta las lecturas del dataset base antes del CRUD o utiliza una copia de pruebas equivalente para cada comparacion. Una ejecucion CRUD cambia los conteos fisicos del dataset; el validador de postmigracion con conteos exactos corresponde al estado inicial.

Si falla un paso, el script intenta limpiar los IDs que ya conoce. `cleanup_failures` debe permanecer en 0. La limpieza es de mejor esfuerzo: si interrumpes el proceso o el servidor crea un registro pero no devuelve su ID, puede quedar un registro de prueba pendiente de limpieza. No se hace un borrado directo de base de datos.

## Variables disponibles

| Variable | Valor predeterminado | Uso |
| --- | --- | --- |
| `PERF_PROFILE` | `load` | `smoke`, `load` o `burst` |
| `PERF_VUS` | `50` | Usuarios en load y burst; smoke siempre usa 1 |
| `PERF_DURATION` | `30s` | Duracion de carga sostenida |
| `PERF_SLEEP` | `1` en load, `0` en los otros | Pausa entre iteraciones, en segundos |
| `PERF_TOPIC_ID` | Descubrimiento automatico | Tema para consultar detalle o ideas |
| `PERF_PAGE_SIZE` | `10` | Elementos por pagina, entre 1 y 100 |
| `PERF_P95_MS` | `2000` | Limite de latencia p95, en milisegundos |
| `CIS_BASE_URL` | `http://localhost:5039` | CIS API |
| `USERS_BASE_URL` | `http://localhost:8080` | Users API |
| `K6_LOGIN`, `K6_PASSWORD` | Sin valor | Credenciales de login |
| `K6_TOKEN` | Sin valor | JWT alternativo al login |

Ejemplo para medir siempre el mismo tema:

```powershell
$env:PERF_TOPIC_ID = "2"
```

Para volver al descubrimiento automatico:

```powershell
Remove-Item Env:PERF_TOPIC_ID -ErrorAction SilentlyContinue
```

El descubrimiento para ideas elige un tema con ideas desde el ranking de actividad. Para comparaciones repetibles conviene fijar el mismo ID y verificar previamente cuantas ideas contiene.

Los controles propios usan el prefijo `PERF_`: las opciones nativas `K6_VUS`, `K6_DURATION`, `--vus` y `--duration` pueden reemplazar la configuracion de escenarios de k6. No las combines con estos perfiles. Si las configuraste antes en la terminal, retira las variables correspondientes:

```powershell
Remove-Item Env:K6_VUS, Env:K6_DURATION, Env:K6_ITERATIONS, Env:K6_STAGES -ErrorAction SilentlyContinue
```

## Resultados para la presentacion

Ejemplo para guardar el resumen de un endpoint:

```powershell
New-Item -ItemType Directory -Force .\TestResults\k6 | Out-Null
$env:PERF_PROFILE = "load"
k6 run --summary-export .\TestResults\k6\topics-mongo.json .\tests\performance\get-topics-k6.js
```

En una segunda ejecucion con CIS API configurada para MySQL, usa otro nombre, por ejemplo `topics-mysql.json`. Mantiene iguales dataset, endpoint, perfil, usuarios, duracion, paginacion, pausa y servicio Users API. Realiza una comprobacion previa para calentar el endpoint y registra la configuracion usada.

Para cada endpoint anota:

- Promedio, mediana y percentiles p90/p95/p99 de `http_req_duration` con `phase:measure` y su etiqueta `endpoint`.
- Peticiones medidas por segundo y porcentaje de peticiones fallidas con `phase:measure`.
- Porcentaje de checks correctos: se comprueban estado HTTP y estructura/contenido de las respuestas.
- Numero de usuarios, duracion, proveedor de base de datos y tamano del dataset.

Los umbrales iniciales son p95 menor a 2000 ms por endpoint, errores HTTP menores al 1 % y checks superiores al 99 %. Son criterios configurados, no resultados obtenidos de tu dataset. El limite de latencia se puede ajustar mediante `PERF_P95_MS` segun el objetivo de la entrega.

Los totales generales de k6 tambien incluyen login y descubrimiento. Para la comparacion usa los subindicadores etiquetados de la fase `measure`, que excluyen esos pasos y las limpiezas de recuperacion. Un umbral incumplido produce un codigo de salida distinto de cero.

## Verificacion de los scripts

```powershell
node --test tests/performance/config.test.cjs tests/performance/verify-k6.cjs
```

Requisitos adicionales de esa verificacion: Node.js, .NET SDK 10, Docker y dependencias .NET restauradas. Inicia MongoDB temporal, una instancia real aislada de CIS API y una simulacion local de Users API con credenciales exclusivas de prueba. No lee secretos de tu API, no utiliza MySQL ni `mongo_cis`, y retira los recursos temporales al terminar.

La verificacion usa un dataset pequeno controlado y un umbral amplio para comprobar los scripts; no es una medicion de rendimiento del seed de 500/2000/5000. Incluye 18 casos: configuracion, cinco consultas reales, carga sostenida, rafagas de 50 lecturas y 50 ciclos CRUD, exclusiones de metricas, credenciales invalidas, JSON malformado y limpieza despues de un fallo, incluido un voto confirmado cuya respuesta es invalida.

Referencias: [escenarios de k6](https://grafana.com/docs/k6/latest/using-k6/scenarios/), [umbrales](https://grafana.com/docs/k6/latest/using-k6/thresholds/) y [etiquetas](https://grafana.com/docs/k6/latest/using-k6/tags-and-groups/).
