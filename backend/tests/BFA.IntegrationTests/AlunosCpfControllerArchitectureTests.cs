using BFA.Web.Areas.Unidade.Controllers;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.IntegrationTests;

public sealed class AlunosCpfControllerArchitectureTests
{
    [Fact]
    public void Endpoint_de_CPF_exige_autenticacao_e_usa_get_com_contexto_da_rota()
    {
        var controller = typeof(AlunosController);
        var authorize = Assert.Single(controller
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        var metodo = controller.GetMethod(nameof(AlunosController.Cpf));

        Assert.NotNull(authorize);
        Assert.NotNull(metodo);
        Assert.NotNull(metodo.GetCustomAttributes(
            typeof(HttpGetAttribute), inherit: true).SingleOrDefault());
        Assert.Contains(metodo.GetParameters(), parametro => parametro.Name == "unidadeId");
        Assert.Contains(metodo.GetParameters(), parametro => parametro.Name == "alunoId");
    }

    [Fact]
    public void Detalhes_nao_embute_CPF_completo_no_HTML_inicial()
    {
        var caminho = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "src",
            "BFA.Web",
            "Areas",
            "Unidade",
            "Views",
            "Alunos",
            "Detalhes.cshtml");
        var view = File.ReadAllText(caminho);

        Assert.Contains("@aluno.CpfMascarado", view, StringComparison.Ordinal);
        Assert.Contains("data-cpf-endpoint", view, StringComparison.Ordinal);
        Assert.DoesNotContain("@aluno.Cpf}", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-cpf-value=\"@aluno.Cpf", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_de_acesso_concedido_respeita_contrato_do_layout_da_unidade()
    {
        Assert.True(typeof(IUnidadeContextoViewModel)
            .IsAssignableFrom(typeof(AcessoAlunoConcedidoViewModel)));

        var caminho = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "src",
            "BFA.Web",
            "Areas",
            "Unidade",
            "Views",
            "Alunos",
            "AcessoAlunoConcedido.cshtml");
        var view = File.ReadAllText(caminho);

        Assert.Contains("Model.AcessoJaExistente", view, StringComparison.Ordinal);
        Assert.Contains("Model.SenhaTemporaria is not null", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Busca_de_alunos_compara_CPF_normalizado_e_preserva_busca_por_nome()
    {
        var caminho = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "src",
            "BFA.Infrastructure",
            "Alunos",
            "AlunosRepositorio.cs");
        var repositorio = File.ReadAllText(caminho);

        Assert.Contains("CpfIdentificador.TentarNormalizar", repositorio, StringComparison.Ordinal);
        Assert.Contains("a.Cpf == cpf", repositorio, StringComparison.Ordinal);
        Assert.Contains("a.NomeCompleto.ToUpper().Contains(termo)", repositorio, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_de_senha_exige_POST_e_antiforgery()
    {
        var metodo = typeof(AlunosController).GetMethod(nameof(AlunosController.RedefinirSenha));

        Assert.NotNull(metodo);
        Assert.NotNull(metodo.GetCustomAttributes(
            typeof(HttpPostAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(metodo.GetCustomAttributes(
            typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
        Assert.Contains(metodo.GetParameters(), parametro => parametro.Name == "unidadeId");
        Assert.Contains(metodo.GetParameters(), parametro => parametro.Name == "alunoId");
    }

    [Fact]
    public void Reset_usa_Identity_sem_registrar_a_senha_e_reativa_troca_obrigatoria()
    {
        var caminho = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "src",
            "BFA.Infrastructure",
            "Alunos",
            "AcessoAlunoServico.cs");
        var servico = File.ReadAllText(caminho);

        Assert.Contains("GeneratePasswordResetTokenAsync", servico, StringComparison.Ordinal);
        Assert.Contains("ResetPasswordAsync", servico, StringComparison.Ordinal);
        Assert.Contains("UpdateSecurityStampAsync", servico, StringComparison.Ordinal);
        Assert.Contains("TrocaSenhaObrigatoria", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("logger.LogInformation(\n            \"Senha temporária do aluno redefinida: {AlunoId} na unidade {UnidadeId}\",\n            alunoId,\n            unidadeId,\n            senhaTemporaria", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_nao_cria_nova_identidade_nem_novo_vinculo()
    {
        var caminho = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "src",
            "BFA.Infrastructure",
            "Alunos",
            "AcessoAlunoServico.cs");
        var servico = File.ReadAllText(caminho);
        var inicio = servico.IndexOf(
            "public async Task<ResultadoAcessoAluno> RedefinirSenhaAsync",
            StringComparison.Ordinal);
        var fim = servico.IndexOf(
            "    private static string GerarSenhaTemporaria()",
            inicio,
            StringComparison.Ordinal);
        var reset = servico[inicio..fim];

        Assert.DoesNotContain("new UsuarioIdentity", reset, StringComparison.Ordinal);
        Assert.DoesNotContain("VinculosAcesso.Add", reset, StringComparison.Ordinal);
    }

    [Fact]
    public void Detalhes_exibe_reset_e_estado_de_primeiro_acesso_sem_reexibir_senha_antiga()
    {
        var caminho = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "src",
            "BFA.Web",
            "Areas",
            "Unidade",
            "Views",
            "Alunos",
            "Detalhes.cshtml");
        var view = File.ReadAllText(caminho);

        Assert.Contains("Redefinir senha de acesso", view, StringComparison.Ordinal);
        Assert.Contains("Aguardando primeiro acesso", view, StringComparison.Ordinal);
        Assert.Contains("aluno.UsuarioId is null", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Senha_temporaria_curta_contem_componentes_exigidos_pela_policy()
    {
        var caminho = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "src",
            "BFA.Infrastructure",
            "Alunos",
            "AcessoAlunoServico.cs");
        var servico = File.ReadAllText(caminho);

        Assert.Contains("RandomNumberGenerator.GetInt32", servico, StringComparison.Ordinal);
        Assert.Contains("return $\"Bfa-{numero:D6}\"", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("WebEncoders.Base64UrlEncode", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void Resultado_de_acesso_formata_CPF_apenas_na_apresentacao()
    {
        var viewModel = new AcessoAlunoConcedidoViewModel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BFA Unidade",
            "Aluno Teste",
            "10966666557",
            "Bfa-482731",
            false,
            false);

        Assert.Equal("109.666.665-57", viewModel.LoginFormatado);
        Assert.Equal("10966666557", viewModel.Usuario);
    }

    [Fact]
    public void Tela_de_resultado_tem_copia_acessivel_sem_submit()
    {
        var caminho = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "src",
            "BFA.Web",
            "Areas",
            "Unidade",
            "Views",
            "Alunos",
            "AcessoAlunoConcedido.cshtml");
        var view = File.ReadAllText(caminho);

        Assert.Contains("data-copy-temporary-password", view, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Copiar senha temporária\"", view, StringComparison.Ordinal);
        Assert.Contains("type=\"button\"", view, StringComparison.Ordinal);
        Assert.Contains("bfa-copy-temporary-password.js", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Lista_de_alunos_calcula_portal_em_join_tenant_escopado_sem_consulta_por_aluno()
    {
        var caminho = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "src",
            "BFA.Infrastructure",
            "Alunos",
            "AlunosRepositorio.cs");
        var repositorio = File.ReadAllText(caminho);

        Assert.Contains("join usuario in dbContext.Users", repositorio, StringComparison.Ordinal);
        Assert.Contains("vinculo.OrganizacaoId == organizacaoId", repositorio, StringComparison.Ordinal);
        Assert.Contains("vinculo.UnidadeId == unidadeId", repositorio, StringComparison.Ordinal);
        Assert.Contains("vinculo.Perfil == PerfilAcesso.Aluno", repositorio, StringComparison.Ordinal);
        Assert.Contains("vinculo.Ativo", repositorio, StringComparison.Ordinal);
        Assert.Contains("acessosPortal.Contains", repositorio, StringComparison.Ordinal);
    }

    [Fact]
    public void Grid_de_alunos_exibe_portal_no_desktop_e_no_mobile()
    {
        var caminho = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "src",
            "BFA.Web",
            "Areas",
            "Unidade",
            "Views",
            "Alunos",
            "Index.cshtml");
        var view = File.ReadAllText(caminho);

        Assert.Contains("<th>Portal</th>", view, StringComparison.Ordinal);
        Assert.Contains("PortalAtivo ? \"Ativo\" : \"Sem acesso\"", view, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bfa-mobile-card__label\">Portal</span>", view, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }
}
