# Cauce API — Referencia de endpoints

**Versión:** 1.0.0 · **Actualizado:** 2026-07-08 · **Contrato:** [`openapi-v1.0.0.json`](openapi-v1.0.0.json) (289 KB, 57 operaciones, ~48 paths)

Referencia human-readable de detalle para el equipo frontend (mobile Flutter primero, web-portal React
después), complementaria a la sección **"Endpoints por rol"** del [`CLAUDE.md`](../../CLAUDE.md) (resumen).
Aquí está el detalle largo: por endpoint, con DTO de request/response, idempotencia, rate limit y códigos de
respuesta semánticos. **El OpenAPI es la fuente de verdad del contrato**; este documento lo narra.

## Convenciones

- **Base URL (dev):** `http://localhost:5074` · **Prefijo:** `/api/v1` (versionado en URL, DEC-B3-05).
- **Serialización:** JSON **camelCase**. **Enums en el JSON = nombres PascalCase del miembro** (ej.
  `"PendingReview"`, `"AbdominalPain"`, `"IbsD"`); en base de datos se persisten en snake_case. Ver
  [CLAUDE.md → Enums](../../CLAUDE.md#enums-y-valores-controlados).
- **Auth:** `Authorization: Bearer <accessToken>` (JWT de Keycloak, realm `cauce`). Roles `patient` /
  `nutritionist` → políticas ASP.NET `Patient` / `Nutritionist`.
- **Schemas:** los DTOs de request y response viven en `openapi-v1.0.0.json` bajo
  `#/components/schemas/<Nombre>`. En las tablas se citan por nombre (ej. `CreateMealRequest`).
- **Errores:** RFC 7807 `application/problem+json` con extensiones `errorCode` (máquina) y `traceId`. El
  cuerpo de todo error 4xx/5xx es un `ProblemDetails`. Los `429` incluyen la extensión `retryAfterSeconds`;
  los `409 unconfirmed_allergens` incluyen `detected` y `allergens[]`.
- **Idempotencia:** header `Idempotency-Key` (UUID v4). En comidas/síntomas/sync también puede viajar en el
  cuerpo (`clientGuid`). "Requerido" = el endpoint lo exige; "opcional" = lo acepta; "—" = no aplica.
- **Casing:** el ruteo de ASP.NET Core es **case-insensitive** (una llamada en otra caja resuelve igual); el
  contrato emite todos los paths en minúscula.

## Índice

1. [Públicos / Transversales](#1-públicos--transversales)
2. [Autenticación](#2-autenticación)
3. [Paciente / Perfil clínico](#3-paciente--perfil-clínico)
4. [Paciente / Registro diario](#4-paciente--registro-diario)
5. [Paciente / IBS-SSS y evolución](#5-paciente--ibs-sss-y-evolución)
6. [Paciente / Recomendaciones y feedback](#6-paciente--recomendaciones-y-feedback)
7. [Paciente / Derechos Ley 29733](#7-paciente--derechos-ley-29733)
8. [Paciente / Referencia](#8-paciente--referencia)
9. [Nutricionista / Panel y pacientes](#9-nutricionista--panel-y-pacientes)
10. [Nutricionista / Recomendaciones HITL](#10-nutricionista--recomendaciones-hitl)
11. [Nutricionista / Reportes clínicos](#11-nutricionista--reportes-clínicos)
12. [Nutricionista / Invitaciones](#12-nutricionista--invitaciones)
13. [Admin](#13-admin)

## Catálogo de `errorCode` → HTTP → significado

Fuente: `Cauce.Api/Middleware/ExceptionHandlingMiddleware.cs` + excepciones de `Cauce.Domain/**/Exceptions/`.
Cada endpoint lista abajo solo los códigos que su flujo produce.

| `errorCode` | HTTP | Significado |
| --- | --- | --- |
| `validation_error` | 400 | Falló validación (FluentValidation o binding de `[ApiController]`). Extensión `errors` por campo. |
| `domain_rule_violation` | 400 | Regla de dominio genérica violada. |
| `invalid_invitation_code` / `expired_invitation_code` / `invitation_code_already_used` | 400 | Código de invitación inexistente / expirado / ya usado (registro). |
| `consent_text_mismatch` | 400 | El hash del texto de consentimiento no coincide con la versión. |
| `invalid_password_reset_token` / `expired_password_reset_token` | 400 | Token de restablecimiento inválido / expirado. |
| `invalid_biometric_value` | 400 | Peso/estatura fuera de rango. |
| `invalid_ibs_sss_dimension` | 400 | Dimensión IBS-SSS fuera de 0–100. |
| `invalid_clinical_note_association` | 400 | La nota no se asocia a exactamente una comida o un síntoma. |
| `invalid_credentials` | 401 | Login fallido (mensaje genérico; no revela si la cuenta existe). |
| *(sin `errorCode`)* | 401 | Falta/expira el Bearer JWT, o falta `X-Admin-Api-Key` en `/admin/*`. |
| `forbidden` | 403 | Rol incorrecto para la política del endpoint. |
| `unauthorized_patient_access` | 403 | El nutricionista no está asignado a ese paciente. |
| `patient_resource_access_denied` | 403 | El recurso (comida/alimento) no pertenece al paciente autenticado. |
| `recommendation_access_denied` | 403 | Sin autorización sobre la recomendación. |
| `report_access_denied` | 403 | Sin autorización sobre el reporte. |
| `audit_log_immutable` | 403 | Intento de modificar `audit_logs` (bloqueado por trigger). |
| `*_not_found` (`patient_profile_not_found`, `food_item_not_found`, `custom_food_not_found`, `meal_not_found`, `symptom_not_found`, `clinical_note_not_found`, `allergy_not_found`, `recommendation_not_found`, `consent_record_not_found`, `not_found`) | 404 | El recurso solicitado no existe. |
| `duplicate_email` | 409 | Correo ya registrado. |
| `duplicate_patient_profile` / `duplicate_patient_allergy` / `duplicate_custom_food` / `duplicate_baseline_assessment` / `duplicate_ingredient` | 409 | Entidad duplicada. |
| `custom_food_in_use` | 409 | El alimento personalizado está referenciado por comidas. |
| `unconfirmed_allergens` | 409 | Coincidencias de alérgenos sin confirmar (extensiones `detected`, `allergens[]`). |
| `idempotency_mismatch` | 409 | La misma `Idempotency-Key` se reusó con carga distinta. |
| `conflict_state` | 409 | Transición de estado inválida en la máquina HITL. |
| `recommendation_expired` | 409 | La recomendación superó su vigencia. |
| `recommendation_not_archivable` | 409 | La recomendación no está en un estado archivable. |
| `active_pilot_retention` | 409 | Baja bloqueada por retención de piloto activo (falta el acuse). |
| `insufficient_clinical_history` | 422 | Historial clínico insuficiente para generar. |
| `all_candidates_filtered_by_allergies` | 422 | Todos los candidatos fueron filtrados por alergias. |
| `no_active_model_version` | 422 | No hay `ModelVersion` activa. |
| `patient_has_no_data_in_period` | 422 | El paciente no tiene datos en el período del reporte. |
| `report_period_invalid` | 422 | Período de reporte inválido. |
| `account_locked` | 423 | Cuenta bloqueada (brute-force lockout de Keycloak). |
| *(sin `errorCode`)* | 429 | Rate limit superado. Extensión `retryAfterSeconds`. |
| `keycloak_integration_error` | 502 | Falla del proveedor de identidad al provisionar el usuario. |
| `internal_server_error` | 500 | Error inesperado. |

---

## 1. Públicos / Transversales

### `GET /api/v1/health`
| Campo | Valor |
| --- | --- |
| Resumen | Estado de salud del servicio + marca de tiempo UTC. |
| US/TS | TS03 |
| Autorización | Anónimo |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `{ status, timestamp }` · 500 |

### `GET /api/v1/health/live`
| Campo | Valor |
| --- | --- |
| Resumen | Health check de liveness (ASP.NET `MapHealthChecks`). **No aparece en el OpenAPI** (no es un controller). |
| US/TS | TS03 |
| Autorización | Anónimo |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** (texto `Healthy`) · 503 (no saludable) |

### `PUT /api/v1/users/me/fcm-token`
| Campo | Valor |
| --- | --- |
| Resumen | Registra/actualiza el token FCM del dispositivo (enviar `null` lo desvincula). |
| US/TS | US14, TS10 CA01 |
| Autorización | Autenticado (cualquier rol) |
| Request body | `UpdateFcmTokenRequest` |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **204** · 400 · 401 · 403 · 500 |

### `GET /api/v1/recommendations/{id}`
| Campo | Valor |
| --- | --- |
| Resumen | Detalle de una recomendación (4 bloques). Autorización por recurso en el handler. |
| US/TS | US14, US15 |
| Autorización | Autenticado (paciente propietario, o nutricionista asignado) |
| Request body | — |
| Idempotencia | — |
| Rate limit | `default-auth` (60/min·usuario) |
| Respuestas | **200** `RecommendationDetailDto` · 401 · **403** (nutri no asignado) · **404** (paciente no propietario / no visible — evita revelar existencia) · 429 · 500 |

> Nota: para el paciente, una recomendación no visible o archivada responde **404** (no 403), para no revelar
> su existencia. El listado del paciente es `GET /recommendations/me` (§6).

---

## 2. Autenticación

### `POST /api/v1/auth/register`
| Campo | Valor |
| --- | --- |
| Resumen | Registra un paciente. Devuelve `user_id`, sin token (debe verificar email antes de loguearse). |
| US/TS | US01, US20 |
| Autorización | Anónimo |
| Request body | `RegisterPatientRequest` (email, fullName, password, consentDocumentVersion, consentTextHash, invitationCode?) |
| Idempotencia | Opcional (header `Idempotency-Key`, UUID v4) |
| Rate limit | `auth-register` (5/h·IP) |
| Respuestas | **201** `RegisterPatientResult` · 400 (`validation_error`, `invalid_invitation_code`, `expired_invitation_code`, `invitation_code_already_used`, `consent_text_mismatch`) · 409 `duplicate_email` · 429 · 502 `keycloak_integration_error` · 500 |

### `POST /api/v1/auth/login`
| Campo | Valor |
| --- | --- |
| Resumen | Passthrough a Keycloak (`grant_type=password`). Devuelve los tokens. |
| US/TS | US05, US06 |
| Autorización | Anónimo |
| Request body | `LoginRequest` (email, password, clientId) |
| Idempotencia | — |
| Rate limit | `auth-login` (10/min·IP) |
| Respuestas | **200** `LoginResult` (accessToken, refreshToken, expiresIn, refreshExpiresIn, tokenType) · 400 · 401 `invalid_credentials` · 429 · 500 |

### `POST /api/v1/auth/logout`
| Campo | Valor |
| --- | --- |
| Resumen | Revoca el refresh token en Keycloak. |
| US/TS | US08 |
| Autorización | Autenticado |
| Request body | `LogoutRequest` (refreshToken, clientId) |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **204** · 400 · 401 · 500 |

### `POST /api/v1/auth/password-reset/request`
| Campo | Valor |
| --- | --- |
| Resumen | Solicita restablecimiento. Responde 200 exista o no la cuenta (no leak). |
| US/TS | US07 |
| Autorización | Anónimo |
| Request body | `RequestPasswordResetRequest` (email) |
| Idempotencia | — |
| Rate limit | `auth-pwreset` (3/h·IP) |
| Respuestas | **200** · 400 · 429 · 500 |

### `POST /api/v1/auth/password-reset/confirm`
| Campo | Valor |
| --- | --- |
| Resumen | Confirma el restablecimiento con el token recibido por correo. |
| US/TS | US07 |
| Autorización | Anónimo |
| Request body | `ConfirmPasswordResetRequest` (token, newPassword) |
| Idempotencia | — |
| Rate limit | `auth-pwreset` (3/h·IP) |
| Respuestas | **200** · 400 (`validation_error`, `invalid_password_reset_token`, `expired_password_reset_token`) · 429 · 500 |

---

## 3. Paciente / Perfil clínico

`Policy = Patient` en todo el módulo → respuestas comunes 401 · 403 · 500.

### `POST /api/v1/patients/profile`
| Campo | Valor |
| --- | --- |
| Resumen | Crea el perfil clínico del paciente autenticado. |
| US/TS | US03 |
| Request body | `CreatePatientProfileRequest` |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **201** `CreatePatientProfileResult` · 400 · 409 `duplicate_patient_profile` · 401 · 403 · 500 |

### `PUT /api/v1/patients/profile`
| Campo | Valor |
| --- | --- |
| Resumen | Actualiza campos modificables del perfil (los `null` no se tocan). Recalcula BMI. |
| US/TS | US03 |
| Request body | `UpdatePatientProfileRequest` |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `UpdatePatientProfileResult` · 400 · 404 `patient_profile_not_found` · 401 · 403 · 500 |

### `GET /api/v1/patients/profile`
| Campo | Valor |
| --- | --- |
| Resumen | Perfil clínico completo del paciente autenticado. |
| US/TS | US03 |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `GetPatientProfileResult` · 404 `patient_profile_not_found` · 401 · 403 · 500 |

### `GET /api/v1/patients/allergies`
| Campo | Valor |
| --- | --- |
| Resumen | Alergias declaradas por el paciente. |
| US/TS | US03 |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `PatientAllergySummary[]` · 401 · 403 · 500 |

### `POST /api/v1/patients/allergies`
| Campo | Valor |
| --- | --- |
| Resumen | Declara una alergia (referida al catálogo `GET /allergies`). |
| US/TS | US03 |
| Request body | `DeclareAllergyRequest` (allergyId, severity, notes?) |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **201** `DeclarePatientAllergyResult` · 400 · 404 `allergy_not_found` · 409 `duplicate_patient_allergy` · 401 · 403 · 500 |

### `DELETE /api/v1/patients/allergies/{patientAllergyId}`
| Campo | Valor |
| --- | --- |
| Resumen | Elimina una declaración de alergia del paciente. |
| US/TS | US03 |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **204** · 404 `allergy_not_found` · 401 · 403 · 500 |

### `GET /api/v1/patients/me/summary`
| Campo | Valor |
| --- | --- |
| Resumen | Perfil agregado: identificación, perfil clínico, fecha de inicio en piloto, nutricionista, IBS-SSS resumido. |
| US/TS | US28 |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `MyProfileSummaryResult` · 404 `patient_profile_not_found` · 401 · 403 · 500 |

---

## 4. Paciente / Registro diario

`Policy = Patient` · `default-auth` (60/min·usuario) salvo `sync` → respuestas comunes 401 · 403 · 429 · 500.

### `POST /api/v1/meals`
| Campo | Valor |
| --- | --- |
| Resumen | Registra una comida. Idempotente por `client_guid` (replay → 200; nueva → 201). |
| US/TS | US09 |
| Request body | `CreateMealRequest` (clientGuid?, mealTime, consumedAt, clientCreatedAt, items[]) |
| Idempotencia | **Sí** — `client_guid` (cuerpo `clientGuid` o header `Idempotency-Key`) |
| Rate limit | `default-auth` |
| Respuestas | **201** / **200** `CreateMealResult` · 400 · 404 `food_item_not_found`/`custom_food_not_found` · 409 `idempotency_mismatch` · 401 · 403 · 429 · 500 |

### `GET /api/v1/meals`
| Campo | Valor |
| --- | --- |
| Resumen | Historial paginado de comidas (rango `from`/`to`). |
| US/TS | US09 |
| Query | `from`, `to` (UTC), `page=1`, `pageSize=50` |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **200** `PagedResult<MealHistoryItem>` · 400 · 401 · 403 · 429 · 500 |

### `POST /api/v1/symptoms`
| Campo | Valor |
| --- | --- |
| Resumen | Registra un síntoma. El servidor calcula la asociación con la comida en la ventana de 4h. Idempotente. |
| US/TS | US11 |
| Request body | `CreateSymptomRequest` (clientGuid?, symptomType, intensity, occurredAt, clientCreatedAt) |
| Idempotencia | **Sí** — `client_guid` |
| Rate limit | `default-auth` |
| Respuestas | **201** / **200** `CreateSymptomResult` · 400 · 409 `idempotency_mismatch` · 401 · 403 · 429 · 500 |

### `GET /api/v1/symptoms`
| Campo | Valor |
| --- | --- |
| Resumen | Historial paginado de síntomas. |
| US/TS | US11 |
| Query | `from`, `to`, `page=1`, `pageSize=50` |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **200** `PagedResult<SymptomHistoryItem>` · 400 · 401 · 403 · 429 · 500 |

### `POST /api/v1/clinical-notes`
| Campo | Valor |
| --- | --- |
| Resumen | Crea una nota clínica asociada a exactamente una comida **o** un síntoma. |
| US/TS | US13 |
| Request body | `CreateClinicalNoteRequest` (mealId?, symptomId?, content) |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **201** `CreateClinicalNoteResult` · 400 (`validation_error`, `invalid_clinical_note_association`) · 404 `meal_not_found`/`symptom_not_found` · 401 · 403 · 429 · 500 |

### `GET /api/v1/clinical-notes`
| Campo | Valor |
| --- | --- |
| Resumen | Notas clínicas del paciente en un rango de fechas. |
| US/TS | US13 |
| Query | `from`, `to` |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **200** `ClinicalNoteSummary[]` · 400 · 401 · 403 · 429 · 500 |

### `POST /api/v1/custom-foods`
| Campo | Valor |
| --- | --- |
| Resumen | Crea un alimento personalizado. `confirmedAllergens=false` por defecto: si hay coincidencias sin confirmar → 409. |
| US/TS | US10 (CA03 alérgenos) |
| Request body | `CreateCustomFoodRequest` (name, portionSizeGrams, ingredients[], confirmedAllergens) |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **201** `CreateCustomFoodResult` · 400 · 404 `food_item_not_found` · 409 (`duplicate_custom_food`, `unconfirmed_allergens` con `allergens[]`) · 401 · 403 · 429 · 500 |

### `GET /api/v1/custom-foods`
| Campo | Valor |
| --- | --- |
| Resumen | Lista los alimentos personalizados del paciente. |
| US/TS | US10 |
| Request body | — |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **200** `CustomFoodSummary[]` · 401 · 403 · 429 · 500 |

### `PUT /api/v1/custom-foods/{customFoodId}`
| Campo | Valor |
| --- | --- |
| Resumen | Actualiza un alimento personalizado del paciente. |
| US/TS | US10 |
| Request body | `UpdateCustomFoodRequest` |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **200** `UpdateCustomFoodResult` · 400 · 404 `custom_food_not_found`/`food_item_not_found` · 409 `duplicate_custom_food` · 401 · 403 · 429 · 500 |

### `DELETE /api/v1/custom-foods/{customFoodId}`
| Campo | Valor |
| --- | --- |
| Resumen | Elimina un alimento personalizado (falla si está en uso por comidas). |
| US/TS | US10 |
| Request body | — |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **204** · 404 `custom_food_not_found` · 409 `custom_food_in_use` · 401 · 403 · 429 · 500 |

### `POST /api/v1/sync/batch`
| Campo | Valor |
| --- | --- |
| Resumen | Sincroniza un lote de comidas y síntomas. Clasifica cada ítem en aceptado/duplicado/error. |
| US/TS | TS06 |
| Request body | `SyncBatchRequest` (meals[], symptoms[]) |
| Idempotencia | **Sí** — `client_guid` por ítem |
| Rate limit | `sync` (120/min·usuario) |
| Respuestas | **200** `SyncBatchResult` · 400 · 401 · 403 · 429 · 500 |

### `GET /api/v1/history`
| Campo | Valor |
| --- | --- |
| Resumen | Historial unificado (comidas + síntomas + notas) ordenado cronológicamente descendente. |
| US/TS | US13 |
| Query | `from`, `to` |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **200** `HistoryEvent[]` · 400 · 401 · 403 · 429 · 500 |

---

## 5. Paciente / IBS-SSS y evolución

`Policy = Patient` · `default-auth` → respuestas comunes 401 · 403 · 429 · 500.

### `POST /api/v1/ibs-sss`
| Campo | Valor |
| --- | --- |
| Resumen | Registra una evaluación IBS-SSS. El servidor calcula puntaje y categoría; la línea base cierra el onboarding. |
| US/TS | US04 (baseline), US12 (periódica) |
| Request body | `CreateIbsSssAssessmentRequest` (assessmentType + 5 dimensiones 0–100) |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **201** `CreateIbsSssAssessmentResult` · 400 (`validation_error`, `invalid_ibs_sss_dimension`) · 409 `duplicate_baseline_assessment` · 401 · 403 · 429 · 500 |

### `GET /api/v1/ibs-sss/evolution`
| Campo | Valor |
| --- | --- |
| Resumen | Serie de evaluaciones con su diferencia respecto de la línea base. |
| US/TS | US23 |
| Request body | — |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **200** `IbsSssEvolutionEntry[]` · 401 · 403 · 429 · 500 |

### `GET /api/v1/ibs-sss/latest`
| Campo | Valor |
| --- | --- |
| Resumen | Evaluación IBS-SSS más reciente (o `null` si no hay). |
| US/TS | US12 |
| Request body | — |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **200** `IbsSssAssessmentSummary` (nullable) · 401 · 403 · 429 · 500 |

---

## 6. Paciente / Recomendaciones y feedback

`Policy = Patient` · `default-auth` → respuestas comunes 401 · 403 · 429 · 500.

### `POST /api/v1/recommendations`
| Campo | Valor |
| --- | --- |
| Resumen | **Solicitud explícita** del paciente para generar una recomendación (no es automático). Nace en `PendingReview` (auto-aprobación off en piloto) + notificación de espera; el paciente la ve tras la aprobación. Replay con la misma clave → 200 con la misma recomendación. |
| US/TS | US14 (CA02 espera, CA03 filtros) |
| Request body | — (solo header) |
| Idempotencia | **Sí — requerido** (header `Idempotency-Key`, UUID v4) |
| Rate limit | `default-auth` |
| Respuestas | **201** / **200** `GenerateRecommendationResult` · 400 · 404 `patient_profile_not_found` · 409 `idempotency_mismatch` · 422 (`insufficient_clinical_history`, `all_candidates_filtered_by_allergies`, `no_active_model_version`) · 401 · 403 · 429 · 500 |

### `GET /api/v1/recommendations/me`
| Campo | Valor |
| --- | --- |
| Resumen | Recomendaciones del paciente, paginadas, con filtro opcional por estado (solo activas en estados visibles). |
| US/TS | US14 |
| Query | `status?` (`RecommendationStatus`), `page=1`, `pageSize=20` |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **200** `PagedResult<RecommendationSummaryDto>` · 400 · 401 · 403 · 429 · 500 |

### `POST /api/v1/recommendations/{id}/deliver`
| Campo | Valor |
| --- | --- |
| Resumen | Marca la recomendación como entregada cuando el paciente la visualiza. Agenda recordatorio de feedback a 24h. |
| US/TS | US15 |
| Request body | — (solo header) |
| Idempotencia | **Sí — requerido** |
| Rate limit | `default-auth` |
| Respuestas | **204** · 400 · 404 `recommendation_not_found` · 409 (`conflict_state`, `recommendation_expired`, `idempotency_mismatch`) · 401 · 403 `recommendation_access_denied` · 429 · 500 |

### `POST /api/v1/recommendations/{id}/feedback`
| Campo | Valor |
| --- | --- |
| Resumen | Retroalimentación del paciente sobre una recomendación entregada. |
| US/TS | US16 |
| Request body | `SubmitFeedbackRequest` (wasApplied, outcome, comment?) |
| Idempotencia | **Sí — requerido** |
| Rate limit | `default-auth` |
| Respuestas | **204** · 400 · 404 `recommendation_not_found` · 409 (`conflict_state`, `idempotency_mismatch`) · 401 · 403 `recommendation_access_denied` · 429 · 500 |

---

## 7. Paciente / Derechos Ley 29733

`Policy = Patient` → respuestas comunes 401 · 403 · 500. Sin rate limit específico.

### `GET /api/v1/patients/me/export-data`
| Campo | Valor |
| --- | --- |
| Resumen | Exporta todos los datos (9 CSV en un ZIP) y devuelve una URL prefirmada (7 días). Portabilidad (US25). |
| US/TS | US25 |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `ExportMyDataResult` (URL + vencimiento) · 401 · 403 · 500 |

### `GET /api/v1/patients/me/consent/pdf`
| Campo | Valor |
| --- | --- |
| Resumen | Descarga el PDF del consentimiento aceptado (sin cifrar; dato propio). US01 CA04. |
| US/TS | US01 CA04 |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `application/pdf` (binario) · 404 `consent_record_not_found` · 401 · 403 · 500 |

### `DELETE /api/v1/patients/me`
| Campo | Valor |
| --- | --- |
| Resumen | Baja de cuenta = **anonimización** (no borrado físico) + disable en Keycloak. Derecho al olvido (US26). |
| US/TS | US26 |
| Query | `confirmedActivePilotAcknowledged` (bool) — si la cuenta está en piloto activo y es `false` → 409 |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **204** · 400 · 409 `active_pilot_retention` · 401 · 403 · 500 |

### `POST /api/v1/patients/me/report`
| Campo | Valor |
| --- | --- |
| Resumen | Autoreporte clínico del paciente (últimos 90 días). PDF cifrado; URL prefirmada + contraseña por correo aparte. |
| US/TS | US24 |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `GenerateMyClinicalReportResult` · 422 `patient_has_no_data_in_period` · 401 · 403 · 500 |

---

## 8. Paciente / Referencia

Catálogos y consulta. `GET /allergies` es transversal (cualquier autenticado); el resto es `default-auth`.

### `GET /api/v1/foods` · `GET /api/v1/foods/search` · `GET /api/v1/foods/{foodId}`
| Campo | Valor |
| --- | --- |
| Resumen | Catálogo de alimentos: listado paginado con filtros; búsqueda por nombre; detalle por id. |
| US/TS | TS12 |
| Autorización | Autenticado (cualquier rol) · `default-auth` |
| Query / Path | List: `page`, `pageSize`, `category?`, `fodmapLevel?` · Search: `q` · Detail: `{foodId}` |
| Idempotencia | — |
| Respuestas | List **200** `PagedResult<FoodItemSummary>` · Search **200** `FoodItemSummary[]` · Detail **200** `FoodItemDetail` (404 `food_item_not_found`) · 400 (list/search) · 401 · 429 · 500 |

### `GET /api/v1/foods/suggestions`
| Campo | Valor |
| --- | --- |
| Resumen | Sugerencias para el paciente: frecuentes (30d), recientes (24h) y una selección rotativa del catálogo. |
| US/TS | US09 CA03 |
| Autorización | **`Policy = Patient`** · `default-auth` |
| Request body | — |
| Idempotencia | — |
| Respuestas | **200** `FoodSuggestionsResult` · 401 · **403** · 429 · 500 |

### `GET /api/v1/allergies`
| Campo | Valor |
| --- | --- |
| Resumen | Catálogo de alergias activas (para declarar en el perfil / advertencias). |
| US/TS | US03, US10 (catálogo) |
| Autorización | Autenticado (cualquier rol) |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `AllergyCatalogItem[]` · 401 · 500 |

### `GET /api/v1/glossary` · `GET /api/v1/glossary/search`
| Campo | Valor |
| --- | --- |
| Resumen | Glosario clínico-nutricional; la definición depende del rol. Búsqueda insensible a tildes (`unaccent`). Contenido en borrador (`contentStatus`). |
| US/TS | US27 |
| Autorización | Autenticado (cualquier rol) · `default-auth` |
| Query | Search: `q` |
| Idempotencia | — |
| Respuestas | **200** `GlossaryResult` · 400 (search) · 401 · 429 · 500 |

---

## 9. Nutricionista / Panel y pacientes

`Policy = Nutritionist` → respuestas comunes 401 · 403 · 500. Sin rate limit específico.

### `GET /api/v1/nutritionists/me/patients`
| Campo | Valor |
| --- | --- |
| Resumen | Panel de triaje: pacientes asignados con `PriorityLevel`, IBS-SSS reciente, última actividad y revisiones vencidas. |
| US/TS | US18 |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `AssignedPatientSummary[]` · 401 · 403 · 500 |

### `GET /api/v1/nutritionists/me/patients/{patientUserId}`
| Campo | Valor |
| --- | --- |
| Resumen | Detalle clínico de un paciente asignado. Requiere asignación activa. |
| US/TS | US18 |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `GetAssignedPatientDetailResult` · 404 `patient_profile_not_found` · 401 · 403 `unauthorized_patient_access` · 500 |

### `GET /api/v1/nutritionists/me/patients/{patientId}/evolution`
| Campo | Valor |
| --- | --- |
| Resumen | Métricas de evolución (US21): serie IBS-SSS, variación vs línea base, respuesta clínica significativa, frecuencia de registro. |
| US/TS | US21 |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **200** `PatientEvolutionForNutritionistResult` · 401 · 403 `unauthorized_patient_access` · 500 |

---

## 10. Nutricionista / Recomendaciones HITL

`Policy = Nutritionist` · `default-auth` → respuestas comunes 401 · 403 · 429 · 500.

### `GET /api/v1/recommendations/pending-review`
| Campo | Valor |
| --- | --- |
| Resumen | Recomendaciones pendientes de revisión de los pacientes asignados, paginadas. |
| US/TS | US17 |
| Query | `page=1`, `pageSize=20` |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **200** `PagedResult<RecommendationSummaryDto>` · 400 · 401 · 403 · 429 · 500 |

### `POST /api/v1/recommendations/{id}/approve`
| Campo | Valor |
| --- | --- |
| Resumen | Aprueba con nota clínica (≥ 20 caracteres). |
| US/TS | US17 (CA01) |
| Request body | `ApproveRecommendationRequest` (note) |
| Idempotencia | **Sí — requerido** |
| Rate limit | `default-auth` |
| Respuestas | **204** · 400 · 404 `recommendation_not_found` · 409 (`conflict_state`, `recommendation_expired`, `idempotency_mismatch`) · 401 · 403 `recommendation_access_denied` · 429 · 500 |

### `POST /api/v1/recommendations/{id}/reject`
| Campo | Valor |
| --- | --- |
| Resumen | Rechaza con motivo. |
| US/TS | US17 |
| Request body | `RejectRecommendationRequest` (reason) |
| Idempotencia | **Sí — requerido** |
| Rate limit | `default-auth` |
| Respuestas | **204** · 400 · 404 `recommendation_not_found` · 409 (`conflict_state`, `recommendation_expired`, `idempotency_mismatch`) · 401 · 403 `recommendation_access_denied` · 429 · 500 |

### `POST /api/v1/recommendations/{id}/modify`
| Campo | Valor |
| --- | --- |
| Resumen | Aprueba tras modificar ítems y/o contenido (`PendingReview → ModifiedApproved`). |
| US/TS | US17 CA03 |
| Request body | `ModifyRecommendationRequest` (clinicalNote, items?, title?, description?, steps?) |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **204** · 400 · 404 `recommendation_not_found` · 409 (`conflict_state`, `recommendation_expired`) · 401 · 403 `recommendation_access_denied` · 429 · 500 |

### `POST /api/v1/recommendations/manual`
| Campo | Valor |
| --- | --- |
| Resumen | Crea manualmente una recomendación para un paciente asignado (queda `ManualApproved`). |
| US/TS | US29 |
| Request body | `CreateManualRecommendationRequest` (patientId, title, description, steps?, clinicalNote, validUntil?) |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **201** `CreateManualRecommendationResult` · 400 · 401 · 403 `recommendation_access_denied` · 429 · 500 |

### `POST /api/v1/recommendations/{id}/archive`
| Campo | Valor |
| --- | --- |
| Resumen | Archiva una recomendación en estado terminal aprobado (flag `IsActive=false`, no estado). |
| US/TS | US30 CA01 |
| Request body | `ArchiveRecommendationRequest` (reason: `ArchiveReason`) |
| Idempotencia | — |
| Rate limit | `default-auth` |
| Respuestas | **204** · 400 · 404 `recommendation_not_found` · 409 `recommendation_not_archivable` · 401 · 403 `recommendation_access_denied` · 429 · 500 |

---

## 11. Nutricionista / Reportes clínicos

### `POST /api/v1/reports/patients/{id}`
| Campo | Valor |
| --- | --- |
| Resumen | Genera el reporte clínico de un paciente en un período. PDF cifrado; URL prefirmada + contraseña por correo aparte. |
| US/TS | US22, TS11 |
| Autorización | `Policy = Nutritionist` · `default-auth` |
| Request body | `GenerateClinicalReportRequest` (periodStart, periodEnd) |
| Idempotencia | **Sí — requerido** (header `Idempotency-Key`) |
| Rate limit | `default-auth` |
| Respuestas | **202** `GenerateClinicalReportResult` · 400 · 409 `idempotency_mismatch` · 422 (`patient_has_no_data_in_period`, `report_period_invalid`) · 401 · 403 `report_access_denied` · 429 · 500 |

---

## 12. Nutricionista / Invitaciones

### `POST /api/v1/invitations`
| Campo | Valor |
| --- | --- |
| Resumen | Genera un código de invitación (vigencia 72h, uso único) para vincular a un paciente. |
| US/TS | US19 |
| Autorización | `Policy = Nutritionist` |
| Request body | — |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **201** `GenerateInvitationCodeResult` (código + expiración) · 401 · 403 · 500 |

---

## 13. Admin

### `POST /api/v1/admin/nutritionists`
| Campo | Valor |
| --- | --- |
| Resumen | Provisiona un nutricionista y le envía credenciales temporales. Protegido por clave de API, no JWT. |
| US/TS | US02, TS02 |
| Autorización | `[AdminApiKey]` — header **`X-Admin-Api-Key`** (401 si falta/incorrecta) |
| Request body | `CreateNutritionistRequest` (email, fullName) |
| Idempotencia | — |
| Rate limit | — |
| Respuestas | **201** `CreateNutritionistResult` · 400 · 401 (API key inválida) · 409 `duplicate_email` · 500 |
