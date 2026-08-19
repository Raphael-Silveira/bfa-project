using BFA.Domain.Franqueados;
using BFA.Domain.Organizacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class FranqueadoConfiguration : IEntityTypeConfiguration<Franqueado>
{
    public void Configure(EntityTypeBuilder<Franqueado> builder)
    {
        builder.ToTable("franqueados", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_franqueados_tipo_pessoa_valido",
                "tipo_pessoa IN ('PessoaFisica', 'PessoaJuridica')");
            tableBuilder.HasCheckConstraint(
                "ck_franqueados_nome_razao_social_nao_vazio",
                "btrim(nome_razao_social) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_franqueados_documento_tipo_pessoa",
                "(tipo_pessoa = 'PessoaFisica' AND documento ~ '^[0-9]{11}$') OR "
                + "(tipo_pessoa = 'PessoaJuridica' AND documento ~ '^[A-Z0-9]{12}[0-9]{2}$')");
            tableBuilder.HasCheckConstraint(
                "ck_franqueados_email_nao_vazio",
                "btrim(email) <> ''");
        });

        builder.HasKey(franqueado => franqueado.Id)
            .HasName("pk_franqueados");

        builder.Property(franqueado => franqueado.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(franqueado => franqueado.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(franqueado => franqueado.TipoPessoa)
            .HasColumnName("tipo_pessoa")
            .HasColumnType("varchar(30)")
            .HasMaxLength(Franqueado.TipoPessoaTamanhoMaximo)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(franqueado => franqueado.NomeRazaoSocial)
            .HasColumnName("nome_razao_social")
            .HasColumnType("varchar(200)")
            .HasMaxLength(Franqueado.NomeRazaoSocialTamanhoMaximo)
            .IsRequired();

        builder.Property(franqueado => franqueado.NomeFantasia)
            .HasColumnName("nome_fantasia")
            .HasColumnType("varchar(200)")
            .HasMaxLength(Franqueado.NomeFantasiaTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Documento)
            .HasColumnName("documento")
            .HasColumnType("varchar(14)")
            .HasMaxLength(Franqueado.DocumentoTamanhoMaximo)
            .IsRequired();

        builder.Property(franqueado => franqueado.Telefone)
            .HasColumnName("telefone")
            .HasColumnType("varchar(30)")
            .HasMaxLength(Franqueado.TelefoneTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Email)
            .HasColumnName("email")
            .HasColumnType("varchar(256)")
            .HasMaxLength(Franqueado.EmailTamanhoMaximo)
            .IsRequired();

        builder.Property(franqueado => franqueado.EmailFinanceiro)
            .HasColumnName("email_financeiro")
            .HasColumnType("varchar(256)")
            .HasMaxLength(Franqueado.EmailFinanceiroTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.ResponsavelLegal)
            .HasColumnName("responsavel_legal")
            .HasColumnType("varchar(150)")
            .HasMaxLength(Franqueado.ResponsavelLegalTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Logradouro)
            .HasColumnName("logradouro")
            .HasColumnType("varchar(200)")
            .HasMaxLength(Franqueado.LogradouroTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Numero)
            .HasColumnName("numero")
            .HasColumnType("varchar(30)")
            .HasMaxLength(Franqueado.NumeroTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Complemento)
            .HasColumnName("complemento")
            .HasColumnType("varchar(100)")
            .HasMaxLength(Franqueado.ComplementoTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Bairro)
            .HasColumnName("bairro")
            .HasColumnType("varchar(100)")
            .HasMaxLength(Franqueado.BairroTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Cidade)
            .HasColumnName("cidade")
            .HasColumnType("varchar(100)")
            .HasMaxLength(Franqueado.CidadeTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Estado)
            .HasColumnName("estado")
            .HasColumnType("varchar(2)")
            .HasMaxLength(Franqueado.EstadoTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Cep)
            .HasColumnName("cep")
            .HasColumnType("varchar(8)")
            .HasMaxLength(Franqueado.CepTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("varchar(2000)")
            .HasMaxLength(Franqueado.ObservacoesTamanhoMaximo)
            .IsRequired(false);

        builder.Property(franqueado => franqueado.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(franqueado => franqueado.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(franqueado => franqueado.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(franqueado => new { franqueado.OrganizacaoId, franqueado.Id })
            .HasName("uq_franqueados_organizacao_id_id");

        builder.HasIndex(franqueado => new
            {
                franqueado.OrganizacaoId,
                franqueado.Documento
            })
            .IsUnique()
            .HasDatabaseName("uq_franqueados_organizacao_id_documento");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(franqueado => franqueado.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_franqueados_organizacoes_organizacao_id");
    }
}
