# Comandos de QA

Preparan, comprueban e inspeccionan las cuentas y los datos de los casos de prueba (CP) de la semana de QA. Usan el mismo binario de la API. En la VM, `bash vm/qa.sh <comando>` los corre en un contenedor aparte (`docker compose run --rm --no-deps api <comando>`), con su propio límite de memoria:

    bash vm/qa.sh seed-qa
    bash vm/qa.sh reset-qa CP082 CP066
    bash vm/qa.sh verify-qa --all
    bash vm/qa.sh audit-log CP045 --action approve
    bash vm/qa.sh inspect-qa CP066 --state
    bash vm/qa.sh archive-recommendations

Qué comando usar en cada paso del operador: `docs/qa/RUNBOOK-QA.md`.

La fuente de verdad es el manifiesto `docs/qa/qa-manifest-*.json`. La imagen lo publica en `qa/` junto a la API. Para usar otro sin recompilar: `--manifest <ruta>`, con el archivo montado en el contenedor. El código no contiene datos de ningún CP: si el manifiesto cambia, solo cambia el archivo.

## Variables de entorno

| Variable | Uso | Comandos |
|---|---|---|
| `QA_MODE=1` | Habilita los comandos de QA | Todos |
| `QA_ALLOW_DESTRUCTIVE=1` | Permite borrar y recrear cuentas | `seed-qa` y `reset-qa` |
| `QA_PATIENT_PASSWORD` | Contraseña de los pacientes del manifiesto | `seed-qa` y `reset-qa` |
| `QA_NUTRITIONIST_PASSWORD` | Contraseña de los nutricionistas del manifiesto | `seed-qa` y `reset-qa` |
| `QA_SMOKE_PASSWORD` | Contraseña de las cuentas de humo del portal | `seed-qa` y `reset-qa` |

- Las contraseñas solo se leen de estas variables. No van en el manifiesto ni en el repositorio, y no aparecen en el log ni en la salida.
- Antes de escribir nada, se validan contra la política de contraseñas del realm. Si alguna falla, el mensaje nombra la variable y la regla incumplida, nunca el valor.

## Códigos de salida

| Código | Significado |
|---|---|
| 0 | Terminó bien. En `verify-qa`, todos los casos están en OK |
| 1 | Falló, o `verify-qa` encontró al menos un caso en FALLA. Si el manifiesto o el catálogo tienen problemas, se listan todos y no se escribe nada |
| 2 | El entorno o los argumentos no lo permiten (falta una variable, contraseña débil, opción desconocida). No se escribe nada |

## seed-qa

Siembra idempotente: se puede correr las veces que haga falta.

1. Comprueba las guardas, el manifiesto, el catálogo (alimentos y alergias), los tipos de siembra y las contraseñas. Si algo falta, falla antes de escribir y lista todo lo que falta.
2. Retira las cuentas que no deben existir (`mustNotExist`).
3. Crea las cuentas que faltan, con sus datos. Una cuenta que ya existe no recibe datos nuevos: solo recupera su estado, su contraseña y su correo verificado, y se le limpia el bloqueo por fuerza bruta.
4. Crea las cuatro cuentas de humo del portal:
   - `smoke.nutricionista@ejemplo.com`, activa;
   - `smoke.paciente@ejemplo.com`, activa;
   - `smoke.nutricionista.inactivo@ejemplo.com`, deshabilitada;
   - `smoke.nutricionista.pendiente@ejemplo.com`, pendiente de activación.

**Estados de las cuentas**

| Estado del manifiesto | Keycloak | Backend |
|---|---|---|
| `active` | Habilitada, correo verificado, sin acciones pendientes, contraseña permanente | Activa |
| `disabled` | Deshabilitada | Suspendida |
| `pendingActivation` | Sin contraseña; el enlace de activación llega por el camino de producción | Pendiente de activación |

- Ninguna cuenta lleva token FCM.
- Los pacientes nacen en el piloto activo y con el consentimiento vigente aceptado.

**Efectos secundarios.** La siembra no crea notificaciones, correos ni filas de outbox. La única excepción es el correo de activación de las cuentas pendientes. Los datos se escriben con las fábricas del dominio, no con los casos de uso que avisan al paciente.

## reset-qa

    reset-qa <CPxxx | correo> [...]

Rearma uno o más casos o cuentas, y vuelve a anclar al reloj los datos relativos.

| Tipo de cuenta | Qué hace el rearme |
|---|---|
| Exclusiva del caso (aparece en un solo CP) | Se borra en Keycloak y se retira en el backend: un paciente se anonimiza como en la baja de cuenta y un nutricionista se retira. Después se siembra de nuevo con sus datos. Así se limpia todo lo que dejó la prueba |
| Compartida (aparece en más de un CP) | No se borra. Recupera su estado y su contraseña, se le limpia el bloqueo y el caso vuelve a sembrar sus datos sobre ella |
| Pendiente de activación | Queda sin activar y recibe un enlace nuevo, vigente y real |
| `mustNotExist` del caso | Se borra en Keycloak y se retira en el backend si la prueba la creó |

- Ninguna cuenta se borra físicamente del backend. La cuenta retirada queda inactiva y con el correo liberado.
- Sus vínculos se desactivan, así que no aparece en el panel del nutricionista ni en la cola de revisión.
- Un objetivo desconocido detiene el comando antes de tocar nada.

## verify-qa

    verify-qa [CPxxx ... | --all]

Solo lectura: no escribe en la base ni en Keycloak. Sin argumentos revisa todos los casos. Imprime una línea por caso (`CP036 OK` o `CP036 FALLA: motivo`) y un resumen.

Qué comprueba:
- **Cuentas:** que existan, con su estado en Keycloak y en el backend, sin bloqueo y sin acciones pendientes. Cada paciente, con el consentimiento vigente.
- **Cuentas `mustNotExist`:** que no existan.
- **Datos del caso:**
  - vínculos activos;
  - perfil y alergias;
  - recomendaciones contadas por estado, con la expiración que se evalúa al leer;
  - ninguna recomendación archivada, y las manuales con la vigencia vencida (CP075) pendientes de archivo;
  - códigos de invitación;
  - cuestionarios pendientes.
- **Edad del ancla de los casos con reloj:**

| Reloj del caso | Tolerancia |
|---|---|
| `hours` | 2 horas desde el rearme |
| `days` | El mismo día de Lima |

Un caso consumido sale en FALLA hasta que se corre `reset-qa` sobre él. Después de `archive-recommendations`, CP075 sale en FALLA hasta su rearme.

No comprueba comidas, síntomas, alimentos propios ni cuestionarios respondidos (la siembra los genera también desde `activity` y `supportingData`), ni notas, push, buzón o glosario.

## audit-log

    audit-log <CPxxx | correo> [--since <6h | 2d | 2026-10-24 | 2026-10-24T09:30>] [--action <tipo>[,<tipo>]] [--limit N]

Solo lectura. Muestra las filas de `audit_logs` de las cuentas de un caso, o de una cuenta del manifiesto. Por omisión, las últimas 24 horas y 50 filas, de la más antigua a la más reciente. Una línea por fila: hora de Lima, hora UTC, acción, entidad, contexto y autor.

Qué filas entran:
- las que hizo una cuenta del caso que no se usa en otros casos;
- de una cuenta compartida, sus ingresos y lo que hizo sobre los datos del caso;
- las que tocan los datos de los pacientes del caso (recomendaciones, comidas, perfil, vínculos), también las de los procesos automáticos;
- si el caso tiene cuentas que no deben existir, los ingresos fallidos sin actor de la ventana;
- las solicitudes de recuperación y de reenvío cuyo correo enmascarado coincide con una cuenta del caso.

| Autor | Significa |
|---|---|
| Un correo `@ejemplo.com` | Lo hizo esa cuenta del caso |
| Sistema (proceso automático) | Un worker, un comando o un trigger sin sesión |
| correo sin cuenta (no se puede atribuir) | Ingreso con un correo que no existe; el registro no guarda el correo |
| sin sesión (coincide el correo enmascarado) | Solicitud anónima; el registro guarda solo la primera letra y el dominio |
| otra cuenta | Una cuenta que no es del caso |

- Nunca imprime IP, user agent ni hashes: no los lee de la base.
- Del contexto adicional solo salen las claves de una lista blanca (canal, causa, operación, motivo, origen, correo enmascarado, alérgenos aceptados, conteos, rol y resultado). Lo demás sale como «(otros datos omitidos)».
- Encuentra al paciente anonimizado por la baja de cuenta por su identidad en Keycloak, que conserva el correo.
- `--action` acepta el nombre del tipo con o sin guion bajo (`failed_login`, `FailedLogin`).
- Un intento rechazado de modificar una recomendación aprobada no deja fila propia (CP048).

## inspect-qa

    inspect-qa <CPxxx | correo> [--consent] [--meals] [--ibs] [--codes] [--recs] [--state] [--reveal-codes]

Solo lectura. Sin banderas, muestra todas las secciones. Conteos, estados y fechas, sin textos clínicos ni nombres de alimentos.

| Bandera | Muestra |
|---|---|
| `--consent` | Versión aceptada, fecha y si está vigente |
| `--meals` | Comidas que llegaron hoy (día de Lima): hora de llegada, de registro en el teléfono, de consumo, franja y sincronización |
| `--ibs` | Cuestionarios respondidos, los pendientes, si salió el aviso o el recordatorio, y la hora del aviso (09:00 de Lima) |
| `--codes` | Códigos de invitación de los nutricionistas, enmascarados (`2C****FK`), con su estado y fechas |
| `--recs` | Recomendaciones del paciente: estado, si está activa o archivada, vigencia y la respuesta del paciente |
| `--state` | Estado de la cuenta, si el correo está anonimizado y qué datos conserva |
| `--reveal-codes` | Implica `--codes`. Muestra completos solo los códigos de los nutricionistas con códigos sembrados por el manifiesto (CP053 y CP088); los demás siguen enmascarados |

Los códigos completos se entregan al revisor por el documento de entrega o por un medio privado, nunca por un chat público: los repositorios son públicos.

## archive-recommendations y expire-recommendations

Una pasada a demanda de los workers que el modo QA apaga, con la misma lógica (`RecommendationSweeper`). No reciben argumentos y afectan a todas las cuentas.

| Comando | Qué hace | Worker equivalente |
|---|---|---|
| `archive-recommendations` | Archiva las recomendaciones aprobadas activas cuya vigencia venció, con el motivo «vigencia vencida» | `RecommendationArchivalWorker` (03:00 UTC) |
| `expire-recommendations` | Pasa a vencidas las generadas, pendientes o aprobadas sin entregar cuya ventana venció | `RecommendationExpirationWorker` (cada 6 horas) |

Imprimen cuántas recomendaciones afectaron. El archivado queda en la auditoría con «Sistema» como autor.

## Reloj

- Cada ejecución de `seed-qa` o `reset-qa` usa un único ancla: el instante en que empieza.
- Todo «hace N horas o días» se cuenta desde el ancla.
- «Hoy» es el día de Lima (UTC−5, sin horario de verano).
- Una fecha fija del manifiesto (CP058 y CP059) se siembra a las 12:00 de Lima de ese día.

## Valores por omisión

Lo que el manifiesto no trae toma valores fijos, así que dos siembras dan el mismo resultado.

**Cuentas y perfil**

| Dato | Valor |
|---|---|
| Nombre visible | El del manifiesto o, si no trae, el correo: `paciente.perfil1` da «Paciente Perfil1» |
| Fecha de nacimiento y sexo | 1990-01-01, femenino |
| Peso, talla y subtipo | 62 kg, 165 cm, SII-D |
| Severidad de cada alergia | Moderada |
| Perfil incompleto (`complete: false`) | Sin perfil: el onboarding queda sin empezar |

**Registros clínicos**

| Dato | Valor |
|---|---|
| Comida | 150 g, almuerzo si no trae franja |
| Síntoma sin intensidad | 50 (escala de 1 a 100) |
| Actividad de registro | Una comida de «Arroz blanco cocido» por día con registro, repartida de forma pareja |

**Recomendaciones**

| Dato | Valor |
|---|---|
| Ítems del motor | `suggests`, `reduce` y `avoid` dan ítems Suggest, Reduce y Avoid. Sin ninguno, Reduce sobre «Cebolla cocida» |
| Título | Solo las manuales (`createdByNutritionist`) lo guardan |
| Confianza | high 0,85; medium 0,60; low 0,30; sin nivel, 0,80 |
| Explicación | Texto personalizado fijo con origen LlmGenerated. Con respaldo, el texto del proveedor de respaldo y origen Fallback. Ninguna se genera en vivo |
| Revisor | El nutricionista vinculado al paciente o, sin vínculo, el primer nutricionista activo del manifiesto |
| Vencimiento | Pendiente: ancla + 10 días. Aprobada y no entregada: ancla + 30 días, porque la expiración también se evalúa al leer |
| Paciente abrió o respondió | `patientOpened` la deja entregada; `patientResponded` le agrega una respuesta |
| Notas sin texto en el caso | Valores fijos para la nota de aprobación, el motivo de rechazo y la descripción y nota de una manual |
| `supportingData` y `withRecentHistory` | Comidas del primer alimento a reducir, cada una seguida de un dolor abdominal a las 2 horas |

**Códigos de invitación**

| Estado | Cómo se siembra |
|---|---|
| Vigente | Vence en las horas que indica el caso |
| Vencido | Emitido hace los días que indica el caso |
| Canjeado | A nombre del primer paciente del manifiesto, sin vínculo |

## Límites conocidos

- El alta de la cuenta en el backend (`users.created_at`) siempre es la del día de la siembra. El dominio no permite fijarla.
- `validDaysLeft` en una recomendación del motor es informativo: solo las manuales llevan fecha de vigencia.
- Los pasos (`steps`, `stepsCount`) solo se guardan en las manuales.
- Cada rearme deja en la base la cuenta retirada y sus datos, inactivos y sin vínculos.
