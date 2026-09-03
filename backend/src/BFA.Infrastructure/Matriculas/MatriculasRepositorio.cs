using BFA.Application.Matriculas;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Unidades;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BFA.Infrastructure.Matriculas;

public sealed class MatriculasRepositorio(BfaDbContext dbContext)
    : IMatriculasRepositorio
{
    public async Task<IReadOnlyList<MatriculaListaItem>> ListarAsync(
        Guid organizacaoId, Guid unidadeId, string? texto, StatusMatricula? status,
        CancellationToken cancellationToken)
    {
        var consulta =
            from matricula in dbContext.Matriculas.AsNoTracking()
            where matricula.OrganizacaoId == organizacaoId
                && matricula.UnidadeId == unidadeId
            join aluno in dbContext.Alunos.AsNoTracking()
                on new { matricula.OrganizacaoId, Id = matricula.AlunoId }
                equals new { aluno.OrganizacaoId, aluno.Id }
            join versao in dbContext.PlanosVersoes.AsNoTracking()
                on new { matricula.OrganizacaoId, Id = matricula.PlanoVersaoId }
                equals new { versao.OrganizacaoId, versao.Id }
            join plano in dbContext.Planos.AsNoTracking()
                on new { versao.OrganizacaoId, Id = versao.PlanoId }
                equals new { plano.OrganizacaoId, plano.Id }
            select new { matricula, aluno, versao, plano };

        if (status.HasValue)
            consulta = consulta.Where(item => item.matricula.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var termo = texto.Trim().ToUpper();
            consulta = consulta.Where(item => item.aluno.NomeCompleto.ToUpper().Contains(termo));
        }

        return await consulta
            .OrderBy(item => item.aluno.NomeCompleto)
            .ThenByDescending(item => item.matricula.DataInicio)
            .Select(item => new MatriculaListaItem(
                item.matricula.Id,
                item.aluno.Id,
                item.aluno.NomeCompleto,
                item.matricula.Status,
                item.matricula.DataInicio,
                item.matricula.DataFimPrevista,
                item.matricula.DataFimReal,
                item.plano.Nome,
                item.versao.NumeroVersao,
                item.versao.FrequenciaSemanal,
                item.matricula.ValorMensalContratado,
                dbContext.MatriculasHorarios.Count(grade =>
                    grade.OrganizacaoId == organizacaoId
                    && grade.UnidadeId == unidadeId
                    && grade.MatriculaId == item.matricula.Id
                    && grade.VigenciaFim == null)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<MatriculaDetalhe?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid matriculaId,
        CancellationToken cancellationToken)
    {
        var basico = await (
            from matricula in dbContext.Matriculas.AsNoTracking()
            where matricula.Id == matriculaId
                && matricula.OrganizacaoId == organizacaoId
                && matricula.UnidadeId == unidadeId
            join aluno in dbContext.Alunos.AsNoTracking()
                on new { matricula.OrganizacaoId, Id = matricula.AlunoId }
                equals new { aluno.OrganizacaoId, aluno.Id }
            join versao in dbContext.PlanosVersoes.AsNoTracking()
                on new { matricula.OrganizacaoId, Id = matricula.PlanoVersaoId }
                equals new { versao.OrganizacaoId, versao.Id }
            join plano in dbContext.Planos.AsNoTracking()
                on new { versao.OrganizacaoId, Id = versao.PlanoId }
                equals new { plano.OrganizacaoId, plano.Id }
            select new
            {
                Matricula = matricula,
                Aluno = aluno,
                Versao = versao,
                Plano = plano
            }).SingleOrDefaultAsync(cancellationToken);
        if (basico is null) return null;

        var responsaveis = await (
            from vinculo in dbContext.AlunosResponsaveis.AsNoTracking()
            where vinculo.OrganizacaoId == organizacaoId
                && vinculo.AlunoId == basico.Aluno.Id
            join responsavel in dbContext.Responsaveis.AsNoTracking()
                on new { vinculo.OrganizacaoId, Id = vinculo.ResponsavelId }
                equals new { responsavel.OrganizacaoId, responsavel.Id }
            orderby vinculo.Ativo descending, vinculo.PrincipalContato descending,
                responsavel.NomeCompleto
            select new ResponsavelMatriculaResumo(
                responsavel.Id,
                responsavel.NomeCompleto,
                responsavel.Telefone,
                responsavel.Email,
                vinculo.TipoRelacao,
                vinculo.DescricaoRelacao,
                vinculo.PrincipalContato,
                vinculo.ResponsavelFinanceiro,
                vinculo.Ativo,
                responsavel.Ativo)).ToArrayAsync(cancellationToken);

        var grade = await (
            from item in dbContext.MatriculasHorarios.AsNoTracking()
            where item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && item.MatriculaId == matriculaId
            join horario in dbContext.TurmasHorarios.AsNoTracking()
                on new { item.OrganizacaoId, item.UnidadeId, Id = item.TurmaHorarioId }
                equals new { horario.OrganizacaoId, horario.UnidadeId, horario.Id }
            join turma in dbContext.Turmas.AsNoTracking()
                on new { horario.OrganizacaoId, horario.UnidadeId, Id = horario.TurmaId }
                equals new { turma.OrganizacaoId, turma.UnidadeId, turma.Id }
            join vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
                on new
                {
                    horario.OrganizacaoId,
                    horario.UnidadeId,
                    Id = horario.ProfessorUnidadeId
                }
                equals new { vinculo.OrganizacaoId, vinculo.UnidadeId, vinculo.Id }
            join professor in dbContext.Professores.AsNoTracking()
                on new { vinculo.OrganizacaoId, Id = vinculo.ProfessorId }
                equals new { professor.OrganizacaoId, professor.Id }
            orderby item.VigenciaInicio descending, horario.DiaSemana, horario.HoraInicio
            select new GradeMatriculaResumo(
                item.Id,
                horario.Id,
                turma.Id,
                turma.Nome,
                professor.NomeCompleto,
                horario.DiaSemana,
                horario.HoraInicio,
                horario.HoraFim,
                item.VigenciaInicio,
                item.VigenciaFim)).ToArrayAsync(cancellationToken);

        return new(
            basico.Matricula.Id,
            basico.Aluno.Id,
            basico.Aluno.NomeCompleto,
            basico.Aluno.DataNascimento,
            basico.Aluno.Cpf,
            basico.Aluno.Telefone,
            basico.Aluno.Email,
            basico.Matricula.Status,
            basico.Matricula.DataInicio,
            basico.Matricula.DataFimPrevista,
            basico.Matricula.DataFimReal,
            basico.Plano.Id,
            basico.Versao.Id,
            basico.Plano.Nome,
            basico.Versao.NumeroVersao,
            basico.Versao.DuracaoMeses,
            basico.Versao.FrequenciaSemanal,
            basico.Versao.ValorMensal,
            basico.Matricula.ValorMensalContratado,
            basico.Matricula.CobraTaxaMatricula,
            basico.Matricula.ValorTaxaMatricula,
            responsaveis,
            grade.Where(item => item.VigenciaFim is null).ToArray(),
            grade.Where(item => item.VigenciaFim is not null).ToArray());
    }

    public async Task<IReadOnlyList<AlunoRelacionadoUnidadeResumo>>
        ListarAlunosRelacionadosAsync(
            Guid organizacaoId, Guid unidadeId, string? texto,
            CancellationToken cancellationToken)
    {
        var consulta =
            from matricula in dbContext.Matriculas.AsNoTracking()
            where matricula.OrganizacaoId == organizacaoId
                && matricula.UnidadeId == unidadeId
            join aluno in dbContext.Alunos.AsNoTracking()
                on new { matricula.OrganizacaoId, Id = matricula.AlunoId }
                equals new { aluno.OrganizacaoId, aluno.Id }
            select new { matricula, aluno };
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var termo = texto.Trim().ToUpper();
            consulta = consulta.Where(item => item.aluno.NomeCompleto.ToUpper().Contains(termo));
        }
        var alunos = await consulta.GroupBy(item => new
            { item.aluno.Id, item.aluno.NomeCompleto, item.aluno.DataNascimento })
            .OrderBy(grupo => grupo.Key.NomeCompleto)
            .Select(grupo => new AlunoRelacionadoUnidadeResumo(
                grupo.Key.Id,
                grupo.Key.NomeCompleto,
                grupo.Key.DataNascimento,
                grupo.Any(item => item.matricula.Status == StatusMatricula.Ativa),
                Array.Empty<ResponsavelMatriculaResumo>()))
            .ToArrayAsync(cancellationToken);

        if (alunos.Length == 0) return alunos;

        var alunoIds = alunos.Select(item => item.AlunoId).ToArray();
        var responsaveis = await (
            from vinculo in dbContext.AlunosResponsaveis.AsNoTracking()
            where vinculo.OrganizacaoId == organizacaoId
                && alunoIds.Contains(vinculo.AlunoId)
                && vinculo.Ativo
            join responsavel in dbContext.Responsaveis.AsNoTracking()
                on new { vinculo.OrganizacaoId, Id = vinculo.ResponsavelId }
                equals new { responsavel.OrganizacaoId, responsavel.Id }
            where responsavel.Ativo
            orderby vinculo.PrincipalContato descending, responsavel.NomeCompleto
            select new
            {
                vinculo.AlunoId,
                Resumo = new ResponsavelMatriculaResumo(
                    responsavel.Id,
                    responsavel.NomeCompleto,
                    responsavel.Telefone,
                    responsavel.Email,
                    vinculo.TipoRelacao,
                    vinculo.DescricaoRelacao,
                    vinculo.PrincipalContato,
                    vinculo.ResponsavelFinanceiro,
                    vinculo.Ativo,
                    responsavel.Ativo)
            }).ToArrayAsync(cancellationToken);

        return alunos.Select(aluno => aluno with
        {
            Responsaveis = responsaveis
                .Where(item => item.AlunoId == aluno.AlunoId)
                .Select(item => item.Resumo)
                .ToArray()
        }).ToArray();
    }

    public async Task<IReadOnlyList<PlanoElegivelMatriculaResumo>>
        ListarPlanosElegiveisAsync(
            Guid organizacaoId, Guid unidadeId, DateOnly dataInicio,
            CancellationToken cancellationToken) => await (
        from versao in dbContext.PlanosVersoes.AsNoTracking()
        join plano in dbContext.Planos.AsNoTracking()
            on new { versao.OrganizacaoId, Id = versao.PlanoId }
            equals new { plano.OrganizacaoId, plano.Id }
        where versao.OrganizacaoId == organizacaoId
            && plano.Ativo
            && versao.VigenciaInicio <= dataInicio
            && (versao.VigenciaFim == null || versao.VigenciaFim >= dataInicio)
            && (plano.UnidadeId == unidadeId
                || plano.UnidadeId == null
                && dbContext.PlanosDisponibilidadesUnidades.Any(disponibilidade =>
                    disponibilidade.OrganizacaoId == organizacaoId
                    && disponibilidade.UnidadeId == unidadeId
                    && disponibilidade.PlanoId == plano.Id
                    && disponibilidade.Ativo))
        orderby plano.Nome, versao.NumeroVersao descending
        select new PlanoElegivelMatriculaResumo(
            plano.Id,
            versao.Id,
            plano.Nome,
            versao.NumeroVersao,
            versao.DuracaoMeses,
            versao.FrequenciaSemanal,
            versao.ValorMensal,
            versao.CobraMatricula,
            versao.ValorMatricula,
            plano.UnidadeId == null
                ? EscopoPlanoMatricula.Rede
                : EscopoPlanoMatricula.Local)).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<HorarioElegivelMatriculaResumo>>
        ListarHorariosElegiveisAsync(
            Guid organizacaoId, Guid unidadeId, DateOnly dataInicio, DateOnly dataFim,
            CancellationToken cancellationToken)
    {
        var horarios = await CarregarHorariosAsync(
            organizacaoId, unidadeId, null, dataInicio, cancellationToken);
        var ids = horarios.Select(item => item.Id).ToArray();
        var ocupacoes = await dbContext.MatriculasHorarios.AsNoTracking()
            .Where(item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && ids.Contains(item.TurmaHorarioId)
                && item.VigenciaInicio <= dataFim
                && (item.VigenciaFim == null || item.VigenciaFim >= dataInicio))
            .Select(item => new IntervaloOcupacao(
                item.TurmaHorarioId, item.VigenciaInicio, item.VigenciaFim))
            .ToArrayAsync(cancellationToken);
        return horarios.OrderBy(item => item.NomeTurma)
            .ThenBy(item => item.DiaSemana).ThenBy(item => item.HoraInicio)
            .Select(item =>
            {
                var ocupacao = RegraGradeMatricula.MaximoSimultaneo(
                    ocupacoes.Where(intervalo => intervalo.TurmaHorarioId == item.Id)
                        .Select(intervalo => new IntervaloVigenciaGrade(
                            intervalo.Inicio, intervalo.Fim)),
                    dataInicio,
                    dataFim);
                return new HorarioElegivelMatriculaResumo(
                    item.Id, item.TurmaId, item.NomeTurma, item.Professor,
                    item.DiaSemana, item.HoraInicio, item.HoraFim,
                    item.Capacidade, ocupacao, Math.Max(0, item.Capacidade - ocupacao));
            }).ToArray();
    }

    public async Task<ResultadoMatriculas<ResultadoCriacaoMatricula>> CriarAsync(
        Guid organizacaoId, Guid unidadeId, Guid usuarioId,
        bool permitirReusoOrganizacional, CriarMatriculaSolicitacao solicitacao,
        DateOnly dataCivilAtual, DateTime agoraUtc,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        try
        {
            var plano = await ResolverPlanoElegivelAsync(
                organizacaoId, unidadeId, solicitacao.PlanoVersaoId,
                solicitacao.DataInicio, cancellationToken);
            if (plano is null)
                return new(EstadoMatriculas.PlanoNaoElegivel);

            var idsHorarios = solicitacao.TurmaHorarioIds.OrderBy(id => id).ToArray();
            if (idsHorarios.Length > plano.FrequenciaSemanal)
                return new(EstadoMatriculas.FrequenciaExcedida);

            var alunoResultado = await ResolverAlunoAsync(
                organizacaoId, unidadeId, permitirReusoOrganizacional,
                solicitacao, dataCivilAtual, agoraUtc, cancellationToken);
            if (alunoResultado.Estado != EstadoMatriculas.Sucesso
                || alunoResultado.Aluno is null)
                return new(alunoResultado.Estado);
            var aluno = alunoResultado.Aluno;

            var responsaveisResultado = await ResolverResponsaveisAsync(
                organizacaoId, aluno, solicitacao.Responsaveis,
                agoraUtc, cancellationToken);
            if (responsaveisResultado.Estado != EstadoMatriculas.Sucesso)
                return new(responsaveisResultado.Estado);
            try
            {
                RegraResponsavelMatricula.Validar(
                    aluno, solicitacao.DataInicio, responsaveisResultado.Vinculados);
            }
            catch (InvalidOperationException)
            {
                return new(EstadoMatriculas.MenorSemResponsavel);
            }

            if (dbContext.ChangeTracker.Entries().Any(item =>
                    item.State is EntityState.Added or EntityState.Modified))
                await dbContext.SaveChangesAsync(cancellationToken);

            var matricula = new Matricula(
                Guid.NewGuid(), organizacaoId, unidadeId, aluno.Id,
                plano.PlanoVersaoId, solicitacao.DataInicio, plano.DuracaoMeses,
                solicitacao.ValorMensalContratado,
                solicitacao.CobraTaxaMatricula,
                solicitacao.ValorTaxaMatricula,
                usuarioId,
                agoraUtc);
            dbContext.Matriculas.Add(matricula);
            await dbContext.SaveChangesAsync(cancellationToken);

            await GradeLoteLocks.BloquearTurmasHorariosAsync(
                dbContext, organizacaoId, unidadeId, idsHorarios, cancellationToken);
            var estadoGrade = await ValidarGradePropostaAsync(
                organizacaoId, unidadeId, aluno.Id, matricula.Id,
                idsHorarios, solicitacao.DataInicio, plano.FrequenciaSemanal,
                new HashSet<Guid>(), cancellationToken);
            if (estadoGrade != EstadoMatriculas.Sucesso)
                return new(estadoGrade);

            var grade = idsHorarios.Select(id => new MatriculaHorario(
                Guid.NewGuid(), organizacaoId, unidadeId, matricula.Id, id,
                solicitacao.DataInicio, usuarioId, agoraUtc)).ToArray();
            dbContext.MatriculasHorarios.AddRange(grade);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return new(EstadoMatriculas.Sucesso,
                new(matricula.Id, aluno.Id, grade.Length));
        }
        catch (ArgumentException)
        {
            await transacao.RollbackAsync(cancellationToken);
            return new(EstadoMatriculas.DadosInvalidos);
        }
        catch (InvalidOperationException)
        {
            await transacao.RollbackAsync(cancellationToken);
            return new(EstadoMatriculas.DadosInvalidos);
        }
        catch (DbUpdateException exception)
        {
            await transacao.RollbackAsync(cancellationToken);
            return new(MapearErroBanco(exception));
        }
    }

    public async Task<ResultadoMatriculas<ResultadoAlteracaoGrade>> AlterarGradeAsync(
        Guid organizacaoId, Guid unidadeId, Guid matriculaId, Guid usuarioId,
        AlterarGradeMatriculaSolicitacao solicitacao, DateTime agoraUtc,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        try
        {
            var matricula = await dbContext.Matriculas.SingleOrDefaultAsync(item =>
                item.Id == matriculaId && item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId, cancellationToken);
            if (matricula is null) return new(EstadoMatriculas.MatriculaNaoEncontrada);
            if (matricula.Status != StatusMatricula.Ativa)
                return new(EstadoMatriculas.EstadoTerminal);
            var data = solicitacao.DataInicioNovaConfiguracao;
            if (data < matricula.DataInicio || data > matricula.DataFimPrevista)
                return new(EstadoMatriculas.DataInvalida);

            await GradeLoteLocks.BloquearMatriculasAsync(
                dbContext, organizacaoId, unidadeId, [matriculaId], cancellationToken);
            await GradeLoteLocks.BloquearAlunosAsync(
                dbContext, organizacaoId, [matricula.AlunoId], cancellationToken);
            await dbContext.Entry(matricula).ReloadAsync(cancellationToken);
            if (matricula.Status != StatusMatricula.Ativa)
                return new(EstadoMatriculas.EstadoTerminal);

            var atuais = await dbContext.MatriculasHorarios
                .Where(item => item.OrganizacaoId == organizacaoId
                    && item.UnidadeId == unidadeId
                    && item.MatriculaId == matriculaId
                    && item.VigenciaFim == null)
                .OrderBy(item => item.TurmaHorarioId).ThenBy(item => item.Id)
                .ToArrayAsync(cancellationToken);
            var idsNovos = solicitacao.TurmaHorarioIds.OrderBy(id => id).ToArray();
            var conjuntoNovo = idsNovos.ToHashSet();
            var preservados = atuais.Where(item => conjuntoNovo.Contains(item.TurmaHorarioId))
                .ToArray();
            var removidos = atuais.Where(item => !conjuntoNovo.Contains(item.TurmaHorarioId))
                .ToArray();
            var idsAtuais = atuais.Select(item => item.TurmaHorarioId).ToHashSet();
            var adicionados = idsNovos.Where(id => !idsAtuais.Contains(id)).ToArray();
            if (removidos.Any(item => data <= item.VigenciaInicio))
                return new(EstadoMatriculas.DataInvalida);

            var idsBloqueio = atuais.Select(item => item.TurmaHorarioId)
                .Concat(idsNovos).Distinct().OrderBy(id => id).ToArray();
            await GradeLoteLocks.BloquearTurmasHorariosAsync(
                dbContext, organizacaoId, unidadeId, idsBloqueio, cancellationToken);
            var frequencia = await dbContext.PlanosVersoes.AsNoTracking()
                .Where(item => item.OrganizacaoId == organizacaoId
                    && item.Id == matricula.PlanoVersaoId)
                .Select(item => item.FrequenciaSemanal)
                .SingleAsync(cancellationToken);
            var estadoGrade = await ValidarGradePropostaAsync(
                organizacaoId, unidadeId, matricula.AlunoId, matriculaId,
                idsNovos, data, frequencia,
                atuais.Select(item => item.Id).ToHashSet(), cancellationToken);
            if (estadoGrade != EstadoMatriculas.Sucesso)
                return new(estadoGrade);

            var fimAnterior = data.AddDays(-1);
            foreach (var removido in removidos)
                removido.Encerrar(fimAnterior, usuarioId, agoraUtc);
            if (removidos.Length > 0)
                await dbContext.SaveChangesAsync(cancellationToken);

            var novos = adicionados.Select(id => new MatriculaHorario(
                Guid.NewGuid(), organizacaoId, unidadeId, matriculaId, id,
                data, usuarioId, agoraUtc)).ToArray();
            dbContext.MatriculasHorarios.AddRange(novos);
            if (novos.Length > 0)
                await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return new(EstadoMatriculas.Sucesso,
                new(preservados.Length, removidos.Length, novos.Length));
        }
        catch (ArgumentException)
        {
            await transacao.RollbackAsync(cancellationToken);
            return new(EstadoMatriculas.DataInvalida);
        }
        catch (DbUpdateException exception)
        {
            await transacao.RollbackAsync(cancellationToken);
            return new(MapearErroBanco(exception));
        }
    }

    public async Task<EstadoMatriculas> FinalizarAsync(
        Guid organizacaoId, Guid unidadeId, Guid matriculaId, Guid usuarioId,
        DateOnly dataFinalEfetiva, bool cancelar, DateTime agoraUtc,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        try
        {
            var matricula = await dbContext.Matriculas.SingleOrDefaultAsync(item =>
                item.Id == matriculaId && item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId, cancellationToken);
            if (matricula is null) return EstadoMatriculas.MatriculaNaoEncontrada;
            await GradeLoteLocks.BloquearMatriculasAsync(
                dbContext, organizacaoId, unidadeId, [matriculaId], cancellationToken);
            await GradeLoteLocks.BloquearAlunosAsync(
                dbContext, organizacaoId, [matricula.AlunoId], cancellationToken);
            await dbContext.Entry(matricula).ReloadAsync(cancellationToken);
            if (matricula.Status != StatusMatricula.Ativa)
                return EstadoMatriculas.EstadoTerminal;
            if (dataFinalEfetiva < matricula.DataInicio)
                return EstadoMatriculas.DataInvalida;

            var grades = await dbContext.MatriculasHorarios
                .Where(item => item.OrganizacaoId == organizacaoId
                    && item.UnidadeId == unidadeId
                    && item.MatriculaId == matriculaId
                    && item.VigenciaFim == null)
                .OrderBy(item => item.TurmaHorarioId).ThenBy(item => item.Id)
                .ToArrayAsync(cancellationToken);
            if (grades.Any(item => dataFinalEfetiva < item.VigenciaInicio))
                return EstadoMatriculas.DataInvalida;
            await GradeLoteLocks.BloquearTurmasHorariosAsync(
                dbContext, organizacaoId, unidadeId,
                grades.Select(item => item.TurmaHorarioId), cancellationToken);

            foreach (var grade in grades)
                grade.Encerrar(dataFinalEfetiva, usuarioId, agoraUtc);
            if (grades.Length > 0)
                await dbContext.SaveChangesAsync(cancellationToken);
            if (cancelar)
                matricula.Cancelar(dataFinalEfetiva, usuarioId, agoraUtc);
            else
                matricula.Encerrar(dataFinalEfetiva, usuarioId, agoraUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return EstadoMatriculas.Sucesso;
        }
        catch (ArgumentException)
        {
            await transacao.RollbackAsync(cancellationToken);
            return EstadoMatriculas.DataInvalida;
        }
        catch (InvalidOperationException)
        {
            await transacao.RollbackAsync(cancellationToken);
            return EstadoMatriculas.EstadoTerminal;
        }
        catch (DbUpdateException exception)
        {
            await transacao.RollbackAsync(cancellationToken);
            return MapearErroBanco(exception);
        }
    }

    private async Task<ResultadoAluno> ResolverAlunoAsync(
        Guid organizacaoId, Guid unidadeId, bool permitirReusoOrganizacional,
        CriarMatriculaSolicitacao solicitacao, DateOnly dataCivilAtual,
        DateTime agoraUtc, CancellationToken cancellationToken)
    {
        if (solicitacao.AlunoId.HasValue)
        {
            var aluno = await dbContext.Alunos.SingleOrDefaultAsync(item =>
                item.Id == solicitacao.AlunoId.Value
                && item.OrganizacaoId == organizacaoId && item.Ativo,
                cancellationToken);
            if (aluno is null) return new(EstadoMatriculas.AlunoNaoEncontrado, null);
            if (!permitirReusoOrganizacional
                && !await dbContext.Matriculas.AsNoTracking().AnyAsync(item =>
                    item.OrganizacaoId == organizacaoId
                    && item.UnidadeId == unidadeId
                    && item.AlunoId == aluno.Id, cancellationToken))
                return new(EstadoMatriculas.AlunoNaoRelacionadoUnidade, null);
            return new(EstadoMatriculas.Sucesso, aluno);
        }

        var novo = solicitacao.NovoAluno!;
        var cpf = NormalizarCpf(novo.Cpf);
        if (cpf is not null && await dbContext.Alunos.AsNoTracking().AnyAsync(item =>
                item.OrganizacaoId == organizacaoId && item.Cpf == cpf,
                cancellationToken))
            return new(EstadoMatriculas.AlunoDuplicado, null);
        var criado = new Aluno(
            Guid.NewGuid(), organizacaoId, novo.NomeCompleto,
            novo.DataNascimento, dataCivilAtual, agoraUtc,
            cpf: cpf, telefone: novo.Telefone, email: novo.Email);
        dbContext.Alunos.Add(criado);
        return new(EstadoMatriculas.Sucesso, criado);
    }

    private async Task<ResultadoResponsaveis> ResolverResponsaveisAsync(
        Guid organizacaoId, Aluno aluno,
        IReadOnlyList<NovoResponsavelMatriculaSolicitacao> solicitados,
        DateTime agoraUtc, CancellationToken cancellationToken)
    {
        if (solicitados.Count(item => item.PrincipalContato) > 1)
            return new(EstadoMatriculas.ResponsavelInvalido, []);
        var cpfs = solicitados.Select(item => NormalizarCpf(item.Cpf))
            .Where(item => item is not null).Cast<string>().ToArray();
        if (cpfs.Distinct(StringComparer.Ordinal).Count() != cpfs.Length)
            return new(EstadoMatriculas.ResponsavelDuplicado, []);

        var vinculados = await (
            from vinculo in dbContext.AlunosResponsaveis
            where vinculo.OrganizacaoId == organizacaoId
                && vinculo.AlunoId == aluno.Id
            join responsavel in dbContext.Responsaveis
                on new { vinculo.OrganizacaoId, Id = vinculo.ResponsavelId }
                equals new { responsavel.OrganizacaoId, responsavel.Id }
            select new ResponsavelVinculadoMatricula(vinculo, responsavel))
            .ToListAsync(cancellationToken);

        foreach (var solicitado in solicitados)
        {
            var cpf = NormalizarCpf(solicitado.Cpf);
            Responsavel? responsavel = null;
            if (cpf is not null)
                responsavel = await dbContext.Responsaveis.SingleOrDefaultAsync(item =>
                    item.OrganizacaoId == organizacaoId && item.Cpf == cpf,
                    cancellationToken);
            if (responsavel is null)
            {
                responsavel = new Responsavel(
                    Guid.NewGuid(), organizacaoId, solicitado.NomeCompleto, agoraUtc,
                    cpf: cpf, telefone: solicitado.Telefone, email: solicitado.Email);
                dbContext.Responsaveis.Add(responsavel);
            }
            else
            {
                responsavel.AtualizarDados(
                    solicitado.NomeCompleto, cpf, solicitado.Telefone,
                    solicitado.Email, agoraUtc);
                if (!responsavel.Ativo) responsavel.Ativar(agoraUtc);
            }

            var vinculo = vinculados.SingleOrDefault(item =>
                item.Responsavel.Id == responsavel.Id)?.Vinculo;
            if (solicitado.PrincipalContato && vinculados.Any(item =>
                    item.Vinculo.Id != vinculo?.Id
                    && item.Vinculo.Ativo && item.Vinculo.PrincipalContato))
                return new(EstadoMatriculas.ResponsavelInvalido, []);
            if (vinculo is null)
            {
                vinculo = new AlunoResponsavel(
                    Guid.NewGuid(), organizacaoId, aluno.Id, responsavel.Id,
                    solicitado.TipoRelacao, solicitado.PrincipalContato,
                    solicitado.ResponsavelFinanceiro, agoraUtc,
                    solicitado.DescricaoRelacao);
                dbContext.AlunosResponsaveis.Add(vinculo);
                vinculados.Add(new(vinculo, responsavel));
            }
            else
            {
                vinculo.AtualizarClassificacao(
                    solicitado.TipoRelacao, solicitado.DescricaoRelacao,
                    solicitado.PrincipalContato, solicitado.ResponsavelFinanceiro,
                    agoraUtc);
                if (!vinculo.Ativo) vinculo.Ativar(agoraUtc);
            }
        }
        return new(EstadoMatriculas.Sucesso, vinculados);
    }

    private async Task<PlanoPersistencia?> ResolverPlanoElegivelAsync(
        Guid organizacaoId, Guid unidadeId, Guid planoVersaoId, DateOnly dataInicio,
        CancellationToken cancellationToken) => await (
        from versao in dbContext.PlanosVersoes.AsNoTracking()
        where versao.OrganizacaoId == organizacaoId && versao.Id == planoVersaoId
        join plano in dbContext.Planos.AsNoTracking()
            on new { versao.OrganizacaoId, Id = versao.PlanoId }
            equals new { plano.OrganizacaoId, plano.Id }
        where plano.Ativo
            && versao.VigenciaInicio <= dataInicio
            && (versao.VigenciaFim == null || versao.VigenciaFim >= dataInicio)
            && (plano.UnidadeId == unidadeId
                || plano.UnidadeId == null
                && dbContext.PlanosDisponibilidadesUnidades.Any(disponibilidade =>
                    disponibilidade.OrganizacaoId == organizacaoId
                    && disponibilidade.UnidadeId == unidadeId
                    && disponibilidade.PlanoId == plano.Id
                    && disponibilidade.Ativo))
        select new PlanoPersistencia(
            versao.Id, versao.DuracaoMeses, versao.FrequenciaSemanal))
        .SingleOrDefaultAsync(cancellationToken);

    private async Task<EstadoMatriculas> ValidarGradePropostaAsync(
        Guid organizacaoId, Guid unidadeId, Guid alunoId, Guid matriculaId,
        IReadOnlyList<Guid> idsHorarios, DateOnly inicio, int frequenciaSemanal,
        IReadOnlySet<Guid> idsGradesIgnorados, CancellationToken cancellationToken)
    {
        if (idsHorarios.Count > frequenciaSemanal)
            return EstadoMatriculas.FrequenciaExcedida;
        var horarios = await CarregarHorariosAsync(
            organizacaoId, unidadeId, idsHorarios, inicio, cancellationToken);
        if (horarios.Length != idsHorarios.Count)
            return EstadoMatriculas.HorarioNaoElegivel;
        if (RegraGradeMatricula.PossuiConflito(horarios.Select(item =>
                new IntervaloHorarioGrade(
                    item.DiaSemana, item.HoraInicio, item.HoraFim)).ToArray()))
            return EstadoMatriculas.ConflitoHorarioAluno;

        var gradesAluno = await (
            from grade in dbContext.MatriculasHorarios.AsNoTracking()
            join matricula in dbContext.Matriculas.AsNoTracking()
                on new { grade.OrganizacaoId, grade.UnidadeId, Id = grade.MatriculaId }
                equals new { matricula.OrganizacaoId, matricula.UnidadeId, matricula.Id }
            join horario in dbContext.TurmasHorarios.AsNoTracking()
                on new { grade.OrganizacaoId, grade.UnidadeId, Id = grade.TurmaHorarioId }
                equals new { horario.OrganizacaoId, horario.UnidadeId, horario.Id }
            where matricula.OrganizacaoId == organizacaoId
                && matricula.AlunoId == alunoId
                && !idsGradesIgnorados.Contains(grade.Id)
                && (grade.VigenciaFim == null || grade.VigenciaFim >= inicio)
            select new GradeConflitoPersistencia(
                grade.Id, grade.MatriculaId, grade.TurmaHorarioId,
                horario.DiaSemana, horario.HoraInicio, horario.HoraFim,
                grade.VigenciaInicio, grade.VigenciaFim))
            .ToArrayAsync(cancellationToken);
        if (horarios.Any(novo => gradesAluno.Any(existente =>
                existente.DiaSemana == novo.DiaSemana
                && novo.HoraInicio < existente.HoraFim
                && existente.HoraInicio < novo.HoraFim)))
            return EstadoMatriculas.ConflitoHorarioAluno;

        var historicoMatricula = gradesAluno
            .Where(item => item.MatriculaId == matriculaId)
            .Select(item => new IntervaloVigenciaGrade(
                item.VigenciaInicio, item.VigenciaFim))
            .Concat(idsHorarios.Select(_ => new IntervaloVigenciaGrade(inicio, null)));
        if (RegraGradeMatricula.MaximoSimultaneo(
                historicoMatricula, inicio, null) > frequenciaSemanal)
            return EstadoMatriculas.FrequenciaExcedida;

        var ids = idsHorarios.ToArray();
        var ocupacoes = await dbContext.MatriculasHorarios.AsNoTracking()
            .Where(item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && ids.Contains(item.TurmaHorarioId)
                && !idsGradesIgnorados.Contains(item.Id)
                && (item.VigenciaFim == null || item.VigenciaFim >= inicio))
            .Select(item => new IntervaloOcupacao(
                item.TurmaHorarioId, item.VigenciaInicio, item.VigenciaFim))
            .ToArrayAsync(cancellationToken);
        foreach (var horario in horarios)
        {
            var intervalos = ocupacoes
                .Where(item => item.TurmaHorarioId == horario.Id)
                .Select(item => new IntervaloVigenciaGrade(item.Inicio, item.Fim))
                .Append(new IntervaloVigenciaGrade(inicio, null));
            if (RegraGradeMatricula.MaximoSimultaneo(
                    intervalos, inicio, null) > horario.Capacidade)
                return EstadoMatriculas.CapacidadeEsgotada;
        }
        return EstadoMatriculas.Sucesso;
    }

    private async Task<HorarioPersistencia[]> CarregarHorariosAsync(
        Guid organizacaoId, Guid unidadeId, IReadOnlyList<Guid>? ids,
        DateOnly inicio, CancellationToken cancellationToken) => await (
        from horario in dbContext.TurmasHorarios.AsNoTracking()
        where horario.OrganizacaoId == organizacaoId
            && horario.UnidadeId == unidadeId
            && (ids == null || ids.Contains(horario.Id))
            && horario.Ativo && horario.VigenciaFim == null
            && horario.VigenciaInicio <= inicio
        join turma in dbContext.Turmas.AsNoTracking()
            on new { horario.OrganizacaoId, horario.UnidadeId, Id = horario.TurmaId }
            equals new { turma.OrganizacaoId, turma.UnidadeId, turma.Id }
        join vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
            on new
            {
                horario.OrganizacaoId,
                horario.UnidadeId,
                Id = horario.ProfessorUnidadeId
            }
            equals new { vinculo.OrganizacaoId, vinculo.UnidadeId, vinculo.Id }
        join professor in dbContext.Professores.AsNoTracking()
            on new { vinculo.OrganizacaoId, Id = vinculo.ProfessorId }
            equals new { professor.OrganizacaoId, professor.Id }
        where turma.Ativo && vinculo.Ativo && professor.Ativo
        select new HorarioPersistencia(
            horario.Id, turma.Id, turma.Nome, professor.NomeCompleto,
            horario.DiaSemana, horario.HoraInicio, horario.HoraFim,
            turma.Capacidade)).ToArrayAsync(cancellationToken);

    private static string? NormalizarCpf(string? cpf) =>
        string.IsNullOrWhiteSpace(cpf) ? null : cpf.Trim();

    private static EstadoMatriculas MapearErroBanco(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgres)
            return EstadoMatriculas.Falha;
        var restricao = postgres.ConstraintName ?? string.Empty;
        var mensagem = postgres.MessageText;
        if (restricao.Contains("uq_matriculas_ativa", StringComparison.Ordinal))
            return EstadoMatriculas.MatriculaAtivaExistente;
        if (restricao.Contains("uq_alunos_organizacao_cpf", StringComparison.Ordinal))
            return EstadoMatriculas.AlunoDuplicado;
        if (restricao.Contains("uq_responsaveis_organizacao_cpf", StringComparison.Ordinal))
            return EstadoMatriculas.ResponsavelDuplicado;
        if (restricao.Contains("uq_alunos_responsaveis_principal", StringComparison.Ordinal))
            return EstadoMatriculas.ResponsavelInvalido;
        if (restricao.Contains("uq_matriculas_horarios", StringComparison.Ordinal))
            return EstadoMatriculas.HorarioDuplicado;
        if (mensagem.Contains("frequencia semanal", StringComparison.OrdinalIgnoreCase))
            return EstadoMatriculas.FrequenciaExcedida;
        if (mensagem.Contains("capacidade", StringComparison.OrdinalIgnoreCase))
            return EstadoMatriculas.CapacidadeEsgotada;
        if (mensagem.Contains("conflitante", StringComparison.OrdinalIgnoreCase))
            return EstadoMatriculas.ConflitoHorarioAluno;
        if (mensagem.Contains("plano", StringComparison.OrdinalIgnoreCase)
            || mensagem.Contains("versao", StringComparison.OrdinalIgnoreCase)
            || mensagem.Contains("disponivel", StringComparison.OrdinalIgnoreCase))
            return EstadoMatriculas.PlanoNaoElegivel;
        if (mensagem.Contains("horario recorrente", StringComparison.OrdinalIgnoreCase)
            || mensagem.Contains("turma ativa", StringComparison.OrdinalIgnoreCase))
            return EstadoMatriculas.HorarioNaoElegivel;
        return postgres.SqlState is PostgresErrorCodes.UniqueViolation
            or PostgresErrorCodes.CheckViolation
            ? EstadoMatriculas.ConflitoConcorrencia
            : EstadoMatriculas.Falha;
    }

    private sealed record PlanoPersistencia(
        Guid PlanoVersaoId, int DuracaoMeses, int FrequenciaSemanal);

    private sealed record HorarioPersistencia(
        Guid Id, Guid TurmaId, string NomeTurma, string Professor,
        DiaSemana DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFim, int Capacidade);

    private sealed record IntervaloOcupacao(
        Guid TurmaHorarioId, DateOnly Inicio, DateOnly? Fim);

    private sealed record GradeConflitoPersistencia(
        Guid Id, Guid MatriculaId, Guid TurmaHorarioId,
        DiaSemana DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFim,
        DateOnly VigenciaInicio, DateOnly? VigenciaFim);

    private sealed record ResultadoAluno(EstadoMatriculas Estado, Aluno? Aluno);

    private sealed record ResultadoResponsaveis(
        EstadoMatriculas Estado,
        IReadOnlyList<ResponsavelVinculadoMatricula> Vinculados);
}
