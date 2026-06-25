using System.Net;
using FluentValidation;

namespace Cauce.Application.Identity.UseCases.RegisterPatient;

/// <summary>
/// Validador del comando <see cref="RegisterPatientCommand"/>. Refleja la política
/// de contraseñas del realm y las restricciones de longitud y formato de entrada.
/// </summary>
public sealed class RegisterPatientCommandValidator : AbstractValidator<RegisterPatientCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public RegisterPatientCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(150)
            .EmailAddress();

        RuleFor(x => x.FullName)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("El nombre completo no puede estar vacío.")
            .Length(2, 150);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("La contraseña debe contener al menos una letra mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe contener al menos una letra minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe contener al menos un dígito.");

        RuleFor(x => x.ConsentDocumentVersion)
            .NotEmpty();

        RuleFor(x => x.ConsentTextHash)
            .NotEmpty()
            .Length(64).WithMessage("El hash de consentimiento debe tener 64 caracteres.")
            .Matches("^[0-9a-fA-F]{64}$").WithMessage("El hash de consentimiento debe estar en formato hexadecimal.");

        RuleFor(x => x.IpAddress)
            .Must(BeAValidIpAddress)
            .When(x => !string.IsNullOrWhiteSpace(x.IpAddress))
            .WithMessage("La dirección IP no es válida.");

        RuleFor(x => x.InvitationCode!)
            .Length(8, 20)
            .Matches("^[A-Z0-9]+$").WithMessage("El código de invitación debe ser alfanumérico en mayúsculas.")
            .When(x => !string.IsNullOrWhiteSpace(x.InvitationCode));
    }

    private static bool BeAValidIpAddress(string? ipAddress)
    {
        return ipAddress is not null && IPAddress.TryParse(ipAddress, out _);
    }
}
