using BFA.Domain.Cobrancas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class CobrancaConfiguration : IEntityTypeConfiguration<Cobranca>
{
    public void Configure(EntityTypeBuilder<Cobranca> builder)
    {
        builder.ToTable("cobrancas");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id");

        builder.Property(c => c.OrganizacaoId)
            .HasColumnName("organizacao_id");

        builder.Property(c => c.UnidadeId)
            .HasColumnName("unidade_id");

        builder.Property(c => c.AlunoId)
            .HasColumnName("aluno_id");

        builder.Property(c => c.MatriculaId)
            .HasColumnName("matricula_id");

        builder.Property(c => c.Tipo)
            .HasColumnName("tipo")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(c => c.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(200);

        builder.Property(c => c.Valor)
            .HasColumnName("valor")
            .HasColumnType("numeric(12,2)");

        builder.Property(c => c.ValorPago)
            .HasColumnName("valor_pago")
            .HasColumnType("numeric(12,2)");

        builder.Property(c => c.DataEmissao)
            .HasColumnName("data_emissao");

        builder.Property(c => c.DataVencimento)
            .HasColumnName("data_vencimento");

        builder.Property(c => c.DataPagamento)
            .HasColumnName("data_pagamento");

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(c => c.Observacoes)
            .HasColumnName("observacoes");

        builder.Property(c => c.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id");

        builder.Property(c => c.AtualizadoPorUsuarioId)
            .HasColumnName("atualizado_por_usuario_id");

        builder.Property(c => c.CriadoEmUtc)
            .HasColumnName("criado_em_utc");

        builder.Property(c => c.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc");
    }
}
