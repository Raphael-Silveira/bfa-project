using BFA.Domain.Organizacoes;
using BFA.Domain.Planos;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class PlanoDisponibilidadeUnidadeConfiguration
    : IEntityTypeConfiguration<PlanoDisponibilidadeUnidade>
{
    public void Configure(EntityTypeBuilder<PlanoDisponibilidadeUnidade> builder)
    {
        builder.ToTable("planos_disponibilidades_unidades", tableBuilder =>
            tableBuilder.HasTrigger("trg_proteger_plano_disponibilidade_unidade"));

        builder.HasKey(disponibilidade => disponibilidade.Id)
            .HasName("pk_planos_disponibilidades_unidades");

        builder.Property(disponibilidade => disponibilidade.Id)
            .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(disponibilidade => disponibilidade.OrganizacaoId)
            .HasColumnName("organizacao_id").HasColumnType("uuid").IsRequired();
        builder.Property(disponibilidade => disponibilidade.PlanoId)
            .HasColumnName("plano_id").HasColumnType("uuid").IsRequired();
        builder.Property(disponibilidade => disponibilidade.UnidadeId)
            .HasColumnName("unidade_id").HasColumnType("uuid").IsRequired();
        builder.Property(disponibilidade => disponibilidade.Ativo)
            .HasColumnName("ativo").HasColumnType("boolean").IsRequired();
        builder.Property(disponibilidade => disponibilidade.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id").HasColumnType("uuid").IsRequired();
        builder.Property(disponibilidade => disponibilidade.AtualizadoPorUsuarioId)
            .HasColumnName("atualizado_por_usuario_id").HasColumnType("uuid").IsRequired();
        builder.Property(disponibilidade => disponibilidade.CriadoEmUtc)
            .HasColumnName("criado_em_utc").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(disponibilidade => disponibilidade.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasAlternateKey(disponibilidade => new
            { disponibilidade.OrganizacaoId, disponibilidade.Id })
            .HasName("uq_planos_disponibilidades_unidades_organizacao_id_id");
        builder.HasIndex(disponibilidade => new
            { disponibilidade.OrganizacaoId, disponibilidade.PlanoId, disponibilidade.UnidadeId })
            .IsUnique()
            .HasDatabaseName("uq_planos_disponibilidades_unidades_organizacao_plano_unidade");
        builder.HasIndex(disponibilidade => new
            { disponibilidade.OrganizacaoId, disponibilidade.UnidadeId, disponibilidade.Ativo })
            .HasDatabaseName("ix_planos_disponibilidades_unidades_organizacao_unidade_ativo");
        builder.HasIndex(disponibilidade => new
            { disponibilidade.OrganizacaoId, disponibilidade.PlanoId, disponibilidade.Ativo })
            .HasDatabaseName("ix_planos_disponibilidades_unidades_organizacao_plano_ativo");
        builder.HasIndex(disponibilidade => disponibilidade.CriadoPorUsuarioId)
            .HasDatabaseName("ix_planos_disponibilidades_unidades_criado_por_usuario_id");
        builder.HasIndex(disponibilidade => disponibilidade.AtualizadoPorUsuarioId)
            .HasDatabaseName("ix_planos_disponibilidades_unidades_atualizado_por_usuario_id");

        builder.HasOne<Organizacao>().WithMany()
            .HasForeignKey(disponibilidade => disponibilidade.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_disponibilidades_unidades_organizacao");
        builder.HasOne<Plano>().WithMany()
            .HasForeignKey(disponibilidade => new
                { disponibilidade.OrganizacaoId, disponibilidade.PlanoId })
            .HasPrincipalKey(plano => new { plano.OrganizacaoId, plano.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_disponibilidades_unidades_plano");
        builder.HasOne<Unidade>().WithMany()
            .HasForeignKey(disponibilidade => new
                { disponibilidade.OrganizacaoId, disponibilidade.UnidadeId })
            .HasPrincipalKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_disponibilidades_unidades_unidade");
        builder.HasOne<UsuarioIdentity>().WithMany()
            .HasForeignKey(disponibilidade => disponibilidade.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_disponibilidades_unidades_criado_por_usuario_id");
        builder.HasOne<UsuarioIdentity>().WithMany()
            .HasForeignKey(disponibilidade => disponibilidade.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_disponibilidades_unidades_atualizado_por_usuario_id");
    }
}
