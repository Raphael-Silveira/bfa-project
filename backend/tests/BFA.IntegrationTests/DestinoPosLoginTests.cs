using BFA.Application.Acessos;
using BFA.Domain.Acessos;

namespace BFA.IntegrationTests;

public sealed class DestinoPosLoginTests
{
    [Fact]
    public async Task Administrador_rede_ativo_tem_prioridade_sobre_unidades()
    {
        var usuarioId = Guid.NewGuid();
        var acessos = new TestAcessoUsuarioConsulta();
        var unidades = new TestUnidadesUsuarioConsulta();
        acessos.Adicionar(
            usuarioId,
            Guid.NewGuid(),
            unidadeId: null,
            PerfilAcesso.AdministradorRede);
        unidades.Adicionar(
            usuarioId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BFA Tietê");
        var servico = new DestinoPosLogin(acessos, unidades);

        var resultado = await servico.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(DestinoAcesso.AdministradorRede, resultado.Destino);
        Assert.Null(resultado.UnidadeId);
    }

    [Fact]
    public async Task Uma_unidade_ativa_retorna_destino_com_identificador()
    {
        var usuarioId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var unidades = new TestUnidadesUsuarioConsulta();
        unidades.Adicionar(
            usuarioId,
            Guid.NewGuid(),
            unidadeId,
            "BFA Sorocaba");
        var servico = new DestinoPosLogin(new TestAcessoUsuarioConsulta(), unidades);

        var resultado = await servico.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(DestinoAcesso.Unidade, resultado.Destino);
        Assert.Equal(unidadeId, resultado.UnidadeId);
    }

    [Fact]
    public async Task Multiplas_unidades_ativas_exigem_selecao()
    {
        var usuarioId = Guid.NewGuid();
        var unidades = new TestUnidadesUsuarioConsulta();
        unidades.Adicionar(usuarioId, Guid.NewGuid(), Guid.NewGuid(), "BFA A");
        unidades.Adicionar(usuarioId, Guid.NewGuid(), Guid.NewGuid(), "BFA B");
        var servico = new DestinoPosLogin(new TestAcessoUsuarioConsulta(), unidades);

        var resultado = await servico.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(DestinoAcesso.SelecionarUnidade, resultado.Destino);
        Assert.Null(resultado.UnidadeId);
    }

    [Fact]
    public async Task Sem_vinculo_ativo_retorna_sem_acesso()
    {
        var servico = new DestinoPosLogin(
            new TestAcessoUsuarioConsulta(),
            new TestUnidadesUsuarioConsulta());

        var resultado = await servico.ObterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(DestinoAcesso.SemAcesso, resultado.Destino);
    }

    [Fact]
    public async Task Vinculos_inativos_nao_definem_destino()
    {
        var usuarioId = Guid.NewGuid();
        var acessos = new TestAcessoUsuarioConsulta();
        var unidades = new TestUnidadesUsuarioConsulta();
        acessos.Adicionar(
            usuarioId,
            Guid.NewGuid(),
            unidadeId: null,
            PerfilAcesso.AdministradorRede,
            ativo: false);
        unidades.Adicionar(
            usuarioId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BFA Inativa",
            ativa: false);
        var servico = new DestinoPosLogin(acessos, unidades);

        var resultado = await servico.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(DestinoAcesso.SemAcesso, resultado.Destino);
    }

    [Fact]
    public async Task Professor_com_uma_unidade_vai_direto_para_a_area()
    {
        var usuarioId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var unidades = new TestUnidadesUsuarioConsulta();
        unidades.AdicionarProfessor(
            usuarioId, Guid.NewGuid(), unidadeId, "BFA Cerquilho");
        var servico = new DestinoPosLogin(new TestAcessoUsuarioConsulta(), unidades);

        var resultado = await servico.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(DestinoAcesso.ProfessorUnidade, resultado.Destino);
        Assert.Equal(unidadeId, resultado.UnidadeId);
    }

    [Fact]
    public async Task Professor_com_multiplas_unidades_vai_para_selecao_propria()
    {
        var usuarioId = Guid.NewGuid();
        var unidades = new TestUnidadesUsuarioConsulta();
        unidades.AdicionarProfessor(usuarioId, Guid.NewGuid(), Guid.NewGuid(), "BFA A");
        unidades.AdicionarProfessor(usuarioId, Guid.NewGuid(), Guid.NewGuid(), "BFA B");
        var servico = new DestinoPosLogin(new TestAcessoUsuarioConsulta(), unidades);

        var resultado = await servico.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(DestinoAcesso.SelecionarUnidadeProfessor, resultado.Destino);
    }

    [Fact]
    public async Task Administrador_unidade_tem_prioridade_sobre_professor()
    {
        var usuarioId = Guid.NewGuid();
        var unidadeAdmin = Guid.NewGuid();
        var unidades = new TestUnidadesUsuarioConsulta();
        unidades.Adicionar(usuarioId, Guid.NewGuid(), unidadeAdmin, "BFA Admin");
        unidades.AdicionarProfessor(usuarioId, Guid.NewGuid(), Guid.NewGuid(), "BFA Professor");
        var servico = new DestinoPosLogin(new TestAcessoUsuarioConsulta(), unidades);

        var resultado = await servico.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(DestinoAcesso.Unidade, resultado.Destino);
        Assert.Equal(unidadeAdmin, resultado.UnidadeId);
    }
}
