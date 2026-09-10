# Acta A47: Deuda diferida — sin ciclo de vida de cuentas de nutricionista

**Estado:** Aprobada, deuda diferida. Se resuelve en el bloque **Nutritionist-Activation-1**
**Fecha:** 2026-09-08
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, provisión y estados de las cuentas de nutricionista.

---

## Contexto

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

## Decisión

**No resolver en Backend-Fix-2.** Se documenta como deuda y se asigna al bloque
**Nutritionist-Activation-1**, que le da alcance propio.

La razón de diferirlo es de alcance, no de dificultad: Backend-Fix-2 cierra tres deudas acotadas de
identidad, y construir un ciclo de vida de cuentas implica decisiones de producto que exceden ese marco
(qué activa una cuenta, quién puede suspenderla, si hay endpoint administrativo, si el portal web lo
expone).

La comprobación de D11 **se implementa igual**, con sus pruebas. Queda correcta y esperando.

## Consecuencias mientras la deuda siga abierta

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

## Alcance sugerido para Nutritionist-Activation-1

Sin comprometer el diseño, que es trabajo de ese bloque:

1. Provisionar la cuenta en `PendingActivation` y activarla cuando el nutricionista complete su primer
   acceso con cambio de contraseña.
2. Exponer suspensión y reactivación, decidiendo si por endpoint administrativo con clave de API, como
   `POST /admin/nutritionists`, o desde el portal web.
3. Definir qué ocurre con los códigos vigentes y las asignaciones activas de un nutricionista suspendido.
4. Verificar que las tres ramas de `nutritionist_not_available` quedan alcanzables end to end.

## Referencias

- `src/Cauce.Domain/Identity/User.cs` (`Activate`, `Suspend`, `Reactivate`, `CreateNutritionist`).
- `src/Cauce.Application/Identity/UseCases/CreateNutritionist/CreateNutritionistCommandHandler.cs`
- `src/Cauce.Application/Patients/UseCases/AssignNutritionist/AssignNutritionistCommandHandler.cs`
- Acta [A41](A41-nutritionist-assignment-endpoint.md), que introduce la validación D11.
- `docs/api/CONTRACT-IDENTITY-v1.md` v1.2, §2.11.
