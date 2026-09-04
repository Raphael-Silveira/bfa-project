using BFA.Application.Bootstrap;
using BFA.Web.Bootstrap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.IntegrationTests;

public sealed class BootstrapInicialCommandTests
{
    [Fact]
    public void Somente_argumento_explicito_solicita_bootstrap()
    {
        Assert.False(BootstrapInicialCommand.Solicitado([]));
        Assert.False(BootstrapInicialCommand.Solicitado(["--BOOTSTRAP-INICIAL"]));
        Assert.True(BootstrapInicialCommand.Solicitado(["--bootstrap-inicial"]));
    }

    [Fact]
    public async Task Ausencia_de_configuracao_obrigatoria_causa_erro_claro()
    {
        var bootstrap = new TestBootstrapInicial();
        var configuration = CreateConfiguration(includeAdmin2Password: false);
        var command = new BootstrapInicialCommand(
            CreateEnvironment(Environments.Development),
            configuration,
            bootstrap,
            NullLogger<BootstrapInicialCommand>.Instance);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await command.ExecutarAsync(output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(0, bootstrap.Execucoes);
        Assert.Contains("Bootstrap:Admin2:Password", error.ToString());
    }

    [Fact]
    public async Task Resultado_do_console_nao_expoe_emails_ou_senhas()
    {
        var bootstrap = new TestBootstrapInicial();
        var configuration = CreateConfiguration();
        var command = new BootstrapInicialCommand(
            CreateEnvironment(Environments.Development),
            configuration,
            bootstrap,
            NullLogger<BootstrapInicialCommand>.Instance);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await command.ExecutarAsync(output, error);
        var console = output.ToString() + error;
        var request = Assert.IsType<BootstrapInicialSolicitacao>(bootstrap.UltimaSolicitacao);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, bootstrap.Execucoes);
        Assert.Contains("Organização BFA criada.", console);
        Assert.DoesNotContain(request.Administrador1.Email, console);
        Assert.DoesNotContain(request.Administrador1.Senha, console);
        Assert.DoesNotContain(request.Administrador2.Email, console);
        Assert.DoesNotContain(request.Administrador2.Senha, console);
        Assert.DoesNotContain("PasswordHash", console, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", console, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_bloqueia_bootstrap_antes_de_ler_credenciais()
    {
        var bootstrap = new TestBootstrapInicial();
        var command = new BootstrapInicialCommand(
            CreateEnvironment(Environments.Production),
            new ConfigurationBuilder().Build(),
            bootstrap,
            NullLogger<BootstrapInicialCommand>.Instance);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await command.ExecutarAsync(output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(0, bootstrap.Execucoes);
        Assert.Contains("somente pode ser executado em Development", error.ToString());
    }

    private static IConfiguration CreateConfiguration(bool includeAdmin2Password = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["Bootstrap:Admin1:Email"] = CreateEmail(),
            ["Bootstrap:Admin1:Password"] = CreatePassword(),
            ["Bootstrap:Admin2:Email"] = CreateEmail()
        };

        if (includeAdmin2Password)
        {
            values["Bootstrap:Admin2:Password"] = CreatePassword();
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static TestHostEnvironment CreateEnvironment(string environmentName)
    {
        return new TestHostEnvironment { EnvironmentName = environmentName };
    }

    private static string CreateEmail()
    {
        return $"bootstrap-{Guid.NewGuid():N}@example.invalid";
    }

    private static string CreatePassword()
    {
        return $"Aa1!{Guid.NewGuid():N}";
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "BFA.IntegrationTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
