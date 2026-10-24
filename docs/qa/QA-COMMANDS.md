# Comandos de QA: seed-qa, reset-qa y verify-qa

Preparan y comprueban las cuentas y los datos de los casos de prueba (CP) de la semana de QA. Corren dentro del contenedor de la API, con el mismo binario:

    dc exec api dotnet Cauce.Api.dll seed-qa
    dc exec api dotnet Cauce.Api.dll reset-qa CP082 CP066
    dc exec api dotnet Cauce.Api.dll verify-qa --all

La fuente de verdad es el manifiesto `docs/qa/qa-manifest-*.json`. La imagen lo publica en `qa/` junto a la API. Para usar otro sin recompilar: `--manifest <ruta>`, con el archivo montado en el contenedor. El código no contiene datos de ningún CP: si el manifiesto cambia, solo cambia el archivo.

## Variables de entorno

| Variable | Uso | Comandos |
|---|---|---|
| `QA_MODE=1` | Habilita los comandos de QA | Los tres |
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
- **Cuentas:** que existan, con su estado en Keycloak y en el backend, sin bloqueo y sin acciones pendientes.
- **Cuentas `mustNotExist`:** que no existan.
- **Datos del caso:**
  - vínculos activos;
  - perfil y alergias;
  - recomendaciones contadas por estado, con la expiración que se evalúa al leer;
  - códigos de invitación;
  - cuestionarios pendientes.
- **Edad del ancla de los casos con reloj:**

| Reloj del caso | Tolerancia |
|---|---|
| `hours` | 2 horas desde el rearme |
| `days` | El mismo día de Lima |

Un caso consumido sale en FALLA hasta que se corre `reset-qa` sobre él.

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
