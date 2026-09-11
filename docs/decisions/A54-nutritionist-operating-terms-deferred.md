# Acta A54: Deuda diferida — términos operativos del nutricionista

**Estado:** Aprobada, deuda diferida. Bloque por definir
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, `ConsentRecord`, `IConsentService` y primer acceso del nutricionista.

---

## Contexto

La Fase 0 de Nutritionist-Activation-1 confirmó dos cosas:

- **`ConsentRecord` solo se usa para pacientes.** La entidad no depende del rol, pero únicamente se captura
  en el registro de pacientes.
- **`IConsentService` maneja un único documento vigente,** y su texto es de participación en el piloto.

El nutricionista trata datos clínicos protegidos por la Ley N° 29733, y es probable que deba aceptar
términos propios de confidencialidad y tratamiento de datos. Sería un documento distinto, que el servicio
actual no puede representar.

## Decisión

No implementar en este bloque. El bloque que lo tome necesita:

1. Documentos de consentimiento por tipo o rol, cada uno con su versión y su hash.
2. Que `IConsentService` resuelva varios documentos vigentes en paralelo.
3. Capturar la aceptación en el primer acceso del nutricionista, previsiblemente en el portal web.
4. Un comprobante en PDF, como el que ya tiene el paciente.

## Pregunta abierta

El texto y la exigencia misma son un requisito legal o del hospital. Los tiene que definir el Complejo
Hospitalario Guillermo Kaelín de la Fuente, o el área legal de la tesis. No son trabajo técnico.

## Consecuencias

Hoy ningún nutricionista acepta términos dentro del sistema.

## Referencias

- `src/Cauce.Domain/Identity/ConsentRecord.cs`
- `src/Cauce.Application/Common/Interfaces/Identity/IConsentService.cs`
