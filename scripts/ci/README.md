# Scripts del pipeline

Estos archivos los ejecuta GitLab en una imagen Linux x86_64. Para trabajar en Windows usa las guías de [cobertura](../coverage/README.md) y [pruebas completas](../phase2/README.md).

## Configuración del runner

El runner debe permitir Docker-in-Docker en modo privilegiado. Se configura por su administrador.

La configuración del repositorio define DOCKER_HOST=tcp://docker:2375, DOCKER_TLS_CERTDIR vacío y TESTCONTAINERS_HOST_OVERRIDE=docker dentro del job. No necesitas copiar esas variables a tu PowerShell local.

## Qué ejecuta GitLab

En la imagen de CI:

~~~bash
bash scripts/ci/install-tools.sh node docker postman
bash scripts/ci/wait-for-docker.sh
bash scripts/ci/test-and-coverage.sh
~~~

- install-tools.sh instala las versiones fijadas y comprueba sus SHA-256. Escribe en /usr/local y /opt dentro de la imagen de CI.
- wait-for-docker.sh espera hasta 60 segundos a que Docker responda.
- test-and-coverage.sh compila, ejecuta las tres suites .NET, genera cobertura y aplica el umbral.

El último script necesita una carpeta nueva TestResults/Phase2/CI-Tests; falla si ya existe para no mezclar reportes.

COVERAGE_THRESHOLD vale 80 por defecto y puede configurarse como variable de GitLab. El resto de conexiones de prueba se genera automáticamente.

El pipeline conserva los reportes como artefactos. El job package-api genera una API publicable; el destino de despliegue se configura aparte.
