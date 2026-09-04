# Plano: Financeiro Unidade x Aluno

## Status

**Concluído**

## Objetivo

Implementar o módulo **Financeiro** na Área da Unidade, permitindo:
- Gestão de cobranças (geração manual e automática)
- Registro de pagamentos
- Visão financeira do aluno
- Dashboard de receitas e inadimplência

## Contexto Atual

- Matricula já possui campos financeiros snapshot: `ValorMensalContratado`, `CobraTaxaMatricula`, `ValorTaxaMatricula`
- PlanoVersao define os preços: `ValorMensal`, `CobraMatricula`, `ValorMatricula`
- Nenhuma entidade financeira (Cobranca, Pagamento) existe
- Nenhuma migration financeira existe (V016+)

## Regras de Design

1. **Cobranca é ocorrência financeira concreta** — gerada a partir de uma Matricula
2. **Pagamento é registro de recebimento** — vinculado a uma ou mais Cobrancas
3. **Parcelas são geradas automaticamente** ao criar a matrícula ou manualmente
4. **Dois contextos financeiros distintos** (Franqueadora x Franquia vs Unidade x Aluno) — NÃO misturar

---

## 1. Migration V016

### Tabela `cobrancas`

| Coluna | Tipo | Constraints |
|--------|------|-------------|
| `id` | uuid | PK |
| `organizacao_id` | uuid | FK → organizacoes, NOT NULL |
| `unidade_id` | uuid | FK → unidades(organizacao_id, id), NOT NULL |
| `aluno_id` | uuid | FK → alunos(organizacao_id, id), NOT NULL |
| `matricula_id` | uuid | FK → matriculas(organizacao_id, unidade_id, id), NOT NULL |
| `tipo` | varchar(20) | NOT NULL, CHECK IN ('Matricula','Mensalidade','Avulso') |
| `descricao` | varchar(200) | NOT NULL |
| `valor` | numeric(12,2) | NOT NULL, CHECK valor > 0 |
| `valor_pago` | numeric(12,2) | NOT NULL DEFAULT 0, CHECK valor_pago >= 0 |
| `data_emissao` | date | NOT NULL |
| `data_vencimento` | date | NOT NULL |
| `data_pagamento` | date | NULL |
| `status` | varchar(20) | NOT NULL, DEFAULT 'Pendente', CHECK IN ('Pendente','Paga','Atrasada','Cancelada') |
| `observacoes` | text | NULL |
| `criado_por_usuario_id` | uuid | FK → usuarios, NOT NULL |
| `atualizado_por_usuario_id` | uuid | FK → usuarios, NOT NULL |
| `criado_em_utc` | timestamptz | NOT NULL |
| `atualizado_em_utc` | timestamptz | NOT NULL |

**Unique:** `uq_cobrancas_organizacao_unidade_id` (organizacao_id, unidade_id, id)
**Indexes:**
- `ix_cobrancas_organizacao_aluno` (organizacao_id, aluno_id)
- `ix_cobrancas_organizacao_matricula` (organizacao_id, matricula_id)
- `ix_cobrancas_organizacao_vencimento` (organizacao_id, data_vencimento, status)

**Triggers:**
- `trg_proteger_cobranca`: bloqueia mudança de id, organizacao_id, unidade_id, aluno_id, matricula_id, tipo, valor, data_emissao, data_vencimento, criado_por_usuario_id, criado_em_utc

### Tabela `pagamentos`

| Coluna | Tipo | Constraints |
|--------|------|-------------|
| `id` | uuid | PK |
| `organizacao_id` | uuid | FK → organizacoes, NOT NULL |
| `unidade_id` | uuid | FK → unidades(organizacao_id, id), NOT NULL |
| `cobranca_id` | uuid | FK → cobrancas(organizacao_id, unidade_id, id), NOT NULL |
| `valor` | numeric(12,2) | NOT NULL, CHECK valor > 0 |
| `data_pagamento` | date | NOT NULL |
| `data_registro` | timestamptz | NOT NULL |
| `forma_pagamento` | varchar(20) | NOT NULL, CHECK IN ('Dinheiro','Pix','CartaoCredito','CartaoDebito','Boleto','Transferencia','Outros') |
| `observacoes` | text | NULL |
| `registrado_por_usuario_id` | uuid | FK → usuarios, NOT NULL |
| `criado_em_utc` | timestamptz | NOT NULL |

**Unique:** `uq_pagamentos_organizacao_id` (organizacao_id, id)
**Indexes:**
- `ix_pagamentos_organizacao_cobranca` (organizacao_id, cobranca_id)

**Triggers:**
- `trg_proteger_pagamento`: bloqueia mudança de campos identitários; ao inserir, atualiza `valor_pago` e `status` da cobranca associada

### Trigger `trg_atualizar_cobranca_apos_pagamento`

Após INSERT ou DELETE em `pagamentos`:
- Recalcula `valor_pago` da cobranca (soma dos pagamentos)
- Atualiza `status`: se valor_pago >= valor → 'Paga'; senão se data_vencimento < hoje → 'Atrasada'; senão → 'Pendente'
- Atualiza `data_pagamento`: se status = 'Paga', data do último pagamento; senão NULL

---

## 2. Domain

### StatusCobranca.cs
```csharp
public enum StatusCobranca { Pendente, Paga, Atrasada, Cancelada }
```

### TipoCobranca.cs
```csharp
public enum TipoCobranca { Matricula, Mensalidade, Avulso }
```

### FormaPagamento.cs
```csharp
public enum FormaPagamento { Dinheiro, Pix, CartaoCredito, CartaoDebito, Boleto, Transferencia, Outros }
```

### Cobranca.cs
- Properties: Id, OrganizacaoId, UnidadeId, AlunoId, MatriculaId, Tipo, Descricao, Valor, ValorPago, DataEmissao, DataVencimento, DataPagamento?, Status, Observacoes?, audit
- Methods: Cancelar(), AtualizarObservacoes()

### Pagamento.cs
- Properties: Id, OrganizacaoId, UnidadeId, CobrancaId, Valor, DataPagamento, DataRegistro, FormaPagamento, Observacoes?, RegistradoPorUsuarioId, CriadoEmUtc

---

## 3. Application

### ICobrancasServico / CobrancasServico

| Método | Descrição |
|--------|-----------|
| `ListarAsync(usuarioId, unidadeId, filtros)` | Lista cobranças com filtros |
| `ObterAsync(usuarioId, unidadeId, cobrancaId)` | Detalhe da cobrança |
| `CriarAsync(usuarioId, unidadeId, solicitacao)` | Cria cobrança manual |
| `CancelarAsync(usuarioId, unidadeId, cobrancaId)` | Cancela cobrança pendente |
| `RegistrarPagamentoAsync(usuarioId, unidadeId, cobrancaId, pagamento)` | Registra pagamento |
| `ListarAlunosAsync(usuarioId, unidadeId)` | Lista alunos para seleção |
| `ObterResumoFinanceiroAsync(usuarioId, unidadeId)` | Dashboard de receitas |

### DTOs

```csharp
public sealed record CobrancaListaItem(
    Guid CobrancaId, string AlunoNome, string Descricao,
    TipoCobranca Tipo, decimal Valor, decimal ValorPago,
    DateOnly DataVencimento, StatusCobranca Status);

public sealed record CobrancaDetalhe(
    Guid CobrancaId, Guid AlunoId, string AlunoNome,
    string? AlunoCpf, string Descricao, TipoCobranca Tipo,
    decimal Valor, decimal ValorPago, DateOnly DataEmissao,
    DateOnly DataVencimento, DateOnly? DataPagamento,
    StatusCobranca Status, string? Observacoes,
    IReadOnlyList<PagamentoResumo> Pagamentos);

public sealed record PagamentoResumo(
    Guid PagamentoId, decimal Valor, DateOnly DataPagamento,
    FormaPagamento FormaPagamento, string? Observacoes);

public sealed record CriarCobrancaSolicitacao(
    Guid AlunoId, Guid MatriculaId, TipoCobranca Tipo,
    string Descricao, decimal Valor, DateOnly DataVencimento,
    string? Observacoes);

public sealed record RegistrarPagamentoSolicitacao(
    decimal Valor, DateOnly DataPagamento,
    FormaPagamento FormaPagamento, string? Observacoes);

public sealed record FiltroCobrancas(
    Guid? AlunoId, StatusCobranca? Status,
    TipoCobranca? Tipo, DateOnly? DataVencimentoInicio,
    DateOnly? DataVencimentoFim);

public sealed record ResumoFinanceiro(
    decimal TotalReceita, decimal TotalPendente,
    decimal TotalAtrasado, int CobrancasPendentes,
    int CobrancasAtrasadas);
```

---

## 4. Repository

### ICobrancasRepositorio / CobrancasRepositorio

| Método | Descrição |
|--------|-----------|
| `ListarAsync(orgId, unidadeId, filtros)` | Lista com filtros |
| `ObterAsync(orgId, unidadeId, cobrancaId)` | Detalhe completo |
| `CriarAsync(cobranca)` | Insere cobrança |
| `CancelarAsync(cobrancaId, orgId, atualizado)` | Cancela cobrança |
| `RegistrarPagamentoAsync(pagamento)` | Insere pagamento |
| `ObterResumoAsync(orgId, unidadeId)` | Dados do dashboard |
| `ListarAlunosAsync(orgId, unidadeId)` | Alunos da unidade |

---

## 5. Controller

**Rota:** `unidade/{unidadeId}/cobrancas`

| HTTP | Action | Descrição |
|------|--------|-----------|
| GET | `Index` | Lista cobranças |
| GET | `Nova` | Formulário de criação |
| POST | `Nova` | Criar cobrança |
| GET | `Detalhes` | Detalhe + pagamentos |
| POST | `Cancelar` | Cancelar cobrança |
| POST | `RegistrarPagamento` | Registrar pagamento |
| GET | `Resumo` | Dashboard financeiro |

---

## 6. Views

### Cobrancas/Index.cshtml
- Filtros: aluno, status, tipo, período
- Tabela: aluno, descrição, tipo, valor, vencimento, status, ação
- Botão "Nova cobrança"

### Cobrancas/Nova.cshtml
- Formulário: selecionar aluno, tipo, descrição, valor, vencimento

### Cobrancas/Detalhes.cshtml
- Resumo da cobrança
- Lista de pagamentos registrados
- Formulário de registro de pagamento

### Cobrancas/Resumo.cshtml
- KPIs: total receita, total pendente, total atrasado
- Indicadores: cobranças pendentes, cobranças atrasadas

---

## 7. Regras de Negócio

### Geração de Cobrança
- Tipo Matricula: gerada ao criar matrícula (se CobraTaxaMatricula = true)
- Tipo Mensalidade: gerada manualmente ou automaticamente (mensal)
- Tipo Avulso: criada manualmente pela unidade

### Pagamento
- Só pode pagar cobrança Pendente ou Atrasada
- Valor do pagamento não pode exceder saldo devedor
- Pagamento parcial permitido
- Status da cobranca atualizado automaticamente via trigger

### Cancelamento
- Só pode cancelar cobrança Pendente
- Pagamentos vinculados são mantidos (histórico)

---

## 8. Escopo

### Incluído
- Migration V016 (cobrancas + pagamentos)
- Domain: Cobranca, Pagamento, StatusCobranca, TipoCobranca, FormaPagamento
- EF Configurations
- Application: CobrancasServico
- Repository: CobrancasRepositorio
- Controller: CobrancasController
- Views: Index, Nova, Detalhes, Resumo
- Dashboard financeiro

### Fora do escopo
- Geração automática de parcelas
- Multa/juros por atraso
- Pagamento online
- Relatórios avançados
- Integração com gateways de pagamento

---

## 9. Resultado

**Concluído em:** 2026-09-04

### Arquivos Criados
- `database/migrations/V016__criar_cobrancas_e_pagamentos.sql`
- `backend/src/BFA.Domain/Cobrancas/Cobranca.cs`
- `backend/src/BFA.Domain/Cobrancas/Pagamento.cs`
- `backend/src/BFA.Domain/Cobrancas/StatusCobranca.cs`
- `backend/src/BFA.Domain/Cobrancas/TipoCobranca.cs`
- `backend/src/BFA.Domain/Cobrancas/FormaPagamento.cs`
- `backend/src/BFA.Infrastructure/Persistence/Configurations/CobrancaConfiguration.cs`
- `backend/src/BFA.Infrastructure/Persistence/Configurations/PagamentoConfiguration.cs`
- `backend/src/BFA.Application/Cobrancas/CobrancasUnidade.cs`
- `backend/src/BFA.Infrastructure/Cobrancas/CobrancasRepositorio.cs`
- `backend/src/BFA.Web/Areas/Unidade/Controllers/CobrancasController.cs`
- `backend/src/BFA.Web/ViewModels/Unidade/CobrancaViewModels.cs`
- `backend/src/BFA.Web/Areas/Unidade/Views/Cobrancas/Index.cshtml`
- `backend/src/BFA.Web/Areas/Unidade/Views/Cobrancas/Nova.cshtml`
- `backend/src/BFA.Web/Areas/Unidade/Views/Cobrancas/Detalhes.cshtml`
- `backend/src/BFA.Web/Areas/Unidade/Views/Cobrancas/Resumo.cshtml`

### Arquivos Modificados
- `BFA.Infrastructure/DependencyInjection.cs` — Registrado ICobrancasRepositorio + ICobrancasServico
- `BFA.Infrastructure/Persistence/BfaDbContext.cs` — Adicionados DbSets Cobrancas e Pagamentos
- `_UnidadeNavLinks.cshtml` — Adicionado link "Financeiro" no menu
- `FluxosTurmaGradeArchitectureTests.cs` — Migration count atualizado (15→16)
- `MatriculasOperacionaisArchitectureTests.cs` — Migration count atualizado (15→16)

### Testes
- 484 unitários passando
- 693 integração passando
- Total: 1.177
