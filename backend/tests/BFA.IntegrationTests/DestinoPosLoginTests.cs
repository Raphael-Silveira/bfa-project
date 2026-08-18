using BFA.Application.Acessos;
using BFA.Domain.Acessos;

namespace BFA.IntegrationTests;

public sealed class DestinoPosLoginTests
{
    [Fact]
    public async Task Vinculo_administrador_rede_ativo_retorna_destino_administrador_rede()
    {
        var usuarioId = Guid.NewGuid();
        var acessos = new TestAcessoUsuarioConsulta();
        acessos.Adicionar(
            usuarioId,
            Guid.NewGuid(),
            unidadeId: null,
            PerfilAcesso.AdministradorRede);
        var servico = new DestinoPosLogin(acessos);

        var destino = await servico.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(DestinoAcesso.AdministradorRede, destino);
    }

    [Fact]
    public async Task Sem_vinculo_administrador_rede_retorna_destino_padrao()
    {
        var usuarioId = Guid.NewGuid();
        var acessos = new TestAcessoUsuarioConsulta();
        var servico = new DestinoPosLogin(acessos);

        var destino = await servico.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(DestinoAcesso.Padrao, destino);
    }

    [Fact]
    public async Task Vinculo_administrador_rede_inativo_retorna_destino_padrao()
    {
        var usuarioId = Guid.NewGuid();
        var acessos = new TestAcessoUsuarioConsulta();
        acessos.Adicionar(
            usuarioId,
            Guid.NewGuid(),
            unidadeId: null,
            PerfilAcesso.AdministradorRede,
            ativo: false);
        var servico = new DestinoPosLogin(acessos);

        var destino = await servico.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(DestinoAcesso.Padrao, destino);
    }
}
