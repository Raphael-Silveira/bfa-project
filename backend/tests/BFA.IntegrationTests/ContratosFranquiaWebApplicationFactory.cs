using Microsoft.AspNetCore.Hosting;

namespace BFA.IntegrationTests;

public sealed class ContratosFranquiaWebApplicationFactory
    : UsuariosFranqueadoraWebApplicationFactory
{
    public string DiretorioArmazenamento { get; } = Path.Combine(
        Path.GetTempPath(),
        "bfa-contratos-web",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "Armazenamento:Documentos:DiretorioBase",
            DiretorioArmazenamento);
        builder.UseSetting(
            "Armazenamento:Documentos:TamanhoMaximoBytes",
            (20 * 1024 * 1024).ToString());
        base.ConfigureWebHost(builder);
    }
}
