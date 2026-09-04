# Plano: Relatórios

## Status

**Concluído**

## Objetivo

Implementar o módulo **Relatórios** na Área da Unidade, centralizando:
- Dashboard com cards de navegação para todos os relatórios
- Relatório Financeiro Detalhado (receita por tipo, status, período)
- Relatório de Inadimplência (cobranças atrasadas com detalhes do aluno)
- Links para relatórios existentes (Frequência, Resumo Financeiro)

## Contexto Atual

- Frequência já existe em `/aulas/frequencia` (AulasController)
- Resumo Financeiro já existe em `/cobrancas/resumo` (CobrancasController)
- Dados disponíveis: aulas, presenças, cobranças, pagamentos, matrículas
- Nenhum controller Relatorios existe

## Regras de Design

1. **Relatórios são read-only** — sem estados, sem mutations
2. **Dados vêm dos serviços existentes** — não duplicar lógica
3. **Centralizar navegação** — hub de relatórios com cards
4. **Filtros por período** — padrão dataInicio/dataFim

---

## 1. Application — RelatoriosServico

### IRelatoriosServico / RelatoriosServico

| Método | Descrição |
|--------|-----------|
| `ObterResumoGeralAsync(usuarioId, unidadeId)` | Dashboard: contadores gerais |
| `ObterFinanceiroDetalhadoAsync(usuarioId, unidadeId, filtro)` | Receita detalhada por tipo/status/período |
| `ObterInadimplenciaAsync(usuarioId, unidadeId)` | Cobranças atrasadas com detalhes |

### DTOs

```csharp
public sealed record ResumoGeralRelatorios(
    int TotalAlunosAtivos,
    int TotalMatriculasAtivas,
    int TotalAulasConcluidas,
    int TotalCobrancasPendentes,
    int TotalCobrancasAtrasadas,
    decimal TotalReceita,
    decimal TotalPendente,
    decimal TotalAtrasado);

public sealed record FiltroRelatorio(
    DateOnly? DataInicio,
    DateOnly? DataFim);

public sealed record FinanceiroDetalheRelatorio(
    decimal TotalReceita,
    decimal TotalPendente,
    decimal TotalAtrasado,
    IReadOnlyList<FinanceiroPorTipo> PorTipo,
    IReadOnlyList<FinanceiroPorStatus> PorStatus,
    IReadOnlyList<FinanceiroPorPeriodo> PorPeriodo);

public sealed record FinanceiroPorTipo(
    TipoCobranca Tipo,
    decimal Valor,
    int Quantidade);

public sealed record FinanceiroPorStatus(
    StatusCobranca Status,
    decimal Valor,
    int Quantidade);

public sealed record FinanceiroPorPeriodo(
    int Ano,
    int Mes,
    decimal Receita,
    decimal Pendente);

public sealed record InadimplenciaRelatorio(
    decimal TotalAtrasado,
    int TotalAlunos,
    IReadOnlyList<InadimplenciaAluno> Alunos);

public sealed record InadimplenciaAluno(
    Guid AlunoId,
    string NomeCompleto,
    string? Cpf,
    int CobrancasAtrasadas,
    decimal ValorTotalAtrasado,
    DateOnly? PrimeiraDataVencimento,
    DateOnly? UltimaDataVencimento);
```

---

## 2. Controller — RelatoriosController

**Rota:** `unidade/{unidadeId}/relatorios`

| HTTP | Action | Descrição |
|------|--------|-----------|
| GET | `Index` | Dashboard com cards de navegação |
| GET | `Financeiro` | Relatório financeiro detalhado |
| GET | `Inadimplencia` | Relatório de inadimplência |

---

## 3. Views

### Relatorios/Index.cshtml
- 4 cards de navegação:
  - Frequência → /aulas/frequencia
  - Financeiro → /relatorios/financeiro
  - Inadimplência → /relatorios/inadimplencia
  - Resumo Financeiro → /cobrancas/resumo
- KPIs gerais: alunos ativos, matrículas ativas, aulas concluídas

### Relatorios/Financeiro.cshtml
- Filtros: dataInicio, dataFim
- KPIs: receita, pendente, atrasado
- Tabela por tipo (Matrícula, Mensalidade, Avulso)
- Tabela por status (Pendente, Paga, Atrasada, Cancelada)
- Tabela por período (mês a mês)

### Relatorios/Inadimplencia.cshtml
- KPIs: total atrasado, total alunos
- Tabela: aluno, CPF, cobranças atrasadas, valor, primeira/última data

---

## 4. Navigation

- Adicionar "Relatórios" ao menu da Unidade (após Financeiro)

---

## 5. Escopo

### Incluído
- Application: IRelatoriosServico + RelatoriosServico + DTOs
- Controller: RelatoriosController (3 actions)
- Views: Index, Financeiro, Inadimplencia
- Navigation link

### Fora do escopo
- Relatórios da Franqueadora (nível rede)
- Exportação PDF/Excel
- Gráficos interativos
- Relatórios por turma específica
- Relatórios por professor

---

## 6. Resultado

**Concluído em:** 2026-09-04

### Arquivos Criados
- `backend/src/BFA.Application/Relatorios/IRelatoriosServico.cs`
- `backend/src/BFA.Application/Relatorios/RelatoriosServico.cs`
- `backend/src/BFA.Web/Areas/Unidade/Controllers/RelatoriosController.cs`
- `backend/src/BFA.Web/ViewModels/Unidade/RelatorioViewModels.cs`
- `backend/src/BFA.Web/Areas/Unidade/Views/Relatorios/Index.cshtml`
- `backend/src/BFA.Web/Areas/Unidade/Views/Relatorios/Financeiro.cshtml`
- `backend/src/BFA.Web/Areas/Unidade/Views/Relatorios/Inadimplencia.cshtml`

### Arquivos Modificados
- `BFA.Infrastructure/DependencyInjection.cs` — Registrado IRelatoriosServico
- `_UnidadeNavLinks.cshtml` — Adicionado link "Relatórios"
- `PROJECT-STATE.md` — Atualizado roadmap
- `docs/plans/README.md` — Adicionado plano 08

### Testes
- 484 unitários passando
- 693 integração passando
- Total: 1.177
