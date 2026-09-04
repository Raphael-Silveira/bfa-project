using BFA.Application.Franqueadora.Franqueados;
using BFA.Web.Franqueados;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.IntegrationTests;

public sealed class DiagnosticarVinculosFranqueadoCommandTests
{
    [Fact]
    public void Somente_argumento_explicito_solicita_diagnostico()
    {
        Assert.False(DiagnosticarVinculosFranqueadoCommand.Solicitado([]));
        Assert.False(DiagnosticarVinculosFranqueadoCommand.Solicitado(
            ["--DIAGNOSTICAR-VINCULOS-FRANQUEADOS"]));
        Assert.True(DiagnosticarVinculosFranqueadoCommand.Solicitado(
            ["--diagnosticar-vinculos-franqueados"]));
    }

    [Fact]
    public async Task Development_exibe_as_duas_inconsistencias_sem_alterar_dados()
    {
        var acessoSemComercial = NovaInconsistencia("Franqueado A", "Unidade A");
        var comercialSemAcesso = NovaInconsistencia("Franqueado B", "Unidade B");
        var consulta = new TestDiagnosticoConsulta(new(
            [acessoSemComercial],
            [comercialSemAcesso]));
        var command = new DiagnosticarVinculosFranqueadoCommand(
            CriarAmbiente(Environments.Development),
            consulta,
            NullLogger<DiagnosticarVinculosFranqueadoCommand>.Instance);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await command.ExecutarAsync(output, error);
        var texto = output.ToString();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, consulta.Execucoes);
        Assert.Contains("Acessos administrativos sem vínculo comercial ativo: 1", texto);
        Assert.Contains("Vínculos comerciais ativos sem acesso do usuário principal: 1", texto);
        Assert.Contains("Nenhum dado foi alterado", texto);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task Fora_de_development_bloqueia_antes_da_consulta()
    {
        var consulta = new TestDiagnosticoConsulta(new([], []));
        var command = new DiagnosticarVinculosFranqueadoCommand(
            CriarAmbiente(Environments.Production),
            consulta,
            NullLogger<DiagnosticarVinculosFranqueadoCommand>.Instance);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await command.ExecutarAsync(output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(0, consulta.Execucoes);
        Assert.Contains("somente pode ser executado em Development", error.ToString());
    }

    private static InconsistenciaVinculosFranqueado NovaInconsistencia(
        string franqueado,
        string unidade) =>
        new(Guid.NewGuid(), franqueado, Guid.NewGuid(), Guid.NewGuid(), unidade);

    private static TestHostEnvironment CriarAmbiente(string nome) =>
        new() { EnvironmentName = nome };

    private sealed class TestDiagnosticoConsulta(DiagnosticoVinculosFranqueado resultado)
        : IDiagnosticoVinculosFranqueadoConsulta
    {
        public int Execucoes { get; private set; }

        public Task<DiagnosticoVinculosFranqueado> DiagnosticarAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Execucoes++;
            return Task.FromResult(resultado);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "BFA.IntegrationTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
