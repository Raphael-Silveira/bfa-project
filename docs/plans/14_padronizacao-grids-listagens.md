# Plano 14 — Padronização Progressiva de Grids/Listagens Administrativas

## Objetivo

Padronizar visual e estruturalmente todas as listagens administrativas existentes no projeto BFA, usando a implementação aprovada de **Professores** como referência oficial.

## Referência aprovada

**Professores/Index.cshtml** — commit `e41d60f`

Componentes aprovados:
- Cabeçalho com eyebrow + título + descrição + ações
- Toolbar com busca + filtros por aba
- Tabela desktop com `.bfa-data-card`, colunas agrupadas, badges `.bfa-data-badge`
- Menu de ações com partial `_ProfessorAcoes`
- Cards mobile com `.bfa-mobile-card-list`
- Paginação com `_Paginacao.cshtml` + `PaginacaoViewModel`
- Estado vazio com `.bfa-admin-empty-state`

## Design system utilizado

- `docs/UI-ADMIN-STANDARDS.md` — referência normativa
- `bfa-theme.css` — tokens (superfícies, bordas, raios, sombras)
- `admin.css` — componentes compartilhados
- `brand/guide/brand-guide.md` — paleta e tipografia

## Inventário completo

### Listagens principais do módulo (candidatas a padronização completa)

| # | Área | Tela | Rota | View | Controller | Possui busca | Possui filtros | Possui paginação | Qtd provável registros |
|---|------|------|------|------|------------|-------------|----------------|-------------------|----------------------|
| 1 | Unidade | Professores | `/unidade/{id}/professores` | Professores/Index | ProfessoresController | ✅ nome/CPF/contato | ✅ Ativos/Encerrados/Todos | ✅ client-side 10/pág | 5–50 |
| 2 | Unidade | Alunos | `/unidade/{id}/alunos` | Alunos/Index | AlunosController | ✅ texto | ❌ | ✅ client-side 10/pág | 10–200 |
| 3 | Unidade | Matrículas | `/unidade/{id}/matriculas` | Matriculas/Index | MatriculasController | ✅ texto | ✅ status select | ✅ client-side 10/pág | 10–500 |
| 4 | Unidade | Turmas | `/unidade/{id}/turmas` | Turmas/Index | TurmasController | ❌ | ❌ | ❌ | 3–20 |
| 5 | Unidade | Aulas | `/unidade/{id}/aulas` | Aulas/Index | AulasController | ✅ data range | ✅ data range | ❌ | 5–100 |
| 6 | Unidade | Planos | `/unidade/{id}/planos` | Planos/Index | PlanosController | ❌ | ✅ Ativos/Inativos/Todos | ❌ | 2–10 |
| 7 | Unidade | Cobranças | `/unidade/{id}/cobrancas` | Cobrancas/Index | CobrancasController | ❌ | ✅ status+tipo+data | ✅ client-side 10/pág | 10–500 |
| 8 | Unidade | Inadimplência | `/unidade/{id}/relatorios/inadimplencia` | Relatorios/Inadimplencia | RelatoriosController | ❌ | ❌ | ❌ | 0–50 |
| 9 | Unidade | Frequência | `/unidade/{id}/aulas/frequencia` | Aulas/Frequencia | AulasController | ✅ data range | ✅ data range | ❌ | 5–50 |
| 10 | Franqueadora | Franqueados | `/franqueadora/franqueados` | Franqueados/Index | FranqueadosController | ❌ | ❌ | ❌ | 2–20 |
| 11 | Franqueadora | Unidades | `/franqueadora/unidades` | Unidades/Index | UnidadesController | ❌ | ❌ | ❌ | 3–30 |
| 12 | Franqueadora | Usuários | `/franqueadora/usuarios` | Usuarios/Index | UsuariosController | ❌ | ❌ | ❌ | 5–50 |
| 13 | Franqueadora | Alunos da Rede | `/franqueadora/alunos` | Alunos/Index | AlunosController | ✅ texto+unidade | ✅ unidade select | ✅ server-side | 10–1000 |
| 14 | Franqueadora | Acessos Unidade | `/franqueadora/unidades/{id}/acessos` | AcessosUnidade/Index | AcessosUnidadeController | ❌ | ❌ | ❌ | 1–10 |
| 15 | Franqueadora | Planos da Rede | `/franqueadora/planos` | Planos/Index | PlanosController | ❌ | ✅ Ativos/Inativos/Todos | ❌ | 2–10 |

### Listagens secundárias/históricas (reutilizar tipografia, tabela, badges — sem toolbar/paginação)

| # | Área | Tela | Tipo | Notas |
|---|------|------|------|-------|
| 16 | Unidade | Responsáveis do Aluno | Lista de cards | Já é card-based, reutilizar badges e ações |
| 17 | Unidade | Contrato | Painel/detalhe | Não é listagem, é detail view |
| 18 | Unidade | Relatórios Index | Dashboard/resumo | Cards de métricas, não listagem |
| 19 | Unidade | Relatório Financeiro | Relatório com tabela | Tabela informativa sem paginação |
| 20 | Franqueadora | Contratos | Painel/detalhe | Não é listagem |
| 21 | Professor | Turmas | Card grid | Já é card grid, reutilizar badges |
| 22 | Aluno | Matrículas | Lista read-only | Tabela simples sem ações |

### Telas que NÃO são listagens (não aplicar padrão)

| # | Área | Tela | Motivo |
|---|------|------|--------|
| 23 | Unidade | Dashboard | Painel de métricas |
| 24 | Franqueadora | Dashboard | Painel de métricas |
| 25 | Aluno | Dashboard | Perfil pessoal |
| 26 | Professor | Dashboard | Painel de turmas |
| 27 | Qualquer | Detalhes | Detail view |
| 28 | Qualquer | Formulários | Form view |
| 29 | Qualquer | Login/Acesso | Auth view |

## Classificação das telas — Esforço

| Esforço | Definição | Telas |
|---------|-----------|-------|
| **BAIXO** | Somente CSS/Razor, sem mudança de ViewModel ou backend | Turmas (Unidade), Planos (Unidade), Planos (Franq), Franqueados, Unidades (Franq), Usuários, AcessosUnidade, Inadimplência, Frequência, Aluno/Matrículas, Professor/Turmas |
| **MÉDIO** | ViewModel + busca/filtro/paginação, backend já suporta | Alunos (Unidade), Matrículas (Unidade), Aulas (Unidade), Cobranças (Unidade) |
| **ALTO** | Query/Application/Infrastructure precisam de adaptação para paginação server-side | Alunos da Rede (Franq) — já tem paginação mas usa padrão diferente |

## Dependências compartilhadas

### Já implementadas (Plano 13 + commit e41d60f)

- `PaginaResultado<T>` — `BFA.Application/PaginaResultado.cs`
- `PaginacaoViewModel` — `BFA.Web/ViewModels/Shared/PaginacaoViewModel.cs`
- `_Paginacao.cshtml` — `BFA.Web/Views/Shared/_Paginacao.cshtml`
- CSS patterns em `admin.css`: `.bfa-list-toolbar`, `.bfa-list-search`, `.bfa-filter-tabs`, `.bfa-data-card`, `.bfa-data-primary`, `.bfa-data-secondary`, `.bfa-data-stack`, `.bfa-data-badge`, `.bfa-kebab`, `.bfa-table-footer`, `.bfa-pagination`, `.bfa-mobile-card-*`, `.bfa-form-grid`, `.bfa-form-card`, `.bfa-card-title-row`, `.bfa-card-icon`, `.bfa-fields`, `.bfa-field`, `.bfa-field-grid-2`, `.bfa-money-input`

### Nenhuma dependência nova necessária

Todas as infraestruturas de paginação e CSS já estão consolidadas.

## Paginação compartilhada

**Decisão:** Paginação server-side para listagens com potencial > 50 registros.

| Tela | Paginação necessária | Estratégia |
|------|---------------------|------------|
| Professores | ✅ já implementada | Client-side (volume baixo) |
| Alunos (Unidade) | ✅ sim | Server-side |
| Matrículas (Unidade) | ✅ sim | Server-side |
| Cobranças (Unidade) | ✅ sim | Server-side |
| Aulas (Unidade) | ❌ não | Filtro por data já limita volume |
| Turmas (Unidade) | ❌ não | Volume baixo (3–20) |
| Planos (Unidade) | ❌ não | Volume baixo (2–10) |
| Inadimplência | ❌ não | Lista de alunos em atraso |
| Frequência | ❌ não | Filtro por data já limita volume |
| Franqueados | ❌ não | Volume baixo (2–20) |
| Unidades (Franq) | ❌ não | Volume baixo (3–30) |
| Usuários (Franq) | ❌ não | Volume baixo (5–50) |
| Alunos da Rede (Franq) | ✅ sim | Server-side (já tem) |
| Acessos Unidade | ❌ não | Volume baixo (1–10) |
| Planos da Rede | ❌ não | Volume baixo (2–10) |

## Ordem de implementação

Priorização: (1) mais utilizadas, (2) Área Unidade, (3) semelhantes a Professores, (4)受益em de paginação, (5) Franqueadora, (6) demais.

| Ordem | Área | Tela | Esforço | Paginação | Busca | Filtros | Justificativa |
|------:|------|------|---------|-----------|-------|---------|---------------|
| 1 | Unidade | Professores | — | ✅ | ✅ | ✅ | **REFERÊNCIA** — já concluído |
| 2 | Unidade | Alunos | MÉDIO | ✅ server-side | ✅ | ❌ | Alta utilização, busca existente, precisa paginação |
| 3 | Unidade | Matrículas | MÉDIO | ✅ server-side | ✅ | ✅ | Alta utilização, busca+status existentes |
| 4 | Unidade | Cobranças | MÉDIO | ✅ server-side | ❌ | ✅ | Alta utilização, filtros existentes |
| 5 | Unidade | Turmas | BAIXO | ❌ | ❌ | ❌ | Simples, semelhante a Professores, rapida |
| 6 | Unidade | Planos | BAIXO | ❌ | ❌ | ✅ | Usa partial compartilhada, rapida |
| 7 | Unidade | Aulas | BAIXO | ❌ | ✅ | ✅ | Já tem date range, padronizar visual |
| 8 | Unidade | Inadimplência | BAIXO | ❌ | ❌ | ❌ | Read-only, padronizar tabela/badges |
| 9 | Unidade | Frequência | BAIXO | ❌ | ✅ | ✅ | Read-only, padronizar visual |
| 10 | Franqueadora | Franqueados | BAIXO | ❌ | ❌ | ❌ | Padronizar tabela/badges/mobile |
| 11 | Franqueadora | Unidades | BAIXO | ❌ | ❌ | ❌ | Padronizar tabela/badges/mobile |
| 12 | Franqueadora | Usuários | BAIXO | ❌ | ❌ | ❌ | Padronizar tabela/badges/mobile |
| 13 | Franqueadora | Alunos da Rede | ALTO | ✅ server-side (já tem) | ✅ | ✅ | Já tem paginação mas usa padrão diferente |
| 14 | Franqueadora | Acessos Unidade | BAIXO | ❌ | ❌ | ❌ | Lista curta, padronizar visual |
| 15 | Franqueadora | Planos da Rede | BAIXO | ❌ | ❌ | ✅ | Usa partial compartilhada, rapida |

## Estratégia desktop

Cada listagem deve usar:

1. `.bfa-admin-page-header` com eyebrow + título + descrição + ações
2. `.bfa-list-toolbar` com busca (quando aplicável) + filtros
3. `.bfa-data-card` > `.bfa-table-wrap` > `table` com `thead`/`tbody`
4. Colunas agrupadas usando `.bfa-data-primary` + `.bfa-data-secondary` + `.bfa-data-stack`
5. Badges com `.bfa-data-badge--active` / `.bfa-data-badge--inactive`
6. Ações com `.bfa-admin-actions` ou `.bfa-kebab`
7. Rodapé com `.bfa-table-footer` + paginação (quando aplicável)

## Estratégia mobile

1. `.bfa-admin-desktop-list` → `display: none` no mobile
2. `.bfa-mobile-card-list` → `display: flex` no mobile
3. Cards com `.bfa-mobile-card`, `__head`, `__grid`, `__label`, `__actions`
4. Informação principal primeiro
5. Badges visíveis no head do card
6. Ações acessíveis no rodapé do card

## Estratégia de commits

- **UM commit por tela/módulo lógico**
- Mensagem: `feat(ui): padroniza listagem de <modulo>`
- Se mudanças de paginação: `feat(<modulo>): adiciona paginacao na listagem`
- Componentes compartilhados primeiro: `refactor(ui): consolida componentes compartilhados de listagem`
- Não misturar telas no mesmo commit

## Testes

Antes de cada commit:
1. `dotnet build` — 0 erros, 0 warnings
2. Testes do módulo/área afetada
3. Testes HTTP relevantes (autorização, paginação, busca/filtros)
4. Quando viável: `dotnet test backend/BFA.sln`

Não commitar com teste falhando.

## QA visual por tela

Antes de cada commit, validar em:

| Viewport | Largura | O que validar |
|----------|---------|---------------|
| Desktop largo | 1920px | Largura aproveitada, colunas proporcionais |
| Desktop padrão | 1440px | Referência principal, consistente com Professores |
| Notebook | 1024px | Sidebar, tabela, hierarquia |
| Tablet | 768px | Transição navegação, reorganização |
| Mobile comum | 390px | Cards, ações, paginação |
| Mobile compacto | 360px | Leitura, overflow, touch |
| Zoom | 200% | Legibilidade, sem sobreposição |

Perguntas-guia:
- Parece o mesmo produto que Professores?
- Mesma densidade visual?
- Mesma tipografia?
- Mesma toolbar?
- Mesma paginação?
- Mobile natural?
- Amarelo continua accent?
- Nenhuma borda pesada voltou?

## Riscos

1. **CSS compartilhado quebrar Professores** — verificar regressão visual sempre que `admin.css` mudar
2. **ViewModels sem suporte a paginação** — server-side pagination requer Changes no Controller + ViewModel + (opcionalmente) Repository
3. **Mudanças de URL/QueryString** — preservar compatibilidade com links existentes
4. **Testes de integração** — views alteradas podem quebrar asserts de HTML nos testes

## Status por tela

| Ordem | Área | Tela | Situação | Paginação | Esforço | Status | Commit |
|------:|------|------|----------|-----------|---------|--------|--------|
| 1 | Unidade | Professores | Referência | Sim (client) | — | Concluído | `e41d60f` |
| 2 | Unidade | Alunos | Padronizado | Sim (client) | MÉDIO | Concluído | `944de87` |
| 3 | Unidade | Matrículas | Padronizado | Sim (client) | MÉDIO | Concluído | `ad07a60` |
| 4 | Unidade | Cobranças | Padronizado | Sim (client) | MÉDIO | Concluído | `c4a047e` |
| 5 | Unidade | Turmas | Padronizado | Não | BAIXO | Concluído | `b334490` |
| 6 | Unidade | Planos | Padronizado | Não | BAIXO | Concluído | `b99279c` |
| 7 | Unidade | Aulas | Divergente | Não | BAIXO | Planejado | — |
| 8 | Unidade | Inadimplência | Divergente | Não | BAIXO | Planejado | — |
| 9 | Unidade | Frequência | Divergente | Não | BAIXO | Planejado | — |
| 10 | Franqueadora | Franqueados | Divergente | Não | BAIXO | Planejado | — |
| 11 | Franqueadora | Unidades | Divergente | Não | BAIXO | Planejado | — |
| 12 | Franqueadora | Usuários | Divergente | Não | BAIXO | Planejado | — |
| 13 | Franqueadora | Alunos da Rede | Parcial | Sim (diferente) | ALTO | Planejado | — |
| 14 | Franqueadora | Acessos Unidade | Divergente | Não | BAIXO | Planejado | — |
| 15 | Franqueadora | Planos da Rede | Parcial | Não | BAIXO | Planejado | — |

## Commits planejados

```
refactor(ui): consolida badges e acoes para padronizacao de listagens
feat(ui): padroniza listagem de alunos (Unidade)
feat(ui): padroniza listagem de matriculas (Unidade)
feat(ui): padroniza listagem de cobrancas (Unidade)
feat(ui): padroniza listagem de turmas (Unidade)
feat(ui): padroniza listagem de planos (Unidade)
feat(ui): padroniza listagem de aulas (Unidade)
feat(ui): padroniza listagem de inadimplencia (Unidade)
feat(ui): padroniza listagem de frequencia (Unidade)
feat(ui): padroniza listagem de franqueados (Franqueadora)
feat(ui): padroniza listagem de unidades (Franqueadora)
feat(ui): padroniza listagem de usuarios (Franqueadora)
feat(ui): padroniza listagem de alunos da rede (Franqueadora)
feat(ui): padroniza listagem de acessos unidade (Franqueadora)
feat(ui): padroniza listagem de planos da rede (Franqueadora)
```
