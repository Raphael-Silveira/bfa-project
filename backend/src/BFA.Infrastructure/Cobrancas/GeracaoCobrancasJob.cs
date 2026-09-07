using BFA.Application.Cobrancas;
using BFA.Domain.Cobrancas;
using Microsoft.Extensions.Logging;

namespace BFA.Infrastructure.Cobrancas;

public sealed class GeracaoCobrancasJob(
    ICobrancasRepositorio repositorio,
    TimeProvider timeProvider,
    ILogger<GeracaoCobrancasJob> logger)
{
    public async Task GerarMensalidadesAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Iniciando geração de mensalidades");

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var hoje = DateOnly.FromDateTime(agora);
        var mesAtual = hoje.Month;
        var anoAtual = hoje.Year;
        var diaVencimento = 5;

        var matriculas = await repositorio.ListarMatriculasAtivasParaGeracaoAsync(cancellationToken);

        logger.LogInformation("Encontradas {Count} matrículas ativas para verificação", matriculas.Count);

        var criadas = 0;

        foreach (var matricula in matriculas)
        {
            if (matricula.DataInicio > hoje)
                continue;

            if (matricula.DataFimPrevista < hoje)
                continue;

            var jaExiste = await repositorio.ExisteMensalidadeNoMesAsync(
                matricula.MatriculaId, anoAtual, mesAtual, cancellationToken);

            if (jaExiste)
                continue;

            var dataVencimento = new DateOnly(anoAtual, mesAtual, diaVencimento);
            if (dataVencimento < hoje)
                dataVencimento = hoje.AddDays(1);

            var descricao = $"Mensalidade {matricula.PlanoNome} - {mesAtual:D2}/{anoAtual}";

            var cobranca = new Cobranca(
                Guid.NewGuid(),
                matricula.OrganizacaoId,
                matricula.UnidadeId,
                matricula.AlunoId,
                matricula.MatriculaId,
                TipoCobranca.Mensalidade,
                descricao,
                matricula.ValorMensalContratado,
                hoje,
                dataVencimento,
                null,
                agora);

            await repositorio.CriarAsync(cobranca, cancellationToken);

            criadas++;

            logger.LogInformation(
                "Mensalidade criada: {CobrancaId} para aluno {AlunoId} na unidade {UnidadeId} - Valor: {Valor}",
                cobranca.Id, matricula.AlunoId, matricula.UnidadeId, matricula.ValorMensalContratado);

            if (matricula.CobraTaxaMatricula
                && matricula.ValorTaxaMatricula.HasValue)
            {
                var jaExisteTaxa = await repositorio.ExisteTaxaMatriculaAsync(
                    matricula.MatriculaId, cancellationToken);

                if (!jaExisteTaxa)
                {
                    var descricaoTaxa = $"Taxa de matrícula - {matricula.PlanoNome}";

                    var cobrancaTaxa = new Cobranca(
                        Guid.NewGuid(),
                        matricula.OrganizacaoId,
                        matricula.UnidadeId,
                        matricula.AlunoId,
                        matricula.MatriculaId,
                        TipoCobranca.Matricula,
                        descricaoTaxa,
                        matricula.ValorTaxaMatricula.Value,
                        hoje,
                        dataVencimento,
                        null,
                        agora);

                    await repositorio.CriarAsync(cobrancaTaxa, cancellationToken);

                    criadas++;

                    logger.LogInformation(
                        "Taxa de matrícula criada: {CobrancaId} para aluno {AlunoId} na unidade {UnidadeId} - Valor: {Valor}",
                        cobrancaTaxa.Id, matricula.AlunoId, matricula.UnidadeId, matricula.ValorTaxaMatricula.Value);
                }
            }
        }

        logger.LogInformation("Geração de mensalidades concluída: {Criadas} cobranças criadas", criadas);
    }

    public async Task GerarTaxasMatriculaAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Iniciando geração de taxas de matrícula");

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var hoje = DateOnly.FromDateTime(agora);

        var matriculas = await repositorio.ListarMatriculasAtivasParaGeracaoAsync(cancellationToken);

        logger.LogInformation("Encontradas {Count} matrículas ativas para verificação de taxa", matriculas.Count);

        var criadas = 0;

        foreach (var matricula in matriculas)
        {
            if (!matricula.CobraTaxaMatricula)
                continue;

            if (!matricula.ValorTaxaMatricula.HasValue)
                continue;

            if (matricula.DataInicio > hoje)
                continue;

            var jaExiste = await repositorio.ExisteTaxaMatriculaAsync(
                matricula.MatriculaId, cancellationToken);

            if (jaExiste)
                continue;

            var descricao = $"Taxa de matrícula - {matricula.PlanoNome}";

            var cobranca = new Cobranca(
                Guid.NewGuid(),
                matricula.OrganizacaoId,
                matricula.UnidadeId,
                matricula.AlunoId,
                matricula.MatriculaId,
                TipoCobranca.Matricula,
                descricao,
                matricula.ValorTaxaMatricula.Value,
                hoje,
                hoje,
                null,
                agora);

            await repositorio.CriarAsync(cobranca, cancellationToken);

            criadas++;

            logger.LogInformation(
                "Taxa de matrícula criada: {CobrancaId} para aluno {AlunoId} na unidade {UnidadeId} - Valor: {Valor}",
                cobranca.Id, matricula.AlunoId, matricula.UnidadeId, matricula.ValorTaxaMatricula.Value);
        }

        logger.LogInformation("Geração de taxas de matrícula concluída: {Criadas} cobranças criadas", criadas);
    }

    public async Task MarcarAtrasadasAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Iniciando marcação de cobranças atrasadas");

        var marcadas = await repositorio.MarcarAtrasadasAsync(cancellationToken);

        if (marcadas > 0)
        {
            logger.LogInformation(
                "Marcação de atrasadas concluída: {Marcadas} cobranças marcadas como atrasadas",
                marcadas);
        }
        else
        {
            logger.LogDebug("Nenhuma cobrança atrasada encontrada para marcação");
        }
    }
}
