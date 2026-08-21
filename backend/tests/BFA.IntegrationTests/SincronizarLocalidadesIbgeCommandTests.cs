using BFA.Application.Localidades;
using BFA.Web.Localidades;

namespace BFA.IntegrationTests;

public sealed class SincronizarLocalidadesIbgeCommandTests
{
    [Fact]
    public void Somente_argumento_explicito_solicita_sincronizacao()
    {
        Assert.False(SincronizarLocalidadesIbgeCommand.Solicitado([]));
        Assert.False(SincronizarLocalidadesIbgeCommand.Solicitado(
            ["--SINCRONIZAR-LOCALIDADES-IBGE"]));
        Assert.True(SincronizarLocalidadesIbgeCommand.Solicitado(
            ["--sincronizar-localidades-ibge"]));
    }

    [Fact]
    public async Task Comando_exibe_resumo_sem_detalhes_internos()
    {
        var servico = new TestSincronizacaoServico(
            new LocalidadesSincronizacaoResultado(2, 3));
        var command = new SincronizarLocalidadesIbgeCommand(servico);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await command.ExecutarAsync(output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, servico.Execucoes);
        Assert.Contains("Estados processados: 2", output.ToString());
        Assert.Contains("Municípios processados: 3", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task Falha_controlada_retorna_erro_sem_credenciais()
    {
        var servico = new TestSincronizacaoServico(
            new LocalidadesSincronizacaoException("Catálogo incompleto."));
        var command = new SincronizarLocalidadesIbgeCommand(servico);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await command.ExecutarAsync(output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Catálogo incompleto", error.ToString());
        Assert.DoesNotContain("ConnectionString", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestSincronizacaoServico : ILocalidadesSincronizacaoServico
    {
        private readonly LocalidadesSincronizacaoResultado? _resultado;
        private readonly Exception? _exception;

        public TestSincronizacaoServico(LocalidadesSincronizacaoResultado resultado)
        {
            _resultado = resultado;
        }

        public TestSincronizacaoServico(Exception exception)
        {
            _exception = exception;
        }

        public int Execucoes { get; private set; }

        public Task<LocalidadesSincronizacaoResultado> SincronizarAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Execucoes++;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_resultado!);
        }
    }
}
