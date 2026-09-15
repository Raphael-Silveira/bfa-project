# Melhorar Visão Geral da Área Franqueadora

## Problem Statement

Recompor a tela `/franqueadora` usando o padrão visual já aprovado no dashboard da Unidade, preservando métricas, rotas, autorização, sidebar e módulos existentes.

## Objective

- Agrupar as cinco métricas operacionais e as três métricas financeiras em seções semânticas.
- Reutilizar os cards, ícones, corpo, labels, valores e links do Admin Shell.
- Renderizar valores monetários em pt-BR (`R$ 960,00`).
- Exibir “Unidades da Rede” com a listagem responsiva já padronizada, preservando o link `/unidade/{id}`.
- Preservar a sidebar da Franqueadora sem “Alunos da Rede” e “Planos da Rede”.
- Adicionar regressão automatizada para a composição e a formatação.

## Out of Scope

- Alterar regras de negócio, autorização, consultas, contratos ou rotas.
- Criar migrations, alterar banco ou executar Hangfire.
- Criar novo padrão visual ou novos módulos.

## Assumptions & Open Questions

| Assumption | Chosen default | Rationale |
|---|---|---|
| Dados exibidos | Manter o resumo atual | A consulta já entrega todos os indicadores aprovados. |
| Padrão visual | Dashboard da Unidade | É a referência existente e aprovada. |
| Cultura monetária | `pt-BR` no mapper | Evita depender da cultura do processo. |
| Git | Sem commit e sem push | Restrição explícita do usuário. |

**Open questions: none**

## User Stories

### US-01 — Consultar visão geral da rede

Como administrador da Franqueadora, quero visualizar os indicadores operacionais, financeiros e unidades da rede em uma composição compacta e responsiva.

**Acceptance Criteria**:

1. WHEN an authorized network administrator opens `/franqueadora`, the system SHALL render all five operational and three financial metrics.
2. WHEN monetary values are rendered, the system SHALL use Brazilian Real formatting with `R$` and decimal comma.
3. WHEN units exist, the system SHALL render desktop and mobile list patterns and preserve `/unidade/{id}` links.
4. WHEN the shell is rendered, the system SHALL preserve the approved Franqueadora sidebar entries and omit Alunos da Rede and Planos da Rede.

### US-02 — Preservar escopo técnico

Como responsável pelo produto, quero que a melhoria seja somente visual/testável, sem alterar regras, autorização ou banco.

**Acceptance Criteria**:

1. WHEN the feature is implemented, the system SHALL not alter application queries, authorization, routes, migrations, schema, or Hangfire configuration.
2. WHEN the full gates run, build and tests SHALL finish with zero errors and no new warnings.

## Critérios de aceitação

- **AC1**: Dado um administrador de rede autorizado, quando acessar `/franqueadora`, então a resposta é 200 e exibe as cinco métricas operacionais e as três financeiras com seus valores.
- **AC2**: Dado um resumo com valores monetários, quando o ViewModel for mapeado, então os valores usam cultura pt-BR e símbolo `R$`.
- **AC3**: Dado que existam unidades, quando a página for renderizada, então a lista apresenta nome, alunos e status no desktop e versão responsiva no mobile, mantendo o link `/unidade/{id}`.
- **AC4**: Dado o shell da Franqueadora, quando a página for renderizada, então a sidebar continua contendo Visão Geral, Usuários, Unidades e Franqueados, sem Alunos da Rede ou Planos da Rede.
- **AC5**: Nenhum arquivo de banco, migration, consulta de aplicação, autorização ou Hangfire é alterado.
- **AC6**: Build e testes completos passam sem erros; warnings novos não são introduzidos.

## Requirement Traceability

| ID | Requirement | Evidence / planned verification |
|---|---|---|
| UI-01 | Cards operacionais e financeiros seguem a referência Unidade | Inspeção da view + teste HTTP |
| DATA-01 | Todas as oito métricas permanecem visíveis | Teste HTTP do dashboard |
| MONEY-01 | Valores usam pt-BR | Teste unitário do mapper |
| LIST-01 | Lista responsiva e links preservados | Teste HTTP + inspeção Razor |
| NAV-01 | Sidebar preservada | Teste HTTP existente |
| DB-01 | Sem alterações de banco ou migrations | `git diff`/status em `database/migrations` |

## Evidência esperada

- Teste de integração do dashboard cobrindo a composição, valores monetários, lista/link e sidebar.
- Teste de integração do dashboard cobrindo cultura pt-BR.
- `dotnet build backend/BFA.sln` e `dotnet test backend/BFA.sln`.
