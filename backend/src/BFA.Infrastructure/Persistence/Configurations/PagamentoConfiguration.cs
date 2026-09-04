using BFA.Domain.Cobrancas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("pagamentos");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.OrganizacaoId)
            .HasColumnName("organizacao_id");

        builder.Property(p => p.UnidadeId)
            .HasColumnName("unidade_id");

        builder.Property(p => p.CobrancaId)
            .HasColumnName("cobranca_id");

        builder.Property(p => p.Valor)
            .HasColumnName("valor")
            .HasColumnType("numeric(12,2)");

        builder.Property(p => p.DataPagamento)
            .HasColumnName("data_pagamento");

        builder.Property(p => p.DataRegistro)
            .HasColumnName("data_registro");

        builder.Property(p => p.FormaPagamento)
            .HasColumnName("forma_pagamento")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(p => p.Observacoes)
            .HasColumnName("observacoes");

        builder.Property(p => p.RegistradoPorUsuarioId)
            .HasColumnName("registrado_por_usuario_id");

        builder.Property(p => p.CriadoEmUtc)
            .HasColumnName("criado_em_utc");
    }
}
