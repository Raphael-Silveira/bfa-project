using BFA.Application.Acessos;
using BFA.Domain.Acessos;
using BFA.Domain.Professores;

namespace BFA.Application.Unidades.Professores;

public sealed record CriarProfessorUnidadeSolicitacao(
    string NomeCompleto,
    string? Cpf,
    string? Telefone,
    string? Email,
    ModalidadeRemuneracaoProfessor Modalidade,
    decimal Valor,
    DateOnly VigenciaInicio,
    string? Observacao);

public sealed record VincularProfessorExistenteSolicitacao(
    Guid ProfessorId,
    ModalidadeRemuneracaoProfessor Modalidade,
    decimal Valor,
    DateOnly VigenciaInicio,
    string? Observacao);

public sealed record AtualizarProfessorSolicitacao(
    string NomeCompleto,
    string? Cpf,
    string? Telefone,
    string? Email);

public sealed record AlterarProfessorRemuneracaoSolicitacao(
    ModalidadeRemuneracaoProfessor Modalidade,
    decimal Valor,
    DateOnly VigenciaInicio,
    string? Observacao);

public enum EstadoProfessoresUnidade
{
    Sucesso,
    SemAcesso,
    UnidadeNaoEncontrada,
    CpfDuplicado,
    ProfessorNaoEncontrado,
    ProfessorInativo,
    JaVinculado,
    VinculoNaoEncontrado,
    VinculoJaEncerrado,
    DataEncerramentoInvalida,
    VigenciaInicioInvalida,
    RemuneracaoNaoEncontrada,
    DadosInvalidos,
    Falha
}

public sealed record ResultadoProfessoresUnidade<T>(
    EstadoProfessoresUnidade Estado,
    T? Valor = default);

public interface IProfessoresUnidadeConsulta
{
    Task<ResultadoProfessoresUnidade<IReadOnlyList<ProfessorUnidadeResumo>>> ListarAsync(
        Guid usuarioId,
        Guid unidadeId,
        FiltroProfessoresUnidade filtro,
        CancellationToken cancellationToken);

    Task<ResultadoProfessoresUnidade<IReadOnlyList<ProfessorExistenteResumo>>>
        BuscarExistentesAsync(
            Guid usuarioId,
            Guid unidadeId,
            string? termo,
            CancellationToken cancellationToken);

    Task<ResultadoProfessoresUnidade<ProfessorExistenteResumo>> ObterExistenteAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken);

    Task<ResultadoProfessoresUnidade<ProfessorUnidadeGerenciamentoResumo>>
        ObterGerenciamentoAsync(
            Guid usuarioId,
            Guid unidadeId,
            Guid professorId,
            CancellationToken cancellationToken);

    Task<ResultadoProfessoresUnidade<ProfessorRemuneracaoGerenciamentoResumo>>
        ObterRemuneracaoAsync(
            Guid usuarioId,
            Guid unidadeId,
            Guid professorId,
            CancellationToken cancellationToken);
}

public interface IProfessoresUnidadeServico
{
    Task<ResultadoProfessoresUnidade<Guid>> CriarAsync(
        Guid usuarioId,
        Guid unidadeId,
        CriarProfessorUnidadeSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoProfessoresUnidade<Guid>> VincularExistenteAsync(
        Guid usuarioId,
        Guid unidadeId,
        VincularProfessorExistenteSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoProfessoresUnidade<Guid>> AtualizarCadastroAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid professorId,
        AtualizarProfessorSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoProfessoresUnidade<Guid>> EncerrarVinculoAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid professorId,
        DateOnly dataEncerramento,
        CancellationToken cancellationToken);

    Task<ResultadoProfessoresUnidade<Guid>> AlterarRemuneracaoAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid professorId,
        AlterarProfessorRemuneracaoSolicitacao solicitacao,
        CancellationToken cancellationToken);
}

public sealed class ProfessoresUnidadeServico(
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IProfessoresUnidadeRepositorio repositorio,
    TimeProvider timeProvider) : IProfessoresUnidadeConsulta, IProfessoresUnidadeServico
{
    public async Task<ResultadoProfessoresUnidade<IReadOnlyList<ProfessorUnidadeResumo>>> ListarAsync(
        Guid usuarioId,
        Guid unidadeId,
        FiltroProfessoresUnidade filtro,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAutorizadoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            return new(contexto.Estado);
        }

        var professores = await repositorio.ListarAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, filtro, cancellationToken);
        return new(EstadoProfessoresUnidade.Sucesso, professores);
    }

    public async Task<ResultadoProfessoresUnidade<Guid>> CriarAsync(
        Guid usuarioId,
        Guid unidadeId,
        CriarProfessorUnidadeSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAutorizadoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            return new(contexto.Estado);
        }

        var cpf = NormalizarCpf(solicitacao.Cpf);
        if (cpf is { } valorCpf
            && await repositorio.ExisteCpfAsync(
                contexto.Valor!.OrganizacaoId, valorCpf, cancellationToken))
        {
            return new(EstadoProfessoresUnidade.CpfDuplicado);
        }

        if (solicitacao.VigenciaInicio == default)
        {
            return new(EstadoProfessoresUnidade.DadosInvalidos);
        }

        try
        {
            var agora = timeProvider.GetUtcNow().UtcDateTime;
            var professorId = Guid.NewGuid();
            var vinculoId = Guid.NewGuid();
            var professor = new Professor(
                professorId,
                contexto.Valor!.OrganizacaoId,
                solicitacao.NomeCompleto,
                agora,
                cpf: cpf,
                telefone: solicitacao.Telefone,
                email: solicitacao.Email);
            var vinculo = new ProfessorUnidade(
                vinculoId,
                contexto.Valor.OrganizacaoId,
                professorId,
                unidadeId,
                agora);
            var remuneracao = new ProfessorRemuneracao(
                Guid.NewGuid(),
                contexto.Valor.OrganizacaoId,
                vinculoId,
                solicitacao.Modalidade,
                solicitacao.Valor,
                solicitacao.VigenciaInicio,
                null,
                usuarioId,
                agora,
                solicitacao.Observacao);

            var estado = await repositorio.CriarAsync(
                professor, vinculo, remuneracao, cancellationToken);
            return estado switch
            {
                EstadoPersistenciaProfessorUnidade.Sucesso =>
                    new(EstadoProfessoresUnidade.Sucesso, professorId),
                EstadoPersistenciaProfessorUnidade.CpfDuplicado =>
                    new(EstadoProfessoresUnidade.CpfDuplicado),
                _ => new(EstadoProfessoresUnidade.Falha)
            };
        }
        catch (ArgumentException)
        {
            return new(EstadoProfessoresUnidade.DadosInvalidos);
        }
    }

    public async Task<ResultadoProfessoresUnidade<IReadOnlyList<ProfessorExistenteResumo>>>
        BuscarExistentesAsync(
            Guid usuarioId,
            Guid unidadeId,
            string? termo,
            CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAutorizadoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            return new(contexto.Estado);
        }

        var termoNormalizado = termo?.Trim();
        if (string.IsNullOrWhiteSpace(termoNormalizado) || termoNormalizado.Length < 2)
        {
            return new(EstadoProfessoresUnidade.Sucesso, []);
        }

        var encontrados = await repositorio.BuscarExistentesAsync(
            contexto.Valor!.OrganizacaoId,
            unidadeId,
            termoNormalizado,
            cancellationToken);
        return new(EstadoProfessoresUnidade.Sucesso, encontrados);
    }

    public async Task<ResultadoProfessoresUnidade<ProfessorExistenteResumo>> ObterExistenteAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAutorizadoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            return new(contexto.Estado);
        }

        var professor = await repositorio.ObterExistenteAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, professorId, cancellationToken);
        return professor is null
            ? new(EstadoProfessoresUnidade.ProfessorNaoEncontrado)
            : new(EstadoProfessoresUnidade.Sucesso, professor);
    }

    public async Task<ResultadoProfessoresUnidade<Guid>> VincularExistenteAsync(
        Guid usuarioId,
        Guid unidadeId,
        VincularProfessorExistenteSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAutorizadoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            return new(contexto.Estado);
        }

        if (solicitacao.ProfessorId == Guid.Empty
            || solicitacao.VigenciaInicio == default
            || solicitacao.Valor < 0
            || !Enum.IsDefined(solicitacao.Modalidade))
        {
            return new(EstadoProfessoresUnidade.DadosInvalidos);
        }

        var estado = await repositorio.VincularExistenteAsync(
            contexto.Valor!.OrganizacaoId,
            unidadeId,
            solicitacao.ProfessorId,
            solicitacao.Modalidade,
            solicitacao.Valor,
            solicitacao.VigenciaInicio,
            solicitacao.Observacao,
            usuarioId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        return estado switch
        {
            EstadoPersistenciaProfessorUnidade.Sucesso =>
                new(EstadoProfessoresUnidade.Sucesso, solicitacao.ProfessorId),
            EstadoPersistenciaProfessorUnidade.ProfessorNaoEncontrado =>
                new(EstadoProfessoresUnidade.ProfessorNaoEncontrado),
            EstadoPersistenciaProfessorUnidade.ProfessorInativo =>
                new(EstadoProfessoresUnidade.ProfessorInativo),
            EstadoPersistenciaProfessorUnidade.JaVinculado =>
                new(EstadoProfessoresUnidade.JaVinculado),
            EstadoPersistenciaProfessorUnidade.VigenciaInicioInvalida =>
                new(EstadoProfessoresUnidade.VigenciaInicioInvalida),
            _ => new(EstadoProfessoresUnidade.Falha)
        };
    }

    public async Task<ResultadoProfessoresUnidade<ProfessorUnidadeGerenciamentoResumo>>
        ObterGerenciamentoAsync(
            Guid usuarioId,
            Guid unidadeId,
            Guid professorId,
            CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAutorizadoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            return new(contexto.Estado);
        }

        var resumo = await repositorio.ObterGerenciamentoAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, professorId, cancellationToken);
        return resumo is null
            ? new(EstadoProfessoresUnidade.VinculoNaoEncontrado)
            : new(EstadoProfessoresUnidade.Sucesso, resumo);
    }

    public async Task<ResultadoProfessoresUnidade<ProfessorRemuneracaoGerenciamentoResumo>>
        ObterRemuneracaoAsync(
            Guid usuarioId,
            Guid unidadeId,
            Guid professorId,
            CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAutorizadoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            return new(contexto.Estado);
        }

        var resumo = await repositorio.ObterRemuneracaoAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, professorId, cancellationToken);
        return resumo is null
            ? new(EstadoProfessoresUnidade.VinculoNaoEncontrado)
            : new(EstadoProfessoresUnidade.Sucesso, resumo);
    }

    public async Task<ResultadoProfessoresUnidade<Guid>> AtualizarCadastroAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid professorId,
        AtualizarProfessorSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAutorizadoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            return new(contexto.Estado);
        }

        var cpf = NormalizarCpf(solicitacao.Cpf);
        var estado = await repositorio.AtualizarCadastroAsync(
            contexto.Valor!.OrganizacaoId,
            unidadeId,
            professorId,
            solicitacao.NomeCompleto,
            cpf,
            solicitacao.Telefone,
            solicitacao.Email,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        return MapearPersistencia(estado, professorId);
    }

    public async Task<ResultadoProfessoresUnidade<Guid>> EncerrarVinculoAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid professorId,
        DateOnly dataEncerramento,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAutorizadoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            return new(contexto.Estado);
        }

        if (dataEncerramento == default)
        {
            return new(EstadoProfessoresUnidade.DataEncerramentoInvalida);
        }

        var estado = await repositorio.EncerrarVinculoAsync(
            contexto.Valor!.OrganizacaoId,
            unidadeId,
            professorId,
            dataEncerramento,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        return MapearPersistencia(estado, professorId);
    }

    public async Task<ResultadoProfessoresUnidade<Guid>> AlterarRemuneracaoAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid professorId,
        AlterarProfessorRemuneracaoSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAutorizadoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            return new(contexto.Estado);
        }

        if (professorId == Guid.Empty
            || solicitacao.VigenciaInicio == default
            || solicitacao.Valor < 0
            || !Enum.IsDefined(solicitacao.Modalidade))
        {
            return new(EstadoProfessoresUnidade.DadosInvalidos);
        }

        var estado = await repositorio.AlterarRemuneracaoAsync(
            contexto.Valor!.OrganizacaoId,
            unidadeId,
            professorId,
            solicitacao.Modalidade,
            solicitacao.Valor,
            solicitacao.VigenciaInicio,
            solicitacao.Observacao,
            usuarioId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        return MapearPersistencia(estado, professorId);
    }

    private static ResultadoProfessoresUnidade<Guid> MapearPersistencia(
        EstadoPersistenciaProfessorUnidade estado,
        Guid professorId) => estado switch
        {
            EstadoPersistenciaProfessorUnidade.Sucesso =>
                new(EstadoProfessoresUnidade.Sucesso, professorId),
            EstadoPersistenciaProfessorUnidade.CpfDuplicado =>
                new(EstadoProfessoresUnidade.CpfDuplicado),
            EstadoPersistenciaProfessorUnidade.VinculoNaoEncontrado =>
                new(EstadoProfessoresUnidade.VinculoNaoEncontrado),
            EstadoPersistenciaProfessorUnidade.VinculoJaEncerrado =>
                new(EstadoProfessoresUnidade.VinculoJaEncerrado),
            EstadoPersistenciaProfessorUnidade.DataEncerramentoInvalida =>
                new(EstadoProfessoresUnidade.DataEncerramentoInvalida),
            EstadoPersistenciaProfessorUnidade.VigenciaInicioInvalida =>
                new(EstadoProfessoresUnidade.VigenciaInicioInvalida),
            EstadoPersistenciaProfessorUnidade.RemuneracaoNaoEncontrada =>
                new(EstadoProfessoresUnidade.RemuneracaoNaoEncontrada),
            _ => new(EstadoProfessoresUnidade.Falha)
        };

    private async Task<ResultadoProfessoresUnidade<UnidadeContextoResumo>>
        ObterContextoAutorizadoAsync(
            Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
    {
        var contexto = await unidadeContextoConsulta.ObterAtivaAsync(unidadeId, cancellationToken);
        if (contexto is null)
        {
            return new(EstadoProfessoresUnidade.UnidadeNaoEncontrada);
        }

        var administradorUnidade = await acessoUsuarioConsulta.PossuiPerfilNaUnidadeAsync(
            usuarioId,
            contexto.OrganizacaoId,
            unidadeId,
            PerfilAcesso.AdministradorUnidade,
            cancellationToken);
        var administradorRede = administradorUnidade ||
            await acessoUsuarioConsulta.EhAdministradorRedeNaOrganizacaoAsync(
                usuarioId, contexto.OrganizacaoId, cancellationToken);

        return administradorRede
            ? new(EstadoProfessoresUnidade.Sucesso, contexto)
            : new(EstadoProfessoresUnidade.SemAcesso);
    }

    private static string? NormalizarCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return null;
        }

        var digitos = new string(cpf.Where(char.IsDigit).ToArray());
        return digitos.Length == Professor.CpfTamanho ? digitos : cpf.Trim();
    }
}
