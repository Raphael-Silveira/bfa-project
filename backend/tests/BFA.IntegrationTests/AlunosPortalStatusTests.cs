using BFA.Domain.Acessos;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Organizacoes;
using BFA.Domain.Planos;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Alunos;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.IntegrationTests;

public sealed class AlunosPortalStatusTests
{
    [Fact]
    public async Task Lista_marca_portal_somente_com_identity_e_vinculo_aluno_ativo_na_unidade()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseInMemoryDatabase($"bfa-portal-status-{Guid.NewGuid():N}")
            .Options;
        await using var db = new BfaDbContext(options);
        var agora = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);
        var organizacao = new Organizacao(Guid.NewGuid(), "BFA", "bfa", agora);
        var unidade = new Unidade(Guid.NewGuid(), organizacao.Id, "BFA Unidade", "bfa-unidade", agora);
        var outraUnidade = new Unidade(Guid.NewGuid(), organizacao.Id, "Outra Unidade", "outra-unidade", agora);
        var plano = new Plano(Guid.NewGuid(), organizacao.Id, unidade.Id, "Plano", Guid.NewGuid(), agora);
        var versao = new PlanoVersao(
            Guid.NewGuid(), organizacao.Id, plano.Id, 1, 12, 2, 300m, true, 100m,
            new DateOnly(2026, 1, 1), null, plano.CriadoPorUsuarioId, agora);

        var semAcesso = CriarAluno(organizacao.Id, "Sem Acesso", agora);
        var ativo = CriarAluno(organizacao.Id, "Portal Ativo", agora);
        var inativo = CriarAluno(organizacao.Id, "Vínculo Inativo", agora);
        var outraUnidadeAluno = CriarAluno(organizacao.Id, "Outra Unidade", agora);
        var outroPerfil = CriarAluno(organizacao.Id, "Outro Perfil", agora);
        var usuarios = new[]
        {
            (Aluno: ativo, Perfil: PerfilAcesso.Aluno, UnidadeId: unidade.Id, Ativo: true),
            (Aluno: inativo, Perfil: PerfilAcesso.Aluno, UnidadeId: unidade.Id, Ativo: false),
            (Aluno: outraUnidadeAluno, Perfil: PerfilAcesso.Aluno, UnidadeId: outraUnidade.Id, Ativo: true),
            (Aluno: outroPerfil, Perfil: PerfilAcesso.Professor, UnidadeId: unidade.Id, Ativo: true)
        };

        var identities = usuarios.Select(item =>
        {
            var usuario = new UsuarioIdentity
            {
                Id = Guid.NewGuid(),
                UserName = $"usuario-{Guid.NewGuid():N}"
            };
            item.Aluno.AlterarUsuario(usuario.Id, agora);
            return (Usuario: usuario, item.Perfil, item.UnidadeId, item.Ativo);
        }).ToArray();

        var alunos = new[] { semAcesso, ativo, inativo, outraUnidadeAluno, outroPerfil };
        var matriculas = alunos.Select(aluno => new Matricula(
            Guid.NewGuid(), organizacao.Id, unidade.Id, aluno.Id, versao.Id,
            new DateOnly(2026, 9, 1), 12, 270m, true, 100m,
            plano.CriadoPorUsuarioId, agora)).ToArray();
        var vinculos = identities.Select(item =>
        {
            var vinculo = new VinculoAcesso(
                Guid.NewGuid(), item.Usuario.Id, organizacao.Id, item.UnidadeId,
                item.Perfil, agora);
            if (!item.Ativo)
            {
                vinculo.Desativar(agora.AddMinutes(1));
            }

            return vinculo;
        }).ToArray();

        db.AddRange(organizacao, unidade, outraUnidade, plano, versao);
        db.AddRange(alunos);
        db.AddRange(matriculas);
        db.AddRange(identities.Select(item => item.Usuario));
        db.AddRange(vinculos);
        await db.SaveChangesAsync();

        var repositorio = new AlunosRepositorio(
            db,
            NullLogger<AlunosRepositorio>.Instance);
        var resultado = await repositorio.ListarAsync(
            organizacao.Id, unidade.Id, null, CancellationToken.None);

        Assert.False(resultado.Single(item => item.NomeCompleto == "Sem Acesso").PortalAtivo);
        Assert.True(resultado.Single(item => item.NomeCompleto == "Portal Ativo").PortalAtivo);
        Assert.False(resultado.Single(item => item.NomeCompleto == "Vínculo Inativo").PortalAtivo);
        Assert.False(resultado.Single(item => item.NomeCompleto == "Outra Unidade").PortalAtivo);
        Assert.False(resultado.Single(item => item.NomeCompleto == "Outro Perfil").PortalAtivo);
    }

    private static Aluno CriarAluno(Guid organizacaoId, string nome, DateTime agora)
    {
        return new Aluno(
            Guid.NewGuid(), organizacaoId, nome,
            new DateOnly(2005, 5, 10), new DateOnly(2026, 9, 1), agora);
    }
}
