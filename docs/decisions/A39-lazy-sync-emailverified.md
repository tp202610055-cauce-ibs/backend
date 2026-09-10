# Acta A39: Sincronización lazy de emailVerified desde Keycloak en login

**Estado:** Aprobada, RESUELTA en Backend-Fix-2 (tag `v0.8.0-backend-fix-2`)
**Fecha:** 2026-09-08
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, `LoginCommandHandler` y `KeycloakAdminClient`.

---

## Contexto

La base local no sincronizaba `emailVerified` con Keycloak. El enlace de confirmación lo emite y lo
procesa el propio realm, sin pasar por el backend, de modo que al verificar su correo el paciente
actualizaba el estado **en Keycloak pero no en la copia local**.

La app móvil lee ese valor del objeto `user` que devuelve el login. El efecto para el paciente era que,
habiendo verificado su correo, la aplicación lo seguía tratando como pendiente de verificación, sin
ninguna acción a su alcance que corrigiera la situación.

## Decisión (D1)

Sincronizar de forma diferida en cada inicio de sesión: tras la autenticación exitosa contra Keycloak,
`LoginCommandHandler` invoca `IKeycloakAdminClient.GetUserEmailVerifiedAsync` y actualiza el valor local
si difiere.

La escritura se enrola en el `ChangeTracker` y la confirma el `SaveChangesAsync` que el handler ya hacía
para la marca de último acceso, de modo que ambos cambios viajan en **una sola transacción**.

## Decisión complementaria (D2)

Un fallo de la Admin API se registra como advertencia y el inicio de sesión continúa con el valor local.
**El login no falla por una sincronización auxiliar.** El manejo replica el patrón que ya usaba
`TryResolveLockoutAsync` en el mismo archivo: se capturan todas las excepciones salvo
`OperationCanceledException`, que se propaga.

## La sincronización es unidireccional

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

## Consecuencias

- El comportamiento del login cambia de forma observable: la respuesta puede reflejar por primera vez el
  estado real de verificación.
- El login pasa a depender de la Admin API de Keycloak, con manejo tolerante de fallos.
- El móvil recibe `emailVerified` correcto en la primera sesión posterior a la verificación.
- El contrato HTTP no cambia: mismos códigos de respuesta y mismo cuerpo.

## Referencias

- `src/Cauce.Application/Identity/UseCases/Login/LoginCommandHandler.cs`
- `src/Cauce.Infrastructure/Identity/KeycloakAdminClient.cs`
- `docs/api/CONTRACT-IDENTITY-v1.md` v1.2, §2.2.
