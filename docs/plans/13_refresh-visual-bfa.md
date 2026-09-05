# Plano 13 — Refresh Visual BFA Admin

## 1. Objetivo

Modernizar a identidade visual administrativa do BFA, evoluindo de "dark + amarelo forte + bordas pesadas + componentes pesados" para "dark premium + clean + moderno + esportivo + sofisticado".

**Este plano é SOMENTE UI/UX.** Não altera regras de negócio, Domain, Application, Infrastructure, banco, migrations, autorização, rotas ou ViewModels.

## 2. Diagnóstico Visual Atual

### 2.1 Excesso de amarelo
- Todo card tem `border-top: 0.2rem solid var(--bfa-accent)` — barra amarela em TODOS os cards
- Sidebar: gradiente com amarelo no hover e item ativo (`linear-gradient(90deg, rgba(255, 193, 7, 0.14), ...)`)
- Drawer: `radial-gradient` com amarelo
- Botões: secondary usa amarelo como borda e cor — competem com primary
- Calendário: borda amarela, hover amarelo
- Filtros: selected state usa amarelo

### 2.2 Cards pesados
- `border-top: 0.2rem solid var(--bfa-accent)` cria sensação de caixa pesada
- `box-shadow: 0 1rem 2.5rem rgba(0, 0, 0, 0.2)` — sombra excessiva
- `background: linear-gradient(145deg, rgba(30, 30, 30, 0.98), rgba(13, 13, 13, 0.96))` — gradiente sutil mas desnecessário
- `min-height: 10rem` em metric cards — excessivamente altos

### 2.3 Sidebar dominante
- Item ativo usa `box-shadow: inset 0.18rem 0 var(--bfa-accent)` — barra lateral amarela
- Hover usa gradiente amarelo
- Fundo confunde com conteúdo principal

### 2.4 Tipografia
- Uppercase excessivo em labels, eyebrows, badges, dt
- `font-weight: 800` e `900` em muitos elementos
- Hierarquia não clara entre título, subtítulo e corpo

### 2.5 Botões
- Primary e secondary ambos usam amarelo — confunde hierarquia
- Secondary: `border: 1px solid var(--bfa-accent)` — parece primário

### 2.6 Tabelas
- Hover usa `rgba(255, 193, 7, 0.04)` — amarelo sutil mas presente
- Borders pesados

### 2.7 Headers de card
- Eyebrow sempre em amarelo uppercase — repetitivo

## 3. Direção Visual

### Princípios
1. **Amarelo = acento**, não estrutura
2. **Superfícies com profundidade** — camadas de escuro
3. **Borders neutras** — não amarelas
4. **Tipografia hierárquica** — menos uppercase, pesos moderados
5. **Cards leves** — borda fina neutra, sem barra amarela
6. **Sidebar refinada** — item ativo sutil, sem gradiente pesado
7. **Botões com hierarquia clara** — primary dourado, secondary neutro, tertiary ghost

### Identidade preservada
- Fundo escuro ✓
- Amarelo/dourado BFA ✓
- Branco ✓
- Linguagem esportiva premium ✓

## 4. Design Tokens (bfa-theme.css)

### Novas variáveis de superfície
```css
--bfa-surface: #161616;           /* card/surface background */
--bfa-surface-elevated: #1C1C1C;  /* hover/elevated surface */
--bfa-surface-overlay: #222222;   /* overlay/drawer */
```

### Borders refinadas
```css
--bfa-border: rgba(255, 255, 255, 0.08);      /* border neutra sutil */
--bfa-border-strong: rgba(255, 255, 255, 0.14); /* border mais visível */
--bfa-border-accent: rgba(255, 193, 7, 0.35);  /* border com accent (uso pontual) */
```

### Accent refinado
```css
--bfa-accent: #FFC107;
--bfa-accent-hover: #FFCA28;
--bfa-accent-subtle: rgba(255, 193, 7, 0.08);  /* fundo accent sutil */
--bfa-accent-muted: rgba(255, 193, 7, 0.55);   /* accent para borders pontuais */
```

### Bordas arredondadas
```css
--radius-sm: 0.35rem;
--radius-md: 0.5rem;
--radius-lg: 0.7rem;
--radius-xl: 0.85rem;
```

### Texto
```css
--bfa-text: #FFFFFF;
--bfa-text-secondary: #A0A0A0;
--bfa-text-muted: #737373;
```

## 5. Mudanças por Componente

### 5.1 Cards (admin.css)
- **Remover** `border-top: 0.2rem solid var(--bfa-accent)` de `.bfa-admin-card`
- Usar `border: 1px solid var(--bfa-border)` apenas
- Background: `var(--bfa-surface)` em vez de gradiente
- Shadow: reduzir para `0 0.25rem 0.75rem rgba(0, 0, 0, 0.15)`
- Radius: `var(--radius-lg)`

### 5.2 Metric Cards (admin.css)
- Reduzir `min-height` para `7.5rem` (desktop) / `6.5rem` (mobile)
- Label: remover uppercase, usar `font-weight: 600`, `font-size: 0.8rem`, `color: var(--bfa-text-secondary)`
- Value: `font-size: clamp(1.75rem, 3.5vw, 2.5rem)`, `font-weight: 700`
- Accent value: manter cor accent
- Danger value: manter cor danger

### 5.3 Sidebar (admin.css)
- Background: `var(--bfa-surface)` (levemente mais claro que o fundo)
- Item ativo: `background: var(--bfa-accent-subtle)`, `color: var(--bfa-text)`, sem box-shadow inset
- Item ativo svg: `color: var(--bfa-accent)`
- Hover: `background: var(--bfa-surface-elevated)`, sem border amarelo
- Nav gap: `0.3rem`
- Label: `color: var(--bfa-text-muted)`, `font-size: 0.68rem`, `font-weight: 600`

### 5.4 Header (admin.css)
- Background: `rgba(13, 13, 13, 0.98)` (mais opaco)
- Border-bottom: `1px solid var(--bfa-border)` (neutra)
- Brand identity border-left: `1px solid var(--bfa-border)` (neutra)
- Min-height: reduzir para `4.25rem`
- Context switch: cor neutra, hover com accent sutil
- Logout: botão ghost neutro

### 5.5 Botões (bfa-theme.css + admin.css)
- **Primary**: amarelo sólido (mantido)
- **Secondary**: border neutra, cor texto secundária, hover com surface elevado
- **Tertiary/ghost**: sem border, cor texto, hover com background sutil
- **Danger**: vermelho discreto
- Remover amarelo do secondary

### 5.6 Badges (admin.css)
- Manter `.is-active` verde e `.is-inactive` neutro
- Tamanho: manter compacto
- Font: `0.72rem`, `font-weight: 700`

### 5.7 Tabelas (admin.css)
- Header: `background: var(--bfa-surface)`, `color: var(--bfa-text-secondary)`, `font-weight: 600`
- Row hover: `background: var(--bfa-surface-elevated)` (sem amarelo)
- Border-bottom: `1px solid var(--bfa-border)` (neutra)
- Cells: `padding: 0.85rem 1rem`

### 5.8 Formulários (admin.css)
- Input border: `var(--bfa-border)`, focus: `var(--bfa-accent)` (mantido)
- Input background: `var(--bfa-surface)`
- Label: `color: var(--bfa-text-secondary)`, `font-weight: 600`
- Help text: `color: var(--bfa-text-muted)`

### 5.9 Drawer (admin.css)
- Background: `var(--bfa-surface-overlay)`
- Remover `radial-gradient` com amarelo
- Item ativo: borda esquerda accent, background sutil
- Header: border-bottom neutra

### 5.10 Page Header (admin.css)
- Eyebrow: manter accent (é marca)
- Title: `font-size: clamp(1.75rem, 4vw, 2.75rem)`, `font-weight: 700`, `letter-spacing: -0.02em`
- Description: `color: var(--bfa-text-secondary)`

### 5.11 Tipografia geral
- Labels/dt: `font-weight: 600` (não 800), `font-size: 0.72rem`
- Uppercase: apenas para labels muito pequenos e badges
- Body: `font-weight: 400`
- Card titles: `font-weight: 700`

### 5.12 Empty State / Notice
- Manter estrutura
- Border: neutra
- Marker accent: manter (é pontual)

## 6. Arquivos a Modificar

| Arquivo | Mudança |
|---|---|
| `wwwroot/css/bfa-theme.css` | Design tokens, botões, formulários |
| `wwwroot/css/admin.css` | Cards, sidebar, header, tabela, badges, métricas, drawer, tipografia |
| `wwwroot/css/unidade.css` | Cards de contrato, professor, turma, matrícula |
| `wwwroot/css/franqueadora.css` | Filtros, botões, cards de unidade/usuário |
| `Areas/Unidade/Views/Inicio/Index.cshtml` | Ajustes menores se necessário |

## 7. Fora do Escopo

- Não alterar funcionalidade
- Não alterar Domain/Application/Infrastructure
- Não criar migrations
- Não alterar rotas ou autorização
- Não alterar ViewModels
- Não alterar JavaScript
- Não propagar para outras telas nesta etapa

## 8. Responsividade

Validar em:
- 1920px (monitor grande)
- 1440px (desktop)
- 1024px (notebook)
- 768px (tablet)
- 390px (mobile)
- 360px (mobile compacto)

## 9. Critérios de Aceite

- [ ] Cards sem barra amarela no topo
- [ ] Sidebar leve, item ativo refinado
- [ ] Header clean, sem peso excessivo
- [ ] Botões com hierarquia clara (primary/secondary/tertiary)
- [ ] Badges compactos e discretos
- [ ] Tabelas com hover neutro
- [ ] Formulários com focus accent
- [ ] Tipografia hierárquica, menos uppercase
- [ ] Dashboard com 4 KPIs por linha em >=1400px
- [ ] Build 0 erros
- [ ] Testes passando
- [ ] Nenhum commit/push
