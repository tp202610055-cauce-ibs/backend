namespace Cauce.Infrastructure.Tests.Qa;

/// <summary>
/// Manifiestos de prueba: el publicado en docs/qa y uno mínimo con un paciente y un nutricionista.
/// </summary>
internal static class QaTestManifests
{
    public static string PublishedPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cauce.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("No se encontró la raíz del repositorio.");
        }

        return Directory.GetFiles(Path.Combine(directory.FullName, "docs", "qa"), "qa-manifest*.json").Single();
    }

    public static string Minimal(string version = "1.1-draft2", string seeds = "[]") => $$"""
        {
          "manifestVersion": "{{version}}",
          "accounts": [
            {"email":"paciente.qa@ejemplo.com","role":"patient","disposition":"seed","status":"active","usedBy":["CP900"]},
            {"email":"nutricionista.qa@ejemplo.com","role":"nutritionist","disposition":"seed","status":"active","usedBy":["CP900"]},
            {"email":"registro.qa@ejemplo.com","role":null,"disposition":"mustNotExist","usedBy":["CP900"]}
          ],
          "cases": [
            {
              "cp": "CP900",
              "accounts": ["paciente.qa@ejemplo.com","nutricionista.qa@ejemplo.com"],
              "mustNotExist": ["registro.qa@ejemplo.com"],
              "consumed": true,
              "clock": null,
              "seed": {{seeds}}
            }
          ]
        }
        """;
}
