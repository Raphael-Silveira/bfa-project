using BFA.Domain.Alunos;
using BFA.Domain.DayUses;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class DayUseConfiguration : IEntityTypeConfiguration<DayUse>
{
    public void Configure(EntityTypeBuilder<DayUse> builder)
    {
        builder.ToTable("day_uses", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_day_uses_participante_valido",
                "(aluno_id IS NOT NULL AND nome_avulso IS NULL AND telefone_avulso IS NULL AND email_avulso IS NULL) OR (aluno_id IS NULL AND nome_avulso IS NOT NULL AND btrim(nome_avulso) <> '')");
            tableBuilder.HasCheckConstraint("ck_day_uses_valor_sugerido_nao_negativo", "valor_sugerido >= 0");
            tableBuilder.HasCheckConstraint("ck_day_uses_valor_cobrado_nao_negativo", "valor_cobrado >= 0");
        });

        builder.HasKey(item => item.Id).HasName("pk_day_uses");
        builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(item => item.OrganizacaoId).HasColumnName("organizacao_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.UnidadeId).HasColumnName("unidade_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.AlunoId).HasColumnName("aluno_id").HasColumnType("uuid").IsRequired(false);
        builder.Property(item => item.NomeAvulso).HasColumnName("nome_avulso").HasColumnType("varchar(150)").HasMaxLength(DayUse.NomeAvulsoTamanhoMaximo).IsRequired(false);
        builder.Property(item => item.TelefoneAvulso).HasColumnName("telefone_avulso").HasColumnType("varchar(30)").HasMaxLength(DayUse.TelefoneAvulsoTamanhoMaximo).IsRequired(false);
        builder.Property(item => item.EmailAvulso).HasColumnName("email_avulso").HasColumnType("varchar(256)").HasMaxLength(DayUse.EmailAvulsoTamanhoMaximo).IsRequired(false);
        builder.Property(item => item.DataUso).HasColumnName("data_uso").HasColumnType("date").IsRequired();
        builder.Property(item => item.ValorSugerido).HasColumnName("valor_sugerido").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(item => item.ValorCobrado).HasColumnName("valor_cobrado").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(item => item.Pago).HasColumnName("pago").HasColumnType("boolean").IsRequired();
        builder.Property(item => item.CriadoPorUsuarioId).HasColumnName("criado_por_usuario_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.CriadoEmUtc).HasColumnName("criado_em_utc").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasAlternateKey(item => new { item.OrganizacaoId, item.Id }).HasName("uq_day_uses_organizacao_id_id");
        builder.HasIndex(item => new { item.OrganizacaoId, item.UnidadeId, item.AlunoId, item.DataUso })
            .IsUnique().HasFilter("aluno_id IS NOT NULL").HasDatabaseName("uq_day_uses_aluno_data");
        builder.HasIndex(item => new { item.OrganizacaoId, item.UnidadeId, item.DataUso }).HasDatabaseName("ix_day_uses_unidade_data");

        builder.HasOne<Organizacao>().WithMany().HasForeignKey(item => item.OrganizacaoId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_day_uses_organizacao");
        builder.HasOne<Unidade>().WithMany().HasForeignKey(item => new { item.OrganizacaoId, item.UnidadeId }).HasPrincipalKey(item => new { item.OrganizacaoId, item.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_day_uses_unidade");
        builder.HasOne<Aluno>().WithMany().HasForeignKey(item => new { item.OrganizacaoId, item.AlunoId }).HasPrincipalKey(item => new { item.OrganizacaoId, item.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_day_uses_aluno");
        builder.HasOne<UsuarioIdentity>().WithMany().HasForeignKey(item => item.CriadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_day_uses_criado_por_usuario");
    }
}
