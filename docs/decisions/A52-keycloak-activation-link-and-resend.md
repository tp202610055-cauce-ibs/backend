# Acta A52: Enlace de Keycloak para que el nutricionista defina su contraseña, y su reenvío

**Estado:** Aprobada, RESUELTA en Nutritionist-Activation-1
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, provisión de nutricionistas, cliente de la Admin API de Keycloak y endpoints admin.

---

## Contexto

La provisión generaba una contraseña temporal y la enviaba **en texto plano** por el SMTP del backend, con
un enlace a `PortalAppBaseUrl`, que en producción está vacío. Además había dos problemas:

- **El primer acceso era imposible.** Con `UPDATE_PASSWORD` pendiente, el login por Direct Grant responde
  401, así que un nutricionista real no podía completarlo.
- **Cuenta huérfana.** El `SaveChanges` ocurría antes del envío del correo. Si el correo fallaba, la
  compensación borraba el usuario de Keycloak, pero la fila local ya estaba confirmada. Los reintentos
  respondían 409 `duplicate_email`.

## Decisión

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

## Verificación contra el realm real

Verificado con Keycloak 25.0.6 antes de implementar:

- `execute-actions-email` responde 204 y el correo llega en español: asunto "Actualiza tu cuenta", con el
  aviso "expirará en 720 minutos".
- El token trae `typ=execute-actions`, `rqac=UPDATE_PASSWORD` y `exp − iat = 43200 s`.
- La acción viaja en el token, no en el usuario: `requiredActions` sigue en `[]`.
- El enlace abre una página de confirmación antes del formulario, así que un escáner de correo no lo
  consume solo.
- El enlace es de un solo uso. Al reusarlo, Keycloak responde "Acción caducada".
- Después de definir la contraseña, el login por Direct Grant funciona.

## Requisito de despliegue

El correo sale por el **SMTP configurado en el realm**, no por el del backend: en desarrollo, `realm.json`
apunta a `mailpit:1025`. El host del enlace sale de `KC_HOSTNAME`. En producción, el realm necesita el SMTP
del hospital y `KC_HOSTNAME` debe ser el dominio público. Si falta cualquiera de los dos, la provisión
responde `activationEmailSent: false` o envía enlaces inservibles.

## Consecuencias

- El texto del correo es la plantilla de Keycloak (tema `keycloak`, locale `es`), no una propia.
  Personalizarlo es trabajo de tema de Keycloak.
- `SetTemporaryPasswordAsync` queda solo para `DevAdminSeeder`.

## Referencias

- `src/Cauce.Infrastructure/Identity/KeycloakAdminClient.cs`
- `src/Cauce.Application/Identity/UseCases/CreateNutritionist/CreateNutritionistCommandHandler.cs`
- `src/Cauce.Application/Identity/UseCases/ResendNutritionistActivationEmail/`
- `infrastructure/keycloak/import/realm.json` (`actionTokenGeneratedByAdminLifespan`, `smtpServer`)
- Actas [A49](A49-admin-nutritionist-provisioning-response.md) y [A51](A51-nutritionist-activation-mechanism.md).
