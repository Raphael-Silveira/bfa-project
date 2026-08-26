using BFA.Domain.Turmas;

namespace BFA.UnitTests.Turmas;

public sealed class TurmaHorarioTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026, 8, 24, 3, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly VigenciaInicio = new(2026, 9, 1);

    [Theory]
    [InlineData(DiaSemana.Segunda, 1)]
    [InlineData(DiaSemana.Terca, 2)]
    [InlineData(DiaSemana.Quarta, 3)]
    [InlineData(DiaSemana.Quinta, 4)]
    [InlineData(DiaSemana.Sexta, 5)]
    [InlineData(DiaSemana.Sabado, 6)]
    [InlineData(DiaSemana.Domingo, 7)]
    public void Criacao_aceita_dia_iso_e_horario_valido(
        DiaSemana diaSemana,
        short valorIso)
    {
        var professorUnidadeId = Guid.NewGuid();
        var horario = Criar(
            diaSemana: diaSemana,
            professorUnidadeId: professorUnidadeId);

        Assert.Equal(valorIso, (short)horario.DiaSemana);
        Assert.Equal(professorUnidadeId, horario.ProfessorUnidadeId);
        Assert.Equal(new TimeOnly(19, 0), horario.HoraInicio);
        Assert.Equal(new TimeOnly(20, 0), horario.HoraFim);
        Assert.True(horario.Ativo);
    }

    [Fact]
    public void Criacao_rejeita_dia_invalido()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(diaSemana: (DiaSemana)8));

        Assert.Equal("diaSemana", exception.ParamName);
    }

    [Theory]
    [InlineData(19, 0)]
    [InlineData(18, 30)]
    [InlineData(0, 30)]
    public void Criacao_rejeita_hora_final_igual_anterior_ou_apos_meia_noite(
        int horaFim,
        int minutoFim)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Criar(horaFim: new TimeOnly(horaFim, minutoFim)));

        Assert.Equal("horaFim", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_vigencia_final_anterior()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Criar(vigenciaFim: VigenciaInicio.AddDays(-1)));

        Assert.Equal("vigenciaFim", exception.ParamName);
    }

    [Fact]
    public void Vigencia_final_pode_ser_preenchida_uma_unica_vez()
    {
        var horario = Criar();
        var vigenciaFim = new DateOnly(2026, 12, 31);
        var usuarioId = Guid.NewGuid();

        horario.Encerrar(vigenciaFim, usuarioId, CriadoEmUtc.AddHours(1));

        Assert.Equal(vigenciaFim, horario.VigenciaFim);
        Assert.Equal(usuarioId, horario.AtualizadoPorUsuarioId);
        Assert.Throws<InvalidOperationException>(() =>
            horario.Encerrar(vigenciaFim.AddDays(1), usuarioId, CriadoEmUtc.AddHours(2)));
        Assert.Equal(vigenciaFim, horario.VigenciaFim);
    }

    [Fact]
    public void Inativacao_e_reativacao_preservam_regra_historica()
    {
        var horario = Criar();
        var id = horario.Id;
        var turmaId = horario.TurmaId;
        var usuarioId = Guid.NewGuid();

        horario.Desativar(usuarioId, CriadoEmUtc.AddHours(1));
        horario.Ativar(usuarioId, CriadoEmUtc.AddHours(2));

        Assert.True(horario.Ativo);
        Assert.Equal(id, horario.Id);
        Assert.Equal(turmaId, horario.TurmaId);
        Assert.Equal(VigenciaInicio, horario.VigenciaInicio);
    }

    [Fact]
    public void Troca_do_responsavel_atual_nao_reinterpreta_professor_do_horario_historico()
    {
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var turmaId = Guid.NewGuid();
        var professorAnteriorId = Guid.NewGuid();
        var professorNovoId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var turma = new Turma(
            turmaId,
            organizacaoId,
            unidadeId,
            professorAnteriorId,
            "Turma Iniciante",
            20,
            usuarioId,
            CriadoEmUtc);
        var horarioHistorico = new TurmaHorario(
            Guid.NewGuid(),
            organizacaoId,
            unidadeId,
            turmaId,
            professorAnteriorId,
            DiaSemana.Segunda,
            new TimeOnly(19, 0),
            new TimeOnly(20, 0),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 8, 31),
            usuarioId,
            CriadoEmUtc);

        turma.Atualizar(
            turma.Nome,
            turma.Capacidade,
            professorNovoId,
            usuarioId,
            CriadoEmUtc.AddHours(1));
        var novoHorario = new TurmaHorario(
            Guid.NewGuid(),
            organizacaoId,
            unidadeId,
            turmaId,
            professorNovoId,
            DiaSemana.Segunda,
            new TimeOnly(19, 0),
            new TimeOnly(20, 0),
            new DateOnly(2026, 9, 1),
            null,
            usuarioId,
            CriadoEmUtc.AddHours(1));

        Assert.Equal(professorAnteriorId, horarioHistorico.ProfessorUnidadeId);
        Assert.Equal(professorNovoId, turma.ProfessorUnidadeId);
        Assert.Equal(professorNovoId, novoHorario.ProfessorUnidadeId);
    }

    [Theory]
    [InlineData(nameof(TurmaHorario.Id))]
    [InlineData(nameof(TurmaHorario.OrganizacaoId))]
    [InlineData(nameof(TurmaHorario.UnidadeId))]
    [InlineData(nameof(TurmaHorario.TurmaId))]
    [InlineData(nameof(TurmaHorario.ProfessorUnidadeId))]
    [InlineData(nameof(TurmaHorario.DiaSemana))]
    [InlineData(nameof(TurmaHorario.HoraInicio))]
    [InlineData(nameof(TurmaHorario.HoraFim))]
    [InlineData(nameof(TurmaHorario.VigenciaInicio))]
    [InlineData(nameof(TurmaHorario.CriadoPorUsuarioId))]
    [InlineData(nameof(TurmaHorario.CriadoEmUtc))]
    public void Identidade_historica_nao_possui_setter_publico(string propriedade)
    {
        var property = typeof(TurmaHorario).GetProperty(propriedade);

        Assert.NotNull(property);
        Assert.False(property.SetMethod?.IsPublic ?? false);
    }

    private static TurmaHorario Criar(
        DiaSemana diaSemana = DiaSemana.Segunda,
        TimeOnly? horaFim = null,
        DateOnly? vigenciaFim = null,
        Guid? professorUnidadeId = null) => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            professorUnidadeId ?? Guid.NewGuid(),
            diaSemana,
            new TimeOnly(19, 0),
            horaFim ?? new TimeOnly(20, 0),
            VigenciaInicio,
            vigenciaFim,
            Guid.NewGuid(),
            CriadoEmUtc);
}
