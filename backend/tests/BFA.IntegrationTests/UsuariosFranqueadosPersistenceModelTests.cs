using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Domain.Usuarios;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed class UsuariosFranqueadosPersistenceModelTests
{
    [Fact]
    public void Perfil_usuario_possui_mapeamento_e_relacao_um_para_um_com_identity()
    {
        using var context = CreateContext();
        var entityType = GetDesignTimeModel(context).FindEntityType(typeof(PerfilUsuario));

        Assert.NotNull(entityType);
        Assert.Equal("perfis_usuario", entityType.GetTableName());
        Assert.Equal("pk_perfis_usuario", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(PerfilUsuario.Id), "id", "uuid");
        AssertColumn(entityType, nameof(PerfilUsuario.UsuarioId), "usuario_id", "uuid");
        AssertColumn(
            entityType,
            nameof(PerfilUsuario.NomeCompleto),
            "nome_completo",
            "varchar(150)",
            PerfilUsuario.NomeCompletoTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(PerfilUsuario.Telefone),
            "telefone",
            "varchar(30)",
            PerfilUsuario.TelefoneTamanhoMaximo,
            true);
        AssertColumn(entityType, nameof(PerfilUsuario.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType,
            nameof(PerfilUsuario.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(PerfilUsuario.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");

        var uniqueUsuario = Assert.Single(entityType.GetIndexes());
        Assert.Equal("uq_perfis_usuario_usuario_id", uniqueUsuario.GetDatabaseName());
        Assert.True(uniqueUsuario.IsUnique);
        Assert.Equal(
            [nameof(PerfilUsuario.UsuarioId)],
            uniqueUsuario.Properties.Select(property => property.Name));

        var foreignKey = Assert.Single(entityType.GetForeignKeys());
        AssertForeignKey(
            foreignKey,
            "fk_perfis_usuario_usuarios_usuario_id",
            typeof(UsuarioIdentity),
            nameof(PerfilUsuario.UsuarioId));
        Assert.True(foreignKey.IsUnique);

        Assert.Equal(
            ["ck_perfis_usuario_nome_completo_nao_vazio"],
            entityType.GetCheckConstraints().Select(constraint => constraint.Name));
        Assert.Null(entityType.FindProperty("TipoUsuario"));
    }

    [Fact]
    public void Franqueado_possui_tipos_limites_enum_e_tenancy_explicitos()
    {
        using var context = CreateContext();
        var entityType = GetDesignTimeModel(context).FindEntityType(typeof(Franqueado));

        Assert.NotNull(entityType);
        Assert.Equal("franqueados", entityType.GetTableName());
        Assert.Equal("pk_franqueados", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(Franqueado.Id), "id", "uuid");
        AssertColumn(entityType, nameof(Franqueado.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(
            entityType,
            nameof(Franqueado.TipoPessoa),
            "tipo_pessoa",
            "varchar(30)",
            Franqueado.TipoPessoaTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(Franqueado.NomeRazaoSocial),
            "nome_razao_social",
            "varchar(200)",
            Franqueado.NomeRazaoSocialTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(Franqueado.NomeFantasia),
            "nome_fantasia",
            "varchar(200)",
            Franqueado.NomeFantasiaTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.Documento),
            "documento",
            "varchar(14)",
            Franqueado.DocumentoTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(Franqueado.Telefone),
            "telefone",
            "varchar(30)",
            Franqueado.TelefoneTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.Email),
            "email",
            "varchar(256)",
            Franqueado.EmailTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(Franqueado.EmailFinanceiro),
            "email_financeiro",
            "varchar(256)",
            Franqueado.EmailFinanceiroTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.ResponsavelLegal),
            "responsavel_legal",
            "varchar(150)",
            Franqueado.ResponsavelLegalTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.Logradouro),
            "logradouro",
            "varchar(200)",
            Franqueado.LogradouroTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.Numero),
            "numero",
            "varchar(30)",
            Franqueado.NumeroTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.Complemento),
            "complemento",
            "varchar(100)",
            Franqueado.ComplementoTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.Bairro),
            "bairro",
            "varchar(100)",
            Franqueado.BairroTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.Cidade),
            "cidade",
            "varchar(100)",
            Franqueado.CidadeTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.Estado),
            "estado",
            "varchar(2)",
            Franqueado.EstadoTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.Cep),
            "cep",
            "varchar(8)",
            Franqueado.CepTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Franqueado.Observacoes),
            "observacoes",
            "varchar(2000)",
            Franqueado.ObservacoesTamanhoMaximo,
            true);
        AssertColumn(entityType, nameof(Franqueado.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType,
            nameof(Franqueado.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(Franqueado.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");

        var tipoPessoa = entityType.FindProperty(nameof(Franqueado.TipoPessoa))!;
        var converter = tipoPessoa.GetTypeMapping().Converter;
        Assert.NotNull(converter);
        Assert.Equal(typeof(string), converter.ProviderClrType);
        Assert.Equal(
            "PessoaJuridica",
            converter.ConvertToProvider(TipoPessoaFranqueado.PessoaJuridica));

        var alternateKey = entityType.GetKeys().Single(key => !key.IsPrimaryKey());
        Assert.Equal("uq_franqueados_organizacao_id_id", alternateKey.GetName());
        Assert.Equal(
            [nameof(Franqueado.OrganizacaoId), nameof(Franqueado.Id)],
            alternateKey.Properties.Select(property => property.Name));

        var documentIndex = Assert.Single(entityType.GetIndexes());
        Assert.Equal(
            "uq_franqueados_organizacao_id_documento",
            documentIndex.GetDatabaseName());
        Assert.True(documentIndex.IsUnique);
        Assert.Equal(
            [nameof(Franqueado.OrganizacaoId), nameof(Franqueado.Documento)],
            documentIndex.Properties.Select(property => property.Name));

        AssertForeignKey(
            Assert.Single(entityType.GetForeignKeys()),
            "fk_franqueados_organizacoes_organizacao_id",
            typeof(Organizacao),
            nameof(Franqueado.OrganizacaoId));

        Assert.Equal(
            [
                "ck_franqueados_documento_tipo_pessoa",
                "ck_franqueados_email_nao_vazio",
                "ck_franqueados_nome_razao_social_nao_vazio",
                "ck_franqueados_tipo_pessoa_valido"
            ],
            entityType.GetCheckConstraints()
                .Select(constraint => constraint.Name)
                .OrderBy(name => name));
        var documentoConstraint = entityType.GetCheckConstraints().Single(
            constraint => constraint.Name == "ck_franqueados_documento_tipo_pessoa");
        Assert.Equal(
            "(tipo_pessoa = 'PessoaFisica' AND documento ~ '^[0-9]{11}$') OR "
            + "(tipo_pessoa = 'PessoaJuridica' AND documento ~ '^[A-Z0-9]{12}[0-9]{2}$')",
            documentoConstraint.Sql);
    }

    [Fact]
    public void Franqueado_usuario_possui_uniqueness_indices_e_fks_restritivas()
    {
        using var context = CreateContext();
        var entityType = GetDesignTimeModel(context).FindEntityType(typeof(FranqueadoUsuario));

        Assert.NotNull(entityType);
        Assert.Equal("franqueados_usuarios", entityType.GetTableName());
        Assert.Equal("pk_franqueados_usuarios", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(FranqueadoUsuario.Id), "id", "uuid");
        AssertColumn(
            entityType,
            nameof(FranqueadoUsuario.FranqueadoId),
            "franqueado_id",
            "uuid");
        AssertColumn(entityType, nameof(FranqueadoUsuario.UsuarioId), "usuario_id", "uuid");
        AssertColumn(entityType, nameof(FranqueadoUsuario.Principal), "principal", "boolean");
        AssertColumn(entityType, nameof(FranqueadoUsuario.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType,
            nameof(FranqueadoUsuario.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(FranqueadoUsuario.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");

        var indexes = entityType.GetIndexes()
            .ToDictionary(index => index.GetDatabaseName()!);
        Assert.Equal(3, indexes.Count);
        AssertIndex(
            indexes["uq_franqueados_usuarios_franqueado_id_usuario_id"],
            true,
            nameof(FranqueadoUsuario.FranqueadoId),
            nameof(FranqueadoUsuario.UsuarioId));

        var principalAtivo = indexes["uq_franqueados_usuarios_principal_ativo"];
        AssertIndex(
            principalAtivo,
            true,
            nameof(FranqueadoUsuario.FranqueadoId));
        Assert.Equal("principal = true AND ativo = true", principalAtivo.GetFilter());

        AssertIndex(
            indexes["ix_franqueados_usuarios_usuario_id"],
            false,
            nameof(FranqueadoUsuario.UsuarioId));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        Assert.Equal(2, foreignKeys.Count);
        AssertForeignKey(
            foreignKeys["fk_franqueados_usuarios_franqueado_id"],
            "fk_franqueados_usuarios_franqueado_id",
            typeof(Franqueado),
            nameof(FranqueadoUsuario.FranqueadoId));
        AssertForeignKey(
            foreignKeys["fk_franqueados_usuarios_usuario_id"],
            "fk_franqueados_usuarios_usuario_id",
            typeof(UsuarioIdentity),
            nameof(FranqueadoUsuario.UsuarioId));
    }

    [Fact]
    public void Franqueado_unidade_protege_organizacao_historico_e_vinculo_ativo()
    {
        using var context = CreateContext();
        var entityType = GetDesignTimeModel(context).FindEntityType(typeof(FranqueadoUnidade));

        Assert.NotNull(entityType);
        Assert.Equal("franqueados_unidades", entityType.GetTableName());
        Assert.Equal("pk_franqueados_unidades", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(FranqueadoUnidade.Id), "id", "uuid");
        AssertColumn(
            entityType,
            nameof(FranqueadoUnidade.FranqueadoId),
            "franqueado_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(FranqueadoUnidade.OrganizacaoId),
            "organizacao_id",
            "uuid");
        AssertColumn(entityType, nameof(FranqueadoUnidade.UnidadeId), "unidade_id", "uuid");
        AssertColumn(entityType, nameof(FranqueadoUnidade.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType,
            nameof(FranqueadoUnidade.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(FranqueadoUnidade.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");

        var indexes = entityType.GetIndexes()
            .ToDictionary(index => index.GetDatabaseName()!);
        Assert.Equal(4, indexes.Count);
        AssertIndex(
            indexes["ix_franqueados_unidades_franqueado_id"],
            false,
            nameof(FranqueadoUnidade.FranqueadoId));
        AssertIndex(
            indexes["uq_franqueados_unidades_franqueado_unidade"],
            true,
            nameof(FranqueadoUnidade.OrganizacaoId),
            nameof(FranqueadoUnidade.FranqueadoId),
            nameof(FranqueadoUnidade.UnidadeId));
        Assert.DoesNotContain(
            "ix_franqueados_unidades_organizacao_franqueado",
            indexes.Keys);
        AssertIndex(
            indexes["ix_franqueados_unidades_organizacao_unidade_ativo"],
            false,
            nameof(FranqueadoUnidade.OrganizacaoId),
            nameof(FranqueadoUnidade.UnidadeId),
            nameof(FranqueadoUnidade.Ativo));

        var activeIndex = indexes["uq_franqueados_unidades_unidade_ativa"];
        AssertIndex(
            activeIndex,
            true,
            nameof(FranqueadoUnidade.OrganizacaoId),
            nameof(FranqueadoUnidade.UnidadeId));
        Assert.Equal("ativo = true", activeIndex.GetFilter());

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        Assert.Equal(3, foreignKeys.Count);

        var franqueadoForeignKey =
            foreignKeys["fk_franqueados_unidades_franqueado"];
        AssertForeignKey(
            franqueadoForeignKey,
            "fk_franqueados_unidades_franqueado",
            typeof(Franqueado),
            nameof(FranqueadoUnidade.OrganizacaoId),
            nameof(FranqueadoUnidade.FranqueadoId));
        Assert.Equal(
            [nameof(Franqueado.OrganizacaoId), nameof(Franqueado.Id)],
            franqueadoForeignKey.PrincipalKey.Properties.Select(property => property.Name));

        AssertForeignKey(
            foreignKeys["fk_franqueados_unidades_organizacao"],
            "fk_franqueados_unidades_organizacao",
            typeof(Organizacao),
            nameof(FranqueadoUnidade.OrganizacaoId));

        var unidadeForeignKey = foreignKeys["fk_franqueados_unidades_unidade"];
        AssertForeignKey(
            unidadeForeignKey,
            "fk_franqueados_unidades_unidade",
            typeof(Unidade),
            nameof(FranqueadoUnidade.OrganizacaoId),
            nameof(FranqueadoUnidade.UnidadeId));
        Assert.Equal(
            [nameof(Unidade.OrganizacaoId), nameof(Unidade.Id)],
            unidadeForeignKey.PrincipalKey.Properties.Select(property => property.Name));
    }

    [Fact]
    public void Modelo_preserva_identity_acessos_e_ausencia_de_tipo_usuario()
    {
        using var context = CreateContext();
        var model = GetDesignTimeModel(context);
        var usuario = model.FindEntityType(typeof(UsuarioIdentity));

        Assert.NotNull(usuario);
        Assert.Equal("usuarios", usuario.GetTableName());
        Assert.Equal(typeof(Guid), usuario.FindProperty(nameof(UsuarioIdentity.Id))!.ClrType);
        Assert.Null(usuario.FindProperty("TipoUsuario"));
        Assert.NotNull(model.FindEntityType(typeof(Organizacao)));
        Assert.NotNull(model.FindEntityType(typeof(Unidade)));
        Assert.NotNull(model.FindEntityType(typeof(VinculoAcesso)));
        Assert.NotNull(model.FindEntityType(typeof(PerfilUsuario)));
        Assert.NotNull(model.FindEntityType(typeof(Franqueado)));
        Assert.NotNull(model.FindEntityType(typeof(FranqueadoUsuario)));
        Assert.NotNull(model.FindEntityType(typeof(FranqueadoUnidade)));
        Assert.Equal(
            [
                nameof(PerfilAcesso.AdministradorRede),
                nameof(PerfilAcesso.AdministradorUnidade),
                nameof(PerfilAcesso.Professor),
                nameof(PerfilAcesso.Aluno),
                nameof(PerfilAcesso.Responsavel)
            ],
            Enum.GetNames<PerfilAcesso>());
        Assert.DoesNotContain(nameof(Franqueado), Enum.GetNames<PerfilAcesso>());
        Assert.DoesNotContain(
            model.GetEntityTypes(),
            entityType => entityType.GetTableName()?.Contains(
                "role",
                StringComparison.OrdinalIgnoreCase) == true);
    }

    private static BfaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql()
            .Options;

        return new BfaDbContext(options);
    }

    private static IModel GetDesignTimeModel(BfaDbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

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
        string constraintName,
        Type principalType,
        params string[] propertyNames)
    {
        Assert.Equal(constraintName, foreignKey.GetConstraintName());
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(principalType, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(propertyNames, foreignKey.Properties.Select(property => property.Name));
    }
}
