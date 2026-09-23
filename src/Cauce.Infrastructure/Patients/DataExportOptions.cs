namespace Cauce.Infrastructure.Patients;

/// <summary>
/// Configuración de la exportación de portabilidad de datos del paciente (US25). Se vincula a la
/// sección <c>Export</c> de la configuración.
/// </summary>
public sealed class DataExportOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "Export";

    /// <summary>
    /// Vigencia, en minutos, de la URL prefirmada de descarga del ZIP.
    ///
    /// <para>El valor por defecto es una hora. La URL prefirmada es una credencial al portador: quien
    /// la tenga descarga el expediente clínico completo del paciente sin autenticarse. Como el
    /// endpoint la devuelve de forma síncrona en la respuesta HTTP y el cliente descarga en el acto
    /// (a diferencia del reporte clínico, que sí viaja por correo), no hay razón para que siga viva
    /// más allá de la sesión en la que se pidió; una hora deja margen para un reintento o una
    /// conexión mala sin dejar el enlace expuesto durante días.</para>
    /// </summary>
    public int PresignedUrlValidityMinutes { get; init; } = 60;
}
