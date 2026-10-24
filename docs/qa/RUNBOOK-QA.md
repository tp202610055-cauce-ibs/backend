# Runbook del operador de QA

Qué hacer cuando un caso de prueba (CP) le pide algo al equipo de desarrollo. Hay una fila por cada pedido: 33 pasos de 29 CP, más los dos disparos del cuestionario (CP031 y CP084).

## Antes de empezar

- Todos los comandos se escriben en la VM, dentro de la carpeta del repositorio `infrastructure`, con `bash vm/qa.sh <comando>`.
- Corren en un contenedor aparte: no detienen la API ni molestan a los revisores. Solo corre uno a la vez; si otro está corriendo, el comando avisa y hay que esperar.
- La salida se puede copiar al revisor tal cual. No trae IP, navegador, contraseñas ni datos de otras personas. Los correos que aparecen son los de prueba (`@ejemplo.com`).
- Las horas salen dos veces: la de Lima (la que ve el revisor) y la UTC.
- El modo QA debe estar encendido (`bash vm/qa-mode.sh status`). Con él, los procesos automáticos están apagados y los disparos se hacen a mano con estos comandos.

## Los tres comandos que más se usan

| Comando | Para qué | Ejemplo |
|---|---|---|
| `audit-log` | Muestra el registro de lo que pasó (ingresos, aprobaciones, cambios) | `bash vm/qa.sh audit-log CP045 --action approve` |
| `inspect-qa` | Muestra cómo están los datos ahora (consentimiento, comidas, recomendaciones, códigos) | `bash vm/qa.sh inspect-qa CP066 --state` |
| `verify-qa` | Dice si un caso está listo para probarse | `bash vm/qa.sh verify-qa CP075` |

**Opciones de `audit-log`**
- `--since 1h`: desde hace una hora. Acepta horas (`2h`), días (`1d`), una fecha (`2026-10-26`) o fecha y hora de Lima (`2026-10-26T09:30`). Sin la opción, las últimas 24 horas.
- `--action approve`: solo ese tipo de acción. Se pueden poner varios separados por coma: `--action failed_login,login`.
- `--limit 20`: como máximo 20 filas (por omisión 50).
- En lugar del CP se puede poner un correo de prueba: `audit-log paciente.demo@ejemplo.com`.

**Las cuentas compartidas.** `nutricionista.demo`, `paciente.demo` y otras se usan en varios CP. Sus filas pueden venir de otro revisor. Usa siempre `--since 1h` (o menos) y compara la hora con la del revisor.

**Qué significa cada causa de un ingreso fallido**

| Causa | Significa |
|---|---|
| `wrong_password` | Contraseña incorrecta |
| `unknown_account` | El correo no tiene cuenta |
| `account_disabled` | La cuenta está deshabilitada |
| `pending_activation` | La cuenta todavía no se activó |
| `role_not_allowed` | La cuenta no puede entrar por ese canal (por ejemplo, un paciente en el portal) |
| `account_locked` | La cuenta está bloqueada por intentos fallidos |

Un ingreso con un correo que no tiene cuenta no guarda el correo. Sale como «correo sin cuenta (no se puede atribuir)», junto con todos los de la misma ventana de tiempo, de cualquier revisor.

## Pedidos de los CP

| CP | Paso | Comando | Qué se le muestra o dice al revisor | Cuenta como «pasó» cuando |
|---|---|---|---|---|
| CP001 | 11 | `bash vm/qa.sh inspect-qa CP001 --consent` | La línea «Consentimiento: versión 1.0, aceptado el …, vigente» de `registro.cp001` | Aparece la aceptación vigente, con la hora del registro |
| CP006 | 7 | `bash vm/qa.sh audit-log CP006 --since 1h --action failed_login` | Cuatro filas «Ingreso fallido» por el portal: `nutricionista.demo` (contraseña incorrecta), `nutricionista.inactivo`, `nutricionista.pendiente` y una de «correo sin cuenta» | Hay una fila por variante, a la hora en que el revisor la probó |
| CP014 | 6 | `bash vm/qa.sh audit-log paciente.demo@ejemplo.com --since 1h --action login` | La fila «Ingreso exitoso», canal mobile | Aparece el ingreso a la hora de la prueba |
| CP015 | 9 | `bash vm/qa.sh audit-log CP015 --action failed_login,account_locked` | Las filas «Ingreso fallido» seguidas de «Ingreso rechazado por bloqueo» | Aparecen los intentos fallidos y luego el bloqueo |
| CP016 | 7 | `bash vm/qa.sh audit-log nutricionista.demo@ejemplo.com --since 1h --action login` | La fila «Ingreso exitoso», canal portal | Aparece el ingreso a la hora de la prueba |
| CP017 | 7 | `bash vm/qa.sh audit-log CP017 --since 1h --action failed_login` | Cuatro filas «Ingreso fallido» por el portal: inactiva, pendiente, contraseña incorrecta y `paciente.demo` (no puede entrar al portal) | Hay una fila por variante con su causa |
| CP018 | 10 | `bash vm/qa.sh audit-log CP018 --action password_reset_request` | Las filas «Solicitud de recuperación de contraseña» con `n***@ejemplo.com` y `p***@ejemplo.com`. Si el revisor probó también `tester.sincuenta`, sale otra con `t***@ejemplo.com` | Hay una fila por cada solicitud. El sistema guarda solo la primera letra y el dominio, a propósito |
| CP023 | 10 | `bash vm/qa.sh inspect-qa CP023 --meals` | «Comidas que llegaron al servidor hoy: 1», con la hora en que llegó y la hora en que se registró en el teléfono | La comida llegó después de reconectar: la hora de llegada es posterior a la de registro |
| CP027 | 8 | `bash vm/qa.sh audit-log CP027` | Si el paciente confirmó el plato, la fila «Creación, Alimento propio, alérgenos aceptados=Lactosa» | Confirmó: aparece la fila con «Lactosa». Canceló: no aparece ningún «Alimento propio» |
| CP043 | 11 | `bash vm/qa.sh inspect-qa CP043 --recs` | Por recomendación, la respuesta del paciente: si la aplicó y el resultado | La recomendación que respondió muestra su respuesta con la hora |
| CP044 | 10 | `bash vm/qa.sh inspect-qa CP044 --recs` | Lo mismo, una vez que el teléfono recuperó la conexión | La respuesta aparece después de reconectar |
| CP045 | 9 | `bash vm/qa.sh audit-log CP045 --since 1h --action approve` | La fila «Aprobación, Recomendación», con `nutricionista.demo` como autor | Aparece la aprobación. La nota clínica no se muestra: se guarda protegida |
| CP046 | 8 | `bash vm/qa.sh audit-log CP046 --since 1h --action reject` | La fila «Rechazo, Recomendación», con `nutricionista.demo` como autor | Aparece el rechazo. El motivo no se muestra: se guarda protegido |
| CP048 | 9 | `bash vm/qa.sh audit-log CP048 --since 2h` y después `bash vm/qa.sh inspect-qa CP048 --recs` | Que no hay ninguna «Modificación», «Aprobación» ni «Rechazo» durante la prueba, y que las cuatro recomendaciones siguen en su estado | No aparece ningún cambio y los estados siguen iguales. **El intento rechazado no deja un registro propio**: la prueba es que nada cambió |
| CP051 | 7 | Ver «CP051 variante B» | Se apaga la API | El panel muestra el mensaje de error |
| CP051 | 9 | Ver «CP051 variante B» | Se vuelve a encender la API | Al reintentar, el panel carga |
| CP052 | 6 | `bash vm/qa.sh audit-log CP052 --action register` | Dos filas «Registro, Código de invitación», con `nutricionista.codigo1` como autor | Aparece una fila por código generado |
| CP054 | 8 | `bash vm/qa.sh inspect-qa CP054 --codes` | El código del paso 1, «canjeado el … por `registro.cp054@ejemplo.com`» | El código figura como canjeado por la cuenta nueva |
| CP058 | 11 | `bash vm/qa.sh audit-log CP058 --action export_pdf` | La fila «Reporte PDF», con los días del período y `nutricionista.reporte` como autor | Aparece la generación a la hora de la prueba |
| CP064 | 12 | `bash vm/qa.sh audit-log CP064 --action export` | La fila «Exportación de datos», con los conteos de cada archivo | Aparece la exportación a la hora de la prueba |
| CP066 | 9 | `bash vm/qa.sh inspect-qa CP066 --state` | «Cuenta: inactiva; correo anonimizado» y «Datos que conserva: … comida(s), … síntoma(s) …» | La cuenta quedó inactiva y anonimizada, y los datos clínicos siguen guardados (retención normativa) |
| CP066 | 10 | `bash vm/qa.sh audit-log CP066 --action delete` | La fila «Eliminación o anonimización», operación=anonymization | Aparece la operación a la hora de la prueba |
| CP071 | 9 | `bash vm/qa.sh audit-log CP071 --action update` | La fila «Modificación, Perfil», con `paciente.editar` como autor | Aparece el cambio a la hora de la prueba |
| CP072 | 8 | `bash vm/qa.sh audit-log CP072 --since 1h` | Las filas «Creación, Recomendación» y «Aprobación, Recomendación, origen=manual», con `nutricionista.demo` como autor | Aparece la creación de la recomendación manual |
| CP073 | 6 | **No ejecutable en la VM** | Se le dice al revisor que el portal no ofrece pacientes ajenos y que el sistema rechaza el pedido directo: lo cubre la prueba automática `CreateManual_NutritionistNotAssigned_Returns403` (`tests/Cauce.Api.IntegrationTests/Recommendations/RecommendationsApiTests.cs`) | Se declara en el documento de entrega |
| CP073 | 7 | `bash vm/qa.sh inspect-qa CP073 --recs` | Las recomendaciones de `paciente.manual2` y de `paciente.ajeno` | Ninguno de los dos tiene una recomendación nueva de la prueba |
| CP074 | 8 | `bash vm/qa.sh audit-log CP074 --since 1h --action update` | La fila «Modificación, Recomendación», con el motivo del archivado y `nutricionista.demo` como autor | Aparece el archivado con su motivo |
| CP075 | 3 | `bash vm/qa.sh archive-recommendations` | «1 recomendación(es) archivada(s) por vigencia vencida» | El comando archiva la vencida de `paciente.vencida` |
| CP075 | 6 | `bash vm/qa.sh audit-log CP075 --action update` | La fila «Modificación, Recomendación», con «Sistema (proceso automático)» como autor | El archivado lo hizo el sistema, no una persona |
| CP080 | 6 | `bash vm/qa.sh audit-log CP080 --since 1h --action failed_login,account_locked` | Dos filas «Ingreso fallido»: `paciente.demo` (contraseña incorrecta) y «correo sin cuenta» | Están los dos fallos y ningún bloqueo |
| CP084 | 1 | `bash vm/qa.sh inspect-qa CP084 --ibs` | «09:00 de Lima, todos los días; en QA lo dispara el operador». La salida muestra además «Cuestionario pendiente: le toca hoy» | El revisor recibe la hora |
| CP084 | Disparo | `bash vm/qa.sh process-ibs-sss` | Ver «El disparo del cuestionario» | `paciente.notif` recibe el aviso del día |
| CP031 | Disparo | `bash vm/qa.sh process-ibs-sss` | Ver «El disparo del cuestionario» | `paciente.recordatorio1` recibe el recordatorio y `paciente.recordatorio2` queda como «no registrado» |
| CP086 | 8 | `bash vm/qa.sh audit-log CP086 --since 1h --action approve` | La fila «Aprobación, Recomendación, operación=modified_approved», con `nutricionista.demo` como autor | Aparece la aprobación con cambios |
| CP089 | 6 | **No ejecutable** | «No ejecutable: el backend y el portal no tienen la solicitud de términos (HU0027, escenario 3)» | Se declara en el documento de entrega |

## El disparo del cuestionario (CP031 y CP084)

`process-ibs-sss` hace a mano lo que el sistema hace solo todos los días a las 09:00 de Lima. **Es global:** una sola ejecución atiende a todas las cuentas a la vez.

- `paciente.recordatorio1` (CP031) recibe el recordatorio de las 48 horas.
- `paciente.recordatorio2` (CP031) queda como «no registrado», porque pasaron más de 7 días.
- `paciente.notif` (CP084) recibe el aviso de que hoy le toca el cuestionario.
- Las cuentas con el cuestionario pendiente de CP030, CP032 y CP033 también reciben el aviso del día. No les cambia la prueba.

**Cuándo ejecutarlo:** cuando los revisores de CP031 y de CP084 estén listos, con la app abierta en sus teléfonos y la sesión iniciada. Una vez enviado, no se repite: para volver a probar, `bash vm/qa.sh reset-qa CP031 CP084` y otra vez `process-ibs-sss`.

## CP051 variante B (servicio no disponible)

**Impacto:** mientras la API está apagada, **nadie** puede usar la app ni el portal. Hazlo en una ventana exclusiva, avisando antes a todos los revisores. Dura uno o dos minutos.

1. Avisa a los revisores que el servicio se cortará durante dos minutos.
2. Paso 7, desde la carpeta `infrastructure/vm` de la VM: `docker compose -f docker-compose.vm.yml stop api`. El revisor de CP051 abre el panel y ve el mensaje de error.
3. Paso 9: `docker compose -f docker-compose.vm.yml start api`. Espera unos 30 segundos, hasta que `docker compose -f docker-compose.vm.yml ps api` diga `healthy`. El revisor pulsa reintentar.
4. Avisa que el servicio volvió.

Los dos comandos de `docker compose` los define la configuración de la VM (`infrastructure`). Si cambian, manda lo que diga su README.

## Preparación del operador: códigos de CP053 y CP088

El revisor de estos casos necesita escribir el valor completo de los códigos de invitación de `nutricionista.codigo2` (canjeado, vencido y vigente). El portal no los lista, así que se los da el operador:

    bash vm/qa.sh inspect-qa CP088 --reveal-codes

- Muestra completos solo los códigos que siembra el manifiesto (hoy, los de `nutricionista.codigo2`), con su estado. Los demás salen enmascarados.
- **Entrega los códigos por el documento de entrega o por un medio privado, nunca por un chat público:** los repositorios son públicos.
- Un código dura 72 horas desde que se genera. El vigente de CP088 se siembra con 60 horas por delante.
- Una vez canjeado, el vigente se rearma con `bash vm/qa.sh reset-qa CP088`. El reset global del domingo también lo regenera.

## Medir la memoria de un comando (ensayo del sábado)

La API tiene un límite de 700 MB, y cada comando de `qa.sh` corre en un contenedor aparte con el mismo límite. Para saber el pico de `verify-qa --all`, desde `infrastructure/vm`:

    docker compose -f docker-compose.vm.yml run --rm --no-deps -T -e QA_MODE=1 --entrypoint sh api \
      -c 'dotnet Cauce.Api.dll verify-qa --all; echo "pico: $(cat /sys/fs/cgroup/memory.peak) bytes"'

- La última línea da el pico en bytes. Divide por 1048576 para pasarlo a MB.
- Como alternativa, en otra terminal mientras corre el comando: `docker stats --no-stream $(docker ps -q --filter name=api-run)`.
- Para comparar con `exec`, que comparte el límite con la API en marcha: lee `docker compose -f docker-compose.vm.yml exec api cat /sys/fs/cgroup/memory.peak` antes y después de `docker compose -f docker-compose.vm.yml exec -e QA_MODE=1 api dotnet Cauce.Api.dll verify-qa --all`. Ese pico incluye a la API.
- Si el pico pasa de 600 MB, avisa antes de la semana de pruebas.
