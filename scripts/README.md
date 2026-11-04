# Scripts

Los comandos se ejecutan desde la raíz del repositorio, no desde esta carpeta.

| Carpeta | Uso |
| --- | --- |
| [ci](ci/README.md) | Preparar y ejecutar los controles de GitLab en Linux |
| [coverage](coverage/README.md) | Generar cobertura HTML y comprobar el mínimo |
| [migration](migration/README.md) | Validar el dataset después de migrarlo |
| [phase2](phase2/README.md) | Ejecutar migración, checksum y Postman con bases temporales |

Cada guía indica qué instalar y qué variables configurar. Los archivos auxiliares se cargan desde el script principal; no se ejecutan todos por separado.
