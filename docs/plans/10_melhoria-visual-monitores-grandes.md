# Plano: Melhoria Visual em Monitores Grandes

## Status

**Em andamento**

## Objetivo

Otimizar o uso do espaço em monitores grandes (≥1440px) em todas as áreas do projeto — Franqueadora, Unidade e Aluno.

## Problema Identificado

Em monitores 2K/4K, o conteúdo se estica infinitamente ou fica com colunas desnecessariamente largas:

1. **Tabelas com min-width fixo** — forçam scroll horizontal desnecessário
2. **Grids de 2-3 colunas fixas** — não escalam, desperdiçando espaço
3. **Cards de métricas** — ficam excessivamente largos com apenas 2-3 colunas
4. **Área de conteúdo** — já limitada a 80rem (commit anterior), mas formulários e tabelas específicas ignoram esse limite

## Escopo

### Fase 1 — Tabelas (Quick Wins)

| Seletor | Arquivo | Mudança |
|---------|---------|---------|
| `.bfa-usuarios-table` | franqueadora.css | `min-width: 0` em `min-width: 90rem` |
| `.bfa-franqueados-table` | franqueadora.css | `min-width: 0` em `min-width: 90rem` |
| `.bfa-admin-table` | admin.css | Já feito no commit anterior |

### Fase 2 — Formulários 2-col → 3-col

Em `min-width: 90rem`, converter grids de 2 colunas para 3 onde o formulário tem campos suficientes:

| Seletor | Arquivo | Antes | Depois |
|---------|---------|-------|--------|
| `.bfa-usuario-form__grid` | franqueadora.css | `repeat(2, 1fr)` | `repeat(3, 1fr)` |
| `.bfa-professor-form-grid` | unidade.css | `repeat(2, 1fr)` | `repeat(3, 1fr)` |
| `.bfa-turma-form-grid` | unidade.css | `repeat(2, 1fr)` | `repeat(3, 1fr)` |

### Fase 3 — Tabelas do Aluno (cards contenham tabelas)

As views do Aluno (Matrículas, Frequência, Financeiro, Agenda) colocam tabelas dentro de `bfa-admin-card`, que se estica para 80rem. Adicionar `max-width` nestes cards específicos:

| Seletor | Arquivo | Mudança |
|---------|---------|---------|
| `.bfa-admin-card` com tabela (Aluno) | unidade.css ou admin.css | `max-width: 64rem` em `min-width: 90rem` |

Abordagem: adicionar uma classe `.bfa-admin-card--narrow` ou usar seletor mais específico.

### Fase 4 — Consistência de max-width

Alguns formulários usam max-width abaixo do cap de 80rem, criando alinhamento à esquerda:

| Seletor | Arquivo | Antes | Depois |
|---------|---------|-------|--------|
| `.bfa-professor-form` | unidade.css | `68rem` | `76rem` |
| `.bfa-professor-search` | unidade.css | `68rem` | `76rem` |
| `.bfa-professor-search-results` | unidade.css | `68rem` | `76rem` |
| `.bfa-professor-selected` | unidade.css | `68rem` | `76rem` |
| `.bfa-matricula-filters` | unidade.css | `72rem` | `76rem` |

## Fora do escopo

- Modificar a estrutura HTML das views
- Adicionar JavaScript para layout
- Mudar o shell grid (sidebar + content)
- Alterar a paleta de cores ou tipografia

## Arquivos modificados

| Arquivo | Tipo |
|---------|------|
| `backend/src/BFA.Web/wwwroot/css/admin.css` | CSS global |
| `backend/src/BFA.Web/wwwroot/css/franqueadora.css` | CSS Franqueadora |
| `backend/src/BFA.Web/wwwroot/css/unidade.css` | CSS Unidade |

## Resultado esperado

- Em monitores 2560px: formulários de 3 colunas, tabelas sem scroll, cards de métricas com tamanho proporcional
- Em monitores 1920px: tabelas ocupam espaço disponível sem esticar
- Em monitores <1440px: nenhuma mudança (breakpoint `min-width: 90rem`)

## Critérios de aceite

- [ ] `dotnet build` sem erros
- [ ] `dotnet test` 1.177 passando
- [ ] Nenhuma mudança visível em telas <1440px
- [ ] Tabelas em monitores grandes não forçam scroll horizontal
- [ ] Formulários de 2-col ficam em 3-col em monitores grandes
- [ ] Cards de métricas do Aluno não ficam excessivamente largos
