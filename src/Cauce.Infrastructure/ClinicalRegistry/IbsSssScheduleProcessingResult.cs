namespace Cauce.Infrastructure.ClinicalRegistry;

/// <summary>
/// Resultado de una pasada de <see cref="IbsSssScheduleProcessor"/>.
/// </summary>
/// <param name="RemindersSent">Recordatorios agendados a las 48 horas de vencida la evaluación.</param>
/// <param name="DueNoticesSent">Avisos agendados el día en que vence la evaluación.</param>
/// <param name="MarkedMissed">Agendas marcadas como perdidas.</param>
public sealed record IbsSssScheduleProcessingResult(int RemindersSent, int DueNoticesSent, int MarkedMissed);
