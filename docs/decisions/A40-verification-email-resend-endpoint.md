# Acta A40: Endpoint de reenvío del correo de verificación

**Estado:** Aprobada, RESUELTA en Backend-Fix-2 (tag `v0.8.0-backend-fix-2`)
**Fecha:** 2026-09-08
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, endpoint anónimo nuevo en `AuthController`.

---

## Contexto

Un paciente que perdía el correo de verificación no tenía forma de pedir otro. El enlace vence, el
mensaje se borra o cae en la carpeta de no deseados, y la cuenta queda inutilizable: sin verificar no se
puede iniciar sesión, y sin iniciar sesión no hay ningún endpoint al alcance. La única salida era
contactar a soporte de forma manual, que en un piloto clínico con pacientes reales no es una salida.

## Decisión

Nuevo endpoint `POST /api/v1/auth/verification-email/resend`, anónimo, con una política de rate limit
dedicada.

## Decisiones complementarias

- **D3.** Rate limit `auth-verify-resend`: 3 peticiones por hora, **particionadas por el correo
  normalizado**, no por IP. El mecanismo que lo hace posible está documentado en el acta A44.
- **D4.** Si la cuenta ya está verificada, la respuesta es 200 igual. No se revela el estado.
- **R10.** Validación exhaustiva de la entrada con FluentValidation.

## Respuesta deliberadamente uniforme

Los tres desenlaces terminan en **200 sin cuerpo**: la cuenta no existe, la cuenta existe y ya está
verificada, o la cuenta existe sin verificar y se solicitó el reenvío. El cliente **no puede inferir del
código de respuesta si un correo está registrado ni si fue verificado**.

Distinguirlos convertiría el endpoint en un oráculo de cuentas: cualquiera podría enumerar qué correos
pertenecen a pacientes del piloto, que es información clínica por asociación.

El envío a Keycloak es best-effort: si la Admin API falla, se registra advertencia y la respuesta sigue
siendo 200.

## Consecuencias

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

## Referencias

- `src/Cauce.Api/Controllers/AuthController.cs`
- `src/Cauce.Application/Identity/UseCases/ResendVerificationEmail/`
- `src/Cauce.Api/Configuration/RateLimitingPolicies.cs`
- Acta [A44](A44-rate-limit-partition-by-request-body.md), partición del rate limit por el cuerpo.
- `docs/api/CONTRACT-IDENTITY-v1.md` v1.2, §2.10.
