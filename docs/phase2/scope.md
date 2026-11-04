# Fase 3: cinco requisitos pendientes

Alcance confirmado por el usuario: únicamente los cinco puntos siguientes. Los nombres técnicos de las carpetas phase2 y de los fixtures se conservan por compatibilidad con el trabajo ya preparado; esta entrega corresponde al cierre de pendientes durante Fase 3.

| Punto | Criterio de cierre | Implementación |
| --- | --- | --- |
| 1. Pipeline con migración de BD | Ejecutar migración y validación antes del cliente funcional; conservar reportes y artefacto API | .gitlab-ci.yml: lint → build → test → migration → functional → package |
| 2. Unit tests y cobertura | Pruebas pasan y cobertura combinada de líneas >=80%; pipeline falla bajo el umbral | CIS.Tests, coverage.runsettings, ReportGenerator y gate |
| 3. Validación de migración | Comparar MySQL/MongoDB con SHA-256 e integridad referencial; detectar alteración con iguales conteos | tools/CIS.Migration y sus pruebas con bases reales temporales |
| 4. Integración por endpoint | Al menos tres casos por cada una de las 13 operaciones de CIS | 99 casos HTTP y matriz; MongoDB real |
| 5. Postman CLI | Ejecutar colección CRUD desde consola con variables, validaciones y reporte JUnit | Colección de 32 peticiones y ejecutor reproducible |

Para datos de texto se prueban mínimo 1/1, máximo 150/1000 y promedio entero 75/500 (título/descripción). Para paginación: pageSize 1/100/50; se siembran 101 registros para comprobar ventanas reales de 1, 100 y 50 elementos. El promedio corresponde al punto medio redondeado hacia abajo. En detalle, votos y DELETE se prueban existencia, propiedad y conflicto: son estados, sin un promedio numérico aplicable.

El 80% de cobertura de líneas sigue siendo el objetivo provisional aceptado en la conversación. Se informa cobertura de ramas sin inventar otro umbral. Se conservan los archivos productivos en el alcance y se excluye únicamente código generado documentado.

El fixture contiene cuatro usuarios, 500 temas, 200 etiquetas, 1800 relaciones, 2000 ideas y 5000 votos, con mayoría de temas e ideas de AdrianCis. La proyección de usuarios comprende id/name/login; las contraseñas y la autenticación Java no forman parte del checksum.

Las pruebas usan contenedores nuevos. CIS, JWT, MySQL y MongoDB son reales; el servicio Java de usuarios es un servicio de fixture en la automatización, identificado en sus reportes. La colección también permite apuntar a los servicios reales mediante variables locales.

k6, presupuestos de latencia, optimizaciones de consultas y simplificación de endpoints quedan fuera de esta entrega. El trabajo previo de rendimiento se conserva aparte. La separación MongoDB del proyecto que ya estaba en la rama se conserva como base de las pruebas.

El pipeline está implementado y se comprueba localmente. Su ejecución remota permanece pendiente hasta publicar una rama autorizada y disponer de runner Docker. El job package genera un artefacto de entrega; no se ha definido destino para desplegar la API.

