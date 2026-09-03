using BFA.Application.Matriculas;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Planos;
using BFA.Domain.Professores;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Matriculas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BFA.IntegrationTests;

public sealed class MatriculasOperacionaisRepositorioTests
{
    private static readonly DateTime Agora = new(
        2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Inicio = new(2026, 9, 1);

    [Fact]
    public async Task Listagem_parte_da_unidade_aplica_filtros_e_conta_grade_aberta()
    {
        var cenario = await CriarCenarioAsync();
        await using var db = CriarContexto(cenario.Banco);
        var matricula = CriarMatricula(cenario, cenario.AlunoId, Inicio);
        db.Matriculas.Add(matricula);
        db.MatriculasHorarios.Add(new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[0], Inicio, cenario.UsuarioId, Agora));
        var alunoExterno = new Aluno(
            Guid.NewGuid(), cenario.OrganizacaoId, "Aluno de outra unidade",
            new DateOnly(2000, 1, 1), Inicio, Agora);
        db.Alunos.Add(alunoExterno);
        db.Matriculas.Add(new Matricula(
            Guid.NewGuid(), cenario.OrganizacaoId, Guid.NewGuid(), alunoExterno.Id,
            cenario.PlanoVersaoId, Inicio, 12, 100, false, null,
            cenario.UsuarioId, Agora));
        await db.SaveChangesAsync();

        var itens = await new MatriculasRepositorio(db).ListarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, "aluno existente",
            StatusMatricula.Ativa, CancellationToken.None);

        var item = Assert.Single(itens);
        Assert.Equal(matricula.Id, item.MatriculaId);
        Assert.Equal(1, item.QuantidadeHorariosAtuais);
        Assert.Equal("Plano Local", item.Plano);
    }

    [Fact]
    public async Task Detalhe_retorna_responsaveis_grade_atual_e_historico()
    {
        var cenario = await CriarCenarioAsync();
        await using var db = CriarContexto(cenario.Banco);
        var matricula = CriarMatricula(cenario, cenario.AlunoId, Inicio);
        var responsavel = new Responsavel(
            Guid.NewGuid(), cenario.OrganizacaoId, "Responsável",
            Agora, telefone: "15999990000");
        var vinculo = new AlunoResponsavel(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.AlunoId,
            responsavel.Id, TipoRelacaoResponsavel.Mae, true, true, Agora);
        var atual = new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[0], Inicio, cenario.UsuarioId, Agora);
        var historico = new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[1], Inicio, cenario.UsuarioId, Agora);
        historico.Encerrar(new DateOnly(2026, 9, 30), cenario.UsuarioId, Agora.AddDays(1));
        db.AddRange(matricula, responsavel, vinculo, atual, historico);
        await db.SaveChangesAsync();

        var detalhe = await new MatriculasRepositorio(db).ObterAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, matricula.Id,
            CancellationToken.None);

        Assert.NotNull(detalhe);
        Assert.Single(detalhe.Responsaveis);
        Assert.Single(detalhe.GradeAtual);
        Assert.Single(detalhe.HistoricoGrade);
        Assert.Equal("Professor", detalhe.GradeAtual[0].ProfessorSnapshot);
    }

    [Fact]
    public async Task Planos_elegiveis_incluem_local_e_rede_disponivel_mas_nao_outros()
    {
        var cenario = await CriarCenarioAsync(incluirPlanoRede: true);
        await using var db = CriarContexto(cenario.Banco);
        var outroPlano = new Plano(
            Guid.NewGuid(), cenario.OrganizacaoId, Guid.NewGuid(),
            "Plano externo", cenario.UsuarioId, Agora);
        db.Add(outroPlano);
        db.Add(new PlanoVersao(
            Guid.NewGuid(), cenario.OrganizacaoId, outroPlano.Id, 1, 12, 2,
            100, false, null, new DateOnly(2026, 1, 1), null,
            cenario.UsuarioId, Agora));
        await db.SaveChangesAsync();

        var planos = await new MatriculasRepositorio(db).ListarPlanosElegiveisAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, Inicio, CancellationToken.None);

        Assert.Equal(2, planos.Count);
        Assert.Contains(planos, item => item.Escopo == EscopoPlanoMatricula.Local);
        Assert.Contains(planos, item => item.Escopo == EscopoPlanoMatricula.Rede);
        Assert.DoesNotContain(planos, item => item.Nome == "Plano externo");
    }

    [Fact]
    public async Task Versao_fora_da_vigencia_e_rede_indisponivel_nao_sao_elegiveis()
    {
        var cenario = await CriarCenarioAsync(incluirPlanoRede: true);
        await using var db = CriarContexto(cenario.Banco);
        var local = await db.PlanosVersoes.SingleAsync(item =>
            item.Id == cenario.PlanoVersaoId);
        local.Encerrar(new DateOnly(2026, 8, 31));
        var disponibilidade = await db.PlanosDisponibilidadesUnidades.SingleAsync();
        disponibilidade.Desativar(cenario.UsuarioId, Agora);
        await db.SaveChangesAsync();

        var planos = await new MatriculasRepositorio(db).ListarPlanosElegiveisAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, Inicio, CancellationToken.None);

        Assert.Empty(planos);
    }

    [Fact]
    public async Task Horarios_elegiveis_calculam_ocupacao_do_slot_no_periodo()
    {
        var cenario = await CriarCenarioAsync(capacidade: 2);
        await using var db = CriarContexto(cenario.Banco);
        var ocupante = new Aluno(
            Guid.NewGuid(), cenario.OrganizacaoId, "Ocupante",
            new DateOnly(2000, 1, 1), Inicio, Agora);
        var matricula = CriarMatricula(cenario, ocupante.Id, Inicio);
        db.AddRange(ocupante, matricula, new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[0], Inicio,
            cenario.UsuarioId, Agora));
        await db.SaveChangesAsync();

        var horarios = await new MatriculasRepositorio(db).ListarHorariosElegiveisAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, Inicio,
            new DateOnly(2027, 8, 31), CancellationToken.None);

        var horario = Assert.Single(horarios, item =>
            item.TurmaHorarioId == cenario.Horarios[0]);
        Assert.Equal(1, horario.Ocupacao);
        Assert.Equal(1, horario.VagasDisponiveis);
    }

    [Fact]
    public async Task Criacao_com_aluno_existente_calcula_fim_preserva_preco_taxa_e_grade()
    {
        var cenario = await CriarCenarioAsync(frequencia: 2);
        await using var db = CriarContexto(cenario.Banco);
        var repositorio = new MatriculasRepositorio(db);

        var resultado = await repositorio.CriarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.UsuarioId,
            false, Solicitacao(cenario, cenario.AlunoId,
                [cenario.Horarios[0], cenario.Horarios[1]],
                valor: 137.50m, cobraTaxa: true, taxa: 42m),
            Inicio, Agora, CancellationToken.None);

        Assert.Equal(EstadoMatriculas.Sucesso, resultado.Estado);
        Assert.NotNull(resultado.Valor);
        Assert.Equal(2, resultado.Valor.HorariosCriados);
        var matricula = await db.Matriculas.SingleAsync(item =>
            item.Id == resultado.Valor.MatriculaId);
        Assert.Equal(new DateOnly(2027, 8, 31), matricula.DataFimPrevista);
        Assert.Equal(137.50m, matricula.ValorMensalContratado);
        Assert.Equal(42m, matricula.ValorTaxaMatricula);
    }

    [Fact]
    public async Task Novo_aluno_adulto_e_criado_sem_usuario_ou_responsavel()
    {
        var cenario = await CriarCenarioAsync();
        await using var db = CriarContexto(cenario.Banco);
        var solicitacao = Solicitacao(cenario, null, [cenario.Horarios[0]]) with
        {
            NovoAluno = new(
                "Novo adulto", new DateOnly(1995, 5, 10),
                "12345678901", null, "adulto@teste.local")
        };

        var resultado = await new MatriculasRepositorio(db).CriarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.UsuarioId,
            false, solicitacao, Inicio, Agora, CancellationToken.None);

        Assert.Equal(EstadoMatriculas.Sucesso, resultado.Estado);
        var aluno = await db.Alunos.SingleAsync(item => item.Id == resultado.Valor!.AlunoId);
        Assert.Null(aluno.UsuarioId);
        Assert.Empty(await db.AlunosResponsaveis.Where(item =>
            item.AlunoId == aluno.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Menor_sem_responsavel_e_rejeitado_e_com_responsavel_funciona()
    {
        var cenario = await CriarCenarioAsync();
        await using var db = CriarContexto(cenario.Banco);
        var baseSolicitacao = Solicitacao(cenario, null, [cenario.Horarios[0]]) with
        {
            NovoAluno = new("Menor", new DateOnly(2012, 1, 1), null, null, null)
        };
        var repositorio = new MatriculasRepositorio(db);

        var semResponsavel = await repositorio.CriarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.UsuarioId,
            false, baseSolicitacao, Inicio, Agora, CancellationToken.None);
        var comResponsavel = await repositorio.CriarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.UsuarioId,
            false, baseSolicitacao with
            {
                NovoAluno = new("Menor válido", new DateOnly(2012, 1, 1), null, null, null),
                Responsaveis = [new(
                    "Mãe", "98765432100", "15999990000", null,
                    TipoRelacaoResponsavel.Mae, null, true, true)]
            }, Inicio, Agora, CancellationToken.None);

        Assert.Equal(EstadoMatriculas.MenorSemResponsavel, semResponsavel.Estado);
        Assert.Equal(EstadoMatriculas.Sucesso, comResponsavel.Estado);
        Assert.Single(await db.AlunosResponsaveis.Where(item =>
            item.AlunoId == comResponsavel.Valor!.AlunoId).ToArrayAsync());
    }

    [Fact]
    public async Task Aluno_nao_relacionado_e_plano_local_externo_sao_rejeitados()
    {
        var cenario = await CriarCenarioAsync();
        await using var db = CriarContexto(cenario.Banco);
        var aluno = new Aluno(
            Guid.NewGuid(), cenario.OrganizacaoId, "Sem relação",
            new DateOnly(2000, 1, 1), Inicio, Agora);
        var planoExterno = new Plano(
            Guid.NewGuid(), cenario.OrganizacaoId, Guid.NewGuid(),
            "Externo", cenario.UsuarioId, Agora);
        var versaoExterna = new PlanoVersao(
            Guid.NewGuid(), cenario.OrganizacaoId, planoExterno.Id, 1, 12, 1,
            100, false, null, new DateOnly(2026, 1, 1), null,
            cenario.UsuarioId, Agora);
        db.AddRange(aluno, planoExterno, versaoExterna);
        await db.SaveChangesAsync();
        var repositorio = new MatriculasRepositorio(db);

        var semRelacao = await repositorio.CriarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.UsuarioId,
            false, Solicitacao(cenario, aluno.Id, [cenario.Horarios[0]]),
            Inicio, Agora, CancellationToken.None);
        var planoInvalido = await repositorio.CriarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.UsuarioId,
            true, Solicitacao(cenario, aluno.Id, [cenario.Horarios[0]]) with
            { PlanoVersaoId = versaoExterna.Id },
            Inicio, Agora, CancellationToken.None);

        Assert.Equal(EstadoMatriculas.AlunoNaoRelacionadoUnidade, semRelacao.Estado);
        Assert.Equal(EstadoMatriculas.PlanoNaoElegivel, planoInvalido.Estado);
    }

    [Fact]
    public async Task Frequencia_duplicidade_conflito_e_capacidade_retornam_erros_de_negocio()
    {
        var cenario = await CriarCenarioAsync(frequencia: 2, capacidade: 1);
        await using var db = CriarContexto(cenario.Banco);
        var repositorio = new MatriculasRepositorio(db);

        var frequencia = await repositorio.CriarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.UsuarioId,
            false, Solicitacao(cenario, cenario.AlunoId,
                [cenario.Horarios[0], cenario.Horarios[1], cenario.Horarios[2]]),
            Inicio, Agora, CancellationToken.None);
        var conflito = await repositorio.CriarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.UsuarioId,
            false, Solicitacao(cenario, cenario.AlunoId,
                [cenario.Horarios[0], cenario.HorarioConflitanteId]),
            Inicio, Agora, CancellationToken.None);

        var ocupante = new Aluno(
            Guid.NewGuid(), cenario.OrganizacaoId, "Ocupante",
            new DateOnly(2000, 1, 1), Inicio, Agora);
        var matriculaOcupante = CriarMatricula(cenario, ocupante.Id, Inicio);
        db.AddRange(ocupante, matriculaOcupante, new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matriculaOcupante.Id, cenario.Horarios[0], Inicio,
            cenario.UsuarioId, Agora));
        await db.SaveChangesAsync();
        var capacidade = await repositorio.CriarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.UsuarioId,
            false, Solicitacao(cenario, cenario.AlunoId, [cenario.Horarios[0]]),
            Inicio, Agora, CancellationToken.None);

        Assert.Equal(EstadoMatriculas.FrequenciaExcedida, frequencia.Estado);
        Assert.Equal(EstadoMatriculas.ConflitoHorarioAluno, conflito.Estado);
        Assert.Equal(EstadoMatriculas.CapacidadeEsgotada, capacidade.Estado);
    }

    [Fact]
    public async Task Horarios_adjacentes_e_plano_tres_vezes_sao_aceitos()
    {
        var cenario = await CriarCenarioAsync(frequencia: 3);
        await using var db = CriarContexto(cenario.Banco);
        var baseHorario = await db.TurmasHorarios.SingleAsync(item =>
            item.Id == cenario.Horarios[0]);
        var adjacente = new TurmaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            baseHorario.TurmaId, baseHorario.ProfessorUnidadeId,
            DiaSemana.Segunda, new TimeOnly(20, 0), new TimeOnly(21, 0),
            new DateOnly(2026, 1, 1), null, cenario.UsuarioId, Agora);
        db.TurmasHorarios.Add(adjacente);
        await db.SaveChangesAsync();

        var resultado = await new MatriculasRepositorio(db).CriarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.UsuarioId,
            false, Solicitacao(cenario, cenario.AlunoId,
                [cenario.Horarios[0], adjacente.Id, cenario.Horarios[1]]),
            Inicio, Agora, CancellationToken.None);

        Assert.Equal(EstadoMatriculas.Sucesso, resultado.Estado);
        Assert.Equal(3, resultado.Valor!.HorariosCriados);
    }

    [Fact]
    public async Task Alteracao_de_grade_preserva_slot_inalterado_e_historico_do_removido()
    {
        var cenario = await CriarCenarioAsync(frequencia: 3);
        await using var db = CriarContexto(cenario.Banco);
        var matricula = CriarMatricula(cenario, cenario.AlunoId, Inicio);
        var mantido = new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[0], Inicio, cenario.UsuarioId, Agora);
        var removido = new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[1], Inicio, cenario.UsuarioId, Agora);
        db.AddRange(matricula, mantido, removido);
        await db.SaveChangesAsync();

        var resultado = await new MatriculasRepositorio(db).AlterarGradeAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, matricula.Id,
            cenario.UsuarioId, new(new DateOnly(2026, 10, 1),
                [cenario.Horarios[0], cenario.Horarios[2]]),
            Agora.AddMonths(1), CancellationToken.None);

        Assert.Equal(EstadoMatriculas.Sucesso, resultado.Estado);
        Assert.Equal(new ResultadoAlteracaoGrade(1, 1, 1), resultado.Valor);
        Assert.Null((await db.MatriculasHorarios.SingleAsync(item =>
            item.Id == mantido.Id)).VigenciaFim);
        Assert.Equal(new DateOnly(2026, 9, 30),
            (await db.MatriculasHorarios.SingleAsync(item =>
                item.Id == removido.Id)).VigenciaFim);
        Assert.Contains(await db.MatriculasHorarios.ToArrayAsync(), item =>
            item.TurmaHorarioId == cenario.Horarios[2]
            && item.VigenciaInicio == new DateOnly(2026, 10, 1));
    }

    [Fact]
    public async Task Mudanca_material_no_primeiro_dia_e_rejeitada()
    {
        var cenario = await CriarCenarioAsync();
        await using var db = CriarContexto(cenario.Banco);
        var matricula = CriarMatricula(cenario, cenario.AlunoId, Inicio);
        db.AddRange(matricula, new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[0], Inicio, cenario.UsuarioId, Agora));
        await db.SaveChangesAsync();

        var resultado = await new MatriculasRepositorio(db).AlterarGradeAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, matricula.Id,
            cenario.UsuarioId, new(Inicio, [cenario.Horarios[1]]),
            Agora, CancellationToken.None);

        Assert.Equal(EstadoMatriculas.DataInvalida, resultado.Estado);
    }

    [Fact]
    public async Task Falha_de_frequencia_ou_conflito_na_alteracao_nao_fecha_grade_atual()
    {
        var cenario = await CriarCenarioAsync(frequencia: 2);
        await using var db = CriarContexto(cenario.Banco);
        var matricula = CriarMatricula(cenario, cenario.AlunoId, Inicio);
        var primeira = new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[0], Inicio, cenario.UsuarioId, Agora);
        var segunda = new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[1], Inicio, cenario.UsuarioId, Agora);
        db.AddRange(matricula, primeira, segunda);
        await db.SaveChangesAsync();
        var repositorio = new MatriculasRepositorio(db);

        var frequencia = await repositorio.AlterarGradeAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, matricula.Id,
            cenario.UsuarioId, new(new DateOnly(2026, 10, 1),
                [cenario.Horarios[0], cenario.Horarios[1], cenario.Horarios[2]]),
            Agora.AddMonths(1), CancellationToken.None);
        var conflito = await repositorio.AlterarGradeAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, matricula.Id,
            cenario.UsuarioId, new(new DateOnly(2026, 10, 1),
                [cenario.Horarios[0], cenario.HorarioConflitanteId]),
            Agora.AddMonths(1), CancellationToken.None);

        Assert.Equal(EstadoMatriculas.FrequenciaExcedida, frequencia.Estado);
        Assert.Equal(EstadoMatriculas.ConflitoHorarioAluno, conflito.Estado);
        Assert.All(await db.MatriculasHorarios.Where(item =>
            item.MatriculaId == matricula.Id).ToArrayAsync(),
            item => Assert.Null(item.VigenciaFim));
    }

    [Fact]
    public async Task Falta_de_vaga_na_alteracao_nao_fecha_grade_atual()
    {
        var cenario = await CriarCenarioAsync(frequencia: 2, capacidade: 1);
        await using var db = CriarContexto(cenario.Banco);
        var matricula = CriarMatricula(cenario, cenario.AlunoId, Inicio);
        var atual = new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[0], Inicio, cenario.UsuarioId, Agora);
        var ocupante = new Aluno(
            Guid.NewGuid(), cenario.OrganizacaoId, "Ocupante da sexta",
            new DateOnly(2000, 1, 1), Inicio, Agora);
        var outraMatricula = CriarMatricula(cenario, ocupante.Id, Inicio);
        var ocupacao = new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            outraMatricula.Id, cenario.Horarios[2], Inicio, cenario.UsuarioId, Agora);
        db.AddRange(matricula, atual, ocupante, outraMatricula, ocupacao);
        await db.SaveChangesAsync();

        var resultado = await new MatriculasRepositorio(db).AlterarGradeAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, matricula.Id,
            cenario.UsuarioId, new(new DateOnly(2026, 10, 1),
                [cenario.Horarios[2]]), Agora.AddMonths(1), CancellationToken.None);

        Assert.Equal(EstadoMatriculas.CapacidadeEsgotada, resultado.Estado);
        Assert.Null(atual.VigenciaFim);
        Assert.DoesNotContain(await db.MatriculasHorarios.ToArrayAsync(), item =>
            item.MatriculaId == matricula.Id
            && item.TurmaHorarioId == cenario.Horarios[2]);
    }

    [Theory]
    [InlineData(false, StatusMatricula.Encerrada)]
    [InlineData(true, StatusMatricula.Cancelada)]
    public async Task Finalizacao_fecha_grade_na_data_final_inclusiva(
        bool cancelar, StatusMatricula statusEsperado)
    {
        var cenario = await CriarCenarioAsync();
        await using var db = CriarContexto(cenario.Banco);
        var matricula = CriarMatricula(cenario, cenario.AlunoId, Inicio);
        var grade = new MatriculaHorario(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId,
            matricula.Id, cenario.Horarios[0], Inicio, cenario.UsuarioId, Agora);
        db.AddRange(matricula, grade);
        await db.SaveChangesAsync();
        var fim = new DateOnly(2026, 12, 31);
        var repositorio = new MatriculasRepositorio(db);

        var estado = await repositorio.FinalizarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, matricula.Id,
            cenario.UsuarioId, fim, cancelar, Agora.AddMonths(4),
            CancellationToken.None);
        var novamente = await repositorio.FinalizarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, matricula.Id,
            cenario.UsuarioId, fim, cancelar, Agora.AddMonths(4),
            CancellationToken.None);

        Assert.Equal(EstadoMatriculas.Sucesso, estado);
        Assert.Equal(EstadoMatriculas.EstadoTerminal, novamente);
        Assert.Equal(statusEsperado, matricula.Status);
        Assert.Equal(fim, matricula.DataFimReal);
        Assert.Equal(fim, grade.VigenciaFim);
    }

    private static async Task<Cenario> CriarCenarioAsync(
        int frequencia = 2, int capacidade = 8, bool incluirPlanoRede = false)
    {
        var banco = $"matriculas-operacionais-{Guid.NewGuid():N}";
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var plano = new Plano(
            Guid.NewGuid(), organizacaoId, unidadeId, "Plano Local", usuarioId, Agora);
        var versao = new PlanoVersao(
            Guid.NewGuid(), organizacaoId, plano.Id, 1, 12, frequencia,
            100, false, null, new DateOnly(2026, 1, 1), null, usuarioId, Agora);
        var aluno = new Aluno(
            Guid.NewGuid(), organizacaoId, "Aluno existente",
            new DateOnly(2000, 1, 1), Inicio, Agora);
        var professor = new Professor(Guid.NewGuid(), organizacaoId, "Professor", Agora);
        var vinculo = new ProfessorUnidade(
            Guid.NewGuid(), organizacaoId, professor.Id, unidadeId, Agora);
        var turma = new Turma(
            Guid.NewGuid(), organizacaoId, unidadeId, vinculo.Id,
            "Turma A", capacidade, usuarioId, Agora);
        var outraTurma = new Turma(
            Guid.NewGuid(), organizacaoId, unidadeId, vinculo.Id,
            "Turma B", capacidade, usuarioId, Agora);
        var horarios = new[]
        {
            new TurmaHorario(Guid.NewGuid(), organizacaoId, unidadeId, turma.Id,
                vinculo.Id, DiaSemana.Segunda, new TimeOnly(19, 0),
                new TimeOnly(20, 0), new DateOnly(2026, 1, 1), null,
                usuarioId, Agora),
            new TurmaHorario(Guid.NewGuid(), organizacaoId, unidadeId, turma.Id,
                vinculo.Id, DiaSemana.Quarta, new TimeOnly(19, 0),
                new TimeOnly(20, 0), new DateOnly(2026, 1, 1), null,
                usuarioId, Agora),
            new TurmaHorario(Guid.NewGuid(), organizacaoId, unidadeId, turma.Id,
                vinculo.Id, DiaSemana.Sexta, new TimeOnly(19, 0),
                new TimeOnly(20, 0), new DateOnly(2026, 1, 1), null,
                usuarioId, Agora)
        };
        var conflitante = new TurmaHorario(
            Guid.NewGuid(), organizacaoId, unidadeId, outraTurma.Id,
            vinculo.Id, DiaSemana.Segunda, new TimeOnly(19, 30),
            new TimeOnly(20, 30), new DateOnly(2026, 1, 1), null,
            usuarioId, Agora);
        await using var db = CriarContexto(banco);
        db.AddRange(plano, versao, aluno, professor, vinculo, turma, outraTurma);
        db.TurmasHorarios.AddRange(horarios.Append(conflitante));
        var historica = new Matricula(
            Guid.NewGuid(), organizacaoId, unidadeId, aluno.Id, versao.Id,
            new DateOnly(2025, 1, 1), 12, 100, false, null, usuarioId, Agora);
        historica.Encerrar(new DateOnly(2025, 12, 31), usuarioId, Agora.AddDays(1));
        db.Matriculas.Add(historica);
        if (incluirPlanoRede)
        {
            var rede = new Plano(
                Guid.NewGuid(), organizacaoId, null, "Plano Rede", usuarioId, Agora);
            db.AddRange(rede, new PlanoVersao(
                Guid.NewGuid(), organizacaoId, rede.Id, 1, 6, 1,
                80, false, null, new DateOnly(2026, 1, 1), null,
                usuarioId, Agora), new PlanoDisponibilidadeUnidade(
                Guid.NewGuid(), organizacaoId, rede.Id, unidadeId, usuarioId, Agora));
        }
        await db.SaveChangesAsync();
        return new(banco, organizacaoId, unidadeId, usuarioId, versao.Id,
            aluno.Id, horarios.Select(item => item.Id).ToArray(), conflitante.Id);
    }

    private static Matricula CriarMatricula(
        Cenario cenario, Guid alunoId, DateOnly inicio) => new(
        Guid.NewGuid(), cenario.OrganizacaoId, cenario.UnidadeId, alunoId,
        cenario.PlanoVersaoId, inicio, 12, 100, false, null,
        cenario.UsuarioId, Agora);

    private static CriarMatriculaSolicitacao Solicitacao(
        Cenario cenario, Guid? alunoId, IReadOnlyList<Guid> horarios,
        decimal valor = 100, bool cobraTaxa = false, decimal? taxa = null) => new(
        alunoId, null, [], cenario.PlanoVersaoId, Inicio,
        valor, cobraTaxa, taxa, horarios);

    private static BfaDbContext CriarContexto(string banco) => new(
        new DbContextOptionsBuilder<BfaDbContext>()
            .UseInMemoryDatabase(banco)
            .ConfigureWarnings(warnings => warnings.Ignore(
                InMemoryEventId.TransactionIgnoredWarning)).Options);

    private sealed record Cenario(
        string Banco,
        Guid OrganizacaoId,
        Guid UnidadeId,
        Guid UsuarioId,
        Guid PlanoVersaoId,
        Guid AlunoId,
        Guid[] Horarios,
        Guid HorarioConflitanteId);
}
