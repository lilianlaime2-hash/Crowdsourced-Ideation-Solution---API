# Pipeline de los cinco requisitos — Fase 3

La configuración tiene siete jobs y seis etapas. No contiene jobs ni dependencias de k6.

| Etapa / job | Comprobación |
| --- | --- |
| lint / lint | Formato .NET y pruebas Node del gate, ejecutor y Postman |
| build / build | Compilación Release de la solución |
| test / dotnet-tests | Unitarias, integración y migrador; TRX, cobertura combinada HTML/Cobertura y umbral 80% |
| test / migration-regressions | Validación post-migration con MongoDB real temporal |
| migration / migration-validation | Fixture MySQL, migración, SHA-256, post-migration y nueva comparación |
| functional / functional | Colección Postman CLI con MySQL y MongoDB; reportes JUnit |
| package / package-api | Artefacto publicable de CIS API tras pasar los controles |

La migración y su validación comparten job y contenedores aislados. El job funcional crea sus propias copias reproducibles y solo empieza después de superar la etapa migration. Cada fallo produce un código distinto de cero; no hay allow_failure.

Se requiere runner Linux x86_64 con Docker-in-Docker privilegiado. La configuración usa docker:27.2.0-dind, DOCKER_HOST=tcp://docker:2375, DOCKER_TLS_CERTDIR vacío y TESTCONTAINERS_HOST_OVERRIDE=docker. El daemon está limitado a la red temporal del job. La configuración del runner corresponde a su administrador; el repositorio no puede habilitar ese permiso.

El instalador verifica SHA-256 y versiones de Node22.17.1, DockerCLI27.2.0 y Postman1.74.0; el manifiesto fija ReportGenerator5.5.11. Esas herramientas y la conectividad DIND se comprobaron previamente en Linux; la configuración reducida se valida otra vez. Los scripts Bash fuerzan errores con set -euo pipefail. El job de cobertura construye dentro de su propio entorno antes de usar --no-build y consolida solo reportes de esa ejecución.

Los reportes se conservan 14 días. Los logs de la aplicación solo proceden de fixtures. No se requiere Postman Cloud ni acceso a bases reales.

El job package prepara el artefacto de entrega; no implica un despliegue. La evidencia de un pipeline GitLab real permanece pendiente: no se creó commit, rama nueva, push ni merge request en esta sesión.

