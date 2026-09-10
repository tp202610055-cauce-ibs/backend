# Acta A45: Migración de pruebas en un refactor por inyección de constructor

**Estado:** Aprobada, aplicada en Backend-Fix-2 Fase 3
**Fecha:** 2026-09-06
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Suite de pruebas del backend. No cambia código de producción ni el contrato de API.

---

## Contexto

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

## Decisión

Se admite una excepción acotada a D10, **para preservar R5**, con dos categorías de cambio claramente
separadas y una condición distinta para cada una.

### Categoría 1: swap mecánico

Cambiar el tipo de un doble de prueba y el argumento que recibe el constructor. Nada más.

**Condición:** ninguna aserción, ningún stub y ningún escenario se modifican. Si durante un swap
aparece la necesidad de tocar una aserción, deja de ser mecánico y se detiene la ejecución para
consultarlo.

### Categoría 2: reexpresión al nivel correcto

Una prueba que asertaba sobre una dependencia que se mudó al servicio deja de poder observarla desde el
handler. La prueba del handler pasa a verificar **la delegación y el resultado observable**, y las
aserciones originales se mudan a la prueba del servicio.

**Condición:** las aserciones migradas van **verbatim**. Los mismos stubs, las mismas verificaciones,
sobre los mismos repositorios. Lo único que cambia es el sujeto de la prueba.

## Distinción que hace legítima la excepción

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

## Cómo se preserva la cobertura

Las dos pruebas reexpresadas cubrían la creación de la asignación. Tras el refactor:

- La prueba del **handler** verifica que delega y que el resultado (`NutritionistAssigned`,
  `NutritionistAssignmentId`) refleja lo que el servicio devolvió. Es lo que el handler controla.
- La prueba del **servicio** hereda las aserciones de repositorio verbatim, y suma los casos que el
  handler nunca cubrió, como la asignación activa preexistente.

La cobertura total sube: los mismos escenarios quedan cubiertos, y se agregan los que antes no lo
estaban.

## Alcance de la aplicación en Fase 3

| Archivo | Categoría | Detalle |
| --- | --- | --- |
| `tests/Cauce.Application.Tests/Identity/RegisterPatientCommandHandlerTests.cs` | Mecánico | `IOutboxWriter` pasa a `IPatientNutritionistAssignmentService` en la declaración del doble y en el argumento 6. Sus 4 pruebas conservan escenarios y aserciones. El doble de outbox nunca fue asertado |
| `tests/Cauce.Application.Tests/Patients/CreatePatientProfileCommandHandlerTests.cs` | Mecánico | `Handle_ProfileAlreadyExists_ThrowsDuplicate` y `Handle_NonPatientRole_ThrowsUnauthorized`: solo el constructor |
| `tests/Cauce.Application.Tests/Patients/CreatePatientProfileCommandHandlerTests.cs` | Reexpresión | `Handle_ValidRequestWithoutInvitation_CreatesProfileWithoutAssignment` y `Handle_PatientUsedInvitation_CreatesNutritionistAssignment` |
| `tests/Cauce.Application.Tests/Patients/Services/PatientNutritionistAssignmentServiceTests.cs` | Destino | Recibe verbatim las aserciones de repositorio migradas |

## Consecuencias

- Se establece un precedente acotado: futuros refactors por constructor pueden migrar pruebas bajo
  estas dos categorías, con sus condiciones, sin renegociar D10 cada vez.
- Todo refactor que caiga en la categoría 2 debe declarar en su reporte de fase qué pruebas se
  reexpresaron y a dónde fueron sus aserciones.
- D10 sigue vigente para todo lo demás. Esta excepción no cubre cambios de comportamiento.

## Referencias

- `src/Cauce.Application/Common/Interfaces/Patients/IPatientNutritionistAssignmentService.cs`
- `src/Cauce.Application/Patients/Services/PatientNutritionistAssignmentService.cs`
- Acta A41, endpoint de canje de código de invitación post-registro.
- Acta A43, notificación al nutricionista mediante el evento de dominio existente.
- Reglas D10 y R5 del prompt Backend-Fix-2.
