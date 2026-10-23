namespace Cauce.Application.Common.Notifications;

/// <summary>
/// Textos de las notificaciones push al paciente. Se ven en la pantalla de bloqueo: no nombran
/// cuestionarios clínicos, síntomas, diagnósticos ni al nutricionista.
/// </summary>
public static class PatientPushTexts
{
    /// <summary>
    /// Título de todas las notificaciones push al paciente.
    /// </summary>
    public const string Title = "Cauce";

    /// <summary>
    /// Cuerpo del aviso de una recomendación aprobada, modificada o creada manualmente.
    /// </summary>
    public const string NewRecommendationBody = "Tienes una recomendación nueva. Toca para verla.";

    /// <summary>
    /// Cuerpo del aviso del día en que vence el cuestionario de seguimiento.
    /// </summary>
    public const string FollowUpAvailableBody = "Ya puedes completar tu cuestionario de seguimiento. Toca para empezar.";

    /// <summary>
    /// Cuerpo del recordatorio del cuestionario de seguimiento a las 48 horas de vencido.
    /// </summary>
    public const string FollowUpPendingBody = "Tienes un cuestionario de seguimiento pendiente. Toca para completarlo.";

    /// <summary>
    /// Cuerpo del pedido de retroalimentación, 24 horas después de entregada una recomendación.
    /// </summary>
    public const string FeedbackReminderBody =
        "¿Cómo te fue con la recomendación? Cuéntanos si la aplicaste y cómo te sentiste.";

    /// <summary>
    /// Cuerpo del recordatorio semanal de los lunes.
    /// </summary>
    public const string WeeklyReminderBody = "Recuerda registrar tu diario esta semana.";
}
