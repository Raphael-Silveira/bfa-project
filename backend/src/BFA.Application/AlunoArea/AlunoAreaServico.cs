using BFA.Application.AlunoArea;
using BFA.Domain.Aulas;
using BFA.Domain.Cobrancas;
using BFA.Domain.Matriculas;
using BFA.Domain.Alunos;
using BFA.Application.Localidades;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BFA.Application.AlunoArea;

public sealed class AlunoAreaServico(
    IAlunoAreaRepositorio repositorio,
    ILogger<AlunoAreaServico> logger,
    TimeProvider timeProvider,
    TimeZoneInfo timeZoneInfo,
    ILocalidadesConsulta? localidades = null,
    IFotoPerfilAluno? fotos = null)
    : IAlunoAreaServico
{
    public async Task<DashboardAlunoDto?> ObterDashboardAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterDashboard iniciado para {UsuarioId} na unidade {UnidadeId}",
            usuarioId, unidadeId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            logger.LogWarning("Aluno não encontrado para {UsuarioId} na unidade {UnidadeId}",
                usuarioId, unidadeId);
            return null;
        }

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var fimMes = inicioMes.AddMonths(1).AddDays(-1);

        var aulas = await repositorio.ListarAulasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id,
            hoje, hoje.AddDays(30), cancellationToken);

        var proximaAula = aulas
            .Where(a => a.Data >= hoje)
            .OrderBy(a => a.Data)
            .ThenBy(a => a.HoraInicio)
            .FirstOrDefault();

        var totalAulas = await repositorio.ContarAulasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id,
            inicioMes, fimMes, cancellationToken);

        var presentes = await repositorio.ContarPresencasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id,
            inicioMes, fimMes, cancellationToken);

        var percentual = totalAulas > 0
            ? Math.Round((decimal)presentes / totalAulas * 100, 1)
            : 0m;

        var cobrancas = await repositorio.ListarCobrancasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id, null, null, cancellationToken);

        var matriculaAtiva = (await repositorio.ListarMatriculasAsync(
                aluno.Aluno.OrganizacaoId,
                unidadeId,
                aluno.Aluno.Id,
                cancellationToken))
            .FirstOrDefault(m => m.Status == StatusMatricula.Ativa);

        var totalPendente = cobrancas
            .Where(c => c.Status is StatusCobranca.Pendente or StatusCobranca.Atrasada)
            .Sum(c => c.Valor - c.ValorPago);

        var nomeUnidade = await repositorio.ObterNomeUnidadeAsync(
            aluno.Aluno.OrganizacaoId,
            unidadeId,
            cancellationToken);

        Guid? proximaAulaId = proximaAula.AulaId == Guid.Empty ? null : proximaAula.AulaId;
        var podeAlterarConfirmacao = proximaAulaId.HasValue
            && AulaAindaElegivel(proximaAula.Data, proximaAula.HoraInicio, proximaAula.Status);

        var resultado = new DashboardAlunoDto(
            aluno.Aluno.OrganizacaoId,
            new PerfilAlunoDto(
                aluno.Aluno.Id,
                aluno.Aluno.NomeCompleto,
                aluno.Aluno.Cpf,
                aluno.Aluno.Telefone,
                aluno.Aluno.Email,
                aluno.Aluno.DataNascimento,
                aluno.Aluno.Ativo,
                aluno.Aluno.Apelido,
                aluno.Aluno.Cep,
                aluno.Aluno.EstadoCodigoIbge,
                null,
                null,
                aluno.Aluno.MunicipioCodigoIbge,
                null,
                aluno.Aluno.Bairro,
                aluno.Aluno.Logradouro,
                aluno.Aluno.Numero,
                aluno.Aluno.Complemento,
                aluno.Aluno.FotoPerfilChave,
                aluno.Aluno.FotoPerfilContentType),
            nomeUnidade ?? "Unidade",
            proximaAula.Data != default
                ? $"{proximaAula.Data:dd/MM} - {proximaAula.TurmaNome} ({proximaAula.HoraInicio}–{proximaAula.HoraFim})"
                : null,
            $"{percentual}%",
            FormatBrl(totalPendente),
            totalAulas,
            matriculaAtiva is null ? null : MapearMatricula(matriculaAtiva),
            proximaAulaId,
            proximaAula.ConfirmacaoAtiva,
            podeAlterarConfirmacao);

        logger.LogDebug("ObterDashboard concluído para {AlunoId}", aluno.Aluno.Id);
        return resultado;
    }

    public async Task<PerfilAlunoDto?> ObterPerfilAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterPerfil iniciado para {UsuarioId}", usuarioId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            return null;
        }

        var perfil = new PerfilAlunoDto(
            aluno.Aluno.Id,
            aluno.Aluno.NomeCompleto,
            aluno.Aluno.Cpf,
            aluno.Aluno.Telefone,
            aluno.Aluno.Email,
            aluno.Aluno.DataNascimento,
            aluno.Aluno.Ativo,
            aluno.Aluno.Apelido,
            aluno.Aluno.Cep,
            aluno.Aluno.EstadoCodigoIbge,
            null,
            null,
            aluno.Aluno.MunicipioCodigoIbge,
            null,
            aluno.Aluno.Bairro,
            aluno.Aluno.Logradouro,
            aluno.Aluno.Numero,
            aluno.Aluno.Complemento,
            aluno.Aluno.FotoPerfilChave,
            aluno.Aluno.FotoPerfilContentType);

        if (localidades is null)
            return perfil;

        var estados = await localidades.ListarEstadosAtivosAsync(cancellationToken);
        var estado = perfil.EstadoCodigoIbge is { } estadoId
            ? estados.FirstOrDefault(item => item.CodigoIbge == estadoId)
            : null;
        var municipios = perfil.EstadoCodigoIbge is { } codigoEstado
            ? await localidades.ListarMunicipiosAtivosAsync(codigoEstado, cancellationToken)
            : [];
        var municipio = perfil.MunicipioCodigoIbge is { } municipioId
            ? municipios.FirstOrDefault(item => item.CodigoIbge == municipioId)
            : null;

        return perfil with
        {
            EstadoSigla = estado?.Sigla,
            EstadoNome = estado?.Nome,
            MunicipioNome = municipio?.Nome
        };
    }

    public async Task<ResultadoAtualizacaoPerfilAluno> AtualizarPerfilCompletoAsync(
        Guid usuarioId,
        Guid unidadeId,
        string? apelido,
        string? telefone,
        string? email,
        string? cep,
        int? estadoCodigoIbge,
        int? municipioCodigoIbge,
        string? bairro,
        string? logradouro,
        string? numero,
        string? complemento,
        FotoPerfilUpload? foto,
        CancellationToken cancellationToken)
    {
        if (apelido is not null && (apelido.Trim().Length > Aluno.ApelidoTamanhoMaximo
            || apelido.Any(char.IsControl)))
            return ResultadoAtualizacaoPerfilAluno.ApelidoInvalido;

        var emailNormalizado = email?.Trim();
        if (string.IsNullOrWhiteSpace(emailNormalizado)
            || emailNormalizado.Length > 256
            || !new EmailAddressAttribute().IsValid(emailNormalizado))
            return ResultadoAtualizacaoPerfilAluno.EmailInvalido;

        string? telefoneNormalizado;
        try { telefoneNormalizado = TelefoneBrasileiro.Normalizar(telefone); }
        catch (ArgumentException) { return ResultadoAtualizacaoPerfilAluno.TelefoneInvalido; }

        if (cep is not null && cep.Any(c => !char.IsDigit(c) && c is not ' ' and not '-' and not '.')
            || cep is not null && new string(cep.Where(char.IsDigit).ToArray()) is var cepDigitos && cepDigitos.Length is not 0 and not 8)
            return ResultadoAtualizacaoPerfilAluno.CepInvalido;

        if (estadoCodigoIbge is null && municipioCodigoIbge is not null)
            return ResultadoAtualizacaoPerfilAluno.EnderecoInvalido;

        if (localidades is not null && estadoCodigoIbge is { } estadoId)
        {
            var estados = await localidades.ListarEstadosAtivosAsync(cancellationToken);
            if (estados.All(item => item.CodigoIbge != estadoId))
                return ResultadoAtualizacaoPerfilAluno.EnderecoInvalido;
            if (municipioCodigoIbge is { } municipioId)
            {
                var municipios = await localidades.ListarMunicipiosAtivosAsync(estadoId, cancellationToken);
                if (municipios.All(item => item.CodigoIbge != municipioId))
                    return ResultadoAtualizacaoPerfilAluno.EnderecoInvalido;
            }
        }

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(usuarioId, unidadeId, cancellationToken);
        if (aluno is null) return ResultadoAtualizacaoPerfilAluno.NaoEncontrado;

        FotoPerfilArmazenada? novaFoto = null;
        try
        {
            if (foto is not null)
            {
                if (fotos is null) return ResultadoAtualizacaoPerfilAluno.FotoInvalida;
                novaFoto = await fotos.ValidarProcessarSalvarAsync(
                    aluno.OrganizacaoId, aluno.Aluno.Id, foto, cancellationToken);
            }

            var atualizado = await repositorio.AtualizarPerfilCompletoAsync(
                aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id, apelido, telefoneNormalizado,
                emailNormalizado, cep, estadoCodigoIbge, municipioCodigoIbge, bairro,
                logradouro, numero, complemento, novaFoto?.Chave, novaFoto?.ContentType,
                novaFoto?.AtualizadaEmUtc, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            if (!atualizado)
            {
                if (novaFoto is not null && fotos is not null)
                    await fotos.ExcluirAsync(novaFoto.Chave, cancellationToken);
                return ResultadoAtualizacaoPerfilAluno.NaoEncontrado;
            }

            if (novaFoto is not null && fotos is not null && aluno.Aluno.FotoPerfilChave is not null)
                await fotos.ExcluirAsync(aluno.Aluno.FotoPerfilChave, cancellationToken);

            return ResultadoAtualizacaoPerfilAluno.Sucesso;
        }
        catch (ArgumentException) when (foto is not null)
        {
            await ExcluirFotoCompensatoriaAsync(novaFoto);
            return ResultadoAtualizacaoPerfilAluno.FotoInvalida;
        }
        catch (Exception exception) when (novaFoto is not null)
        {
            await ExcluirFotoCompensatoriaAsync(novaFoto);
            logger.LogError(exception,
                "AtualizarPerfilCompleto falhou após preparar a nova foto para {UsuarioId} na unidade {UnidadeId}",
                usuarioId, unidadeId);
            throw;
        }
    }

    private async Task ExcluirFotoCompensatoriaAsync(FotoPerfilArmazenada? foto)
    {
        if (foto is null || fotos is null)
            return;

        try
        {
            await fotos.ExcluirAsync(foto.Chave, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Não foi possível remover arquivo temporário de perfil após falha de atualização");
        }
    }

    public async Task<(Stream Conteudo, string ContentType)?> AbrirFotoPerfilAsync(
        Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
    {
        if (fotos is null) return null;
        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(usuarioId, unidadeId, cancellationToken);
        if (aluno?.Aluno.FotoPerfilChave is null || aluno.Aluno.FotoPerfilContentType is null)
            return null;
        var stream = await fotos.AbrirAsync(aluno.Aluno.FotoPerfilChave, cancellationToken);
        return stream is null ? null : (stream, aluno.Aluno.FotoPerfilContentType);
    }

    public async Task<ResultadoAtualizacaoPerfilAluno> AtualizarPerfilAsync(
        Guid usuarioId,
        Guid unidadeId,
        string? telefone,
        string? email,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("AtualizarPerfil iniciado para {UsuarioId} na unidade {UnidadeId}",
            usuarioId, unidadeId);

        var emailNormalizado = email?.Trim();
        if (string.IsNullOrWhiteSpace(emailNormalizado)
            || emailNormalizado.Length > 256
            || !new EmailAddressAttribute().IsValid(emailNormalizado))
        {
            logger.LogWarning("AtualizarPerfil rejeitado por e-mail inválido para {UsuarioId}", usuarioId);
            return ResultadoAtualizacaoPerfilAluno.EmailInvalido;
        }

        string? telefoneNormalizado;
        try
        {
            telefoneNormalizado = TelefoneBrasileiro.Normalizar(telefone);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("AtualizarPerfil rejeitado por telefone inválido para {UsuarioId}", usuarioId);
            return ResultadoAtualizacaoPerfilAluno.TelefoneInvalido;
        }

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            logger.LogWarning("AtualizarPerfil não encontrado para {UsuarioId} na unidade {UnidadeId}",
                usuarioId, unidadeId);
            return ResultadoAtualizacaoPerfilAluno.NaoEncontrado;
        }

        var atualizado = await repositorio.AtualizarPerfilAsync(
            aluno.OrganizacaoId,
            unidadeId,
            aluno.Aluno.Id,
            telefoneNormalizado,
            emailNormalizado,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        if (!atualizado)
        {
            logger.LogWarning("AtualizarPerfil perdeu o vínculo autorizado para {UsuarioId}", usuarioId);
            return ResultadoAtualizacaoPerfilAluno.NaoEncontrado;
        }

        logger.LogInformation("AtualizarPerfil concluído para {AlunoId}", aluno.Aluno.Id);
        return ResultadoAtualizacaoPerfilAluno.Sucesso;
    }

    public async Task<IReadOnlyList<MatriculaAlunoDto>> ObterMatriculasAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterMatriculas iniciado para {UsuarioId}", usuarioId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            return [];
        }

        var matriculas = await repositorio.ListarMatriculasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id, cancellationToken);

        return matriculas.Select(MapearMatricula)
            .ToList();
    }

    private bool AulaAindaElegivel(DateOnly data, string horaInicio, string status)
    {
        if (!string.Equals(status, StatusAula.Programada.ToString(), StringComparison.Ordinal))
            return false;

        if (!TimeOnly.TryParseExact(
                horaInicio, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var hora))
            return false;

        var agoraLocal = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZoneInfo);
        var inicioLocal = DateTime.SpecifyKind(data.ToDateTime(hora), DateTimeKind.Unspecified);
        return agoraLocal.DateTime < inicioLocal;
    }

    private static MatriculaAlunoDto MapearMatricula(MatriculaAlunoConsulta matricula) =>
        new(
            matricula.MatriculaId,
            matricula.PlanoNome,
            matricula.FrequenciaSemanal,
            matricula.Status.ToString(),
            matricula.DataInicio,
            matricula.DataFimPrevista,
            matricula.DataFimReal,
            matricula.ValorMensal,
            matricula.Horarios);

    public async Task<IReadOnlyList<AulaAlunoDto>> ObterAgendaAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterAgenda iniciado para {UsuarioId}", usuarioId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            return [];
        }

        var aulas = await repositorio.ListarAulasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id,
            dataInicio, dataFim, cancellationToken);

        return aulas.Select(a => new AulaAlunoDto(
            a.AulaId,
            a.Data,
            a.HoraInicio,
            a.HoraFim,
            a.TurmaNome,
            a.Status,
            a.MotivoCancelamento,
            a.ConfirmacaoAtiva,
            AulaAindaElegivel(a.Data, a.HoraInicio, a.Status))).ToList();
    }

    public async Task<FrequenciaResumoDto?> ObterFrequenciaAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterFrequencia iniciado para {UsuarioId}", usuarioId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            return null;
        }

        var orgId = aluno.Aluno.OrganizacaoId;
        var id = aluno.Aluno.Id;

        var total = await repositorio.ContarAulasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var presentes = await repositorio.ContarPresencasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var ausentes = await repositorio.ContarAusenciasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var justificados = await repositorio.ContarJustificativasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var percentual = total > 0
            ? Math.Round((decimal)presentes / total * 100, 1)
            : 0m;

        var presencas = await repositorio.ListarPresencasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        return new FrequenciaResumoDto(
            total,
            presentes,
            ausentes,
            justificados,
            percentual,
            presencas.Select(p => new PresencaAlunoDto(
                p.Data,
                p.TurmaNome,
                p.HoraInicio,
                p.HoraFim,
                p.Status,
                p.Observacoes)).ToList());
    }

    public async Task<FinanceiroResumoDto?> ObterFinanceiroAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly? dataInicio,
        DateOnly? dataFim,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterFinanceiro iniciado para {UsuarioId}", usuarioId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            return null;
        }

        var orgId = aluno.Aluno.OrganizacaoId;
        var id = aluno.Aluno.Id;

        var cobrancas = await repositorio.ListarCobrancasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var pagamentos = await repositorio.ListarPagamentosAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var totalPendente = cobrancas
            .Where(c => c.Status is StatusCobranca.Pendente or StatusCobranca.Atrasada)
            .Sum(c => c.Valor - c.ValorPago);

        var totalPago = cobrancas
            .Where(c => c.Status == StatusCobranca.Paga)
            .Sum(c => c.ValorPago);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        return new FinanceiroResumoDto(
            FormatBrl(totalPendente),
            FormatBrl(totalPago),
            cobrancas.Select(c => new CobrancaAlunoDto(
                c.Id,
                c.Descricao,
                c.Tipo.ToString(),
                FormatBrl(c.Valor),
                FormatBrl(c.ValorPago),
                FormatBrl(c.Valor - c.ValorPago),
                c.DataVencimento,
                c.Status.ToString(),
                c.Status == StatusCobranca.Atrasada
                    ? (int)(hoje.ToDateTime(TimeOnly.MinValue) - c.DataVencimento.ToDateTime(TimeOnly.MinValue)).TotalDays
                    : 0)).ToList(),
            pagamentos.Select(p => new PagamentoAlunoDto(
                p.Pagamento.DataPagamento,
                p.Tipo.ToString(),
                FormatBrl(p.Pagamento.Valor),
                p.Pagamento.FormaPagamento.ToString())).ToList());
    }

    private static string FormatBrl(decimal valor)
        => $"R$ {valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"))}";
}
