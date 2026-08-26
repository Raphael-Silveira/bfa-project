using BFA.Application.Unidades.Professores;
using BFA.Domain.Professores;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BFA.Infrastructure.Unidades;

public sealed class ProfessoresUnidadeRepositorio(BfaDbContext dbContext)
    : IProfessoresUnidadeRepositorio
{
    private const string RestricaoCpfUnico = "uq_professores_organizacao_cpf";
    private const string RestricaoVinculoUnico =
        "uq_professores_unidades_professor_unidade";
    private const string MensagemSobreposicaoVigencia =
        "O periodo da remuneracao do professor sobrepoe uma vigencia existente.";

    public async Task<IReadOnlyList<ProfessorUnidadeResumo>> ListarAsync(
        Guid organizacaoId,
        Guid unidadeId,
        FiltroProfessoresUnidade filtro,
        CancellationToken cancellationToken)
    {
        return await (
            from vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
            join professor in dbContext.Professores.AsNoTracking()
                on vinculo.ProfessorId equals professor.Id
            join remuneracao in dbContext.ProfessoresRemuneracoes.AsNoTracking()
                    .Where(item => item.VigenciaFim == null)
                on vinculo.Id equals remuneracao.ProfessorUnidadeId into remuneracoes
            from remuneracao in remuneracoes.DefaultIfEmpty()
            where vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && professor.OrganizacaoId == organizacaoId
                && (filtro == FiltroProfessoresUnidade.Todos
                    || (filtro == FiltroProfessoresUnidade.Ativos && vinculo.Ativo)
                    || (filtro == FiltroProfessoresUnidade.Encerrados && !vinculo.Ativo))
            orderby professor.NomeCompleto, professor.Id
            select new ProfessorUnidadeResumo(
                professor.Id,
                professor.NomeCompleto,
                professor.Cpf,
                professor.Telefone,
                professor.Email,
                vinculo.Ativo,
                remuneracao == null ? null : remuneracao.Modalidade,
                remuneracao == null ? null : remuneracao.Valor,
                professor.UsuarioId,
                professor.UsuarioId == null
                    ? null
                    : dbContext.Users
                        .Where(usuario => usuario.Id == professor.UsuarioId)
                        .Select(usuario => usuario.UserName)
                        .FirstOrDefault(),
                professor.UsuarioId != null && dbContext.VinculosAcesso.Any(acesso =>
                    acesso.UsuarioId == professor.UsuarioId
                    && acesso.OrganizacaoId == organizacaoId
                    && acesso.UnidadeId == unidadeId
                    && acesso.Perfil == BFA.Domain.Acessos.PerfilAcesso.Professor
                    && acesso.Ativo)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ProfessorUnidadeGerenciamentoResumo?> ObterGerenciamentoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken)
    {
        return await (
            from vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
            join professor in dbContext.Professores.AsNoTracking()
                on vinculo.ProfessorId equals professor.Id
            join remuneracao in dbContext.ProfessoresRemuneracoes.AsNoTracking()
                    .Where(item => item.VigenciaFim == null)
                on vinculo.Id equals remuneracao.ProfessorUnidadeId into remuneracoes
            from remuneracao in remuneracoes.DefaultIfEmpty()
            where vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.ProfessorId == professorId
                && professor.OrganizacaoId == organizacaoId
            select new ProfessorUnidadeGerenciamentoResumo(
                professor.Id,
                professor.NomeCompleto,
                professor.Cpf,
                professor.Telefone,
                professor.Email,
                professor.Ativo,
                vinculo.Ativo,
                remuneracao == null ? null : remuneracao.Modalidade,
                remuneracao == null ? null : remuneracao.Valor,
                remuneracao == null ? null : remuneracao.VigenciaInicio))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ProfessorRemuneracaoGerenciamentoResumo?> ObterRemuneracaoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken)
    {
        var vinculo = await (
            from professor in dbContext.Professores.AsNoTracking()
            join item in dbContext.ProfessoresUnidades.AsNoTracking()
                on professor.Id equals item.ProfessorId
            where professor.OrganizacaoId == organizacaoId
                && item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && professor.Id == professorId
            select new
            {
                professor.Id,
                professor.NomeCompleto,
                VinculoId = item.Id,
                item.Ativo
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (vinculo is null)
        {
            return null;
        }

        var historico = await dbContext.ProfessoresRemuneracoes
            .AsNoTracking()
            .Where(item => item.OrganizacaoId == organizacaoId
                && item.ProfessorUnidadeId == vinculo.VinculoId)
            .OrderByDescending(item => item.VigenciaInicio)
            .ThenByDescending(item => item.CriadoEmUtc)
            .Select(item => new ProfessorRemuneracaoResumo(
                item.Id,
                item.Modalidade,
                item.Valor,
                item.VigenciaInicio,
                item.VigenciaFim,
                item.Observacao))
            .ToArrayAsync(cancellationToken);

        return new ProfessorRemuneracaoGerenciamentoResumo(
            vinculo.Id,
            vinculo.NomeCompleto,
            vinculo.Ativo,
            historico.SingleOrDefault(item => item.VigenciaFim == null),
            historico);
    }

    public Task<bool> ExisteCpfAsync(
        Guid organizacaoId, string cpf, CancellationToken cancellationToken) =>
        dbContext.Professores.AsNoTracking().AnyAsync(
            item => item.OrganizacaoId == organizacaoId && item.Cpf == cpf,
            cancellationToken);

    public async Task<IReadOnlyList<ProfessorExistenteResumo>> BuscarExistentesAsync(
        Guid organizacaoId,
        Guid unidadeId,
        string termo,
        CancellationToken cancellationToken)
    {
        var texto = termo.Trim().ToLower();
        var cpf = new string(termo.Where(char.IsDigit).ToArray());

        return await (
            from professor in dbContext.Professores.AsNoTracking()
            join vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
                    .Where(item => item.OrganizacaoId == organizacaoId
                        && item.UnidadeId == unidadeId)
                on professor.Id equals vinculo.ProfessorId into vinculos
            from vinculo in vinculos.DefaultIfEmpty()
            where professor.OrganizacaoId == organizacaoId
                && (professor.NomeCompleto.ToLower().Contains(texto)
                    || (professor.Email != null && professor.Email.ToLower().Contains(texto))
                    || (cpf.Length > 0 && professor.Cpf != null && professor.Cpf.Contains(cpf)))
            orderby professor.NomeCompleto, professor.Id
            select new ProfessorExistenteResumo(
                professor.Id,
                professor.NomeCompleto,
                professor.Cpf,
                professor.Telefone,
                professor.Email,
                professor.Ativo,
                vinculo == null
                    ? EstadoVinculoProfessorExistente.SemVinculo
                    : vinculo.Ativo
                        ? EstadoVinculoProfessorExistente.Ativo
                        : EstadoVinculoProfessorExistente.Inativo,
                null))
            .Take(30)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ProfessorExistenteResumo?> ObterExistenteAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken)
    {
        return await (
            from professor in dbContext.Professores.AsNoTracking()
            join vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
                    .Where(item => item.OrganizacaoId == organizacaoId
                        && item.UnidadeId == unidadeId)
                on professor.Id equals vinculo.ProfessorId into vinculos
            from vinculo in vinculos.DefaultIfEmpty()
            where professor.OrganizacaoId == organizacaoId
                && professor.Id == professorId
            select new ProfessorExistenteResumo(
                professor.Id,
                professor.NomeCompleto,
                professor.Cpf,
                professor.Telefone,
                professor.Email,
                professor.Ativo,
                vinculo == null
                    ? EstadoVinculoProfessorExistente.SemVinculo
                    : vinculo.Ativo
                        ? EstadoVinculoProfessorExistente.Ativo
                        : EstadoVinculoProfessorExistente.Inativo,
                vinculo == null
                    ? null
                    : dbContext.ProfessoresRemuneracoes.AsNoTracking()
                        .Where(item => item.OrganizacaoId == organizacaoId
                            && item.ProfessorUnidadeId == vinculo.Id
                            && item.VigenciaFim != null)
                        .Max(item => (DateOnly?)item.VigenciaFim)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<EstadoPersistenciaProfessorUnidade> CriarAsync(
        Professor professor,
        ProfessorUnidade vinculo,
        ProfessorRemuneracao remuneracao,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.Professores.Add(professor);
            dbContext.ProfessoresUnidades.Add(vinculo);
            dbContext.ProfessoresRemuneracoes.Add(remuneracao);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return EstadoPersistenciaProfessorUnidade.Sucesso;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgres
                && postgres.ConstraintName == RestricaoCpfUnico)
        {
            return EstadoPersistenciaProfessorUnidade.CpfDuplicado;
        }
    }

    public async Task<EstadoPersistenciaProfessorUnidade> VincularExistenteAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        ModalidadeRemuneracaoProfessor modalidade,
        decimal valor,
        DateOnly vigenciaInicio,
        string? observacao,
        Guid usuarioId,
        DateTime criadoEmUtc,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var professor = await dbContext.Professores.SingleOrDefaultAsync(
                item => item.OrganizacaoId == organizacaoId && item.Id == professorId,
                cancellationToken);
            if (professor is null)
            {
                return EstadoPersistenciaProfessorUnidade.ProfessorNaoEncontrado;
            }

            if (!professor.Ativo)
            {
                return EstadoPersistenciaProfessorUnidade.ProfessorInativo;
            }

            var vinculo = await dbContext.ProfessoresUnidades.SingleOrDefaultAsync(
                item => item.OrganizacaoId == organizacaoId
                    && item.ProfessorId == professorId
                    && item.UnidadeId == unidadeId,
                cancellationToken);
            if (vinculo?.Ativo == true)
            {
                return EstadoPersistenciaProfessorUnidade.JaVinculado;
            }

            if (vinculo is not null)
            {
                var ultimaVigenciaFim = await dbContext.ProfessoresRemuneracoes
                    .AsNoTracking()
                    .Where(item => item.OrganizacaoId == organizacaoId
                        && item.ProfessorUnidadeId == vinculo.Id
                        && item.VigenciaFim != null)
                    .MaxAsync(item => (DateOnly?)item.VigenciaFim, cancellationToken);
                if (ultimaVigenciaFim is { } termino && vigenciaInicio <= termino)
                {
                    return EstadoPersistenciaProfessorUnidade.VigenciaInicioInvalida;
                }
            }

            if (vinculo is null)
            {
                vinculo = new ProfessorUnidade(
                    Guid.NewGuid(), organizacaoId, professorId, unidadeId, criadoEmUtc);
                dbContext.ProfessoresUnidades.Add(vinculo);
            }
            else
            {
                vinculo.Ativar(criadoEmUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            dbContext.ProfessoresRemuneracoes.Add(new ProfessorRemuneracao(
                Guid.NewGuid(),
                organizacaoId,
                vinculo.Id,
                modalidade,
                valor,
                vigenciaInicio,
                null,
                usuarioId,
                criadoEmUtc,
                observacao));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return EstadoPersistenciaProfessorUnidade.Sucesso;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgres
                && postgres.ConstraintName == RestricaoVinculoUnico)
        {
            await transacao.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return EstadoPersistenciaProfessorUnidade.JaVinculado;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.CheckViolation
                && postgres.MessageText == MensagemSobreposicaoVigencia)
        {
            await transacao.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return EstadoPersistenciaProfessorUnidade.VigenciaInicioInvalida;
        }
        catch (DbUpdateException)
        {
            await transacao.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return EstadoPersistenciaProfessorUnidade.Falha;
        }
        catch (ArgumentException)
        {
            return EstadoPersistenciaProfessorUnidade.Falha;
        }
    }

    public async Task<EstadoPersistenciaProfessorUnidade> AtualizarCadastroAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        string nomeCompleto,
        string? cpf,
        string? telefone,
        string? email,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken)
    {
        var professor = await (
            from item in dbContext.Professores
            join vinculo in dbContext.ProfessoresUnidades
                on item.Id equals vinculo.ProfessorId
            where item.OrganizacaoId == organizacaoId
                && vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && item.Id == professorId
            select item)
            .SingleOrDefaultAsync(cancellationToken);
        if (professor is null)
        {
            return EstadoPersistenciaProfessorUnidade.VinculoNaoEncontrado;
        }

        try
        {
            professor.AtualizarDados(
                nomeCompleto, cpf, telefone, email, atualizadoEmUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            return EstadoPersistenciaProfessorUnidade.Sucesso;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgres
                && postgres.ConstraintName == RestricaoCpfUnico)
        {
            return EstadoPersistenciaProfessorUnidade.CpfDuplicado;
        }
        catch (ArgumentException)
        {
            return EstadoPersistenciaProfessorUnidade.Falha;
        }
    }

    public async Task<EstadoPersistenciaProfessorUnidade> EncerrarVinculoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        DateOnly dataEncerramento,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var vinculo = await dbContext.ProfessoresUnidades.SingleOrDefaultAsync(
            item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && item.ProfessorId == professorId,
            cancellationToken);
        if (vinculo is null)
        {
            return EstadoPersistenciaProfessorUnidade.VinculoNaoEncontrado;
        }
        if (!vinculo.Ativo)
        {
            return EstadoPersistenciaProfessorUnidade.VinculoJaEncerrado;
        }

        var remuneracao = await dbContext.ProfessoresRemuneracoes.SingleOrDefaultAsync(
            item => item.OrganizacaoId == organizacaoId
                && item.ProfessorUnidadeId == vinculo.Id
                && item.VigenciaFim == null,
            cancellationToken);
        if (remuneracao is not null && dataEncerramento < remuneracao.VigenciaInicio)
        {
            return EstadoPersistenciaProfessorUnidade.DataEncerramentoInvalida;
        }

        try
        {
            if (remuneracao is not null)
            {
                remuneracao.Encerrar(dataEncerramento);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            vinculo.Desativar(atualizadoEmUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return EstadoPersistenciaProfessorUnidade.Sucesso;
        }
        catch (ArgumentException)
        {
            return EstadoPersistenciaProfessorUnidade.DataEncerramentoInvalida;
        }
    }

    public async Task<EstadoPersistenciaProfessorUnidade> AlterarRemuneracaoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        ModalidadeRemuneracaoProfessor modalidade,
        decimal valor,
        DateOnly vigenciaInicio,
        string? observacao,
        Guid usuarioId,
        DateTime criadoEmUtc,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var vinculo = await dbContext.ProfessoresUnidades.SingleOrDefaultAsync(
            item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && item.ProfessorId == professorId,
            cancellationToken);
        if (vinculo is null)
        {
            return EstadoPersistenciaProfessorUnidade.VinculoNaoEncontrado;
        }
        if (!vinculo.Ativo)
        {
            return EstadoPersistenciaProfessorUnidade.VinculoJaEncerrado;
        }

        var remuneracaoAtual = await dbContext.ProfessoresRemuneracoes.SingleOrDefaultAsync(
            item => item.OrganizacaoId == organizacaoId
                && item.ProfessorUnidadeId == vinculo.Id
                && item.VigenciaFim == null,
            cancellationToken);
        if (remuneracaoAtual is null)
        {
            return EstadoPersistenciaProfessorUnidade.RemuneracaoNaoEncontrada;
        }
        if (vigenciaInicio <= remuneracaoAtual.VigenciaInicio)
        {
            return EstadoPersistenciaProfessorUnidade.VigenciaInicioInvalida;
        }

        try
        {
            var novaRemuneracao = new ProfessorRemuneracao(
                Guid.NewGuid(),
                organizacaoId,
                vinculo.Id,
                modalidade,
                valor,
                vigenciaInicio,
                null,
                usuarioId,
                criadoEmUtc,
                observacao);

            remuneracaoAtual.Encerrar(vigenciaInicio.AddDays(-1));
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.ProfessoresRemuneracoes.Add(novaRemuneracao);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return EstadoPersistenciaProfessorUnidade.Sucesso;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.CheckViolation
                && postgres.MessageText == MensagemSobreposicaoVigencia)
        {
            await transacao.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return EstadoPersistenciaProfessorUnidade.VigenciaInicioInvalida;
        }
        catch (DbUpdateException)
        {
            await transacao.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return EstadoPersistenciaProfessorUnidade.Falha;
        }
        catch (ArgumentException)
        {
            return EstadoPersistenciaProfessorUnidade.Falha;
        }
    }
}
