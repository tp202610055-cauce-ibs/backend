# Reports (Prompt 5, DEC-B5-08/11, acta A10)

Genera el reporte clínico del paciente en PDF cifrado y lo entrega por URL prefirmada.

Flujo (`GenerateClinicalReportCommandHandler` → aquí):
1. `ClinicalReportDataReader` consolida los datos clínicos del período (perfil, alergias, adherencia,
   síntomas, IBS-SSS, recomendaciones aprobadas, feedback). Solo iniciales del paciente, sin datos
   técnicos del sistema.
2. `ClinicalReportDocument` (QuestPDF) compone el PDF.
3. `PdfReportGenerator` lo cifra con PdfSharp y una contraseña aleatoria (`RandomNumberGenerator.GetString`),
   lo sube a MinIO (`MinioObjectStorage`) y devuelve la URL prefirmada. La contraseña se devuelve al
   handler pero **nunca se persiste ni se registra**.
4. `ClinicalReportMetadataRepository` guarda solo metadatos (paciente, nutricionista, período, ruta,
   tamaño); no la contraseña ni la URL.

El handler envía la URL y la contraseña en **dos correos separados y síncronos** (`IEmailSender`).

**Deuda técnica:** verificar en ejecución (test de integración con MinIO/Postgres reales) que PdfSharp
abre y cifra correctamente la salida de QuestPDF; si no, cambiar la estrategia de cifrado.
