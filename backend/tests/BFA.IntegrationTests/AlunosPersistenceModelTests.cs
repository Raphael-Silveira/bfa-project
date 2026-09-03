using BFA.Domain.Alunos;
using BFA.Domain.Organizacoes;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed class AlunosPersistenceModelTests
{
    [Fact]
    public void Aluno_mapeia_dados_opcionais_sem_UnidadeId_e_com_fks_restritivas()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(Aluno));

        Assert.NotNull(entityType);
        Assert.Equal("alunos", entityType.GetTableName());
        Assert.Equal("pk_alunos", entityType.FindPrimaryKey()!.GetName());
        Assert.Null(entityType.FindProperty("UnidadeId"));
        AssertColumn(entityType, nameof(Aluno.Id), "id", "uuid");
        AssertColumn(entityType, nameof(Aluno.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(entityType, nameof(Aluno.UsuarioId), "usuario_id", "uuid", isNullable: true);
        AssertColumn(
            entityType,
            nameof(Aluno.NomeCompleto),
            "nome_completo",
            "varchar(150)",
            Aluno.NomeCompletoTamanhoMaximo);
        AssertColumn(entityType, nameof(Aluno.DataNascimento), "data_nascimento", "date");
        AssertColumn(
            entityType,
            nameof(Aluno.Cpf),
            "cpf",
            "varchar(11)",
            Aluno.CpfTamanho,
            true);
        AssertColumn(
            entityType,
            nameof(Aluno.Telefone),
            "telefone",
            "varchar(30)",
            Aluno.TelefoneTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Aluno.Email),
            "email",
            "varchar(256)",
            Aluno.EmailTamanhoMaximo,
            true);

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(
            indexes["uq_alunos_organizacao_cpf"],
            true,
            nameof(Aluno.OrganizacaoId),
            nameof(Aluno.Cpf));
        Assert.Equal("cpf IS NOT NULL", indexes["uq_alunos_organizacao_cpf"].GetFilter());
        AssertIndex(
            indexes["uq_alunos_organizacao_usuario"],
            true,
            nameof(Aluno.OrganizacaoId),
            nameof(Aluno.UsuarioId));
        Assert.Equal(
            "usuario_id IS NOT NULL",
            indexes["uq_alunos_organizacao_usuario"].GetFilter());
        AssertIndex(
            indexes["ix_alunos_organizacao_ativo"],
            false,
            nameof(Aluno.OrganizacaoId),
            nameof(Aluno.Ativo));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_alunos_organizacao"],
            typeof(Organizacao),
            nameof(Aluno.OrganizacaoId));
        AssertForeignKey(
            foreignKeys["fk_alunos_usuario"],
            typeof(UsuarioIdentity),
            nameof(Aluno.UsuarioId));
        Assert.Equal(5, entityType.GetCheckConstraints().Count());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_aluno");
    }

    [Fact]
    public void Responsavel_mapeia_contato_obrigatorio_sem_UnidadeId()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(Responsavel));

        Assert.NotNull(entityType);
        Assert.Equal("responsaveis", entityType.GetTableName());
        Assert.Equal("pk_responsaveis", entityType.FindPrimaryKey()!.GetName());
        Assert.Null(entityType.FindProperty("UnidadeId"));
        AssertColumn(
            entityType,
            nameof(Responsavel.UsuarioId),
            "usuario_id",
            "uuid",
            isNullable: true);
        AssertColumn(
            entityType,
            nameof(Responsavel.Cpf),
            "cpf",
            "varchar(11)",
            Responsavel.CpfTamanho,
            true);
        AssertColumn(
            entityType,
            nameof(Responsavel.Telefone),
            "telefone",
            "varchar(30)",
            Responsavel.TelefoneTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Responsavel.Email),
            "email",
            "varchar(256)",
            Responsavel.EmailTamanhoMaximo,
            true);

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(
            indexes["uq_responsaveis_organizacao_cpf"],
            true,
            nameof(Responsavel.OrganizacaoId),
            nameof(Responsavel.Cpf));
        AssertIndex(
            indexes["uq_responsaveis_organizacao_usuario"],
            true,
            nameof(Responsavel.OrganizacaoId),
            nameof(Responsavel.UsuarioId));
        Assert.Equal(5, entityType.GetCheckConstraints().Count());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_responsavel");
    }

    [Fact]
    public void Vinculo_mapeia_enum_indices_e_fks_tenant_safe_sem_cascade()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(AlunoResponsavel));

        Assert.NotNull(entityType);
        Assert.Equal("alunos_responsaveis", entityType.GetTableName());
        Assert.Equal("pk_alunos_responsaveis", entityType.FindPrimaryKey()!.GetName());
        AssertColumn(
            entityType,
            nameof(AlunoResponsavel.TipoRelacao),
            "tipo_relacao",
            "varchar(30)",
            AlunoResponsavel.TipoRelacaoTamanhoMaximo);
        Assert.Equal(
            typeof(string),
            entityType.FindProperty(nameof(AlunoResponsavel.TipoRelacao))!
                .GetTypeMapping().Converter!.ProviderClrType);
        AssertColumn(
            entityType,
            nameof(AlunoResponsavel.DescricaoRelacao),
            "descricao_relacao",
            "varchar(100)",
            AlunoResponsavel.DescricaoRelacaoTamanhoMaximo,
            true);

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(
            indexes["uq_alunos_responsaveis_aluno_responsavel"],
            true,
            nameof(AlunoResponsavel.OrganizacaoId),
            nameof(AlunoResponsavel.AlunoId),
            nameof(AlunoResponsavel.ResponsavelId));
        AssertIndex(
            indexes["uq_alunos_responsaveis_principal_ativo"],
            true,
            nameof(AlunoResponsavel.OrganizacaoId),
            nameof(AlunoResponsavel.AlunoId));
        Assert.Equal(
            "principal_contato = true AND ativo = true",
            indexes["uq_alunos_responsaveis_principal_ativo"].GetFilter());
        AssertIndex(
            indexes["ix_alunos_responsaveis_organizacao_aluno_ativo"],
            false,
            nameof(AlunoResponsavel.OrganizacaoId),
            nameof(AlunoResponsavel.AlunoId),
            nameof(AlunoResponsavel.Ativo));
        AssertIndex(
            indexes["ix_alunos_responsaveis_organizacao_responsavel_ativo"],
            false,
            nameof(AlunoResponsavel.OrganizacaoId),
            nameof(AlunoResponsavel.ResponsavelId),
            nameof(AlunoResponsavel.Ativo));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_alunos_responsaveis_aluno"],
            typeof(Aluno),
            nameof(AlunoResponsavel.OrganizacaoId),
            nameof(AlunoResponsavel.AlunoId));
        AssertForeignKey(
            foreignKeys["fk_alunos_responsaveis_responsavel"],
            typeof(Responsavel),
            nameof(AlunoResponsavel.OrganizacaoId),
            nameof(AlunoResponsavel.ResponsavelId));
        AssertForeignKey(
            foreignKeys["fk_alunos_responsaveis_organizacao"],
            typeof(Organizacao),
            nameof(AlunoResponsavel.OrganizacaoId));
        Assert.Equal(2, entityType.GetCheckConstraints().Count());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_aluno_responsavel");
    }

    [Fact]
    public void Contexto_unico_expoe_os_tres_DbSets_e_nao_usa_delete_em_cascata()
    {
        using var context = CreateContext();
        var model = GetModel(context);

        Assert.NotNull(context.Alunos);
        Assert.NotNull(context.Responsaveis);
        Assert.NotNull(context.AlunosResponsaveis);

        foreach (var type in new[]
        {
            typeof(Aluno),
            typeof(Responsavel),
            typeof(AlunoResponsavel)
        })
        {
            var entityType = model.FindEntityType(type);
            Assert.NotNull(entityType);
            Assert.All(
                entityType.GetForeignKeys(),
                foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        }
    }

    private static BfaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql()
            .Options;

        return new BfaDbContext(options);
    }

    private static IModel GetModel(BfaDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    private static void AssertColumn(
        IEntityType entityType,
        string propertyName,
        string columnName,
        string columnType,
        int? maxLength = null,
        bool isNullable = false)
    {
        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(columnName, property.GetColumnName());
        Assert.Equal(columnType, property.GetColumnType());
        Assert.Equal(maxLength, property.GetMaxLength());
        Assert.Equal(isNullable, property.IsNullable);
    }

    private static void AssertIndex(
        IIndex index,
        bool isUnique,
        params string[] propertyNames)
    {
        Assert.Equal(isUnique, index.IsUnique);
        Assert.Equal(propertyNames, index.Properties.Select(property => property.Name));
    }

    private static void AssertForeignKey(
        IForeignKey foreignKey,
        Type principalType,
        params string[] propertyNames)
    {
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(principalType, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(propertyNames, foreignKey.Properties.Select(property => property.Name));
    }
}
