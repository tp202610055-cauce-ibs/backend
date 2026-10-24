# Manifiesto de cuentas y datos para QA (D-2) — BORRADOR 1.1

Versión 1.1-draft2. Fuente: Casos_de_Prueba-PY20261049_v3.xlsx + reemplazos de catálogo propuestos (pendientes de Mirian). Archivo gemelo: `qa-manifest.json` (es el que lee el código; este documento es la versión legible).

**Este archivo no contiene contraseñas.** Cada rol toma la suya de una variable de entorno de la VM.

## 1. Cifras

- Casos de prueba: 90.
- Correos nombrados en los CP: 116.
- Cuentas a sembrar: **105** (89 pacientes y 16 nutricionistas).
- Correos que **no deben existir**: **11** (8 `registro.*`, `tester.sincuenta`, `noexiste.cp080` y `nutricionista.noexiste`).
- Casos que consumen su dato (una sola corrida sin reiniciar): 53.
- Casos atados al reloj: 9 por horas, 12 por días, 2 con fechas fijas.
- Casos con pasos para el operador: 29. Casos que usan el buzón de prueba: 13.
- Casos con notificación push: 7 deben recibirla (A), 3 no deben recibirla aunque haya algo que avisar (B), 2 no generan push (N).

## 2. Reglas globales de siembra

1. Contraseñas: NUNCA en este archivo ni en el repositorio. Cada rol toma la suya de una variable de entorno de la VM (QA_PATIENT_PASSWORD, QA_NUTRITIONIST_PASSWORD). Los comandos que borran o recrean cuentas exigen además QA_ALLOW_DESTRUCTIVE=1.
2. Toda cuenta sembrada nace habilitada, con el correo verificado, sin acciones pendientes en Keycloak y, si es paciente, con el consentimiento v1.0 aceptado. Excepciones: status pendingActivation (enlace de activación vigente en el buzón) y status disabled.
3. Ninguna cuenta lleva token FCM: lo registra la app en el teléfono donde el revisor inicia sesión.
4. Tiempo: todo «hace N horas/días» se ancla al instante en que corre seed-qa o reset-qa (hora de Lima para «hoy»). Los datos con clock=hours vencen en horas y los de clock=days se desplazan: el operador corre reset-qa del CP justo antes de la prueba.
5. Pendientes de revisión: ExpiresAt explícito a +10 días, no la ventana de 72 h. La expiración se evalúa también al leer o decidir (no solo en el worker), así que una pendiente sembrada con la ventana normal vence sola.
6. Una cuenta exclusiva de un CP se rearma borrándola y volviéndola a sembrar (reset-qa CPxxx). Las cuentas compartidas (nutricionista.demo, paciente.demo) no se borran: solo se rearman los pacientes del CP.
7. Los 11 correos mustNotExist deben NO existir antes de cada ejecución del CP; reset-qa los borra si el CP los creó (Keycloak y base de datos).
8. Nombres visibles: nutricionista.demo = «Ana Gómez»; paciente.vista = «María Fernanda López Quispe»; el resto son sintéticos (el CP no los fija).
9. Valores por omisión cuando el CP no los da: perfil clínico 62 kg, 165 cm, subtipo SII-D, sin alergias declaradas (el «Ninguna conocida» de los CP significa lista vacía).
10. Las explicaciones personalizadas NO se generan (no hay Ollama en la VM): se siembran con origen LlmGenerated (CP040) o con origen de respaldo (CP077). Ningún CP genera una recomendación en vivo.
11. Modo QA: workers IbsSssReminder, WeeklyReminder, RecommendationArchival y RecommendationExpiration apagados; OutboxDispatcher y NotificationDispatcher encendidos. Los disparos van por comandos a demanda.
12. Ítems de las recomendaciones del motor (status pendingReview, approvedAsGenerated, approvedModified, rejected, expired): suggests produce ítems Suggest, reduce produce Reduce y avoid produce Avoid, cada uno sobre un alimento del catálogo. El título del motor no se guarda: las apps muestran un título compuesto («Reducir 2 alimentos») y la descripción lista los alimentos. Solo las recomendaciones createdByNutritionist (manuales) llevan título propio; para ellas, si faltan description o clinicalNote, usa valores por omisión deterministas.
13. CP075: sus dos recomendaciones son manuales (createdByNutritionist) con ValidUntil = ancla - 2 días y ancla + 30 días; el archivado temporal solo aplica a recomendaciones con ValidUntil.
14. La siembra no debe generar notificaciones, correos ni filas de outbox como efecto secundario (salvo el correo de activación de las cuentas pendingActivation): CP037, CP046 y CP073 verifican que NO llegue un aviso.
15. Este manifiesto es el BORRADOR 1.1: aplica los reemplazos de catálogo propuestos a Mirian. Si ella acepta otros, solo cambian los textos de este archivo, no el código.

## 3. Catálogos: lo que los CP piden y el backend no tiene

ALINEADO con el catálogo actual del backend (26 alimentos, 10 alergias).

**Reemplazos aplicados (CP original → catálogo):**
- «Arroz con pollo» → «Causa rellena» (CP021, CP022, CP023, CP024, CP028, CP034, CP035, CP062, CP064, CP066, CP067, CP076, CP087)
- «Papa a la huancaína» → «Papa amarilla cocida» (CP024)
- «Ají de gallina» → «Aji de gallina» (CP062 (el catálogo lo escribe sin tilde))
- «Lomo de res» → «Carne de res magra» (CP025)
- «Cebolla» → «Cebolla cocida» (CP025 y recomendaciones)
- «Tomate» → «Zanahoria cocida» (CP025)
- «Arroz» → «Arroz blanco cocido» (CP026)
- «Ajo» → «Pan de trigo blanco» (16 CP con «cebolla y ajo»: CP036, CP038, CP040, CP041, CP042, CP043, CP044, CP047, CP048, CP058, CP062, CP064, CP068, CP074, CP077, CP086)
- «Maní tostado + alergia Maní» → «Queso fresco + alergia Lactosa» (CP027)

- Todo alimento y alergia que aparece en una siembra debe existir en el catálogo; si falta, seed-qa falla antes de escribir nada y lista TODO lo que falta.
- Heurística por nombre (AllergyHeuristicMatcher). «Maní» no tiene regla; «Frutos secos» sí. CP027 usa Lactosa + Queso fresco.
- «Ninguna conocida» no es una alergia del catálogo: se siembra sin alergias declaradas (lista vacía).

## 4. Cuentas

| Correo | Rol | Estado | Usado por |
|---|---|---|---|
| registro.cp001 | — | **NO debe existir** | CP001 |
| paciente.demo | patient | active | CP002, CP004, CP014, CP017, CP020, CP080 |
| registro.cp002 | — | **NO debe existir** | CP002 |
| registro.cp003 | — | **NO debe existir** | CP003 |
| nutricionista.nuevo | nutritionist | pendingActivation | CP005 |
| nutricionista.demo («Ana Gómez») | nutritionist | active | CP006, CP009, CP012, CP016, CP017, CP030, CP031, CP034, CP036, CP037, CP038, CP040, CP041, CP042, CP043, CP044, CP045, CP046, CP047, CP048, CP056, CP057, CP068, CP069, CP070, CP072, CP073, CP074, CP075, CP077, CP086, CP087, CP090 |
| nutricionista.inactivo | nutritionist | disabled | CP006, CP017 |
| nutricionista.noexiste | — | **NO debe existir** | CP006 |
| nutricionista.pendiente | nutritionist | pendingActivation | CP006, CP017 |
| paciente.perfil1 | patient | active | CP007 |
| paciente.perfil2 | patient | active | CP008 |
| paciente.edicion | patient | active | CP009 |
| paciente.perfil3 | patient | active | CP010 |
| paciente.perfil4 | patient | active | CP011 |
| paciente.base | patient | active | CP012 |
| paciente.base2 | patient | active | CP013 |
| paciente.bloqueo | patient | active | CP015 |
| nutricionista.reset1 | nutritionist | active | CP018 |
| paciente.reset1 | patient | active | CP018 |
| tester.sincuenta | — | **NO debe existir** | CP018, CP081 |
| paciente.reset2 | patient | active | CP019 |
| nutricionista.sesion | nutritionist | active | CP021 |
| paciente.sesion | patient | active | CP021 |
| paciente.diario | patient | active | CP022 |
| paciente.offline | patient | active | CP023 |
| paciente.sugerencias | patient | active | CP024 |
| paciente.platos | patient | active | CP025 |
| paciente.platos2 | patient | active | CP026 |
| paciente.alergia | patient | active | CP027 |
| paciente.sintomas | patient | active | CP028 |
| paciente.sintomas2 | patient | active | CP029 |
| paciente.cuest | patient | active | CP030 |
| paciente.recordatorio1 | patient | active | CP031 |
| paciente.recordatorio2 | patient | active | CP031 |
| paciente.resultado2 | patient | active | CP032 |
| paciente.resultado | patient | active | CP032 |
| paciente.sinpuntaje | patient | active | CP033 |
| paciente.notas | patient | active | CP034 |
| paciente.notas2 | patient | active | CP035 |
| paciente.entrega | patient | active | CP036 |
| paciente.revision | patient | active | CP037 |
| paciente.filtro | patient | active | CP038 |
| paciente.resumen2 | patient | active | CP039 |
| paciente.resumen3 | patient | active | CP039 |
| paciente.resumen | patient | active | CP039 |
| paciente.explic | patient | active | CP040 |
| paciente.revisada | patient | active | CP041 |
| paciente.bloques | patient | active | CP042 |
| paciente.feedback | patient | active | CP043 |
| paciente.feedback2 | patient | active | CP044 |
| paciente.aprob | patient | active | CP045 |
| paciente.rechazo | patient | active | CP046 |
| paciente.modif | patient | active | CP047 |
| paciente.inmutable | patient | active | CP048 |
| nutricionista.panel | nutritionist | active | CP049 |
| paciente.panel1 | patient | active | CP049 |
| paciente.panel2 | patient | active | CP049 |
| paciente.panel3 | patient | active | CP049 |
| paciente.panel4 | patient | active | CP049 |
| paciente.panel5 | patient | active | CP049 |
| paciente.panel6 | patient | active | CP049 |
| nutricionista.sinpacientes | nutritionist | active | CP050 |
| nutricionista.error | nutritionist | active | CP051 |
| nutricionista.codigo1 | nutritionist | active | CP052 |
| nutricionista.codigo2 | nutritionist | active | CP053 |
| paciente.vinc1 | patient | active | CP053 |
| registro.cp053 | — | **NO debe existir** | CP053 |
| registro.cp053b | — | **NO debe existir** | CP053 |
| nutricionista.vinc | nutritionist | active | CP054 |
| registro.cp054 | — | **NO debe existir** | CP054 |
| registro.cp055 | — | **NO debe existir** | CP055 |
| paciente.evol2 | patient | active | CP056 |
| paciente.evol | patient | active | CP056 |
| paciente.linea | patient | active | CP057 |
| nutricionista.reporte | nutritionist | active | CP058 |
| paciente.reporte1 | patient | active | CP058 |
| nutricionista.reporte2 | nutritionist | active | CP059 |
| paciente.reporte2 | patient | active | CP059 |
| paciente.graf | patient | active | CP060 |
| paciente.solobase | patient | active | CP061 |
| paciente.reporte | patient | active | CP062 |
| paciente.sindatos | patient | active | CP063 |
| paciente.export | patient | active | CP064 |
| paciente.export2 | patient | active | CP065 |
| paciente.eliminar | patient | active | CP066 |
| paciente.cancelar | patient | active | CP067 |
| paciente.glosario | patient | active | CP068 |
| paciente.glosario2 | patient | active | CP069 |
| paciente.vista2 | patient | active | CP070 |
| paciente.vista3 | patient | active | CP070 |
| paciente.vista («María Fernanda López Quispe») | patient | active | CP070 |
| paciente.editar | patient | active | CP071 |
| paciente.manual | patient | active | CP072 |
| paciente.ajeno | patient | active | CP073 |
| paciente.manual2 | patient | active | CP073 |
| paciente.archivar | patient | active | CP074 |
| paciente.vencida | patient | active | CP075 |
| paciente.diario2 | patient | active | CP076 |
| paciente.respaldo | patient | active | CP077 |
| paciente.editar2 | patient | active | CP078 |
| nutricionista.activar | nutritionist | pendingActivation | CP079 |
| noexiste.cp080 | — | **NO debe existir** | CP080 |
| paciente.inactivo | patient | disabled | CP081 |
| paciente.reset4 | patient | active | CP081 |
| paciente.reset3 | patient | active | CP082 |
| paciente.sintomas3 | patient | active | CP083 |
| paciente.notif | patient | active | CP084 |
| paciente.sinnutri | patient | active | CP085 |
| paciente.modexito | patient | active | CP086 |
| paciente.vinculo2 | patient | active | CP087 |
| paciente.vinculo | patient | active | CP087 |
| paciente.codigo | patient | active | CP088 |
| registro.cp088 | — | **NO debe existir** | CP088 |
| nutricionista.glosario | nutritionist | active | CP089 |
| paciente.glosario3 | patient | active | CP089 |
| paciente.manual3 | patient | active | CP090 |

## 5. Datos por caso de prueba

Leyenda de banderas: `consumido` = la prueba gasta el dato (reset-qa antes de repetirla); `reloj` = horas / días / fecha fija; `push` = A recibe, B no debe recibir, N no genera; `buzón` = usa el buzón de prueba; `operador` = pide algo al equipo de desarrollo.

### CP001 — REGISTRO EXITOSO CON DATOS VÁLIDOS
*Plataforma:* mobile. *Banderas:* consumido, buzón, operador.
- Deben NO existir: registro.cp001
- Registro desde la app; el consentimiento aceptado debe quedar registrado.
- **Operador, paso 11:** Pedir al equipo de desarrollo que confirme el registro de la aceptación del consentimiento de esa cuenta.

### CP002 — RECHAZO POR DATOS INVÁLIDOS O CORREO DUPLICADO
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.demo
- Deben NO existir: registro.cp002

### CP003 — BLOQUEO SIN ACEPTACIÓN DEL CONSENTIMIENTO INFORMADO
*Plataforma:* mobile. *Banderas:* ninguna (estático).
- Deben NO existir: registro.cp003

### CP004 — CONSULTA DEL CONSENTIMIENTO ACEPTADO
*Plataforma:* mobile. *Banderas:* ninguna (estático).
- Cuentas: paciente.demo
- nota: paciente.demo: consentimiento v1.0 aceptado al registrarse.

### CP005 — PRIMER ACCESO EXITOSO TRAS ACTIVAR LA CUENTA
*Plataforma:* portal. *Banderas:* consumido, buzón.
- Cuentas: nutricionista.nuevo
- cuenta nutricionista.nuevo: nutritionist, estado **pendingActivation**
- nota: Correo de activación con enlace vigente en el buzón de prueba. reset-qa debe reenviarlo.

### CP006 — RECHAZO POR CREDENCIALES INCORRECTAS O CUENTA INACTIVA
*Plataforma:* portal. *Banderas:* operador.
- Cuentas: nutricionista.demo, nutricionista.inactivo, nutricionista.pendiente
- Deben NO existir: nutricionista.noexiste
- cuenta nutricionista.inactivo: nutritionist, estado **disabled**
- cuenta nutricionista.pendiente: nutritionist, estado **pendingActivation**
- **Operador, paso 7:** Pedir al equipo de desarrollo que muestre el registro de los intentos de ingreso de las cuatro variantes.

### CP007 — PERFIL CLÍNICO GUARDADO CORRECTAMENTE
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.perfil1
- perfil clínico **incompleto** (paciente.perfil1)

### CP008 — BLOQUEO POR CAMPOS INCOMPLETOS O VALORES FUERA DE RANGO
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.perfil2
- perfil clínico **incompleto** (paciente.perfil2)

### CP009 — EDICIÓN POSTERIOR DEL PERFIL
*Plataforma:* both. *Banderas:* consumido.
- Cuentas: nutricionista.demo, paciente.edicion
- perfil completo paciente.edicion: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.edicion → nutricionista.demo

### CP010 — CÁLCULO EN VIVO DEL ÍNDICE DE MASA CORPORAL
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.perfil3
- perfil clínico **incompleto** (paciente.perfil3)

### CP011 — APLAZAMIENTO DEL ONBOARDING SIN BLOQUEO DE ACCESO
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.perfil4
- perfil clínico **incompleto** (paciente.perfil4)
- nota: Sin ningún avance de onboarding guardado.

### CP012 — LÍNEA BASE REGISTRADA EXITOSAMENTE
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: nutricionista.demo, paciente.base
- perfil completo paciente.base: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.base → nutricionista.demo
- nota: Sin línea base IBS-SSS todavía.

### CP013 — BLOQUEO POR CUESTIONARIO INCOMPLETO
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.base2
- perfil completo paciente.base2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- nota: Sin línea base IBS-SSS; sin nutricionista.

### CP014 — AUTENTICACIÓN EXITOSA
*Plataforma:* mobile. *Banderas:* operador.
- Cuentas: paciente.demo
- nota: Sin intentos fallidos de ingreso (limpiar bloqueo de Keycloak).
- **Operador, paso 6:** Pedir al equipo de desarrollo que muestre el registro de accesos de esa cuenta.

### CP015 — BLOQUEO POR INTENTOS FALLIDOS CONSECUTIVOS
*Plataforma:* mobile. *Banderas:* consumido, operador.
- Cuentas: paciente.bloqueo
- nota: No bloqueado y sin intentos fallidos. reset-qa limpia el estado de fuerza bruta de Keycloak.
- **Operador, paso 9:** Pedir al equipo de desarrollo que muestre el registro de seguridad de esa cuenta.

### CP016 — ACCESO EXITOSO AL PORTAL CLÍNICO
*Plataforma:* portal. *Banderas:* operador.
- Cuentas: nutricionista.demo
- **Operador, paso 7:** Pedir al equipo de desarrollo que muestre el registro de accesos de esa cuenta.

### CP017 — RECHAZO POR CUENTA INACTIVA O CREDENCIALES INCORRECTAS
*Plataforma:* portal. *Banderas:* operador.
- Cuentas: nutricionista.demo, nutricionista.inactivo, nutricionista.pendiente, paciente.demo
- **Operador, paso 7:** Pedir al equipo de desarrollo que muestre el registro de los intentos de ingreso de las cuatro variantes.

### CP018 — ENLACE DE RECUPERACIÓN ENVIADO EXITOSAMENTE
*Plataforma:* both. *Banderas:* consumido, buzón, operador.
- Cuentas: nutricionista.reset1, paciente.reset1
- Deben NO existir: tester.sincuenta
- **Operador, paso 10:** Pedir al equipo de desarrollo que muestre el registro de las solicitudes de recuperación de las dos cuentas activas.

### CP019 — ENLACE EXPIRADO O YA UTILIZADO
*Plataforma:* mobile. *Banderas:* consumido, buzón.
- Cuentas: paciente.reset2
- La prueba dura unos 30 minutos.
- La prueba dura más de 30 min (enlace que vence).

### CP020 — CIERRE DE SESIÓN EXITOSO
*Plataforma:* mobile. *Banderas:* ninguna (estático).
- Cuentas: paciente.demo

### CP021 — CIERRE AUTOMÁTICO DE SESIÓN POR INACTIVIDAD
*Plataforma:* both. *Banderas:* consumido, reloj: días.
- Cuentas: nutricionista.sesion, paciente.sesion
- perfil completo paciente.sesion: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.sesion: «Causa rellena», 150 g, almuerzo, hoy
- vínculo paciente.sesion → nutricionista.sesion
- La prueba dura unos 20 minutos.

### CP022 — REGISTRO EXITOSO DE COMIDA CON PORCIÓN DEFINIDA
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.diario
- perfil completo paciente.diario: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- nota: Ninguna comida registrada hoy. Requiere «Causa rellena» en el catálogo.

### CP023 — REGISTRO OFFLINE CON SINCRONIZACIÓN DIFERIDA
*Plataforma:* mobile. *Banderas:* consumido, operador.
- Cuentas: paciente.offline
- perfil completo paciente.offline: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- nota: Ninguna comida registrada hoy.
- **Operador, paso 10:** Pedir al equipo de desarrollo que confirme que la comida llegó al servidor.

### CP024 — BÚSQUEDA CON SUGERENCIAS CONTEXTUALES POR TRES DIMENSIONES
*Plataforma:* mobile. *Banderas:* reloj: horas.
- Cuentas: paciente.sugerencias
- perfil completo paciente.sugerencias: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.sugerencias: «Causa rellena», hace 2 d
- comida paciente.sugerencias: «Causa rellena», hace 7 d
- comida paciente.sugerencias: «Causa rellena», hace 12 d
- comida paciente.sugerencias: «Causa rellena», hace 18 d
- comida paciente.sugerencias: «Causa rellena», hace 25 d
- comida paciente.sugerencias: «Papa amarilla cocida», hace 12 h
- comida paciente.sugerencias: «Lomo saltado», hace 30 h
- nota: Causa rellena debe ser el alimento más registrado (5 veces en 30 días). La búsqueda por «quin» devuelve «Quinua blanca cocida».

### CP025 — CREACIÓN EXITOSA DE ALIMENTO PERSONALIZADO CON INGREDIENTES DEL CATÁLOGO
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.platos
- perfil completo paciente.platos: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- nota: Sin alimentos personalizados. «Lomo saltado casero» NO debe existir en el catálogo (el catálogo tiene «Lomo saltado», que no coincide con la búsqueda completa). Ingredientes del catálogo: Carne de res magra, Cebolla cocida y Zanahoria cocida.

### CP026 — RECHAZO POR NOMBRE DUPLICADO O SIN INGREDIENTES SELECCIONADOS
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.platos2
- perfil completo paciente.platos2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- alimento personalizado paciente.platos2: «Mi arroz con pollo» con ingredientes Arroz blanco cocido
- nota: Es el único alimento personalizado. El nombre «Mi arroz con pollo» es libre; su ingrediente es «Arroz blanco cocido».

### CP027 — ADVERTENCIA POR ALÉRGENO DETECTADO EN INGREDIENTES SELECCIONADOS
*Plataforma:* mobile. *Banderas:* consumido, operador.
- Cuentas: paciente.alergia
- perfil completo paciente.alergia: 62 kg, 165 cm, SII-D, alergias: Lactosa
- nota: Alergia declarada «Lactosa» (el backend detecta alérgenos por reglas sobre el nombre del alimento; «Maní» no tiene regla). Ingrediente que dispara la advertencia: «Queso fresco». Plato del CP: «Salsa de queso casera». La advertencia muestra el nombre de la alergia: «Lactosa».
- **Operador, paso 8:** Pedir al equipo de desarrollo que muestre el registro de la decisión del paciente.

### CP028 — REGISTRO EXITOSO DE SÍNTOMA CON CATÁLOGO CLÍNICAMENTE EXTENDIDO
*Plataforma:* mobile. *Banderas:* consumido, reloj: horas.
- Cuentas: paciente.sintomas
- perfil completo paciente.sintomas: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.sintomas: «Causa rellena», hace 2 h
- nota: Sin síntomas hoy. La comida debe quedar dentro de la ventana de 4 h al momento de la prueba.

### CP029 — REGISTRO DE SÍNTOMA SIN COMIDA RECIENTE ASOCIADA
*Plataforma:* mobile. *Banderas:* consumido, reloj: horas.
- Cuentas: paciente.sintomas2
- perfil completo paciente.sintomas2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.sintomas2: «Causa rellena», hace 5 h
- nota: Ninguna comida en las últimas 4 h.

### CP030 — CUESTIONARIO PERIÓDICO COMPLETADO EXITOSAMENTE
*Plataforma:* mobile. *Banderas:* consumido, reloj: días, push N.
- Cuentas: nutricionista.demo, paciente.cuest
- perfil completo paciente.cuest: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.cuest → nutricionista.demo
- IBS-SSS baseline paciente.cuest: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.cuest: 300 puntos, hace 14 d
- cuestionario pendiente (paciente.cuest): Último cuestionario hace 14 días: tiene pendiente el de las dos semanas.

### CP031 — RECORDATORIO ANTE CUESTIONARIO NO COMPLETADO EN EL PLAZO INICIAL
*Plataforma:* unknown. *Banderas:* consumido, reloj: horas, push A.
- Cuentas: nutricionista.demo, paciente.recordatorio1, paciente.recordatorio2
- perfil completo paciente.recordatorio1: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.recordatorio1 → nutricionista.demo
- perfil completo paciente.recordatorio2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.recordatorio2 → nutricionista.demo
- invitación IBS-SSS paciente.recordatorio1: enviada hace 48 h, sin responder
- invitación IBS-SSS paciente.recordatorio2: enviada hace 9 d, sin responder
- nota: Sin token FCM sembrado: lo registra la app en el teléfono del revisor al iniciar sesión. El recordatorio lo dispara process-ibs-sss.

### CP032 — VISUALIZACIÓN DEL RESULTADO DESPUÉS DE ENVIAR EL CUESTIONARIO
*Plataforma:* unknown. *Banderas:* consumido.
- Cuentas: paciente.resultado2, paciente.resultado
- perfil completo paciente.resultado: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- IBS-SSS baseline paciente.resultado: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.resultado: 280 puntos, hace 14 d
- cuestionario pendiente (paciente.resultado): Cuestionario de las dos semanas pendiente.
- perfil completo paciente.resultado2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- IBS-SSS baseline paciente.resultado2: 320 puntos, hace 14 d
- cuestionario pendiente (paciente.resultado2): Cuestionario de las dos semanas pendiente.

### CP033 — SIN EXPOSICIÓN DEL PUNTAJE ANTES DEL ENVÍO
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.sinpuntaje
- perfil completo paciente.sinpuntaje: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- IBS-SSS baseline paciente.sinpuntaje: 320 puntos, hace 14 d
- cuestionario pendiente (paciente.sinpuntaje): Cuestionario de las dos semanas pendiente.

### CP034 — NOTA DE CONTEXTO GUARDADA Y ASOCIADA CORRECTAMENTE
*Plataforma:* both. *Banderas:* consumido.
- Cuentas: nutricionista.demo, paciente.notas
- perfil completo paciente.notas: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.notas → nutricionista.demo
- comida paciente.notas: «Causa rellena», almuerzo, hoy
- síntoma paciente.notas: «Dolor abdominal», hoy
- nota: Ambos registros de hoy sin nota.

### CP035 — NOTA VACÍA O QUE SUPERA EL LÍMITE DE CARACTERES
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.notas2
- perfil completo paciente.notas2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.notas2: «Causa rellena», almuerzo, hoy
- nota: Registro de hoy sin nota.

### CP036 — ENTREGA EXITOSA DE RECOMENDACIÓN APROBADA
*Plataforma:* portal. *Banderas:* consumido, push A.
- Cuentas: nutricionista.demo, paciente.entrega
- perfil completo paciente.entrega: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.entrega → nutricionista.demo
- recomendación paciente.entrega: «Reducir cebolla y pan de trigo», estado **pendingReview**; explicación: personalized; vence en (d): 10; sugiere: Zanahoria cocida; sustitutos: con justificación por contenido de FODMAP

### CP037 — AVISO EN LA APLICACIÓN CUANDO LA RECOMENDACIÓN ESTÁ EN REVISIÓN
*Plataforma:* unknown. *Banderas:* push B.
- Cuentas: nutricionista.demo, paciente.revision
- perfil completo paciente.revision: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.revision → nutricionista.demo
- recomendación paciente.revision: «Recomendación en revisión», estado **pendingReview**; explicación: personalized; vence en (d): 10
- nota: Es su única recomendación y no tiene ninguna aprobada. El aviso «en revisión» ya no se envía.

### CP038 — SOLO SE MUESTRAN AL PACIENTE LAS RECOMENDACIONES APROBADAS
*Plataforma:* unknown. *Banderas:* ninguna (estático).
- Cuentas: nutricionista.demo, paciente.filtro
- perfil completo paciente.filtro: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.filtro → nutricionista.demo
- recomendación paciente.filtro: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; abierta por el paciente: False
- recomendación paciente.filtro: «Aumentar la fibra soluble», estado **approvedModified**; abierta por el paciente: True; sugiere: Avena en hojuelas cocida, Zanahoria cocida
- recomendación paciente.filtro: «Hidratación entre comidas», estado **createdByNutritionist**; abierta por el paciente: True; respondida: True
- recomendación paciente.filtro: «Probar avena en el desayuno», estado **pendingReview**; vence en (d): 10; sugiere: Avena en hojuelas cocida
- recomendación paciente.filtro: «Eliminar todos los lácteos», estado **rejected**; evitar: Leche entera de vaca, Queso fresco
- recomendación paciente.filtro: «Reducir legumbres», estado **expired**; nota: Vencida sin haber sido revisada.

### CP039 — VISUALIZACIÓN RESUMIDA DE EVOLUCIÓN EN LA PANTALLA DE INICIO
*Plataforma:* unknown. *Banderas:* ninguna (estático).
- Cuentas: paciente.resumen2, paciente.resumen3, paciente.resumen
- perfil completo paciente.resumen: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- IBS-SSS baseline paciente.resumen: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.resumen: 280 puntos, hace 14 d
- IBS-SSS periodic paciente.resumen: 245 puntos, hoy
- perfil completo paciente.resumen2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- IBS-SSS baseline paciente.resumen2: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.resumen2: 300 puntos, hace 14 d
- IBS-SSS periodic paciente.resumen2: 290 puntos, hoy
- perfil completo paciente.resumen3: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- IBS-SSS baseline paciente.resumen3: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.resumen3: 300 puntos, hace 14 d
- IBS-SSS periodic paciente.resumen3: 270 puntos, hoy

### CP040 — EXPLICACIÓN Y NIVEL DE CONFIANZA VISIBLES CORRECTAMENTE
*Plataforma:* mobile. *Banderas:* ninguna (estático).
- Cuentas: nutricionista.demo, paciente.explic
- perfil completo paciente.explic: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.explic → nutricionista.demo
- recomendación paciente.explic: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; explicación: personalized; origen: LlmGenerated; confianza: high
- recomendación paciente.explic: «Aumentar la fibra soluble», estado **approvedAsGenerated**; explicación: personalized; origen: LlmGenerated; confianza: medium; sugiere: Avena en hojuelas cocida, Zanahoria cocida
- recomendación paciente.explic: «Reducir legumbres», estado **approvedAsGenerated**; explicación: personalized; origen: LlmGenerated; confianza: low
- nota: Historial con dolor abdominal después de comidas con cebolla y pan de trigo. Las 3 explicaciones se siembran (no hay servicio de IA en la VM).

### CP041 — INDICACIÓN VISIBLE DE REVISIÓN POR EL NUTRICIONISTA
*Plataforma:* mobile. *Banderas:* ninguna (estático).
- Cuentas: nutricionista.demo, paciente.revisada
- perfil completo paciente.revisada: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.revisada → nutricionista.demo
- recomendación paciente.revisada: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; nota clínica: Reducir cebolla y pan de trigo por intolerancia a FODMAP
- recomendación paciente.revisada: «Aumentar la fibra soluble», estado **approvedModified**; nota clínica: Aumentar la fibra de forma gradual; sugiere: Avena en hojuelas cocida, Zanahoria cocida
- recomendación paciente.revisada: «Hidratación entre comidas», estado **createdByNutritionist**; nota clínica: Tomar agua entre comidas y no durante ellas

### CP042 — DETALLE ESTRUCTURADO EN CUATRO BLOQUES DIFERENCIADOS
*Plataforma:* mobile. *Banderas:* ninguna (estático).
- Cuentas: nutricionista.demo, paciente.bloques
- perfil completo paciente.bloques: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.bloques → nutricionista.demo
- recomendación paciente.bloques: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; explicación: personalized; nota clínica: Reducir cebolla y pan de trigo por intolerancia a FODMAP; pasos: 3; datos de respaldo: {"painRecords": 4, "windowDays": 14, "afterMeals": "cebolla"}; la explicación menciona: clasificaciones FODMAP

### CP043 — RETROALIMENTACIÓN REGISTRADA EXITOSAMENTE
*Plataforma:* unknown. *Banderas:* consumido, reloj: horas, push N, operador.
- Cuentas: nutricionista.demo, paciente.feedback
- perfil completo paciente.feedback: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.feedback → nutricionista.demo
- recomendación paciente.feedback: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; recibida: hace 30 h; respondida: False
- recomendación paciente.feedback: «Aumentar la fibra soluble», estado **approvedAsGenerated**; recibida: hace 20 h; respondida: False; sugiere: Avena en hojuelas cocida, Zanahoria cocida
- recomendación paciente.feedback: «Hidratación entre comidas», estado **createdByNutritionist**; recibida: hace 3 d; respondida: False
- nota: Límite de 24 h para responder: la de 20 h sí admite respuesta y la de 30 h no (verificar contra el CP).
- **Operador, paso 11:** Pedir al equipo de desarrollo que confirme las respuestas registradas de esta cuenta.

### CP044 — RETROALIMENTACIÓN GUARDADA LOCALMENTE SIN CONEXIÓN
*Plataforma:* unknown. *Banderas:* consumido, reloj: horas, operador.
- Cuentas: nutricionista.demo, paciente.feedback2
- perfil completo paciente.feedback2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.feedback2 → nutricionista.demo
- recomendación paciente.feedback2: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; recibida: hace 30 h; respondida: False
- **Operador, paso 10:** Pedir al equipo de desarrollo que confirme las respuestas registradas de esta cuenta.

### CP045 — APROBACIÓN EXITOSA DE RECOMENDACIÓN CON NOTA CLÍNICA
*Plataforma:* portal. *Banderas:* consumido, push A, operador.
- Cuentas: nutricionista.demo, paciente.aprob
- perfil completo paciente.aprob: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.aprob → nutricionista.demo
- recomendación paciente.aprob: «Recomendación pendiente de revisión», estado **pendingReview**; explicación: personalized; vence en (d): 10; con historial reciente: True
- nota: Historial reciente de comidas y síntomas visible para el nutricionista.
- **Operador, paso 9:** Pedir al equipo de desarrollo que muestre el registro de la decisión.

### CP046 — RECHAZO DE RECOMENDACIÓN CON JUSTIFICACIÓN CLÍNICA OBLIGATORIA
*Plataforma:* portal. *Banderas:* consumido, push B, operador.
- Cuentas: nutricionista.demo, paciente.rechazo
- perfil completo paciente.rechazo: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.rechazo → nutricionista.demo
- recomendación paciente.rechazo: «Eliminar todos los lácteos», estado **pendingReview**; explicación: personalized; vence en (d): 10; evitar: Leche entera de vaca, Queso fresco
- **Operador, paso 8:** Pedir al equipo de desarrollo que muestre el registro del rechazo.

### CP047 — VALIDACIÓN OBLIGATORIA DE NOTA CLÍNICA AL MODIFICAR
*Plataforma:* portal. *Banderas:* consumido.
- Cuentas: nutricionista.demo, paciente.modif
- perfil completo paciente.modif: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.modif → nutricionista.demo
- recomendación paciente.modif: «Reducir cebolla y pan de trigo», estado **pendingReview**; explicación: personalized; vence en (d): 10

### CP048 — INMUTABILIDAD POST-APROBACIÓN
*Plataforma:* portal. *Banderas:* operador.
- Cuentas: nutricionista.demo, paciente.inmutable
- perfil completo paciente.inmutable: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.inmutable → nutricionista.demo
- recomendación paciente.inmutable: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**
- recomendación paciente.inmutable: «Aumentar la fibra soluble», estado **approvedModified**; sugiere: Avena en hojuelas cocida, Zanahoria cocida
- recomendación paciente.inmutable: «Hidratación entre comidas», estado **createdByNutritionist**; explicación: none
- recomendación paciente.inmutable: «Eliminar todos los lácteos», estado **rejected**; evitar: Leche entera de vaca, Queso fresco
- **Operador, paso 9:** Pedir al equipo de desarrollo que muestre el registro de los intentos de cambio.

### CP049 — PANEL CARGADO CON PRIORIZACIÓN CLÍNICA CORRECTA
*Plataforma:* portal. *Banderas:* reloj: horas.
- Cuentas: nutricionista.panel, paciente.panel1, paciente.panel2, paciente.panel3, paciente.panel4, paciente.panel5, paciente.panel6
- cuenta nutricionista.panel: nutritionist, estado **active**
- perfil completo paciente.panel1: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.panel1 → nutricionista.panel
- perfil completo paciente.panel2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.panel2 → nutricionista.panel
- perfil completo paciente.panel3: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.panel3 → nutricionista.panel
- perfil completo paciente.panel4: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.panel4 → nutricionista.panel
- perfil completo paciente.panel5: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.panel5 → nutricionista.panel
- perfil completo paciente.panel6: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.panel6 → nutricionista.panel
- IBS-SSS periodic paciente.panel1: 340 puntos, hace 1 d
- IBS-SSS periodic paciente.panel2: 180 puntos, hace 1 d
- recomendación paciente.panel2: «Recomendación pendiente», estado **pendingReview**; vence en (d): 10; pendiente desde: hace 30 h
- IBS-SSS periodic paciente.panel3: 300 puntos, hace 1 d
- IBS-SSS periodic paciente.panel4: 200 puntos, hace 1 d
- recomendación paciente.panel4: «Recomendación pendiente», estado **pendingReview**; vence en (d): 10; pendiente desde: hace 20 h
- IBS-SSS periodic paciente.panel5: 200 puntos, hace 1 d
- IBS-SSS periodic paciente.panel6: 150 puntos, hace 1 d
- nota: P1,P2,P3,P4,P6 con registros al día (comidas y síntomas hoy). P5 sin ningún registro de comida ni síntoma desde hace 4 días. El límite de 24 h separa a P2 (30 h) de P4 (20 h): reset-qa antes de cada prueba.

### CP050 — PANEL INFORMATIVO SIN PACIENTES ASIGNADOS
*Plataforma:* portal. *Banderas:* ninguna (estático).
- Cuentas: nutricionista.sinpacientes

### CP051 — MANEJO DE ERROR AL CARGAR EL PANEL
*Plataforma:* portal. *Banderas:* operador, HORARIO EXCLUSIVO.
- Cuentas: nutricionista.error, paciente.ajeno
- vínculo paciente.ajeno → nutricionista.error
- nota: Cumple «al menos un paciente asignado» con paciente.ajeno (cuenta compartida con CP073).
- La variante B tumba la API para todos: horario exclusivo.
- **Operador, paso 7:** Variante B (servicio no disponible): pedir al equipo de desarrollo que deje el portal no disponible y volver a abrir el panel desde el menú.
- **Operador, paso 9:** Pedir al equipo de desarrollo que restablezca el servicio y pulsar la opción de reintentar.

### CP052 — CÓDIGO DE INVITACIÓN GENERADO CORRECTAMENTE
*Plataforma:* portal. *Banderas:* consumido, operador.
- Cuentas: nutricionista.codigo1
- **Operador, paso 6:** Pedir al equipo de desarrollo que muestre el registro de los dos códigos.

### CP053 — CÓDIGO EXPIRADO O YA UTILIZADO AL INTENTAR REGISTRARSE
*Plataforma:* both. *Banderas:* consumido, reloj: horas.
- Cuentas: nutricionista.codigo2, paciente.vinc1
- Deben NO existir: registro.cp053, registro.cp053b
- código de invitación de nutricionista.codigo2: expired, generado hace 4 d
- código de invitación de nutricionista.codigo2: used, usado por otro paciente
- nota: El operador entrega el valor de cada código al revisor.

### CP054 — VINCULACIÓN EXITOSA CON CÓDIGO VÁLIDO
*Plataforma:* both. *Banderas:* consumido, operador.
- Cuentas: nutricionista.vinc
- Deben NO existir: registro.cp054
- **Operador, paso 8:** Pedir al equipo de desarrollo que confirme el estado del código del paso 1.

### CP055 — REGISTRO SIN CÓDIGO DE INVITACIÓN
*Plataforma:* mobile. *Banderas:* ninguna (estático).
- Deben NO existir: registro.cp055

### CP056 — GRÁFICO DE EVOLUCIÓN DISPONIBLE CON DATOS SUFICIENTES
*Plataforma:* portal. *Banderas:* reloj: días.
- Cuentas: nutricionista.demo, paciente.evol2, paciente.evol
- perfil completo paciente.evol: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.evol → nutricionista.demo
- IBS-SSS baseline paciente.evol: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.evol: 280 puntos, hace 14 d
- IBS-SSS periodic paciente.evol: 245 puntos, hoy
- actividad paciente.evol: {"daysWithRecordsLast14": 10}
- perfil completo paciente.evol2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.evol2 → nutricionista.demo
- IBS-SSS baseline paciente.evol2: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.evol2: 290 puntos, hace 14 d

### CP057 — VISTA INFORMATIVA CUANDO SOLO EXISTE LA LÍNEA BASE
*Plataforma:* portal. *Banderas:* reloj: días.
- Cuentas: nutricionista.demo, paciente.linea
- perfil completo paciente.linea: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.linea → nutricionista.demo
- IBS-SSS baseline paciente.linea: 310 puntos, hace 5 d

### CP058 — REPORTE PDF GENERADO CORRECTAMENTE CON PROTECCIÓN
*Plataforma:* portal. *Banderas:* reloj: fecha fija, buzón, operador.
- Cuentas: nutricionista.reporte, paciente.reporte1
- cuenta nutricionista.reporte: nutritionist, estado **active**
- perfil completo paciente.reporte1: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.reporte1 → nutricionista.reporte
- IBS-SSS periodic paciente.reporte1: 320 puntos, el 2026-08-05
- IBS-SSS periodic paciente.reporte1: 280 puntos, el 2026-08-19
- recomendación paciente.reporte1: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; nota clínica: Reducir cebolla y pan de trigo por intolerancia a FODMAP; aprobada en: 2026-08
- actividad paciente.reporte1: {"daysWithRecordsInMonth": 20, "month": "2026-08", "monthDays": 31}
- **Operador, paso 11:** Pedir al equipo de desarrollo que muestre el registro de la generación del reporte.

### CP059 — ERROR DE GENERACIÓN POR DATOS INSUFICIENTES EN EL PERÍODO
*Plataforma:* portal. *Banderas:* reloj: fecha fija, buzón.
- Cuentas: nutricionista.reporte2, paciente.reporte2
- cuenta nutricionista.reporte2: nutritionist, estado **active**
- perfil completo paciente.reporte2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.reporte2 → nutricionista.reporte2
- nota: El paciente ingresó al sistema el 2026-09-15 y no tiene ningún registro anterior a esa fecha.

### CP060 — GRÁFICO DE EVOLUCIÓN DISPONIBLE CON DATOS SUFICIENTES
*Plataforma:* mobile. *Banderas:* reloj: días.
- Cuentas: paciente.graf
- perfil completo paciente.graf: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- IBS-SSS baseline paciente.graf: 320 puntos, hace 42 d
- IBS-SSS periodic paciente.graf: 280 puntos, hace 28 d
- IBS-SSS periodic paciente.graf: 260 puntos, hace 14 d
- IBS-SSS periodic paciente.graf: 245 puntos, hoy

### CP061 — ESTADO INFORMATIVO CON UNA SOLA EVALUACIÓN DISPONIBLE
*Plataforma:* mobile. *Banderas:* reloj: días.
- Cuentas: paciente.solobase
- perfil completo paciente.solobase: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- IBS-SSS baseline paciente.solobase: 310 puntos, hace 5 d

### CP062 — REPORTE PERSONAL GENERADO CORRECTAMENTE
*Plataforma:* mobile. *Banderas:* reloj: días, buzón.
- Cuentas: paciente.reporte
- perfil completo paciente.reporte: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.reporte: «Causa rellena», hace 10 d
- comida paciente.reporte: «Lomo saltado», hace 60 d
- comida paciente.reporte: «Aji de gallina», hace 120 d
- síntoma paciente.reporte: «Dolor abdominal» (intensidad 65), hace 10 d
- síntoma paciente.reporte: «Distensión abdominal» (intensidad 40), hace 60 d
- IBS-SSS baseline paciente.reporte: 320 puntos, hace 70 d
- IBS-SSS periodic paciente.reporte: 280 puntos, hace 42 d
- IBS-SSS periodic paciente.reporte: 245 puntos, hace 14 d
- recomendación paciente.reporte: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; recibida: hace 12 d
- recomendación paciente.reporte: «Aumentar la fibra soluble», estado **approvedAsGenerated**; recibida: hace 50 d; sugiere: Avena en hojuelas cocida, Zanahoria cocida

### CP063 — SIN DATOS EN EL PERÍODO SELECCIONADO
*Plataforma:* mobile. *Banderas:* reloj: días, buzón.
- Cuentas: paciente.sindatos
- perfil completo paciente.sindatos: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.sindatos: «Lomo saltado», hace 40 d
- síntoma paciente.sindatos: «Dolor abdominal» (intensidad 50), hace 40 d
- nota: Ningún registro de comidas ni síntomas en los últimos 30 días.

### CP064 — EXPORTACIÓN EXITOSA DE TODOS LOS DATOS DEL PACIENTE
*Plataforma:* mobile. *Banderas:* operador.
- Cuentas: paciente.export
- perfil completo paciente.export: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.export: «Causa rellena», hace 3 d
- comida paciente.export: «Lomo saltado», hace 2 d
- síntoma paciente.export: «Dolor abdominal» (intensidad 65), hace 2 d
- síntoma paciente.export: «Distensión abdominal» (intensidad 40), hace 1 d
- IBS-SSS baseline paciente.export: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.export: 280 puntos, hace 14 d
- recomendación paciente.export: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; recibida: hace 5 d
- nota: Exactamente estos registros: la exportación se compara contra ellos.
- **Operador, paso 12:** Pedir al equipo de desarrollo que muestre el registro de la exportación.

### CP065 — EXPORTACIÓN CUANDO SOLO EXISTE EL PERFIL CLÍNICO SIN REGISTROS ADICIONALES
*Plataforma:* mobile. *Banderas:* ninguna (estático).
- Cuentas: paciente.export2
- perfil completo paciente.export2: 70 kg, 170 cm, SII-E, alergias: ninguna declarada
- nota: SOLO el perfil clínico: sin comidas, síntomas, cuestionarios ni recomendaciones.

### CP066 — ELIMINACIÓN EXITOSA CON RETENCIÓN NORMATIVA INFORMADA AL PACIENTE
*Plataforma:* mobile. *Banderas:* consumido, buzón, operador.
- Cuentas: paciente.eliminar
- perfil completo paciente.eliminar: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.eliminar: «Causa rellena», hace 1 d
- síntoma paciente.eliminar: «Dolor abdominal» (intensidad 65), hace 1 d
- **Operador, paso 9:** Pedir al equipo de desarrollo que muestre el estado de los datos del paciente.
- **Operador, paso 10:** Pedir al equipo de desarrollo que muestre el registro de la operación.

### CP067 — CANCELACIÓN DE LA SOLICITUD DE ELIMINACIÓN
*Plataforma:* mobile. *Banderas:* consumido, buzón.
- Cuentas: paciente.cancelar
- perfil completo paciente.cancelar: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.cancelar: «Causa rellena», hace 1 d
- nota: Buzón sin ningún correo de eliminación de cuenta.

### CP068 — CONSULTA EXITOSA DE UN TÉRMINO DEL GLOSARIO
*Plataforma:* both. *Banderas:* ninguna (estático).
- Cuentas: nutricionista.demo, paciente.glosario
- perfil completo paciente.glosario: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.glosario → nutricionista.demo
- recomendación paciente.glosario: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; explicación: personalized; marca términos: FODMAP

### CP069 — TÉRMINO NO ENCONTRADO EN EL GLOSARIO
*Plataforma:* both. *Banderas:* ninguna (estático).
- Cuentas: nutricionista.demo, paciente.glosario2
- nota: El glosario NO debe tener «disacaridasas» ni «xyzqw» y SÍ «Disacáridos».

### CP070 — VISUALIZACIÓN EXITOSA DEL PERFIL COMPLETO
*Plataforma:* mobile. *Banderas:* reloj: días.
- Cuentas: nutricionista.demo, paciente.vista2, paciente.vista3, paciente.vista
- perfil completo paciente.vista: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.vista → nutricionista.demo desde hace 20 d
- cuenta paciente.vista: patient, estado **active**, nombre «María Fernanda López Quispe»
- IBS-SSS baseline paciente.vista: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.vista: 250 puntos, hace 14 d
- perfil completo paciente.vista2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.vista2 → nutricionista.demo desde hace 20 d
- IBS-SSS baseline paciente.vista2: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.vista2: 290 puntos, hace 14 d
- perfil completo paciente.vista3: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.vista3 → nutricionista.demo desde hace 20 d
- IBS-SSS baseline paciente.vista3: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.vista3: 270 puntos, hace 14 d

### CP071 — EDICIÓN DE INFORMACIÓN PERSONAL
*Plataforma:* mobile. *Banderas:* consumido, operador.
- Cuentas: paciente.editar
- perfil completo paciente.editar: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- **Operador, paso 9:** Pedir al equipo de desarrollo que muestre el registro de la modificación.

### CP072 — CREACIÓN EXITOSA DE RECOMENDACIÓN MANUAL
*Plataforma:* both. *Banderas:* consumido, push A, operador.
- Cuentas: nutricionista.demo, paciente.manual
- perfil completo paciente.manual: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.manual → nutricionista.demo
- **Operador, paso 8:** Pedir al equipo de desarrollo que muestre el registro de la creación.

### CP073 — RECHAZO POR DATOS OBLIGATORIOS INCOMPLETOS O PACIENTE NO ASIGNADO
*Plataforma:* both. *Banderas:* consumido, push B, operador.
- Cuentas: nutricionista.demo, nutricionista.error, paciente.ajeno, paciente.manual2
- perfil completo paciente.manual2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.manual2 → nutricionista.demo
- perfil completo paciente.ajeno: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.ajeno → nutricionista.error
- nota: paciente.ajeno debe estar asignado a OTRO nutricionista: se propone nutricionista.error (ver CP051), sin crear cuentas fuera del Excel.
- **Operador, paso 6:** Variante E (paciente no asignado): como el portal solo muestra los pacientes asignados, pedir al equipo de desarrollo que intente crear, con la sesión de este nutricionista, una recomendación con todos los datos válidos para un paciente que no es suyo.
- **Operador, paso 7:** Pedir al equipo de desarrollo que muestre las recomendaciones de los dos pacientes.

### CP074 — ARCHIVADO EXITOSO CON MOTIVO REGISTRADO
*Plataforma:* both. *Banderas:* consumido, operador.
- Cuentas: nutricionista.demo, paciente.archivar
- perfil completo paciente.archivar: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.archivar → nutricionista.demo
- recomendación paciente.archivar: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; vigencia restante (d): 30
- recomendación paciente.archivar: «Aumentar la fibra soluble», estado **approvedAsGenerated**; vigencia restante (d): 30; sugiere: Avena en hojuelas cocida, Zanahoria cocida
- **Operador, paso 8:** Pedir al equipo de desarrollo que muestre el registro de la acción.

### CP075 — ARCHIVADO AUTOMÁTICO POR CADUCIDAD TEMPORAL
*Plataforma:* both. *Banderas:* consumido, reloj: días, operador.
- Cuentas: nutricionista.demo, paciente.vencida
- perfil completo paciente.vencida: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.vencida → nutricionista.demo
- recomendación paciente.vencida: «Reducir legumbres», estado **createdByNutritionist**; vigencia restante (d): -2
- recomendación paciente.vencida: «Aumentar la fibra soluble», estado **createdByNutritionist**; vigencia restante (d): 30
- nota: La revisión automática de vencidas aún no se ejecutó: el worker de archivado queda apagado y se dispara a mano.
- **Operador, paso 3:** Pedir al equipo de desarrollo que ejecute la revisión automática diaria de recomendaciones vencidas (o esperar a su ejecución programada).
- **Operador, paso 6:** Pedir al equipo de desarrollo que confirme quién realizó el archivado.

### CP076 — BLOQUEO POR CANTIDAD INVÁLIDA O CAMPOS INCOMPLETOS AL REGISTRAR UNA COMIDA
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.diario2
- perfil completo paciente.diario2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- nota: Ninguna comida registrada hoy.

### CP077 — EXPLICACIÓN DE RESPALDO CUANDO NO SE PUEDE GENERAR LA EXPLICACIÓN PERSONALIZADA
*Plataforma:* mobile. *Banderas:* ninguna (estático).
- Cuentas: nutricionista.demo, paciente.respaldo
- perfil completo paciente.respaldo: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.respaldo → nutricionista.demo
- recomendación paciente.respaldo: «Reducir cebolla y pan de trigo», estado **approvedAsGenerated**; explicación: fallback; origen: Fallback; nota clínica: Reducir cebolla y pan de trigo por intolerancia a FODMAP; pasos: 3; datos de respaldo: {"painRecords": 4, "windowDays": 14}

### CP078 — BLOQUEO POR DATOS INVÁLIDOS AL EDITAR LA INFORMACIÓN PERSONAL
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.editar2
- perfil completo paciente.editar2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada

### CP079 — CORREO DE ACTIVACIÓN CON ENLACE DE UN SOLO USO
*Plataforma:* portal. *Banderas:* consumido, buzón.
- Cuentas: nutricionista.activar
- cuenta nutricionista.activar: nutritionist, estado **pendingActivation**
- nota: Enlace de activación de un solo uso y vigente: reset-qa lo reenvía.

### CP080 — RECHAZO POR CREDENCIALES INCORRECTAS
*Plataforma:* mobile. *Banderas:* operador.
- Cuentas: paciente.demo
- Deben NO existir: noexiste.cp080
- nota: Sin bloqueo ni intentos previos. Dos fallos seguidos NO deben bloquear.
- **Operador, paso 6:** Pedir al equipo de desarrollo que muestre el registro de intentos de ingreso de esa prueba.

### CP081 — SOLICITUD CON UN CORREO NO REGISTRADO, INACTIVO O MAL ESCRITO
*Plataforma:* mobile. *Banderas:* buzón.
- Cuentas: paciente.inactivo, paciente.reset4
- Deben NO existir: tester.sincuenta
- cuenta paciente.inactivo: patient, estado **disabled**

### CP082 — RESTABLECIMIENTO EXITOSO DE LA CONTRASEÑA
*Plataforma:* mobile. *Banderas:* consumido, buzón.
- Cuentas: paciente.reset3

### CP083 — INTENSIDAD FUERA DEL RANGO PERMITIDO
*Plataforma:* mobile. *Banderas:* consumido.
- Cuentas: paciente.sintomas3
- perfil completo paciente.sintomas3: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- nota: Ningún síntoma registrado hoy.

### CP084 — NOTIFICACIÓN DEL CUESTIONARIO CADA DOS SEMANAS
*Plataforma:* unknown. *Banderas:* consumido, reloj: días, push A, operador.
- Cuentas: paciente.notif
- perfil completo paciente.notif: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- IBS-SSS baseline paciente.notif: 320 puntos, hace 28 d
- IBS-SSS periodic paciente.notif: 300 puntos, hace 14 d
- cuestionario pendiente (paciente.notif): El siguiente cuestionario le toca hoy.
- **Operador, paso 1:** Pedir al equipo de desarrollo la hora programada de hoy para la notificación del cuestionario de esta cuenta.

### CP085 — AVISO PARA EL PACIENTE QUE AÚN NO TIENE NUTRICIONISTA
*Plataforma:* mobile. *Banderas:* ninguna (estático).
- Cuentas: paciente.sinnutri
- perfil completo paciente.sinnutri: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- nota: Sin nutricionista.

### CP086 — MODIFICACIÓN EXITOSA DE RECOMENDACIÓN CON NOTA CLÍNICA
*Plataforma:* portal. *Banderas:* consumido, push A, operador.
- Cuentas: nutricionista.demo, paciente.modexito
- perfil completo paciente.modexito: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.modexito → nutricionista.demo
- recomendación paciente.modexito: «Reducir cebolla y pan de trigo», estado **pendingReview**; explicación: personalized; vence en (d): 10; sugiere: Zanahoria cocida; pasos: Cocinar la zanahoria al vapor
- **Operador, paso 8:** Pedir al equipo de desarrollo que muestre el registro de la decisión.

### CP087 — VINCULACIÓN POSTERIOR DESDE EL PERFIL CON UN CÓDIGO VÁLIDO
*Plataforma:* both. *Banderas:* consumido, reloj: días.
- Cuentas: nutricionista.demo, paciente.vinculo2, paciente.vinculo
- perfil completo paciente.vinculo: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- comida paciente.vinculo: «Causa rellena», almuerzo, hace 3 d
- síntoma paciente.vinculo: «Dolor abdominal» (intensidad 65), hace 3 d
- perfil completo paciente.vinculo2: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- nota: Ninguno de los dos pacientes tiene nutricionista.

### CP088 — AVISO CLARO CUANDO EL CÓDIGO DE INVITACIÓN NO ES VÁLIDO
*Plataforma:* mobile. *Banderas:* consumido, reloj: horas.
- Cuentas: nutricionista.codigo2, paciente.codigo
- Deben NO existir: registro.cp088
- perfil completo paciente.codigo: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- código de invitación de nutricionista.codigo2: used, usado por otro paciente
- código de invitación de nutricionista.codigo2: expired, generado hace 4 d
- código de invitación de nutricionista.codigo2: valid, vigente 60 h más
- nota: El CP no dice de qué nutricionista son los códigos: se propone nutricionista.codigo2 (ya existe).
- nota: Un código usado, uno vencido y uno vigente (dura 72 h: reset-qa lo regenera). El operador entrega los valores.

### CP089 — SOLICITUD DE UN TÉRMINO NUEVO POR EL NUTRICIONISTA
*Plataforma:* both. *Banderas:* operador.
- Cuentas: nutricionista.glosario, paciente.glosario3
- nota: El glosario NO tiene «disacaridasas».
- **Operador, paso 6:** Pedir al equipo de desarrollo que muestre las solicitudes de términos pendientes de revisión.

### CP090 — CREACIÓN DE RECOMENDACIÓN MANUAL SIN PASOS DE APLICACIÓN
*Plataforma:* both. *Banderas:* consumido, push A.
- Cuentas: nutricionista.demo, paciente.manual3
- perfil completo paciente.manual3: 62 kg, 165 cm, SII-D, alergias: ninguna declarada
- vínculo paciente.manual3 → nutricionista.demo
