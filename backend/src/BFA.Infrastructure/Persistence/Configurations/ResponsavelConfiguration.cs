using BFA.Domain.Alunos;
using BFA.Domain.Organizacoes;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class ResponsavelConfiguration : IEntityTypeConfiguration<Responsavel>
{
    public void Configure(EntityTypeBuilder<Responsavel> builder)
    {
        builder.ToTable("responsaveis", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_responsavel");
            tableBuilder.HasCheckConstraint(
                "ck_responsaveis_nome_completo_nao_vazio",
                "btrim(nome_completo) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_responsaveis_cpf_valido",
                "cpf IS NULL OR cpf ~ '^[0-9]{11}$'");
            tableBuilder.HasCheckConstraint(
                "ck_responsaveis_telefone_nao_vazio",
                "telefone IS NULL OR btrim(telefone) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_responsaveis_email_nao_vazio",
                "email IS NULL OR btrim(email) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_responsaveis_contato_obrigatorio",
                "telefone IS NOT NULL OR email IS NOT NULL");
        });

        builder.HasKey(responsavel => responsavel.Id)
            .HasName("pk_responsaveis");

        builder.Property(responsavel => responsavel.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(responsavel => responsavel.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(responsavel => responsavel.UsuarioId)
            .HasColumnName("usuario_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(responsavel => responsavel.NomeCompleto)
            .HasColumnName("nome_completo")
            .HasColumnType("varchar(150)")
            .HasMaxLength(Responsavel.NomeCompletoTamanhoMaximo)
            .IsRequired();

        builder.Property(responsavel => responsavel.Cpf)
            .HasColumnName("cpf")
            .HasColumnType("varchar(11)")
            .HasMaxLength(Responsavel.CpfTamanho)
            .IsRequired(false);

        builder.Property(responsavel => responsavel.Telefone)
            .HasColumnName("telefone")
            .HasColumnType("varchar(30)")
            .HasMaxLength(Responsavel.TelefoneTamanhoMaximo)
            .IsRequired(false);

        builder.Property(responsavel => responsavel.Email)
            .HasColumnName("email")
            .HasColumnType("varchar(256)")
            .HasMaxLength(Responsavel.EmailTamanhoMaximo)
            .IsRequired(false);

        builder.Property(responsavel => responsavel.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(responsavel => responsavel.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(responsavel => responsavel.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(responsavel => new
            {
                responsavel.OrganizacaoId,
                responsavel.Id
            })
            .HasName("uq_responsaveis_organizacao_id_id");

        builder.HasIndex(responsavel => new { responsavel.OrganizacaoId, responsavel.Cpf })
            .IsUnique()
            .HasFilter("cpf IS NOT NULL")
            .HasDatabaseName("uq_responsaveis_organizacao_cpf");

        builder.HasIndex(responsavel => new
            {
                responsavel.OrganizacaoId,
                responsavel.UsuarioId
            })
            .IsUnique()
            .HasFilter("usuario_id IS NOT NULL")
            .HasDatabaseName("uq_responsaveis_organizacao_usuario");

        builder.HasIndex(responsavel => new { responsavel.OrganizacaoId, responsavel.Ativo })
            .HasDatabaseName("ix_responsaveis_organizacao_ativo");

        builder.HasIndex(responsavel => responsavel.UsuarioId)
            .HasDatabaseName("ix_responsaveis_usuario_id");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(responsavel => responsavel.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_responsaveis_organizacao");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(responsavel => responsavel.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_responsaveis_usuario");
    }
}
