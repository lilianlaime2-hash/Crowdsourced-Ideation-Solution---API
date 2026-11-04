# Fase 3: estado de los cinco requisitos

Alcance limitado a los cinco puntos confirmados por el usuario. Verificado localmente el 1 de noviembre de 2026 en feature/mongo-migration. La ruta histórica phase2 se conserva por compatibilidad.

| Punto | Estado local | Evidencia |
| --- | --- | --- |
| 1. CI/CD con migración | Implementado: siete jobs; etapa migration antes de functional; artefacto API al final. Falta ejecutar el pipeline remoto | [.gitlab-ci.yml](../../.gitlab-ci.yml), [ci.md](ci.md) |
| 2. Unit tests y coverage | 161 unitarias; cobertura combinada 2544/2805 = **90,70%** de líneas, sobre el 80% provisional | [coverage.md](coverage.md), TestResults/Phase3/RequiredCoverage/index.html |
| 3. Validación de migración | 19 pruebas; seis comparaciones SHA-256 coinciden y las alteraciones con igual conteo se detectan | [migration.md](migration.md), RequiredFunctional/migration-after.json |
| 4. Integración | 131 pruebas: 99 HTTP, 14 Mongo originales y 18 MySQL. Al menos tres casos para cada una de las 13 operaciones | [Matriz](endpoint-matrix.md), IntegrationBounds/integration.trx |
| 5. Postman CLI | Colección CRUD verificada con ambos proveedores: **32 peticiones y 49 aserciones correctas por proveedor** | RequiredFunctional/postman-MySQL y postman-MongoDB |

Resultado: **311 pruebas .NET** y **67 controles Node** (56 controles rápidos y 11 del post-migration), sin fallos ni omisiones. Cobertura de ramas informativa: 360/428 = 84,11%. Formato .NET, sintaxis de los tres scripts Bash y YAML de siete jobs válidos.

## Mínimos, máximos y promedios

- Título/descripción: mínimo 1/1 caracteres; máximo 150/1000; promedio entero 75/500 (punto medio redondeado hacia abajo).
- Paginación: pageSize 1/100/50. Con 101 registros se comprueban ventanas reales de 1, 100 y 50 elementos.
- Detalle, voto y DELETE: casos de existencia, propiedad y conflicto; esos estados no tienen un promedio numérico aplicable. La matriz documenta su correspondencia.

## Evidencias y reproducción

Todos los reportes vigentes se encuentran en TestResults/Phase3, ignorado por Git. [Snapshot versionado con hashes de los reportes](evidence-2026-11-01.json). El pipeline vuelve a generar sus propios reportes y los conserva 14 días. Comandos: [runbook.md](runbook.md).

| Ruta dentro de TestResults/Phase3 | Contenido |
| --- | --- |
| RequiredChecks/CIS.Tests/tests.trx | 161 unitarias |
| IntegrationBounds/integration.trx | 131 integraciones, con los límites intermedios actuales |
| RequiredChecks/migration/migration.trx | 19 pruebas del migrador |
| RequiredCoverage/ | Cobertura combinada HTML/Cobertura/texto |
| RequiredFunctional/ | Migración, checksum, dos reportes Postman y cierre sin errores |
| node-controls.tap y post-migration.tap | 56 y 11 controles Node |
| publish/ | Artefacto API Release |

Las bases de prueba son nuevas; no se modificaron sd3 ni mongo_cis. CIS, JWT y ambos motores son reales. Users API Java es un servicio de fixture en la automatización; una corrida con ese servicio real queda disponible usando las variables de la colección. La comparación de usuarios cubre id/name/login; las contraseñas no se migran ni se comparan.

## Fuera de esta entrega

k6, umbrales de latencia, rediseño de endpoints y optimizaciones de rendimiento. Los archivos previos de k6 permanecen disponibles. La optimización SQL y los añadidos de rendimiento se respaldaron en la ubicación registrada en TestResults/scope-archive-location.txt; la consulta SQL de producción quedó como estaba antes de esos añadidos.

No se creó rama nueva, commit, push ni merge request. La ejecución remota de GitLab necesita publicación autorizada y runner Docker; no se presenta como completada. El artefacto generado es entrega local; no hay destino de despliegue definido.

