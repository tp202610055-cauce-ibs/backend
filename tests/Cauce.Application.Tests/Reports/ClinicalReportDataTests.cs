using System.Reflection;
using Cauce.Application.Reports.Contracts;
using FluentAssertions;

namespace Cauce.Application.Tests.Reports;

/// <summary>
/// Pruebas de <see cref="ClinicalReportData"/> (CA-B5-28): el contrato que alimenta el PDF no expone
/// datos personales técnicos (correo, identificador de Keycloak, IP) y el paciente se identifica por
/// iniciales. Reemplaza la frágil extracción de texto del PDF cifrado por una verificación estructural.
/// </summary>
public sealed class ClinicalReportDataTests
{
    private static readonly string[] ForbiddenPropertyNames = ["Email", "KeycloakId", "IpAddress", "FullName", "PatientName"];

    [Fact]
    public void ClinicalReportData_AndNestedTypes_DoNotExposePii()
    {
        var visited = new HashSet<Type>();
        var offenders = new List<string>();

        CollectPiiOffenders(typeof(ClinicalReportData), visited, offenders);

        offenders.Should().BeEmpty("el reporte no debe exponer datos personales técnicos");
    }

    [Fact]
    public void ClinicalReportData_IdentifiesPatientByInitials()
    {
        var property = typeof(ClinicalReportData).GetProperty("PatientInitials");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(string));
    }

    private static void CollectPiiOffenders(Type type, HashSet<Type> visited, List<string> offenders)
    {
        if (!visited.Add(type) || type.Namespace?.StartsWith("Cauce", StringComparison.Ordinal) != true)
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (ForbiddenPropertyNames.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                offenders.Add($"{type.Name}.{property.Name}");
            }

            var propertyType = UnwrapType(property.PropertyType);
            CollectPiiOffenders(propertyType, visited, offenders);
        }
    }

    private static Type UnwrapType(Type type)
    {
        if (type.IsGenericType)
        {
            var argument = type.GetGenericArguments().FirstOrDefault();
            if (argument is not null)
            {
                return argument;
            }
        }

        return type;
    }
}
