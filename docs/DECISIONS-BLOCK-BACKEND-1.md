# Cauce Backend — Decisiones de arquitectura

Bloque único de actas del backend. Reemplaza los 19 archivos sueltos que vivían en
`docs/decisions/`, en simetría con `DECISIONS-BLOCK-MOBILE-1.md` del repo mobile.

**Alcance:** actas A38 a A58, de los bloques Backend-Fix-2, Nutritionist-Activation-1 y el
trabajo posterior sobre el consentimiento. Las actas A1 a A37 y las decisiones DEC-B3 a
DEC-B7B no tienen archivo en ningún repo: solo sobreviven resumidas en el `CLAUDE.md` local.
**Numeración:** A42 y A50 no existen como acta.
**Autores:** Trigo (decisión), Kiwicha (redacción).
**Consolidado:** 2026-09-17, sin alterar el contenido de ninguna acta.

---

## Índice

| Acta | Título | Estado |
| --- | --- | --- |
| [A38](#acta-a38-deuda-diferida--isinactivepilot-hardcodeado) | Deuda diferida — isInActivePilot hardcodeado | Aprobada, deuda diferida. Bloqueada por dependencia externa |
| [A39](#acta-a39-sincronización-lazy-de-emailverified-desde-keycloak-en-login) | Sincronización lazy de emailVerified desde Keycloak en login | Aprobada, RESUELTA en Backend-Fix-2 (tag `v0.8.0-backend-fix-2`) |
| [A40](#acta-a40-endpoint-de-reenvío-del-correo-de-verificación) | Endpoint de reenvío del correo de verificación | Aprobada, RESUELTA en Backend-Fix-2 (tag `v0.8.0-backend-fix-2`) |
| [A41](#acta-a41-endpoint-de-canje-de-código-de-invitación-post-registro) | Endpoint de canje de código de invitación post-registro | Aprobada, RESUELTA en Backend-Fix-2 (tag `v0.8.0-backend-fix-2`) |
| [A43](#acta-a43-reemplazo-de-d6-uso-del-evento-de-dominio-existente-para-notificar-al-nutricionista-con-parametrización-del-texto-por-contexto) | Reemplazo de D6, uso del evento de dominio existente para notificar al nutricionista, con parametrización del texto por contexto | Aprobada, aplicada en Backend-Fix-2 Fase 3 |
| [A44](#acta-a44-partición-del-rate-limit-por-el-correo-del-cuerpo-de-la-petición) | Partición del rate limit por el correo del cuerpo de la petición | Aprobada, aplicada en Backend-Fix-2 Fase 2 |
| [A45](#acta-a45-migración-de-pruebas-en-un-refactor-por-inyección-de-constructor) | Migración de pruebas en un refactor por inyección de constructor | Aprobada, aplicada en Backend-Fix-2 Fase 3 |
| [A46](#acta-a46-swashbuckle-cli-como-herramienta-local-para-regenerar-el-snapshot-openapi) | Swashbuckle CLI como herramienta local para regenerar el snapshot OpenAPI | Aprobada, aplicada en Backend-Fix-2 Fase 5 |
| [A47](#acta-a47-deuda-diferida--sin-ciclo-de-vida-de-cuentas-de-nutricionista) | Deuda diferida — sin ciclo de vida de cuentas de nutricionista | Aprobada. **Activación RESUELTA en Nutritionist-Activation-1** (actas A51 a A53); suspensión, reactivación y baja siguen diferidas (actas A55 y A56) |
| [A48](#acta-a48-asimetría-en-la-persistencia-de-la-auditoría-de-intentos-anónimos) | Asimetría en la persistencia de la auditoría de intentos anónimos | Documentada, resolución postergada a bloque futuro |
| [A49](#acta-a49-respuesta-del-endpoint-de-provisión-de-nutricionistas) | Respuesta del endpoint de provisión de nutricionistas | Aprobada, RESUELTA en Nutritionist-Activation-1 |
| [A51](#acta-a51-mecanismo-de-activación-de-cuentas-de-nutricionista) | Mecanismo de activación de cuentas de nutricionista | Aprobada, RESUELTA en Nutritionist-Activation-1 |
| [A52](#acta-a52-enlace-de-keycloak-para-que-el-nutricionista-defina-su-contraseña-y-su-reenvío) | Enlace de Keycloak para que el nutricionista defina su contraseña, y su reenvío | Aprobada, RESUELTA en Nutritionist-Activation-1 |
| [A53](#acta-a53-estado-del-nutricionista-al-registrarse-con-un-código-y-al-generarlo) | Estado del nutricionista al registrarse con un código y al generarlo | Aprobada, RESUELTA en Nutritionist-Activation-1 |
| [A54](#acta-a54-deuda-diferida--términos-operativos-del-nutricionista) | Deuda diferida — términos operativos del nutricionista | Aprobada, deuda diferida. Bloque por definir |
| [A55](#acta-a55-deuda-diferida--la-suspensión-no-corta-sesiones-ni-tokens-ya-emitidos) | Deuda diferida — la suspensión no corta sesiones ni tokens ya emitidos | Aprobada, deuda diferida. Se resuelve junto con el flujo de suspensión |
| [A56](#acta-a56-deuda-diferida--los-códigos-de-invitación-no-se-revocan-al-suspender) | Deuda diferida — los códigos de invitación no se revocan al suspender | Aprobada, deuda diferida. Se resuelve junto con el flujo de suspensión |
| [A57](#acta-a57-el-comprobante-en-pdf-del-consentimiento-se-resuelve-por-versión-aceptada-no-por-la-vigente) | El comprobante en PDF del consentimiento se resuelve por versión aceptada, no por la vigente | Aprobada, implementada en el bloque de cierre de HU0001 escenario 4 |
| [A58](#acta-a58-desviación-de-dec-b3-07-el-seeder-del-consentimiento-corre-en-todos-los-ambientes) | Desviación de DEC-B3-07, el seeder del consentimiento corre en todos los ambientes | Aprobada. Solución intermedia hasta que exista el CLI de producción que prevé DEC-B3-07 |

---

## Acta A38: Deuda diferida — isInActivePilot hardcodeado

**Estado:** Aprobada, deuda diferida. Bloqueada por dependencia externa
**Fecha:** 2026-09-08
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, flujo de registro de paciente.

---

### Contexto

El flag `isInActivePilot` determina si un paciente forma parte del piloto clínico. Tiene dos efectos
reales: viaja al cliente en el objeto `user` del login, y gobierna la retención de datos en la baja de
cuenta (US26), donde un paciente inscrito en un piloto activo no puede eliminar sus datos sin acuse
explícito.

Hoy el valor no se deriva de ninguna fuente autorizada. El Complejo Hospitalario Guillermo Kaelín de la
Fuente todavía no entregó la lista definitiva de pacientes que participarán del piloto, y sin esa lista
no hay contra qué verificar.

### Decisión

**No resolver en Backend-Fix-2.** Esperar la entrega de la lista por parte del hospital. Cuando esté
disponible, implementar la verificación real contra la tabla de pacientes autorizados.

### Consecuencias

- Durante el desarrollo, todos los pacientes registrados aparecen como parte del piloto activo. Es
  aceptable porque el ambiente es de desarrollo y ningún dato es clínico real.
- El gate de retención de US26 responde hoy de forma uniforme para todos, en vez de distinguir a los
  participantes reales.
- **Un smoke test previo al piloto real debe verificar que la lógica no se activó sin la lista
  completa.** Es el riesgo que hay que vigilar: activar la verificación con una lista parcial dejaría
  fuera del piloto a pacientes que sí participan.

### Bloqueante

Dependencia externa del Complejo Hospitalario Guillermo Kaelín de la Fuente (EsSalud Lima Sur). No es
trabajo técnico pendiente: es información que el backend no puede producir por su cuenta.

### Otra deuda diferida de este bloque

Backend-Fix-2 identificó una segunda deuda que también se difiere, registrada en el acta
[A47](#acta-a47-deuda-diferida--sin-ciclo-de-vida-de-cuentas-de-nutricionista): no existe ciclo de vida de cuentas de nutricionista.
A diferencia de esta, **A47 no tiene bloqueante externo** y ya tiene bloque asignado,
Nutritionist-Activation-1.

### Referencias

- `src/Cauce.Application/Identity/UseCases/RegisterPatient/RegisterPatientCommandHandler.cs`
- `src/Cauce.Domain/Identity/User.cs` (`EnrollInActivePilot`).
- Acta A16, activación manual de `IsInActivePilot` por el Kaelín.
- Acta [A47](#acta-a47-deuda-diferida--sin-ciclo-de-vida-de-cuentas-de-nutricionista), la otra deuda diferida del bloque.

---

## Acta A39: Sincronización lazy de emailVerified desde Keycloak en login

**Estado:** Aprobada, RESUELTA en Backend-Fix-2 (tag `v0.8.0-backend-fix-2`)
**Fecha:** 2026-09-08
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, `LoginCommandHandler` y `KeycloakAdminClient`.

---

### Contexto

La base local no sincronizaba `emailVerified` con Keycloak. El enlace de confirmación lo emite y lo
procesa el propio realm, sin pasar por el backend, de modo que al verificar su correo el paciente
actualizaba el estado **en Keycloak pero no en la copia local**.

La app móvil lee ese valor del objeto `user` que devuelve el login. El efecto para el paciente era que,
habiendo verificado su correo, la aplicación lo seguía tratando como pendiente de verificación, sin
ninguna acción a su alcance que corrigiera la situación.

### Decisión (D1)

Sincronizar de forma diferida en cada inicio de sesión: tras la autenticación exitosa contra Keycloak,
`LoginCommandHandler` invoca `IKeycloakAdminClient.GetUserEmailVerifiedAsync` y actualiza el valor local
si difiere.

La escritura se enrola en el `ChangeTracker` y la confirma el `SaveChangesAsync` que el handler ya hacía
para la marca de último acceso, de modo que ambos cambios viajan en **una sola transacción**.

### Decisión complementaria (D2)

Un fallo de la Admin API se registra como advertencia y el inicio de sesión continúa con el valor local.
**El login no falla por una sincronización auxiliar.** El manejo replica el patrón que ya usaba
`TryResolveLockoutAsync` en el mismo archivo: se capturan todas las excepciones salvo
`OperationCanceledException`, que se propaga.

### La sincronización es unidireccional

El prompt de origen se contradecía en el caso borde «Keycloak dice `false`, la base local dice `true`».
Su sección 4.3 indicaba sincronizar hacia `false`; su Fase 1 indicaba no revertir. **Se resolvió no
revertir**, por tres razones:

1. **Seguridad clínica.** Un correo ya verificado no debe des-verificarse por el resultado de una
   consulta auxiliar. Sería un cambio silencioso de estado sin acción explícita del usuario.
2. **Alcance acotado.** La sincronización solo promueve `false → true`. El sentido inverso no se
   automatiza.
3. **Evidencia empírica.** La prueba preexistente `Login_SeededNutritionist_ReturnsNutritionistRole`
   afirma `emailVerified: true` mientras el doble de Keycloak devuelve `false`. Revertir la habría roto,
   violando la regla de cero regresiones en el primer commit del bloque.

Si en el futuro hiciera falta des-verificar un correo, debe ser mediante un flujo explícito con
auditoría dedicada, nunca como efecto colateral de un login. El caso se registra hoy como advertencia.

### Consecuencias

- El comportamiento del login cambia de forma observable: la respuesta puede reflejar por primera vez el
  estado real de verificación.
- El login pasa a depender de la Admin API de Keycloak, con manejo tolerante de fallos.
- El móvil recibe `emailVerified` correcto en la primera sesión posterior a la verificación.
- El contrato HTTP no cambia: mismos códigos de respuesta y mismo cuerpo.

### Referencias

- `src/Cauce.Application/Identity/UseCases/Login/LoginCommandHandler.cs`
- `src/Cauce.Infrastructure/Identity/KeycloakAdminClient.cs`
- `docs/api/CONTRACT-IDENTITY-v1.md` v1.2, §2.2.

---

## Acta A40: Endpoint de reenvío del correo de verificación

**Estado:** Aprobada, RESUELTA en Backend-Fix-2 (tag `v0.8.0-backend-fix-2`)
**Fecha:** 2026-09-08
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, endpoint anónimo nuevo en `AuthController`.

---

### Contexto

Un paciente que perdía el correo de verificación no tenía forma de pedir otro. El enlace vence, el
mensaje se borra o cae en la carpeta de no deseados, y la cuenta queda inutilizable: sin verificar no se
puede iniciar sesión, y sin iniciar sesión no hay ningún endpoint al alcance. La única salida era
contactar a soporte de forma manual, que en un piloto clínico con pacientes reales no es una salida.

### Decisión

Nuevo endpoint `POST /api/v1/auth/verification-email/resend`, anónimo, con una política de rate limit
dedicada.

### Decisiones complementarias

- **D3.** Rate limit `auth-verify-resend`: 3 peticiones por hora, **particionadas por el correo
  normalizado**, no por IP. El mecanismo que lo hace posible está documentado en el acta A44.
- **D4.** Si la cuenta ya está verificada, la respuesta es 200 igual. No se revela el estado.
- **R10.** Validación exhaustiva de la entrada con FluentValidation.

### Respuesta deliberadamente uniforme

Los tres desenlaces terminan en **200 sin cuerpo**: la cuenta no existe, la cuenta existe y ya está
verificada, o la cuenta existe sin verificar y se solicitó el reenvío. El cliente **no puede inferir del
código de respuesta si un correo está registrado ni si fue verificado**.

Distinguirlos convertiría el endpoint en un oráculo de cuentas: cualquiera podría enumerar qué correos
pertenecen a pacientes del piloto, que es información clínica por asociación.

El envío a Keycloak es best-effort: si la Admin API falla, se registra advertencia y la respuesta sigue
siendo 200.

### Consecuencias

- Es la **octava política de rate limit** del sistema, y la primera que particiona por contenido del
  cuerpo de la petición.
- **No hizo falta agregar `SendVerifyEmailAsync`** a `IKeycloakAdminClient`: ya existía desde el flujo de
  registro. Solo carecía de pruebas unitarias de su implementación HTTP, que se agregaron en este bloque.
- La validación del correo quedó **más estricta que la de `RequestPasswordResetCommandValidator`**, que
  usa `EmailAddress()` sin más. Aquí se rechazan además los espacios internos, porque el correo es la
  clave de partición del rate limit y un valor con espacios ensucia las cubetas. Unificar el criterio en
  todos los validadores queda como trabajo transversal pendiente.
- Todo intento que supere la validación escribe `verification_email_resend_request` en `audit_logs`, con
  el correo enmascarado y sin actor. Dado que la respuesta es uniforme, la bitácora es el único rastro
  del intento.

### Referencias

- `src/Cauce.Api/Controllers/AuthController.cs`
- `src/Cauce.Application/Identity/UseCases/ResendVerificationEmail/`
- `src/Cauce.Api/Configuration/RateLimitingPolicies.cs`
- Acta [A44](#acta-a44-partición-del-rate-limit-por-el-correo-del-cuerpo-de-la-petición), partición del rate limit por el cuerpo.
- `docs/api/CONTRACT-IDENTITY-v1.md` v1.2, §2.10.

---

## Acta A41: Endpoint de canje de código de invitación post-registro

**Estado:** Aprobada, RESUELTA en Backend-Fix-2 (tag `v0.8.0-backend-fix-2`)
**Fecha:** 2026-09-08
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, endpoint autenticado nuevo más refactor de dos handlers existentes.

---

### Contexto

Un paciente que se registraba sin código de invitación no tenía forma de vincularse a un nutricionista
después. El código solo se aceptaba en `POST /auth/register`, así que el vínculo únicamente podía nacer
durante el registro inicial. Era un callejón sin salida operativo: la cuenta quedaba sin nutricionista y
sin ningún camino de recuperación dentro de la aplicación.

`MATRIZ-IDENTIDAD.md` lo registraba como **US20-CA02 en estado PARCIAL**, con la evidencia literal
«Canje posterior: NO EXISTE».

### Decisión

Nuevo endpoint `POST /api/v1/patients/me/nutritionist-assignment`, autenticado con `Policy=Patient`,
precedido de un refactor que extrae la lógica de vinculación a un servicio dedicado.

### Decisiones complementarias

- **D5.** Paciente ya asignado → 409 `patient_already_assigned`. Sin sobrescritura: cambiar de
  nutricionista es una decisión clínica, no el efecto de que el paciente pegue otro código.
- **D6 reemplazada por A43.** La premisa de D6 era falsa: la notificación al nutricionista ya existía y
  el handler de registro ya la emitía. Se reusa el evento de dominio existente, con el texto
  parametrizado por contexto, en vez de agendar una notificación nueva que habría producido dos correos
  por un solo canje.
- **D7.** Auditoría del canje en `audit_logs`.
- **D8.** Refactor primero, endpoint después.
- **D11.** Nutricionista no disponible → 409, y **el código no se consume**.
- **R10.** Validación exhaustiva, con las mismas reglas de formato que aplica el registro.

### La vinculación estaba partida en dos handlers

El hallazgo que reescribió el alcance del refactor. La lógica no vivía toda en
`RegisterPatientCommandHandler`, como se suponía:

| Paso | Dónde ocurría |
| --- | --- |
| Consumir el código y publicar el evento de vinculación | `RegisterPatientCommandHandler` |
| **Crear la fila de `nutritionist_patient`** | `CreatePatientProfileCommandHandler` |

El registro **no creaba el vínculo real**: esa fila nacía recién al crear el perfil clínico. Por eso
`IPatientNutritionistAssignmentService` expone **dos operaciones** y no una. Fundirlas habría obligado al
registro a crear la asignación antes de que existiera el perfil, un cambio de comportamiento observable.

De haberse implementado el endpoint con una sola operación, el canje habría marcado el código como
consumido **sin asignar realmente al paciente**.

### Nueva validación (D11)

El nutricionista dueño del código debe estar en `UserStatus.Active` al momento del canje. Cualquier otro
estado falla con **409 `nutritionist_not_available`** y **el código no se consume**, de modo que pueda
reemitirse o reutilizarse.

Se usa un único `errorCode` para el escenario, con el estado exacto en la extensión `reason` del
envelope: `pending_activation`, `inactive` o `suspended`. Para el cliente es un solo caso arquitectónico,
«el nutricionista no puede atender», y el detalle le permite afinar el mensaje o ignorarlo.

**La protección contra `UserStatus.PendingActivation` implementada en este endpoint quedará
operativamente activa cuando se ejecute el bloque Nutritionist-Activation-1 (ver acta
[A47](#acta-a47-deuda-diferida--sin-ciclo-de-vida-de-cuentas-de-nutricionista)).** Hoy ninguna transición de estado de cuentas de
nutricionista tiene invocación en la aplicación, así que la comprobación es defensiva y está probada,
pero no alcanzable por vía de la aplicación.

### Consecuencias

- Refactor de dos handlers con comportamiento externo idéntico, verificado porque las pruebas de
  integración de registro y de creación de perfil pasaron sin modificarse.
- Servicio nuevo `IPatientNutritionistAssignmentService`, con dos sobrecargas de establecimiento: una
  que resuelve el código por paciente y otra que lo recibe ya resuelto, necesaria porque en el canje el
  `MarkAsUsed` todavía no está persistido cuando hay que crear el vínculo.
- Dos `errorCode` nuevos: `patient_already_assigned` (409) y `nutritionist_not_available` (409), este
  último con la extensión `reason`.
- La auditoría del canje se registra **sobre `invitation_codes`**, no sobre `nutritionist_patient`: esa
  tabla ya tiene trigger de auditoría (DEC-B5-03) y duplicarla habría violado la regla de no duplicación
  del acta A8. Se auditan tanto el canje efectivo como el rechazado, con el estado exacto del
  nutricionista en el contexto.
- El refactor obligó a migrar pruebas existentes, bajo el marco del acta A45.

### Referencias

- `src/Cauce.Api/Controllers/PatientsController.cs`
- `src/Cauce.Application/Patients/UseCases/AssignNutritionist/`
- `src/Cauce.Application/Patients/Services/PatientNutritionistAssignmentService.cs`
- `src/Cauce.Application/Common/Interfaces/Patients/IPatientNutritionistAssignmentService.cs`
- Acta [A43](#acta-a43-reemplazo-de-d6-uso-del-evento-de-dominio-existente-para-notificar-al-nutricionista-con-parametrización-del-texto-por-contexto), que reemplaza a D6.
- Acta [A45](#acta-a45-migración-de-pruebas-en-un-refactor-por-inyección-de-constructor), migración de pruebas en el refactor.
- Acta [A47](#acta-a47-deuda-diferida--sin-ciclo-de-vida-de-cuentas-de-nutricionista), ciclo de vida de cuentas de nutricionista.
- `docs/api/CONTRACT-IDENTITY-v1.md` v1.2, §2.11.

---

## Acta A43: Reemplazo de D6, uso del evento de dominio existente para notificar al nutricionista, con parametrización del texto por contexto

**Estado:** Aprobada, aplicada en Backend-Fix-2 Fase 3
**Fecha:** 2026-09-06
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, notificación al nutricionista ante la vinculación de un paciente. No cambia el contrato de API.

---

### Contexto

La decisión **D6** del bloque Backend-Fix-2 indicaba usar «el `INotificationService` existente» para
avisar al nutricionista del canje post-registro, y encargaba a la Fase 0 verificar qué canales soporta,
asumiendo la existencia de un canal in-app.

La verificación de Fase 0 encontró que **la premisa era incorrecta en tres puntos**:

| D6 asumía | El código tiene |
| --- | --- |
| Un servicio `INotificationService` | `INotificationScheduler`, `INotificationSender` e `INotificationRepository` |
| Un canal in-app | `NotificationChannel` solo declara `Push` y `Email` |
| Que la notificación había que construirla | **Ya está construida** |

`PatientLinkedToNutritionistEventHandler` escucha `PatientLinkedToNutritionistEvent` y agenda un correo
al nutricionista (`NotificationType.Alert`, `NotificationChannel.Email`), con idempotencia garantizada
por `ExistsForRelatedAsync` usando el identificador del código de invitación como clave. Es el
cumplimiento de US20 CA01, entregado en el Bloque 6.

#### El riesgo concreto que se evitó

El prompt contenía además una contradicción interna en la Fase 3. Su línea 751 dice que «si el handler
actual NO envía notificación al nutricionista, el servicio extraído TAMPOCO la envía», y la 752 manda
agregarla en la Fase 4. La premisa es falsa: `RegisterPatientCommandHandler` **sí** notifica, publicando
el evento por outbox dentro del mismo bloque que la Fase 3 extrae.

De haberse seguido al pie de la letra, el servicio extraído se habría llevado la publicación del evento
**y** la Fase 4 habría agendado una notificación propia: **dos correos al nutricionista por un solo
canje**.

### Decisión

**D6 queda reemplazada por esta acta.**

1. El servicio extraído en la Fase 3 **conserva la emisión** de `PatientLinkedToNutritionistEvent`. El
   endpoint de canje post-registro hereda la notificación por outbox, sin escribir una sola línea de
   código de notificaciones.
2. Se agrega el parámetro `Context`, de tipo `LinkContext`, al evento, para que el handler redacte el
   texto correcto en cada flujo.

`LinkContext` tiene dos valores: `RegistrationLink` y `PostRegistrationLink`.

#### Mitigación del riesgo del parámetro nuevo

`Context` es el quinto parámetro posicional del `record` y **tiene `RegistrationLink` como valor por
defecto**. Eso preserva tres cosas a la vez:

- El flujo de registro se comporta igual: `RegisterPatientCommandHandler` omite el parámetro.
- Toda construcción de cuatro argumentos sigue compilando, incluida la de las pruebas existentes.
- Los eventos ya persistidos en el outbox deserializan con el valor por defecto, sin migración.

El texto de `RegistrationLink` es **idéntico carácter por carácter** al que había. El de
`PostRegistrationLink` distingue que el paciente ya tenía cuenta y canjeó el código después, porque
decirle a un nutricionista que el paciente «se registró con tu código» cuando en realidad se registró
sin código sería inexacto, y esos correos los leen personas durante el piloto.

### Consecuencias

- Cero código nuevo de notificaciones. Un solo correo por canje.
- Idempotencia ya resuelta por la clave del código de invitación, que es única por canje y sirve igual
  a los dos flujos.
- Consistencia: registro y canje producen el mismo hecho de dominio, tratado por el mismo handler.
- No se necesita decidir ningún canal de reemplazo: el proyecto ya había decidido Email para esta
  notificación en US20 CA01.
- La Fase 4 pierde su tarea 8 (notificación explícita) y conserva la 7 (auditoría
  `NutritionistAssignment`), que sí es específica del canje.

### Referencias

- `src/Cauce.Domain/Identity/Enums/LinkContext.cs`
- `src/Cauce.Domain/Identity/Events/PatientLinkedToNutritionistEvent.cs`
- `src/Cauce.Application/Identity/EventHandlers/PatientLinkedToNutritionistEventHandler.cs`
- `src/Cauce.Application/Patients/Services/PatientNutritionistAssignmentService.cs`
- Acta A41, endpoint de canje de código de invitación post-registro.
- Decisión D6 del prompt Backend-Fix-2, reemplazada por esta acta.

---

## Acta A44: Partición del rate limit por el correo del cuerpo de la petición

**Estado:** Aprobada, aplicada en Backend-Fix-2 Fase 2
**Fecha:** 2026-09-06
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, pipeline HTTP y políticas de limitación de tasa. No cambia el contrato de API.

---

### Contexto

La decisión D3 del bloque Backend-Fix-2 fija que el endpoint `POST /api/v1/auth/verification-email/resend`
se limite a **3 peticiones por hora por correo normalizado**, no por IP. La elección es deliberada:

- Particionar por IP dejaría que un usuario legítimo detrás de una NAT compartida, como la red de un
  hospital, agotara el cupo de todos sus vecinos.
- Particionar por IP tampoco frenaría a quien hostiga un mismo buzón rotando direcciones de origen.

El obstáculo es técnico. Las siete políticas existentes derivan su clave de partición de datos que
`HttpContext` ya tiene resueltos de forma síncrona: `AddIpFixedWindow` lee
`Connection.RemoteIpAddress`, y `AddUserFixedWindow` lee el claim `sub`. El correo, en cambio, viaja en
el **cuerpo** de la petición, y ahí aparecen dos restricciones que se combinan mal:

1. Las fábricas de `RateLimitPartition` que acepta `RateLimiterOptions.AddPolicy` son **síncronas**. No
   hay sobrecarga asíncrona, ni en `IRateLimiterPolicy<TKey>`.
2. El cuerpo de una petición es un stream de **una sola lectura**. Leerlo desde la política exigiría
   I/O síncrona, que Kestrel rechaza por defecto (`AllowSynchronousIO = false`) y que además es un
   antipatrón conocido por el riesgo de agotar el thread pool.

### Decisión

Se agrega un middleware, `VerificationResendPartitionMiddleware`, registrado **antes** de
`app.UseRateLimiter()`. Solo actúa sobre `POST /auth/verification-email/resend`. En esa ruta:

1. Llama a `Request.EnableBuffering()`.
2. Lee el cuerpo de forma asíncrona y extrae la propiedad `email`.
3. La normaliza con `Trim()` y `ToLowerInvariant()`.
4. La deja en `HttpContext.Items` bajo la clave `cauce.verify-resend-email`.
5. Rebobina el stream para que el model binding del controlador lo consuma intacto.

La política `auth-verify-resend` lee esa entrada de `HttpContext.Items` de forma síncrona, que es todo
lo que su fábrica necesita.

**No es un patrón nuevo en este repo.** `AuditingMiddleware` ya bufferiza el cuerpo para leer el correo
del login (`AuditingMiddleware.cs:52` y `TryReadEmailAsync`, líneas 156-176). La única diferencia es la
posición en el pipeline: el de auditoría corre después de `UseAuthorization`, y este tiene que correr
antes del limitador.

### Fallback ante un cuerpo inutilizable

Si el cuerpo no es JSON válido, no es un objeto, o no trae `email` como cadena no vacía, la política
particiona por **la IP de origen**, con el prefijo `ip:` para que no colisione con un correo.

La alternativa evidente, una clave compartida tipo `"unknown"`, se descartó: crearía una cubeta única
que cualquiera podría agotar enviando tres cuerpos malformados, dejando sin servicio a todos los demás.
Caer a la IP acota el daño a quien lo provoca.

### Consecuencias

- Es la octava política de limitación de tasa del sistema, y la primera que particiona por contenido
  del cuerpo. Las otras siete no se tocan.
- El middleware bufferiza el cuerpo **solo** de esa ruta. Cualquier otra petición atraviesa el pipeline
  sin que se lea su cuerpo.
- La normalización se aplica en dos lugares que deben coincidir: el middleware, para la partición, y
  `AuthController.ResendVerificationEmail`, para el comando. Si divergen, una variación de mayúsculas
  eludiría el límite. Hay pruebas de integración que fijan ambas.
- Si en el futuro otro endpoint necesita particionar por cuerpo, conviene generalizar el middleware a
  una lista de rutas y nombres de propiedad, en vez de duplicarlo.

### Alternativas descartadas

| Alternativa | Motivo del descarte |
| --- | --- |
| Leer el cuerpo dentro de la política con I/O síncrona | Exige `AllowSynchronousIO = true` en Kestrel, con riesgo de agotar el thread pool |
| Particionar por IP y validar el límite por correo dentro del handler | El 429 dejaría de emitirlo el limitador, así que habría que replicar el `OnRejected` con su extensión `retryAfterSeconds` |
| Mover el correo a un header para que la política lo lea | Cambia el contrato del endpoint y expone en un header un dato que ya viaja en el cuerpo |

### Referencias

- `src/Cauce.Api/Middleware/VerificationResendPartitionMiddleware.cs`
- `src/Cauce.Api/Configuration/RateLimitingPolicies.cs` (`AuthVerifyResend`, `AddEmailFixedWindow`)
- `src/Cauce.Api/Program.cs` (orden del pipeline)
- `src/Cauce.Api/Middleware/AuditingMiddleware.cs` (patrón de bufferizado preexistente)
- Acta A40, endpoint de reenvío de verificación.

---

## Acta A45: Migración de pruebas en un refactor por inyección de constructor

**Estado:** Aprobada, aplicada en Backend-Fix-2 Fase 3
**Fecha:** 2026-09-06
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Suite de pruebas del backend. No cambia código de producción ni el contrato de API.

---

### Contexto

El bloque Backend-Fix-2 fija dos reglas que, en un refactor por inyección de constructor, entran en
conflicto directo:

| Regla | Qué exige |
| --- | --- |
| **D10** | Zero modificación de tests existentes |
| **R5** | Zero regresiones: los tests existentes deben seguir pasando |

La Fase 3 extrae la lógica de vinculación paciente-nutricionista a
`IPatientNutritionistAssignmentService`, que se inyecta por constructor en
`RegisterPatientCommandHandler` y en `CreatePatientProfileCommandHandler`. Cambiar la firma de un
constructor rompe la compilación de toda prueba que instancie esa clase a mano, que es exactamente lo
que hacen sus pruebas unitarias.

**Aplicar D10 al pie de la letra hace imposible cumplir R5.** Un proyecto de pruebas que no compila no
tiene tests que pasen. No hay forma de extraer una dependencia por constructor sin tocar el punto donde
se construye el objeto: no es un descuido evitable con más cuidado, es una propiedad del refactor.

La alternativa de no extraer nada tampoco sirve: el hallazgo que reescribió la Fase 3 fue que la
vinculación está partida entre dos handlers, y que el vínculo real (`nutritionist_patient`) se crea en
`CreatePatientProfileCommandHandler`, no en el registro. Sin extraer esa mitad, el endpoint de canje
post-registro de la Fase 4 marcaría el código como usado sin asignar realmente al paciente.

### Decisión

Se admite una excepción acotada a D10, **para preservar R5**, con dos categorías de cambio claramente
separadas y una condición distinta para cada una.

#### Categoría 1: swap mecánico

Cambiar el tipo de un doble de prueba y el argumento que recibe el constructor. Nada más.

**Condición:** ninguna aserción, ningún stub y ningún escenario se modifican. Si durante un swap
aparece la necesidad de tocar una aserción, deja de ser mecánico y se detiene la ejecución para
consultarlo.

#### Categoría 2: reexpresión al nivel correcto

Una prueba que asertaba sobre una dependencia que se mudó al servicio deja de poder observarla desde el
handler. La prueba del handler pasa a verificar **la delegación y el resultado observable**, y las
aserciones originales se mudan a la prueba del servicio.

**Condición:** las aserciones migradas van **verbatim**. Los mismos stubs, las mismas verificaciones,
sobre los mismos repositorios. Lo único que cambia es el sujeto de la prueba.

### Distinción que hace legítima la excepción

Esta decisión **no** habilita modificar pruebas para que dejen de incomodar. La diferencia es
verificable:

| Migración legítima (esta acta) | Modificación indebida |
| --- | --- |
| El comportamiento de producción no cambia | El comportamiento cambia y la prueba se ajusta para tolerarlo |
| Los escenarios de negocio siguen siendo los mismos | Se eliminan o debilitan escenarios |
| Las aserciones se conservan, cambia dónde viven | Las aserciones se relajan o se borran |
| La cobertura se mantiene o sube | La cobertura baja |

En la Fase 3 no hay ningún bug que ocultar: es una extracción sin cambios de comportamiento
observable, verificable con las pruebas de integración de registro y de creación de perfil, que pasan
sin tocarse porque resuelven por inyección de dependencias y no construyen los handlers a mano.

### Cómo se preserva la cobertura

Las dos pruebas reexpresadas cubrían la creación de la asignación. Tras el refactor:

- La prueba del **handler** verifica que delega y que el resultado (`NutritionistAssigned`,
  `NutritionistAssignmentId`) refleja lo que el servicio devolvió. Es lo que el handler controla.
- La prueba del **servicio** hereda las aserciones de repositorio verbatim, y suma los casos que el
  handler nunca cubrió, como la asignación activa preexistente.

La cobertura total sube: los mismos escenarios quedan cubiertos, y se agregan los que antes no lo
estaban.

### Alcance de la aplicación en Fase 3

| Archivo | Categoría | Detalle |
| --- | --- | --- |
| `tests/Cauce.Application.Tests/Identity/RegisterPatientCommandHandlerTests.cs` | Mecánico | `IOutboxWriter` pasa a `IPatientNutritionistAssignmentService` en la declaración del doble y en el argumento 6. Sus 4 pruebas conservan escenarios y aserciones. El doble de outbox nunca fue asertado |
| `tests/Cauce.Application.Tests/Patients/CreatePatientProfileCommandHandlerTests.cs` | Mecánico | `Handle_ProfileAlreadyExists_ThrowsDuplicate` y `Handle_NonPatientRole_ThrowsUnauthorized`: solo el constructor |
| `tests/Cauce.Application.Tests/Patients/CreatePatientProfileCommandHandlerTests.cs` | Reexpresión | `Handle_ValidRequestWithoutInvitation_CreatesProfileWithoutAssignment` y `Handle_PatientUsedInvitation_CreatesNutritionistAssignment` |
| `tests/Cauce.Application.Tests/Patients/Services/PatientNutritionistAssignmentServiceTests.cs` | Destino | Recibe verbatim las aserciones de repositorio migradas |

### Consecuencias

- Se establece un precedente acotado: futuros refactors por constructor pueden migrar pruebas bajo
  estas dos categorías, con sus condiciones, sin renegociar D10 cada vez.
- Todo refactor que caiga en la categoría 2 debe declarar en su reporte de fase qué pruebas se
  reexpresaron y a dónde fueron sus aserciones.
- D10 sigue vigente para todo lo demás. Esta excepción no cubre cambios de comportamiento.

### Referencias

- `src/Cauce.Application/Common/Interfaces/Patients/IPatientNutritionistAssignmentService.cs`
- `src/Cauce.Application/Patients/Services/PatientNutritionistAssignmentService.cs`
- Acta A41, endpoint de canje de código de invitación post-registro.
- Acta A43, notificación al nutricionista mediante el evento de dominio existente.
- Reglas D10 y R5 del prompt Backend-Fix-2.

---

## Acta A46: Swashbuckle CLI como herramienta local para regenerar el snapshot OpenAPI

**Estado:** Aprobada, aplicada en Backend-Fix-2 Fase 5
**Fecha:** 2026-09-07
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Herramientas de desarrollo del repo `backend`. No toca código de producción ni el contrato de API.

---

### Contexto

La Fase 5 del bloque Backend-Fix-2 debe regenerar el snapshot OpenAPI como `openapi-v1.1.0.json`, con
los dos endpoints nuevos del bloque. Al buscar cómo se genera, **no se encontró ningún mecanismo
versionado en el repositorio**:

| Se buscó | Resultado |
| --- | --- |
| Script en `tools/` | Solo `generate_dummy_onnx.py`, ajeno al tema |
| Manifiesto de herramientas locales | `.config/dotnet-tools.json` no existía |
| Argumento CLI en `Program.cs` | No hay ninguno que emita el documento |
| Paquete de generación | Solo `Swashbuckle.AspNetCore` 7.2.*, sin su CLI |

El snapshot vigente se generó, presumiblemente, levantando la API y descargando
`/swagger/v1/swagger.json`, que solo se expone en Development. Ese camino tiene tres costos:

- Depende de que los tres user-secrets del perfil local estén configurados (actas A33, A35, A37).
- Arrancar en Development ejecuta los seeders de desarrollo y los siete workers contra la base local.
- No deja nada versionado: la próxima regeneración vuelve a depender de que alguien recuerde el
  procedimiento.

Este bloque es la segunda vez que el proyecto necesita regenerar el snapshot, y no será la última:
cada endpoint nuevo lo desactualiza.

### Decisión

Adoptar **`Swashbuckle.AspNetCore.Cli` como dotnet tool local**, declarada en
`.config/dotnet-tools.json`.

La regeneración pasa a ser:

```bash
dotnet tool restore
dotnet build src/Cauce.Api
ASPNETCORE_ENVIRONMENT=Development dotnet swagger tofile \
  --output docs/api/openapi-v1.1.0.json \
  src/Cauce.Api/bin/Debug/net9.0/Cauce.Api.dll v1
```

La herramienta carga el ensamblado compilado y le pide el documento al generador, sin abrir un puerto y
sin tocar la base de datos.

**`ASPNETCORE_ENVIRONMENT=Development` no es opcional.** Sin esa variable la herramienta corre en
Production, donde no se cargan los user-secrets; la construcción del host falla por configuración
ausente y `HostFactoryResolver` cae a la ruta heredada de `Startup`, produciendo un error engañoso:

```
System.InvalidOperationException: A type named 'StartupProduction' or 'Startup'
could not be found in assembly 'Cauce.Api'.
```

El mensaje no menciona la configuración faltante, así que conviene tenerlo registrado: la aplicación usa
instrucciones de nivel superior y no tiene ni debe tener una clase `Startup`.

**La herramienta se detiene en `builder.Build()`.** No ejecuta lo que viene después en `Program.cs`, de
modo que ni los seeders de desarrollo ni los siete workers llegan a arrancar.

### Consecuencias

- **Dependencia nueva**, declarada y con versión fija en el manifiesto, alineada con la de
  `Swashbuckle.AspNetCore` que ya usa el proyecto. Es reproducible: `dotnet tool restore` la instala
  igual en cualquier máquina.
- **Automatizable.** Deployment-1 la necesitará para publicar la documentación de la API sin
  intervención manual, y sirve tal cual en CI para verificar que el snapshot commiteado coincide con el
  código.
- **No se toca `Program.cs`.** El pipeline de la aplicación queda igual, y Swagger sigue expuesto solo
  en Development.
- El procedimiento queda documentado en `CLAUDE.md` cuando la Fase 6 lo actualice a v2.2.0.

### Alternativas descartadas

| Alternativa | Motivo del descarte |
| --- | --- |
| Levantar la API y descargar el spec | Depende del entorno local, dispara seeders y workers contra la base de desarrollo, y no deja nada versionado |
| Agregar un argumento CLI a `Program.cs` que emita el documento | Mezcla una preocupación de herramientas con el arranque de la aplicación, y hay que mantenerlo a mano |
| Diferir el snapshot | La Fase Final declara `openapi-v1.1.0.json` como criterio de cierre del bloque |

### Referencias

- `.config/dotnet-tools.json`
- `docs/api/openapi-v1.1.0.json`
- Fases 5 y Final del prompt Backend-Fix-2.
- Acta A44, que registra la otra decisión de infraestructura del bloque.

---

## Acta A47: Deuda diferida — sin ciclo de vida de cuentas de nutricionista

**Estado:** Aprobada. **Activación RESUELTA en Nutritionist-Activation-1** (actas A51 a A53); suspensión,
reactivación y baja siguen diferidas (actas A55 y A56)
**Fecha:** 2026-09-08
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, provisión y estados de las cuentas de nutricionista.

---

### Contexto

El hallazgo surgió al implementar la validación D11 del acta A41, que exige que el nutricionista dueño de
un código de invitación esté en `UserStatus.Active` para que el canje proceda. Al escribir las pruebas de
los estados de rechazo apareció el problema: **no hay forma de llevar una cuenta de nutricionista a
ninguno de esos estados desde la aplicación.**

La verificación sobre `src/` es contundente:

| Método de dominio | Efecto | Llamadores en `src/` |
| --- | --- | --- |
| `User.Activate()` | `PendingActivation → Active` | **0** |
| `User.Suspend()` | `→ Suspended` | **0** |
| `User.Reactivate()` | `Suspended`/`Inactive → Active` | **0** |

Los tres métodos existen en `src/Cauce.Domain/Identity/User.cs`, están probados en el dominio, y **ninguno
tiene invocación en la aplicación**.

Además, `CreateNutritionistCommandHandler.cs:74` provisiona la cuenta con `User.CreateNutritionist(...)`,
que la crea directamente en `UserStatus.Active` (`User.cs:139`). El nutricionista recibe credenciales
temporales por correo y Keycloak le exige cambiar la contraseña en el primer acceso, pero **la cuenta
local nace activa**, antes de que esa persona haya hecho nada.

`UserStatus.Inactive` solo se alcanza mediante `User.Anonymize(...)`, que lanza excepción si la cuenta no
es de paciente. Para un nutricionista es inalcanzable por completo.

### Decisión

**No resolver en Backend-Fix-2.** Se documenta como deuda y se asigna al bloque
**Nutritionist-Activation-1**, que le da alcance propio.

La razón de diferirlo es de alcance, no de dificultad: Backend-Fix-2 cierra tres deudas acotadas de
identidad, y construir un ciclo de vida de cuentas implica decisiones de producto que exceden ese marco
(qué activa una cuenta, quién puede suspenderla, si hay endpoint administrativo, si el portal web lo
expone).

La comprobación de D11 **se implementa igual**, con sus pruebas. Queda correcta y esperando.

### Consecuencias mientras la deuda siga abierta

- Las tres ramas de `nutritionist_not_available` son **inalcanzables por vía de la aplicación**. La
  protección es defensiva: cubre manipulación directa de la base y cualquier flujo futuro, pero hoy no
  se dispara sola.
- Un nutricionista provisionado queda operativo de inmediato, sin paso de activación. Para el piloto es
  aceptable porque el alta la ejecuta el equipo con la clave de API de administración, no es un
  autoservicio.
- **No hay forma de dar de baja ni de suspender a un nutricionista** desde la aplicación. Si alguno deja
  el piloto, sus códigos de invitación siguen siendo canjeables y sus pacientes siguen asignados.
- La rama `pending_activation` del `reason` documentada en el contrato v1.2 §2.11 describe un estado que
  el sistema todavía no produce.

### Alcance sugerido para Nutritionist-Activation-1

Sin comprometer el diseño, que es trabajo de ese bloque:

1. Provisionar la cuenta en `PendingActivation` y activarla cuando el nutricionista complete su primer
   acceso con cambio de contraseña.
2. Exponer suspensión y reactivación, decidiendo si por endpoint administrativo con clave de API, como
   `POST /admin/nutritionists`, o desde el portal web.
3. Definir qué ocurre con los códigos vigentes y las asignaciones activas de un nutricionista suspendido.
4. Verificar que las tres ramas de `nutritionist_not_available` quedan alcanzables end to end.

### Resolución parcial en Nutritionist-Activation-1

| Punto del alcance sugerido | Estado |
| --- | --- |
| 1. Provisionar en `PendingActivation` y activar en el primer acceso | **Resuelto.** Actas [A51](#acta-a51-mecanismo-de-activación-de-cuentas-de-nutricionista) y [A52](#acta-a52-enlace-de-keycloak-para-que-el-nutricionista-defina-su-contraseña-y-su-reenvío) |
| 2. Exponer suspensión y reactivación | **Diferido.** `Suspend` y `Reactivate` siguen sin llamador (acta [A55](#acta-a55-deuda-diferida--la-suspensión-no-corta-sesiones-ni-tokens-ya-emitidos)) |
| 3. Códigos vigentes y asignaciones de un suspendido | **Parcial.** El canje y la generación lo rechazan (acta [A53](#acta-a53-estado-del-nutricionista-al-registrarse-con-un-código-y-al-generarlo)); no hay revocación (acta [A56](#acta-a56-deuda-diferida--los-códigos-de-invitación-no-se-revocan-al-suspender)) |
| 4. Las tres ramas de `nutritionist_not_available` alcanzables | `pending_activation` queda como defensa, porque A51 impide que un pendiente tenga códigos. `suspended` e `inactive` serán alcanzables cuando exista el flujo de suspensión |

### Referencias

- `src/Cauce.Domain/Identity/User.cs` (`Activate`, `Suspend`, `Reactivate`, `CreateNutritionist`).
- `src/Cauce.Application/Identity/UseCases/CreateNutritionist/CreateNutritionistCommandHandler.cs`
- `src/Cauce.Application/Patients/UseCases/AssignNutritionist/AssignNutritionistCommandHandler.cs`
- Acta [A41](#acta-a41-endpoint-de-canje-de-código-de-invitación-post-registro), que introduce la validación D11.
- `docs/api/CONTRACT-IDENTITY-v1.md` v1.2, §2.11.

---

## Acta A48: Asimetría en la persistencia de la auditoría de intentos anónimos

**Estado:** Documentada, resolución postergada a bloque futuro
**Fecha:** 2026-09-09
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, endpoints anónimos de identidad con respuesta uniforme.

---

### Contexto

Descubierta durante el smoke de cierre de Backend-Fix-2, ejercitando el runtime real contra Postgres.

El proyecto tiene dos endpoints anónimos hermanos que comparten el mismo diseño de seguridad: responden
**200 exista o no la cuenta**, para no convertirse en un oráculo de correos registrados. En ambos, la
respuesta es deliberadamente muda, así que **la bitácora es el único rastro del intento**.

| Endpoint | Acta que lo define | ¿Persiste la auditoría si la cuenta no existe? |
| --- | --- | --- |
| `POST /auth/verification-email/resend` | [A40](#acta-a40-endpoint-de-reenvío-del-correo-de-verificación) | **Sí** |
| `POST /auth/password-reset/request` | A8 (Bloque 5) | **No** |

Los dos enrolan su fila mediante `IAuditableCommand` y el `AuditingBehavior`, que llama a
`IAuditLogger.LogAsync` **antes** del handler y deja la escritura en el `ChangeTracker`. Quien la
confirma es el `SaveChangesAsync` del handler. De ahí sale la diferencia.

### Evidencia

Dos llamadas consecutivas a cada endpoint, una con un correo registrado y otra con uno inexistente, sobre
la base de desarrollo:

```
POST /auth/verification-email/resend  {"email":"paciente.demo@cauce.local"}   -> 200
POST /auth/verification-email/resend  {"email":"no.existe.jamas@cauce.local"} -> 200
   audit_logs: 2 filas verification_email_resend_request

POST /auth/password-reset/request     {"email":"no.existe.jamas@cauce.local"} -> 200
POST /auth/password-reset/request     {"email":"paciente.demo@cauce.local"}   -> 200
   audit_logs: 1 fila password_reset_request
```

### Causa

`ResendVerificationEmailCommandHandler` llama a `SaveChangesAsync` **incondicionalmente**, con el
comentario que lo justifica: «este handler confirma la transacción aunque no tenga cambios de negocio
propios que persistir».

`RequestPasswordResetCommandHandler` tiene un `return` temprano cuando la cuenta no existe, que sale
**antes** de su `SaveChangesAsync`. La fila enrolada se descarta al cerrar el scope.

**No es un defecto accidental.** El propio código lo declara:

> `// La auditoría (PasswordResetRequest / users, sin trigger) la enrola el AuditingBehavior antes`
> `// de este handler; solo se persiste cuando la cuenta existe y se llama a SaveChanges (acta A8).`

Es decir, hay dos decisiones documentadas que apuntan en sentidos opuestos: A40 resolvió auditar el
intento sobre un correo inexistente, A8 resolvió no hacerlo. **Lo que no está justificado es la
asimetría**, no cada decisión por separado.

### Lo que esta acta NO afirma

Para que un lector futuro no rederive una versión más fuerte de la que la evidencia sostiene:

- **No hay defecto en el reenvío de verificación.** Su auditoría persiste, incluida la del correo
  inexistente. Verificado en código y en runtime.
- **No existe `IAuditLogger.EnqueueAudit`.** El único método de la interfaz es `LogAsync`.
- **No está establecido que haya incumplimiento de la Ley N° 29733.** Ver abajo.

### La pregunta de compliance, planteada

Si un intento de restablecimiento sobre un correo **que no corresponde a ningún titular registrado**
constituye tratamiento de dato personal que deba quedar registrado, es una pregunta jurídica, no
técnica. Hay argumentos en los dos sentidos: la dirección de correo es dato personal aunque no haya
cuenta, pero también puede sostenerse que sin titular en el sistema no hay tratamiento que auditar.

Lo que sí es técnicamente indiscutible es el **valor forense**: sin esa fila no hay forma de detectar
una enumeración de correos contra el endpoint de reset, mientras que contra el de reenvío sí la hay.

Conviene resolverlo **antes de Deployment-1**, con criterio del área legal de la tesis, y unificar los
dos endpoints en el sentido que se decida.

### Nota sobre la cobertura de pruebas

Las pruebas unitarias de ambos handlers usan un doble de `IAuditLogger`, así que verifican que la
auditoría **se solicita**, no que **se persista**. La diferencia solo aparece contra una base real. Si
el bloque que resuelva esto agrega pruebas, deben ser de integración.

### Bloque de resolución

Por definir. Recomendado antes de Deployment-1 por la implicancia de trazabilidad.

### Referencias

- `src/Cauce.Application/Identity/UseCases/RequestPasswordReset/RequestPasswordResetCommandHandler.cs`
- `src/Cauce.Application/Identity/UseCases/ResendVerificationEmail/ResendVerificationEmailCommandHandler.cs`
- `src/Cauce.Application/Common/Behaviors/AuditingBehavior.cs`
- Acta [A40](#acta-a40-endpoint-de-reenvío-del-correo-de-verificación), que decidió auditar el intento uniforme.
- Acta A8 del Bloque 5, que fijó la auditoría en cuatro capas y la regla de no-duplicación.

---

## Acta A49: Respuesta del endpoint de provisión de nutricionistas

**Estado:** Aprobada, RESUELTA en Nutritionist-Activation-1
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, `POST /api/v1/admin/nutritionists` y endpoint nuevo `GET /api/v1/admin/nutritionists/{id}`.

---

### Contexto

La observación se registró en el smoke de cierre de Backend-Fix-2, pero sin archivo propio: solo figuraba
en el historial del `CLAUDE.md` local. Esta acta la redacta en el bloque que la resuelve.

La respuesta de la provisión no se ajustaba al contrato que el resto de la API sigue para una creación:

| Aspecto | Antes |
| --- | --- |
| Estado de la cuenta | Ausente |
| Header `Location` | Ausente |
| `temporaryCredentialsEmailSent` | Siempre `true`: un fallo del correo lanzaba excepción y compensaba, así que nunca se observaba `false` |
| 502 de Keycloak | Posible, pero no declarado en `[ProducesResponseType]` |

### Decisión

| Aspecto | Ahora |
| --- | --- |
| Estado de la cuenta | Campo `status`, que en una provisión siempre vale `PendingActivation` (acta A51) |
| Header `Location` | Apunta a `GET /api/v1/admin/nutritionists/{id}`, con la versión de la API tomada de la ruta de la petición |
| Flag del correo | Se llama `activationEmailSent` y refleja si Keycloak aceptó enviar el enlace (acta A52) |
| 502 de Keycloak | Declarado |

**Endpoint nuevo `GET /api/v1/admin/nutritionists/{id}`**, protegido con la clave de API. Devuelve `userId`,
`email`, `fullName` y `status`. Responde 404 `nutritionist_not_found` tanto para un identificador
inexistente como para uno de otro rol, para no revelar qué identificadores existen. Sirve de destino del
`Location` y para comprobar si un nutricionista ya activó su cuenta.

### Consecuencias

- **Cambio incompatible del contrato admin:** el campo `temporaryCredentialsEmailSent` pasó a llamarse
  `activationEmailSent`. Hoy ningún cliente lo consume, porque el equipo usa el endpoint a mano.
- El snapshot pasa a `openapi-v1.2.0.json` y `ENDPOINTS.md` a v1.3.0.

### Referencias

- `src/Cauce.Api/Controllers/AdminController.cs`
- `src/Cauce.Application/Identity/UseCases/CreateNutritionist/CreateNutritionistResult.cs`
- `src/Cauce.Application/Identity/UseCases/GetNutritionist/`
- Actas [A51](#acta-a51-mecanismo-de-activación-de-cuentas-de-nutricionista) y [A52](#acta-a52-enlace-de-keycloak-para-que-el-nutricionista-defina-su-contraseña-y-su-reenvío).

---

## Acta A51: Mecanismo de activación de cuentas de nutricionista

**Estado:** Aprobada, RESUELTA en Nutritionist-Activation-1
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, dominio `User`, `LoginCommandHandler` y pipeline de MediatR.

---

### Contexto

El acta [A47](#acta-a47-deuda-diferida--sin-ciclo-de-vida-de-cuentas-de-nutricionista) registró que el nutricionista nacía en
`UserStatus.Active` y que ninguna transición de estado tenía llamador en la aplicación. Por eso las ramas
de `nutritionist_not_available` eran inalcanzables.

El diseño inicial proponía activar al nutricionista solo dentro de `LoginCommandHandler`. En la Fase 0
apareció un problema: el cliente `cauce-web-portal` tiene Direct Access Grants apagado, lo que apunta a que
el portal usará Authorization Code + PKCE directo contra Keycloak, sin pasar por `POST /auth/login`. Con ese
diseño, ningún nutricionista del portal se habría activado nunca. La decisión se corrigió antes de
implementar.

### Decisión

1. **Estado inicial en el dominio.** `User.CreateNutritionist` crea la cuenta en `PendingActivation`, con
   el correo verificado. La regla vive en la fábrica, no solo en el handler: el dominio deja de afirmar que
   el nutricionista nace activo.
2. **Una sola regla.** `INutritionistActivationService.ActivateIfPendingAsync` activa únicamente a un
   nutricionista pendiente con el correo verificado. Audita la transición con `AuditActionType.AccountActivation`,
   con el propio nutricionista como actor y el punto de entrada en el contexto (`login` o
   `authenticated_request`). No confirma: eso le toca al llamador.
3. **Dos puntos de entrada.**
   - **Login.** `LoginCommandHandler` invoca la regla porque en esa petición la identidad recién aparece
     cuando Keycloak responde. La confirma su propio `SaveChanges`, en la misma transacción que
     `last_login_at`, igual que la sincronización del acta A39.
   - **Cualquier otra petición autenticada.** `NutritionistActivationBehavior` descarta por el rol del
     token sin consultar la base, y **confirma por su cuenta**, porque las consultas no llaman a
     `SaveChanges` y la activación se perdería. Va registrado entre `ValidationBehavior` y
     `AuditingBehavior`: si fuera después, su `SaveChanges` persistiría antes de tiempo la fila de intención
     de un comando cuyo handler todavía puede fallar (acta A8).

### Por qué autenticarse prueba la activación

Desde el acta [A52](#acta-a52-enlace-de-keycloak-para-que-el-nutricionista-defina-su-contraseña-y-su-reenvío), el nutricionista nace **sin contraseña**,
y Keycloak solo emite un token cuando las acciones requeridas están resueltas. Verificado contra Keycloak
25.0.6: con `UPDATE_PASSWORD` pendiente, Direct Grant responde 400 `Account is not fully set up`.

**Caso borde verificado:** si un administrador retira `UPDATE_PASSWORD` desde la consola sin cambiar la
contraseña, el login entra igual. No aplica a cuentas provisionadas, porque no tienen contraseña temporal
que destrabar. `DevAdminSeeder` sí usa una temporal fija, y por eso activa su cuenta explícitamente.

### Consecuencias

- Cada petición de un nutricionista suma una lectura de `users`. Es aceptable para n=20–50.
- Si las dos primeras peticiones llegan en paralelo, en el peor caso quedan dos filas de auditoría. El
  estado final es el mismo.
- `DevAdminSeeder` activa al nutricionista demo, porque `DemoPatientSeeder` solo le asigna el paciente demo
  a uno activo.
- Pruebas migradas según el acta A45: los helpers de siembra activan explícitamente y `UserTests` afirma
  `PendingActivation`.
- No hay migración: `audit_logs.action_type` es `varchar(50)` y no tiene CHECK.

### Referencias

- `src/Cauce.Domain/Identity/User.cs`
- `src/Cauce.Application/Identity/Services/NutritionistActivationService.cs`
- `src/Cauce.Application/Common/Behaviors/NutritionistActivationBehavior.cs`
- `src/Cauce.Application/Identity/UseCases/Login/LoginCommandHandler.cs`
- Actas [A47](#acta-a47-deuda-diferida--sin-ciclo-de-vida-de-cuentas-de-nutricionista), [A52](#acta-a52-enlace-de-keycloak-para-que-el-nutricionista-defina-su-contraseña-y-su-reenvío) y
  A39.

---

## Acta A52: Enlace de Keycloak para que el nutricionista defina su contraseña, y su reenvío

**Estado:** Aprobada, RESUELTA en Nutritionist-Activation-1
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, provisión de nutricionistas, cliente de la Admin API de Keycloak y endpoints admin.

---

### Contexto

La provisión generaba una contraseña temporal y la enviaba **en texto plano** por el SMTP del backend, con
un enlace a `PortalAppBaseUrl`, que en producción está vacío. Además había dos problemas:

- **El primer acceso era imposible.** Con `UPDATE_PASSWORD` pendiente, el login por Direct Grant responde
  401, así que un nutricionista real no podía completarlo.
- **Cuenta huérfana.** El `SaveChanges` ocurría antes del envío del correo. Si el correo fallaba, la
  compensación borraba el usuario de Keycloak, pero la fila local ya estaba confirmada. Los reintentos
  respondían 409 `duplicate_email`.

### Decisión

1. **Sin contraseña.** El usuario se crea en Keycloak sin credencial. Después de confirmar la cuenta local,
   se pide `execute-actions-email` con `UPDATE_PASSWORD` mediante
   `IKeycloakAdminClient.SendUpdatePasswordEmailAsync`. No se envía `lifespan`: rige
   `actionTokenGeneratedByAdminLifespan` del realm (43200 s), para que la vigencia se configure en un solo
   lugar.
2. **Envío que no tumba la provisión.** Si el pedido falla, la respuesta es 201 con
   `activationEmailSent: false` y no se compensa. **Esto resuelve la cuenta huérfana.**
3. **Reenvío.** `POST /api/v1/admin/nutritionists/{id}/activation-email`, protegido con la clave de API:

   | Caso | Respuesta |
   | --- | --- |
   | Keycloak aceptó el envío | 204, auditado como `ActivationEmailResend` después del envío |
   | Identificador inexistente o de otro rol | 404 `nutritionist_not_found` |
   | La cuenta ya no está pendiente | 409 `nutritionist_not_pending_activation` |
   | Keycloak falla | 502 `keycloak_integration_error` |

   A diferencia de la provisión, aquí el fallo se propaga, porque el operador pidió el reenvío y necesita
   saber si el correo salió.
4. **Retiro** de `ITemporaryPasswordGenerator`, `IEmailSender.SendNutritionistTemporaryCredentialsAsync` y
   sus plantillas.

### Verificación contra el realm real

Verificado con Keycloak 25.0.6 antes de implementar:

- `execute-actions-email` responde 204 y el correo llega en español: asunto "Actualiza tu cuenta", con el
  aviso "expirará en 720 minutos".
- El token trae `typ=execute-actions`, `rqac=UPDATE_PASSWORD` y `exp − iat = 43200 s`.
- La acción viaja en el token, no en el usuario: `requiredActions` sigue en `[]`.
- El enlace abre una página de confirmación antes del formulario, así que un escáner de correo no lo
  consume solo.
- El enlace es de un solo uso. Al reusarlo, Keycloak responde "Acción caducada".
- Después de definir la contraseña, el login por Direct Grant funciona.

### Requisito de despliegue

El correo sale por el **SMTP configurado en el realm**, no por el del backend: en desarrollo, `realm.json`
apunta a `mailpit:1025`. El host del enlace sale de `KC_HOSTNAME`. En producción, el realm necesita el SMTP
del hospital y `KC_HOSTNAME` debe ser el dominio público. Si falta cualquiera de los dos, la provisión
responde `activationEmailSent: false` o envía enlaces inservibles.

### Consecuencias

- El texto del correo es la plantilla de Keycloak (tema `keycloak`, locale `es`), no una propia.
  Personalizarlo es trabajo de tema de Keycloak.
- `SetTemporaryPasswordAsync` queda solo para `DevAdminSeeder`.

### Referencias

- `src/Cauce.Infrastructure/Identity/KeycloakAdminClient.cs`
- `src/Cauce.Application/Identity/UseCases/CreateNutritionist/CreateNutritionistCommandHandler.cs`
- `src/Cauce.Application/Identity/UseCases/ResendNutritionistActivationEmail/`
- `infrastructure/keycloak/import/realm.json` (`actionTokenGeneratedByAdminLifespan`, `smtpServer`)
- Actas [A49](#acta-a49-respuesta-del-endpoint-de-provisión-de-nutricionistas) y [A51](#acta-a51-mecanismo-de-activación-de-cuentas-de-nutricionista).

---

## Acta A53: Estado del nutricionista al registrarse con un código y al generarlo

**Estado:** Aprobada, RESUELTA en Nutritionist-Activation-1
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, `POST /api/v1/auth/register` y `POST /api/v1/invitations`.

---

### Contexto

La decisión D11 del acta [A41](#acta-a41-endpoint-de-canje-de-código-de-invitación-post-registro) exige que el nutricionista dueño de
un código esté activo, pero solo se aplicaba en el canje posterior al registro. La Fase 0 encontró que el
canje dentro de `POST /auth/register` no validaba el estado del nutricionista, y que la generación de
códigos tampoco lo hacía.

### Decisión

- **Registro.** Después de comprobar que el código está vigente, se exige que su nutricionista esté
  `Active`. Si no lo está, responde **409 `nutritionist_not_available`** con la extensión `reason`. El
  rechazo ocurre **antes de crear el usuario en Keycloak**, así que no hay nada que compensar y el código no
  se consume. Un código que apunta a un nutricionista inexistente se trata como
  `invalid_invitation_code`, con un log crítico. No se audita, igual que el resto de los rechazos del
  registro.
- **Generación.** Después de comprobar el rol, se exige `Active`, con el mismo 409 y su `reason`.
  - Un nutricionista pendiente ya llega activado por el behavior (acta [A51](#acta-a51-mecanismo-de-activación-de-cuentas-de-nutricionista)),
    así que para él este chequeo es solo un respaldo.
  - La protección real es contra una cuenta suspendida o dada de baja que todavía tiene un token vigente.
- El mensaje de `NutritionistNotAvailableException` se generalizó para cubrir los tres puntos donde se
  lanza.

### Consecuencias

- `CONTRACT-IDENTITY` pasa a v1.3: el registro puede responder un 409 nuevo. **El cliente móvil todavía no
  mapea este `errorCode`.**
- Con el acta A51, un nutricionista pendiente no puede tener códigos por ninguna vía de la aplicación, así
  que la rama `pending_activation` queda como defensa.
- Las ramas `suspended` e `inactive` se alcanzarán cuando exista el flujo de suspensión (actas
  [A55](#acta-a55-deuda-diferida--la-suspensión-no-corta-sesiones-ni-tokens-ya-emitidos) y [A56](#acta-a56-deuda-diferida--los-códigos-de-invitación-no-se-revocan-al-suspender)).
  Hoy están cubiertas por pruebas de integración que siembran el estado directamente.

### Referencias

- `src/Cauce.Application/Identity/UseCases/RegisterPatient/RegisterPatientCommandHandler.cs`
- `src/Cauce.Application/Identity/UseCases/GenerateInvitationCode/GenerateInvitationCodeCommandHandler.cs`
- `src/Cauce.Domain/Patients/Exceptions/NutritionistNotAvailableException.cs`
- `tests/Cauce.Api.IntegrationTests/Identity/InvitationNutritionistStatusTests.cs`

---

## Acta A54: Deuda diferida — términos operativos del nutricionista

**Estado:** Aprobada, deuda diferida. Bloque por definir
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, `ConsentRecord`, `IConsentService` y primer acceso del nutricionista.

---

### Contexto

La Fase 0 de Nutritionist-Activation-1 confirmó dos cosas:

- **`ConsentRecord` solo se usa para pacientes.** La entidad no depende del rol, pero únicamente se captura
  en el registro de pacientes.
- **`IConsentService` maneja un único documento vigente,** y su texto es de participación en el piloto.

El nutricionista trata datos clínicos protegidos por la Ley N° 29733, y es probable que deba aceptar
términos propios de confidencialidad y tratamiento de datos. Sería un documento distinto, que el servicio
actual no puede representar.

### Decisión

No implementar en este bloque. El bloque que lo tome necesita:

1. Documentos de consentimiento por tipo o rol, cada uno con su versión y su hash.
2. Que `IConsentService` resuelva varios documentos vigentes en paralelo.
3. Capturar la aceptación en el primer acceso del nutricionista, previsiblemente en el portal web.
4. Un comprobante en PDF, como el que ya tiene el paciente.

### Pregunta abierta

El texto y la exigencia misma son un requisito legal o del hospital. Los tiene que definir el Complejo
Hospitalario Guillermo Kaelín de la Fuente, o el área legal de la tesis. No son trabajo técnico.

### Consecuencias

Hoy ningún nutricionista acepta términos dentro del sistema.

### Referencias

- `src/Cauce.Domain/Identity/ConsentRecord.cs`
- `src/Cauce.Application/Common/Interfaces/Identity/IConsentService.cs`

---

## Acta A55: Deuda diferida — la suspensión no corta sesiones ni tokens ya emitidos

**Estado:** Aprobada, deuda diferida. Se resuelve junto con el flujo de suspensión
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, autorización de peticiones y ciclo de vida de cuentas.

---

### Contexto

Ni el login ni la autorización revisan `User.Status` una vez que el token fue emitido. `User.Suspend()`
sigue sin llamador en la aplicación (acta [A47](#acta-a47-deuda-diferida--sin-ciclo-de-vida-de-cuentas-de-nutricionista)). Si el día
de mañana se suspende una cuenta cambiando solo el estado local:

- El JWT sigue siendo válido hasta 15 minutos.
- La renovación sigue funcionando mientras Keycloak no deshabilite al usuario.
- El login por Direct Grant sigue entrando.

### Decisión

No implementar en este bloque. El bloque que exponga la suspensión debería:

1. Llamar a `IKeycloakAdminClient.DisableUserAsync` al suspender. Ya existe y la usa la baja de pacientes
   (US26). Con eso se cortan el login y la renovación.
2. Revocar las sesiones abiertas del usuario en Keycloak, si la ventana de un refresh token vigente no es
   aceptable.
3. Revisar el estado en el pipeline para cubrir la ventana del access token. El
   `NutritionistActivationBehavior` (acta [A51](#acta-a51-mecanismo-de-activación-de-cuentas-de-nutricionista)) ya lee al
   nutricionista en cada petición, así que es el lugar natural.

### Consecuencias

Para el caso concreto de generar códigos, el chequeo del acta
[A53](#acta-a53-estado-del-nutricionista-al-registrarse-con-un-código-y-al-generarlo) ya cubre esa ventana: un nutricionista suspendido con
un token vigente no puede emitirlos.

### Referencias

- `src/Cauce.Domain/Identity/User.cs` (`Suspend`, `Reactivate`)
- `src/Cauce.Infrastructure/Identity/KeycloakAdminClient.cs` (`DisableUserAsync`)

---

## Acta A56: Deuda diferida — los códigos de invitación no se revocan al suspender

**Estado:** Aprobada, deuda diferida. Se resuelve junto con el flujo de suspensión
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, entidad `InvitationCode`.

---

### Contexto

`InvitationCode` solo expira por tiempo (72 horas) y no tiene forma de revocarse. Con el acta
[A53](#acta-a53-estado-del-nutricionista-al-registrarse-con-un-código-y-al-generarlo), los canjes de códigos de un nutricionista suspendido
se rechazan con 409, así que el daño práctico queda acotado. Aun así, quedan dos efectos:

- El código sigue en estado `Active`.
- Si el nutricionista se reactiva dentro de las 72 horas, sus códigos vuelven a funcionar sin que nadie
  los haya reemitido.

### Decisión

No implementar en este bloque. Opciones para cuando exista el flujo de suspensión:

1. Agregar un método de dominio `Revoke` con un estado `Revoked`, y revocar en cascada al suspender.
2. Aceptar el comportamiento actual, ya que el rechazo de A53 más la vigencia de 72 horas pueden alcanzar.

### Consecuencias

El riesgo es bajo por la vigencia corta de los códigos y el rechazo en el canje. Las asignaciones activas
de un nutricionista suspendido tampoco se tocan: esa es una decisión clínica que le corresponde al bloque
que defina la suspensión.

### Referencias

- `src/Cauce.Domain/Identity/InvitationCode.cs`
- Actas [A47](#acta-a47-deuda-diferida--sin-ciclo-de-vida-de-cuentas-de-nutricionista) y [A55](#acta-a55-deuda-diferida--la-suspensión-no-corta-sesiones-ni-tokens-ya-emitidos).

---

## Acta A57: El comprobante en PDF del consentimiento se resuelve por versión aceptada, no por la vigente

**Estado:** Aprobada, implementada en el bloque de cierre de HU0001 escenario 4
**Fecha:** 2026-09-17
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, entidad `ConsentDocument`, `GetMyConsentPdfQueryHandler` e `IConsentPdfRenderer`.

---

### Contexto

Hasta este bloque el texto del consentimiento vivía únicamente en `appsettings` bajo `Consent:Text`,
como un valor actual y sin historia. `ConsentRecord` sí guardaba la versión aceptada y el hash del
texto, pero el PDF se armaba con el texto de configuración vigente al momento de la descarga.

El resultado es un comprobante internamente inconsistente en cuanto la redacción cambia: imprime el
hash de la versión que el paciente aceptó junto al texto de otra. Para un documento cuya única razón
de ser es acreditar qué aceptó esa persona, eso lo invalida.

No es un riesgo teórico. En los datos de prueba aparecieron dos filas de `consent_records` con
`document_version = '1.0'` y hashes distintos, es decir dos textos distintos bajo la misma versión.

### Decisión

Se introduce `consent_documents` como historial versionado del texto: versión, texto íntegro, hash
SHA-256 y marca de vigencia. El PDF resuelve el texto por `consent_records.document_version` contra
esa tabla, y no por la configuración.

**Regla permanente.** Cambiar la redacción del consentimiento significa insertar una fila nueva con
la versión siguiente y marcarla como vigente. Una versión publicada nunca se sobrescribe.

Cuando la versión aceptada no existe en la tabla, que es el caso de las aceptaciones anteriores a
ella, el endpoint responde 404 en lugar de emitir un PDF con el texto vigente.

### Consecuencias

El renderizador dejó de conocer la configuración: `IConsentPdfRenderer.Render` recibe el texto como
parámetro y falla si se lo llaman sin él. Un renderizador que pudiera sustituir el texto por el
vigente reintroduciría el defecto por la puerta de atrás.

Emitir 404 para las aceptaciones sin texto guardado es deliberado. Un comprobante con el texto
equivocado es peor que no emitirlo, porque parece auténtico. El móvil consume ese dato por anticipado
vía `GET /patients/me/consent` y deshabilita la descarga con una explicación, en vez de dejar que el
paciente choque contra el error.

Los tests siembran dos versiones a propósito y no una sola, porque el defecto que esto previene solo
se manifiesta cuando difieren.

### Referencias

- `src/Cauce.Domain/Identity/ConsentDocument.cs`
- `src/Cauce.Application/Identity/UseCases/GetMyConsentPdf/GetMyConsentPdfQueryHandler.cs`
- `tests/Cauce.Application.Tests/Identity/GetMyConsentPdfQueryHandlerTests.cs`
- HU0001 escenario 4, caso de prueba CP004.
- Acta [A58](#acta-a58-desviación-de-dec-b3-07-el-seeder-del-consentimiento-corre-en-todos-los-ambientes), sobre la siembra de la tabla.

---

## Acta A58: Desviación de DEC-B3-07, el seeder del consentimiento corre en todos los ambientes

**Estado:** Aprobada. Solución intermedia hasta que exista el CLI de producción que prevé DEC-B3-07
**Fecha:** 2026-09-17
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, `ConsentDocumentsSeeder`, `ConsentDocumentStartup` y el arranque en `Program.cs`.

---

### Contexto

DEC-B3-07 fijó que los seeders son idempotentes y corren automáticamente solo en Development, con un
CLI para Production. Ese CLI todavía no existe.

El acta [A57](#acta-a57-el-comprobante-en-pdf-del-consentimiento-se-resuelve-por-versión-aceptada-no-por-la-vigente) hace que el PDF resuelva su texto contra
`consent_documents`. Con la tabla vacía no hay texto que reproducir y el endpoint responde 404 para
todos los pacientes, incluso para quienes aceptaron la versión vigente. Gatear la siembra a
Development dejaría el comprobante inoperativo en Staging y en Producción, que es justo donde la
Ley N.° 29733 lo exige.

### Decisión

`EnsureConsentDocumentAsync()` corre en todos los ambientes, fuera del bloque
`if (app.Environment.IsDevelopment())` que envuelve al resto de la siembra. Publica la versión vigente
de `Consent:Text` si no existe, y no la toca si ya está.

Además compara el hash del texto de configuración contra el de la versión almacenada. Si divergen,
registra una advertencia con las dos huellas y la versión, y el arranque continúa. No se loguea el
texto, por la regla de no registrar datos personales ni clínicos en `ILogger`.

### Justificación

El texto del consentimiento no es dato de prueba, es configuración necesaria para que una
funcionalidad de cumplimiento tenga contenido. Ahí se diferencia de los seeders que DEC-B3-07 sí
necesita gatear: el del paciente demo y el del nutricionista de prueba provisionan credenciales, y
esas no tienen por qué existir fuera de desarrollo.

Es idempotente y no sobrescribe. Una versión ya publicada permanece intacta aunque `Consent:Text`
haya cambiado, porque sobrescribirla rompería la correspondencia con los `consent_records` que la
referencian. Por eso la divergencia se informa en lugar de corregirse sola.

Que la advertencia no tumbe el arranque es intencional: un texto de configuración desfasado no
invalida las aceptaciones ya registradas ni impide que el PDF se emita con la versión correcta.

### Consecuencias

Queda una desviación explícita de DEC-B3-07 en el árbol, y esta acta es su explicación. Cuando exista
el CLI de producción corresponde revisar si la publicación de versiones nuevas del consentimiento pasa
a ese canal, con el seeder de arranque reducido a la verificación de hash.

La advertencia de divergencia es la señal operativa de que alguien editó `Consent:Text` sin publicar
una versión nueva. Quien la vea en el log de Staging o Producción debe publicar la versión siguiente,
no editar la fila existente.

### Referencias

- `src/Cauce.Infrastructure/Persistence/Seeders/ConsentDocumentsSeeder.cs`
- `src/Cauce.Infrastructure/Persistence/Seeders/ConsentDocumentStartup.cs`
- `src/Cauce.Api/Program.cs`
- DEC-B3-07, del Bloque 3. Vive en el repo `docs` (`DECISIONS-BLOCK-01.md`), que no está en este checkout.

---
