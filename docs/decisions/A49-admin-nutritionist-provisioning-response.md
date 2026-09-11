# Acta A49: Respuesta del endpoint de provisión de nutricionistas

**Estado:** Aprobada, RESUELTA en Nutritionist-Activation-1
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, `POST /api/v1/admin/nutritionists` y endpoint nuevo `GET /api/v1/admin/nutritionists/{id}`.

---

## Contexto

La observación se registró en el smoke de cierre de Backend-Fix-2, pero sin archivo propio: solo figuraba
en el historial del `CLAUDE.md` local. Esta acta la redacta en el bloque que la resuelve.

La respuesta de la provisión no se ajustaba al contrato que el resto de la API sigue para una creación:

| Aspecto | Antes |
| --- | --- |
| Estado de la cuenta | Ausente |
| Header `Location` | Ausente |
| `temporaryCredentialsEmailSent` | Siempre `true`: un fallo del correo lanzaba excepción y compensaba, así que nunca se observaba `false` |
| 502 de Keycloak | Posible, pero no declarado en `[ProducesResponseType]` |

## Decisión

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

## Consecuencias

- **Cambio incompatible del contrato admin:** el campo `temporaryCredentialsEmailSent` pasó a llamarse
  `activationEmailSent`. Hoy ningún cliente lo consume, porque el equipo usa el endpoint a mano.
- El snapshot pasa a `openapi-v1.2.0.json` y `ENDPOINTS.md` a v1.3.0.

## Referencias

- `src/Cauce.Api/Controllers/AdminController.cs`
- `src/Cauce.Application/Identity/UseCases/CreateNutritionist/CreateNutritionistResult.cs`
- `src/Cauce.Application/Identity/UseCases/GetNutritionist/`
- Actas [A51](A51-nutritionist-activation-mechanism.md) y [A52](A52-keycloak-activation-link-and-resend.md).
