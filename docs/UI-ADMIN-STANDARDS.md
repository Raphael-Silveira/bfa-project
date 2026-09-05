# Padrão de Interface Administrativa — BFA Platform

## 1. Objetivo e caráter normativo

Este documento formaliza o padrão visual e estrutural vigente das interfaces administrativas da BFA Platform. Ele é referência obrigatória para criar, alterar ou revisar qualquer tela administrativa em `BFA.Web`.

O objetivo é preservar consistência entre áreas, reduzir duplicação e fazer a interface evoluir sobre componentes já validados. Este documento não cria funcionalidades nem substitui regras de arquitetura, domínio, autorização ou marca.

Antes de trabalhar em uma interface administrativa, leia, nesta ordem:

1. `AGENTS.md`;
2. `docs/ARCHITECTURE.md`;
3. `brand/guide/brand-guide.md`;
4. este documento;
5. a implementação existente relacionada à tarefa.

`docs/ADMIN-VISUAL.md` permanece como guia resumido da implementação atual. Em caso de dúvida sobre o padrão administrativo, este documento é a referência normativa.

## 2. Referências atuais da implementação

As telas da Área da Franqueadora são a referência concreta inicial deste padrão:

- shell: `backend/src/BFA.Web/Areas/Franqueadora/Views/Shared/_FranqueadoraLayout.cshtml`;
- navegação compartilhada: `backend/src/BFA.Web/Areas/Franqueadora/Views/Shared/_FranqueadoraNavLinks.cshtml`;
- visão geral: `backend/src/BFA.Web/Areas/Franqueadora/Views/Inicio/Index.cshtml`;
- listagem de unidades: `backend/src/BFA.Web/Areas/Franqueadora/Views/Unidades/Index.cshtml`;
- criação de unidade: `backend/src/BFA.Web/Areas/Franqueadora/Views/Unidades/Nova.cshtml`;
- edição de unidade: `backend/src/BFA.Web/Areas/Franqueadora/Views/Unidades/Editar.cshtml`;
- estilos administrativos reutilizáveis: `backend/src/BFA.Web/wwwroot/css/admin.css`;
- ajustes próprios da Área da Franqueadora: `backend/src/BFA.Web/wwwroot/css/franqueadora.css`;
- tokens e elementos globais da marca: `backend/src/BFA.Web/wwwroot/css/bfa-theme.css`.

Novas áreas administrativas devem reutilizar os padrões genéricos de `admin.css`. Ajustes específicos de uma área devem permanecer no arquivo da própria área e não devem duplicar estilos já existentes.

## 3. Identidade visual e direção estética

A interface administrativa deve transmitir a personalidade definida no guia da marca: performance, disciplina, evolução, comunidade, competição, profissionalismo e expansão.

A direção visual é esportiva, premium, objetiva e operacional. Isso significa:

- hierarquia clara e leitura rápida;
- superfícies escuras e contraste elevado;
- amarelo usado como acento funcional, não como decoração indiscriminada;
- espaçamento consistente e densidade adequada a tarefas administrativas;
- ausência de efeitos que prejudiquem legibilidade ou sugiram uma estética genérica de template;
- uso correto da assinatura oficial da BFA, sem distorção, recoloração, efeitos ou recriações.

### 3.1 Paleta oficial

Os valores de referência da marca são:

```css
--bfa-black: #0D0D0D;
--bfa-dark: #1E1E1E;
--bfa-gold: #FFC107;
--bfa-gray: #BDBDBD;
--bfa-white: #FFFFFF;
```

| Papel | Cor | Valor |
|---|---|---|
| Preto principal | BFA Black | `#0D0D0D` |
| Fundo escuro secundário | Dark Gray | `#1E1E1E` |
| Acento principal | BFA Gold | `#FFC107` |
| Texto secundário | Gray | `#BDBDBD` |
| Texto de alto contraste | White | `#FFFFFF` |

### 3.2 Design Tokens

O refresh visual introduziu tokens de superfície, borda e acento em `bfa-theme.css`:

| Token | Uso | Valor |
|---|---|---|
| `--bfa-surface` | Fundo de cards e componentes | `#161616` |
| `--bfa-surface-elevated` | Hover, sobreposições leves | `#1C1C1C` |
| `--bfa-surface-overlay` | Drawers, modais | `#222222` |
| `--bfa-border` | Bordas neutras sutis | `rgba(255,255,255,0.08)` |
| `--bfa-border-strong` | Bordas mais visíveis | `rgba(255,255,255,0.14)` |
| `--bfa-border-accent` | Bordas com acento (uso pontual) | `rgba(255,193,7,0.35)` |
| `--bfa-accent-subtle` | Fundo accent sutil | `rgba(255,193,7,0.08)` |
| `--bfa-accent-muted` | Accent para borders/hover | `rgba(255,193,7,0.55)` |
| `--bfa-text-secondary` | Texto secundário | `#A0A0A0` |
| `--bfa-text-muted` | Texto terciário | `#737373` |
| `--radius-sm` | Bordas pequenas | `0.35rem` |
| `--radius-md` | Bordas médias | `0.5rem` |
| `--radius-lg` | Bordas grandes (cards) | `0.7rem` |
| `--radius-xl` | Bordas extra grandes | `0.85rem` |

Utilize os tokens existentes em `bfa-theme.css` e `admin.css`. Não espalhe valores hexadecimais duplicados pelas Views ou por novos arquivos CSS quando já houver uma variável apropriada.

O amarelo deve identificar principalmente:

- ação primária;
- item ativo de navegação;
- foco e estados interativos relevantes;
- pequenos acentos de hierarquia;
- informações de destaque coerentes com a semântica da tela.

### 3.3 Tipografia

Regras de tipografia do refresh visual:

- Labels e `dt` devem usar `font-weight: 600` (não 800);
- Uppercase é permitido apenas em labels muito pequenos e badges;
- Títulos devem usar `font-weight: 700`;
- Texto secundário usa `var(--bfa-text-secondary)` e texto terciário usa `var(--bfa-text-muted)`.

Estado nunca deve ser comunicado somente por cor. Texto, ícone, forma ou rótulo devem complementar a cor.

## 4. Admin Shell

Toda tela administrativa deve ser renderizada dentro do shell compartilhado de sua área. Uma View não deve reconstruir cabeçalho, navegação lateral, drawer ou rodapé localmente.

O shell é composto por:

1. link de salto para o conteúdo principal;
2. cabeçalho com marca, identificação da área e ações globais;
3. navegação administrativa;
4. conteúdo principal;
5. rodapé;
6. drawer de navegação em telas menores.

As classes genéricas de referência incluem:

- `.bfa-admin`;
- `.bfa-admin-header`;
- `.bfa-admin-brand`;
- `.bfa-admin-shell`;
- `.bfa-admin-sidebar`;
- `.bfa-admin-main`;
- `.bfa-admin-nav` e `.bfa-admin-nav-link`;
- `.bfa-admin-drawer`;
- `.bfa-admin-footer`.

Regras compartilhadas para todos os shells internos:

- o título da seção de navegação deve refletir o contexto real do usuário; áreas operacionais ou pessoais não devem receber rótulos administrativos;
- todo item da sidebar e do drawer deve usar `.bfa-admin-nav-link`, com ícone, texto e os estados compartilhados de hover, foco e item ativo;
- nunca deixe um elemento `<a>` visualmente cru, azul ou sublinhado dentro da navegação interna;
- shells internos novos devem reutilizar o Admin Shell e suas classes de navegação, ainda que possuam identidade e destinos próprios.

### 4.1 Desktop

O cabeçalho utiliza fundo mais opaco (`rgba(13, 13, 13, 0.98)`) e borda inferior neutra (`var(--bfa-border)`), sem acentos amarelos na estrutura.

Em desktop:

- o cabeçalho permanece consistente entre todas as páginas da área;
- a barra lateral permanece fixa/lateral no shell e apresenta somente destinos implementados e autorizados;
- o fundo da sidebar é `var(--bfa-surface)`;
- o item ativo usa `background: var(--bfa-accent-subtle)` (sem box-shadow inset);
- o hover usa `background: var(--bfa-surface-elevated)` (sem gradiente amarelo);
- o conteúdo utiliza a largura e os recuos definidos pelo shell;
- o item atual da navegação usa `aria-current="page"` e o estado visual existente;
- a ação de sair permanece separada da navegação funcional.

### 4.2 Mobile e tablet

Em telas menores, a navegação lateral torna-se um drawer sobre o conteúdo:

- o header fica compacto, preservando branding e botão hambúrguer;
- largura máxima de `320px` e limite de `85vw`;
- altura de `100dvh`;
- overlay escuro sobre o conteúdo restante;
- rolagem interna quando necessária;
- cabeçalho compacto com área, contexto e botão de fechar;
- aproximadamente `16px` de padding horizontal interno;
- navegação agrupada imediatamente abaixo do cabeçalho;
- aproximadamente `8px` a `12px` entre itens;
- itens com aproximadamente `52px` a `56px` de altura;
- item ativo com fundo sutil, ícone amarelo e acento lateral amarelo;
- ação de sair após um separador, com margem aproximada de `20px` a `24px`.

O conteúdo interno do drawer não deve ser esticado para preencher a altura. Na lista principal, não use `justify-content: space-between`, `justify-content: space-around`, `flex: 1`, `height: 100%`, `margin-top: auto` ou comportamento equivalente que gere grandes vazios entre os itens.

O drawer deve ser implementado uma única vez no layout compartilhado. As páginas não devem possuir versões próprias do menu mobile.

## 5. Cabeçalho de página

Cada página administrativa começa com um cabeçalho contextual usando `.bfa-admin-page-header` e suas partes existentes:

- `__copy` para o bloco textual;
- `__eyebrow` para contexto curto, quando útil;
- `__title` para o único título principal da página;
- `__description` para explicar a tarefa ou o escopo;
- `__actions` para ações de página.

Regras:

- mantenha um único `h1` por página;
- use títulos curtos e orientados à tarefa;
- evite repetir no texto aquilo que já está claro no título;
- mantenha ações no mesmo bloco sem competir com a hierarquia textual;
- em telas estreitas, permita que título, descrição e ações se reorganizem verticalmente sem overflow.

## 6. Ação primária

Cada página deve ter, em regra, uma única ação primária evidente. Use os estilos existentes `.bfa-btn-primary` e `.bfa-admin-button`.

A ação primária:

- deve descrever o resultado, como “Nova unidade” ou “Salvar alterações”;
- fica no cabeçalho quando representa a principal entrada da página de consulta;
- fica na área de ações do formulário quando conclui uma edição;
- não deve ser duplicada sem necessidade no mesmo viewport;
- deve manter alvo de toque, foco visível e contraste adequados.

No mobile, ela pode ocupar toda a largura disponível quando isso tornar a ação mais fácil de identificar e tocar.

Ações secundárias usam `.bfa-btn-secondary` e não devem competir visualmente com a ação primária.

Hierarquia de botões: primary (amarelo sólido, mantido) > secondary (border neutra, cor texto secundária) > tertiary/ghost (sem border, cor texto). A hierarquia deve ser clara e consistente.

## 7. Listagens responsivas

Uma mesma fonte de dados pode ter apresentações distintas para desktop e mobile, desde que sem duplicar regras de negócio.

### 7.1 Desktop

Para conjuntos tabulares, use:

- `.bfa-admin-table-container` como superfície e controle de overflow;
- `.bfa-admin-table` para a tabela semântica;
- `.bfa-admin-desktop-list` para sua visibilidade responsiva.

Regras:

- use cabeçalhos de coluna claros e semanticamente corretos;
- alinhe números, estados e ações de forma previsível;
- mantenha a coluna de ações compacta;
- não reduza a fonte ou comprima a tabela a ponto de prejudicar leitura;
- não dependa apenas de scroll horizontal quando uma representação em cartões for mais adequada no mobile;
- row hover: `background: var(--bfa-surface-elevated)` (sem amarelo);
- header: `color: var(--bfa-text-secondary)`, `font-weight: 600`.

### 7.2 Mobile

No mobile, use `.bfa-admin-mobile-list` com cartões `.bfa-admin-card`. A listagem de unidades e `.bfa-unidade-card` demonstram o padrão atual.

Cada cartão deve:

- apresentar primeiro a identidade do registro;
- agrupar rótulos e valores relacionados;
- mostrar estado de forma legível;
- reservar uma região consistente para ações;
- evitar altura artificial e espaços vazios excessivos;
- preservar a mesma informação e as mesmas permissões da tabela desktop.

Não renderize funcionalidades diferentes conforme o viewport. Desktop e mobile são apresentações da mesma capacidade.

## 8. Ações por item

Use `.bfa-admin-actions` para agrupar ações e `.bfa-admin-icon-action` para ações compactas por ícone. A partial `_UnidadeAcoes.cshtml` é a referência atual para reutilização entre tabela e cartões.

Regras obrigatórias:

- cada ação por ícone deve ter nome acessível por `aria-label`;
- use `title` como ajuda complementar, não como único nome;
- o ícone deve ser decorativo quando o nome acessível já estiver no controle;
- preserve foco visível por teclado;
- mantenha área de acionamento confortável para toque;
- diferencie editar, ativar e desativar por semântica, não apenas por cor;
- ações que alteram estado devem usar `POST` e proteção antiforgery;
- não use links `GET` para ativar, desativar, excluir ou executar outra mudança de estado;
- ações indisponíveis por autorização não devem ser oferecidas, mas a autorização continua obrigatória no servidor;
- confirme ações destrutivas ou de impacto relevante conforme a necessidade do fluxo.

Evite menus de reticências quando há poucas ações frequentes e claramente reconhecíveis. Evite também uma sequência longa de ícones sem rótulos ou hierarquia.

Mapeamento visual preferencial para ações recorrentes:

| Ação | Ícone esperado |
|---|---|
| Editar | lápis |
| Ativar | check, power ou toggle coerente |
| Desativar | símbolo correspondente de desativação |
| Visualizar | olho, quando necessário |

## 9. Estados e badges

Use `.bfa-admin-badge` e os modificadores existentes, como `.is-active` e `.is-inactive`, para estados curtos. Estados futuros, como “Pendente” e “Bloqueada”, devem estender o mesmo componente em vez de criar um badge diferente por módulo.

Badges devem:

- conter texto explícito, como "Ativa" ou "Inativa";
- usar cores com contraste adequado;
- manter tamanho compacto (`font-size: 0.72rem`, `font-weight: 700`);
- representar estado, não funcionar como botão sem affordance de botão;
- preservar a mesma terminologia em todas as telas.

Não introduza uma nova cor de estado sem necessidade semântica e sem verificar sua convivência com a paleta oficial.

## 10. Cards, métricas e avisos

Use `.bfa-admin-card` como superfície base e `.bfa-admin-card-grid` para coleções responsivas. Métricas usam `.bfa-admin-metric-card`; avisos operacionais usam `.bfa-admin-notice`.

Cards não possuem `border-top` amarelo. Background é `var(--bfa-surface)` (sólido, não gradiente). Border é `1px solid var(--bfa-border)` (neutra). Shadow reduzido: `0 0.25rem 0.75rem rgba(0, 0, 0, 0.15)`.

Regras:

- cards agrupam conteúdo relacionado, não cada linha ou ação isolada;
- cards ou painéis exibidos em sequência vertical devem manter `gap` consistente e nunca ficar visualmente colados;
- em formulários, agrupe cards ou etapas consecutivas com `.bfa-admin-form-sections`, que centraliza o espaçamento desktop/mobile;
- não crie margens diferentes em cada página para separar painéis sequenciais;
- mantenha bordas, raios, sombras e fundos já definidos;
- evite aparência de “cards grandes” nos itens de navegação;
- mantenha rótulo, valor e contexto com hierarquia clara;
- não torne um card inteiro clicável quando isso prejudicar a semântica ou esconder outras ações;
- use `section`, `article`, títulos e rótulos coerentes com o conteúdo.

Métricas devem apresentar um valor verificável e um rótulo inequívoco. Elas não devem substituir relatórios ou gráficos quando a relação temporal for essencial.

## 11. Formulários

Formulários administrativos usam:

- `.bfa-admin-card` e `.bfa-admin-form-card` para a superfície;
- `.bfa-admin-form` para organização dos campos;
- `.bfa-admin-form-help` para ajuda contextual;
- `.bfa-admin-form-actions` para concluir ou cancelar.

Regras obrigatórias:

- use `label` associado ao respectivo controle;
- informe campos obrigatórios de forma textual e consistente;
- apresente erros de validação junto aos campos e um resumo quando necessário;
- preserve valores informados quando a validação falhar;
- texto de ajuda deve explicar formato ou consequência, sem substituir o label;
- use tipos e atributos HTML apropriados, sem tratar validação do navegador como autoridade;
- validação no servidor permanece obrigatória;
- formulários de mudança de estado usam `POST` e antiforgery;
- ações seguem o padrão POST/Redirect/GET quando aplicável;
- “Cancelar” é ação secundária e deve retornar ao contexto previsível;
- o layout deve passar para uma coluna antes que os controles fiquem comprimidos.

Views recebem ViewModels e contêm somente apresentação. Não consulte banco, não aplique regra de negócio e não use entidades de domínio como modelos de formulário.

Cards ou etapas consecutivas de um formulário devem ser agrupados pelo contêiner reutilizável `.bfa-admin-form-sections`, com espaçamento vertical consistente e um `gap` discretamente menor no mobile. Máscaras de CPF, CNPJ, telefone e CEP são melhoria progressiva de digitação, implementada pelo script compartilhado e atributos `data-bfa-mask`; elas nunca substituem validação e normalização no servidor, que também deve aceitar submissões manuais sem JavaScript. A máscara de CNPJ aceita letras ASCII ou números nas 12 primeiras posições, converte letras para maiúsculas e restringe as 2 posições finais a números.

### 11.1 Área de ações do formulário

Use `.bfa-admin-form-actions` para separar e organizar as ações que concluem ou cancelam um formulário. A classe compartilhada já fornece o espaçamento vertical antes das ações, o `gap` entre botões e o comportamento responsivo; não complemente esse padrão com margens improvisadas específicas por página.

Regras obrigatórias:

- os botões nunca ficam colados ao último campo, card ou etapa do formulário;
- sempre existe espaçamento vertical claro antes da área de ações;
- ações lado a lado mantêm o `gap` compartilhado e consistente;
- a ação primária aparece primeiro e a secundária depois;
- em telas menores, os botões ocupam a largura disponível sem overflow e podem ser empilhados quando a largura não comportar duas ações confortavelmente;
- preserve uma área de toque confortável e foco visível em todos os botões;
- não use `style` inline nem seletores exclusivos de página para reproduzir esse espaçamento.

### 11.2 Combobox pesquisável

Seleções extensas podem usar o componente compartilhado `data-bfa-combobox`, preservando um `select` nativo como valor real do formulário e oferecendo pesquisa local progressiva por texto. A pesquisa deve ser tolerante a maiúsculas, minúsculas e acentos, sem modificar o texto oficial das opções. O componente deve manter operação por clique, setas, `Enter`, `Escape`, clique externo, foco visível e semântica ARIA de combobox/listbox. Relações em cascata, como Estado e Município, carregam o conjunto dependente uma vez após a seleção do campo principal e filtram no cliente; nunca fazem uma requisição por tecla. Texto digitado sem seleção real não constitui um valor válido para submissão.

## 12. Estado vazio

Quando não houver dados, use `.bfa-admin-empty-state`.

O estado vazio deve conter:

- indicação visual discreta e decorativa quando útil;
- título direto;
- explicação curta sobre o motivo ou o próximo passo;
- uma ação primária somente quando o usuário estiver autorizado e ela for útil naquele contexto.

Evite tabelas vazias sem explicação, mensagens técnicas ou ilustrações que desviem da identidade BFA. Use `role="status"` ou uma associação acessível por título conforme o conteúdo e a frequência de atualização.

## 13. Responsividade e validação por viewport

Toda alteração administrativa deve ser verificada, no mínimo, nos seguintes tamanhos:

| Referência | Largura sugerida | O que validar |
|---|---:|---|
| Mobile compacto | `360px` | ausência de corte e overflow; drawer limitado a `85vw` |
| Mobile comum | `390px` | leitura, ações, formulários e cartões |
| Tablet | `768px` | transição da navegação e reorganização do cabeçalho |
| Notebook | `1024px` a `1280px` | sidebar, tabela, hierarquia e uso da largura |
| Desktop largo | `1440px` | limites de conteúdo e ausência de espaços desproporcionais |

Também valide:

- orientação vertical e horizontal quando relevante;
- zoom do navegador em `200%`;
- navegação somente por teclado;
- textos maiores sem sobreposição;
- drawer com conteúdo suficiente para exigir scroll;
- cabeçalhos, tabelas, cards, badges, mensagens e formulários em seus estados reais;
- ausência de scroll horizontal na página, exceto em um contêiner tabular deliberado.

Breakpoints devem seguir os já utilizados pelo projeto. Não crie um breakpoint novo para corrigir apenas um componente sem antes avaliar o comportamento do shell inteiro.

## 14. Navegação progressiva

A navegação administrativa cresce de forma progressiva:

- exponha somente rotas e funcionalidades já implementadas;
- preserve a ordem dos itens entre desktop e mobile;
- reutilize uma partial ou uma única fonte de markup para evitar divergência;
- marque o item atual com `aria-current="page"`;
- não adicione links inativos, placeholders, “em breve” ou destinos futuros apenas para preencher o menu;
- aplique visibilidade coerente com a autorização, sem tratar a ocultação como controle de acesso;
- mantenha “Sair” visual e semanticamente separado das rotas funcionais.

Quando uma nova função administrativa for implementada, sua entrada de navegação deve ser adicionada ao componente compartilhado, não a páginas individuais.

## 15. Reutilização de componentes

Antes de criar markup ou CSS, procure uma composição existente. As principais famílias atuais são:

| Necessidade | Padrão existente |
|---|---|
| Shell e conteúdo | `.bfa-admin-shell`, `.bfa-admin-main` |
| Cabeçalho de página | `.bfa-admin-page-header` e elementos `__*` |
| Navegação | `.bfa-admin-nav`, `.bfa-admin-nav-link` |
| Drawer | `.bfa-admin-drawer` e elementos `__*` |
| Superfície | `.bfa-admin-card` |
| Grade de cards | `.bfa-admin-card-grid` |
| Métrica | `.bfa-admin-metric-card` |
| Tabela | `.bfa-admin-table-container`, `.bfa-admin-table` |
| Lista responsiva | `.bfa-admin-desktop-list`, `.bfa-admin-mobile-list` |
| Estado | `.bfa-admin-badge` |
| Ações de registro | `.bfa-admin-actions`, `.bfa-admin-icon-action` |
| Formulário | `.bfa-admin-form`, `.bfa-admin-form-card`, `.bfa-admin-form-actions` |
| Estado vazio | `.bfa-admin-empty-state` |
| Aviso | `.bfa-admin-notice` |
| Botões | `.bfa-btn-primary`, `.bfa-btn-secondary`, `.bfa-admin-button` |
| **Toolbar de listagem** | `.bfa-list-toolbar` |
| **Busca** | `.bfa-list-search`, `.bfa-list-search__icon` |
| **Filtros por aba** | `.bfa-filter-tabs`, `.bfa-filter-tab`, `.bfa-filter-count` |
| **Card de dados (tabela)** | `.bfa-data-card`, `.bfa-table-wrap` |
| **Texto de célula** | `.bfa-data-primary`, `.bfa-data-secondary`, `.bfa-data-stack` |
| **Badge de estado** | `.bfa-data-badge`, `.bfa-data-badge--active`, `.bfa-data-badge--inactive`, `.bfa-data-badge__dot` |
| **Menu de ações (kebab)** | `.bfa-kebab`, `.bfa-actions-cell` |
| **Rodapé de tabela** | `.bfa-table-footer` |
| **Paginação** | `.bfa-pagination`, `.bfa-page-btn`, `.bfa-page-btn.is-current`, `.bfa-page-btn.is-disabled` |
| **Cards mobile** | `.bfa-mobile-card-list`, `.bfa-mobile-card`, `.bfa-mobile-card__head`, `.bfa-mobile-card__grid`, `.bfa-mobile-card__label`, `.bfa-mobile-card__actions` |
| **Grid de formulário** | `.bfa-form-grid` (2 colunas: 1.12fr / 0.88fr) |
| **Card de formulário** | `.bfa-form-card`, `.bfa-card-title-row`, `.bfa-card-icon` |
| **Campos** | `.bfa-fields`, `.bfa-field`, `.bfa-field-grid-2` |
| **Input monetário** | `.bfa-money-input`, `.bfa-money-input__prefix` |

Se uma composição de markup se repetir ou contiver ações sensíveis, extraia uma Partial View apropriada. Use View Component quando a composição reutilizável exigir trabalho ou dados no servidor. Não crie abstrações genéricas antecipadamente para uma única ocorrência.

## 16. Organização do CSS

A responsabilidade dos estilos é:

- `bfa-theme.css`: tokens, identidade e elementos globais da marca;
- `admin.css`: shell e componentes reutilizáveis entre áreas administrativas;
- CSS da área, como `franqueadora.css`: ajustes realmente específicos daquele contexto.

Regras:

- use classes, não estilos inline;
- prefira as variáveis e classes existentes;
- não duplique um componente genérico no CSS da área;
- não use seletor baseado em estrutura excessivamente frágil;
- mantenha nomes no padrão `bfa-admin-*` para componentes administrativos compartilhados;
- modificadores devem expressar estado ou variação clara, como `.is-active`;
- não introduza outro framework de UI sem decisão arquitetural explícita;
- Bootstrap pode sustentar comportamento técnico já adotado, mas a aparência deve seguir as classes BFA;
- não coloque estilos administrativos em `site.css` sem justificativa explícita;
- não altere CSS público global para resolver apenas uma tela administrativa;
- remova regras obsoletas apenas quando a tarefa autorizar e houver verificação de todas as telas consumidoras.

Uma exceção visual deve ser pequena, motivada pelo domínio e documentada. Não crie um segundo sistema visual dentro de uma área.

### 16.1 Padrão de listagem com toolbar

Toda listagem administrativa deve seguir o padrão de toolbar com busca e filtros:

```html
<section class="bfa-list-toolbar" aria-label="Busca e filtros">
  <form class="bfa-list-search" method="get">
    <svg class="bfa-list-search__icon">...</svg>
    <input type="search" name="termo" placeholder="Buscar..." />
  </form>
  <nav class="bfa-filter-tabs" aria-label="Filtrar por...">
    <a class="bfa-filter-tab is-active" href="?filtro=ativos">Ativos <span class="bfa-filter-count">10</span></a>
    <a class="bfa-filter-tab" href="?filtro=encerrados">Encerrados <span class="bfa-filter-count">2</span></a>
  </nav>
</section>
```

Regras:

- a toolbar usa grid com `grid-template-columns: minmax(280px, 1fr) auto`;
- busca usa `type="search"` com ícone SVG decorativo;
- filtros usam `<a>` (não `<button>`) para navegação por URL;
- o filtro ativo recebe `.is-active`;
- contadores de filtro usam `.bfa-filter-count`;
- no mobile, toolbar vira coluna e filtros ocupam toda a largura.

### 16.2 Padrão de tabela com dados agrupados

A tabela de dados usa `.bfa-data-card` como contêiner:

```html
<div class="bfa-data-card bfa-admin-desktop-list">
  <div class="bfa-table-wrap">
    <table>
      <thead><tr><th>Nome</th><th>Contato</th><th>Ações</th></tr></thead>
      <tbody>
        <tr>
          <td>
            <div class="bfa-data-primary">Nome Principal</div>
            <div class="bfa-data-secondary">Informação secundária</div>
          </td>
          <td>
            <div class="bfa-data-stack">
              <div>Valor principal</div>
              <div class="bfa-data-secondary">Valor secundário</div>
            </div>
          </td>
          <td><div class="bfa-actions-cell">...</div></td>
        </tr>
      </tbody>
    </table>
  </div>
  <div class="bfa-table-footer">
    <div>Mostrando <strong>1–10</strong> de <strong>137</strong> registros</div>
    <nav class="bfa-pagination">...</nav>
  </div>
</div>
```

Regras:

- coluna de identidade usa `.bfa-data-primary` + `.bfa-data-secondary`;
- colunas com múltiplos valores usam `.bfa-data-stack`;
- badges de estado usam `.bfa-data-badge` com modificador `--active` ou `--inactive`;
- rodapé de tabela mostra contagem e paginação;
- ações usam `.bfa-actions-cell` para centralizar.

### 16.3 Paginação

A paginação usa `_Paginacao.cshtml` com `PaginacaoViewModel`:

```csharp
var paginacao = new PaginacaoViewModel
{
    PaginaAtual = paginaAtual,
    TotalPaginas = totalPaginas,
    TotalItens = totalItens,
    PrimeiroIndice = primeiroIndice,
    UltimoIndice = ultimoIndice,
    BaseQueryString = "filtro=ativos&termo=joao"
};
```

Regras:

- `_Paginacao.cshtml` é reutilizável e não contém conhecimento específico de domínio;
- `BaseQueryString` preserva parâmetros de busca e filtro na navegação entre páginas;
- apenas 1 página: partial não renderiza nada;
- estados: `.is-current` para página atual, `.is-disabled` para前后 indisponível;
- acessibilidade: `aria-label` nas setas, `aria-current="page"` na página atual.

### 16.4 Padrão de cards mobile

No mobile (`max-width: 44rem`), a tabela é substituída por cards:

```html
<section class="bfa-mobile-card-list">
  <article class="bfa-mobile-card">
    <div class="bfa-mobile-card__head">
      <div>
        <div class="bfa-data-primary">Nome</div>
        <div class="bfa-data-secondary">Detalhe</div>
      </div>
      <span class="bfa-data-badge bfa-data-badge--active">Ativo</span>
    </div>
    <div class="bfa-mobile-card__grid">
      <div>
        <span class="bfa-mobile-card__label">Contato</span>
        <div>email@example.com</div>
      </div>
    </div>
    <div class="bfa-mobile-card__actions">...</div>
  </article>
</section>
```

Regras:

- `.bfa-mobile-card-list` tem `display: none` em desktop, `display: flex` no mobile;
- head mostra identidade + badge;
- grid usa 2 colunas em desktop mobile, 1 coluna em mobile compacto;
- ações ficam alinhadas à direita no rodapé do card.

### 16.5 Padrão de formulário em duas colunas

Formulários complexos usam `.bfa-form-grid` com dois cards lado a lado:

```html
<form class="bfa-admin-form">
  <div class="bfa-form-grid">
    <section class="bfa-form-card">
      <div class="bfa-card-title-row">
        <div class="bfa-card-icon">♟</div>
        <h2>Dados do registro</h2>
      </div>
      <div class="bfa-fields">
        <div class="bfa-field">
          <label>Nome *</label>
          <input />
        </div>
        <div class="bfa-field-grid-2">
          <div class="bfa-field"><label>CPF</label><input /></div>
          <div class="bfa-field"><label>Telefone</label><input /></div>
        </div>
      </div>
    </section>
    <section class="bfa-form-card">
      <div class="bfa-card-title-row">
        <div class="bfa-card-icon">$</div>
        <h2>Condições</h2>
      </div>
      <div class="bfa-fields">...</div>
    </section>
  </div>
  <div class="bfa-form-actions">
    <a class="bfa-btn-secondary">Cancelar</a>
    <button class="bfa-btn-primary">Salvar</button>
  </div>
</form>
```

Regras:

- grid: `grid-template-columns: minmax(0, 1.12fr) minmax(22rem, 0.88fr)`;
- no mobile (`max-width: 44rem`): coluna única;
- cada card tem `.bfa-card-title-row` com ícone + título;
- `.bfa-fields` organiza campos com gap consistente;
- `.bfa-field-grid-2` cria sub-grid de 2 colunas dentro de um card;
- `.bfa-money-input` para campos monetários com prefixo R$;
- ações ficam abaixo do grid com `justify-content: flex-end`;

## 17. Acessibilidade

O padrão mínimo inclui:

- HTML semântico e ordem lógica de títulos;
- link de salto para o conteúdo;
- nome acessível em controles somente por ícone;
- `aria-current` na navegação ativa;
- `aria-expanded` e associação correta no controle do drawer;
- foco visível com contraste adequado;
- operação completa por teclado;
- fechamento do drawer pelo botão, pela tecla `Escape` e pelo comportamento acessível do componente adotado;
- contraste suficiente para texto, bordas funcionais e estados;
- alvos de toque confortáveis;
- labels de formulário associados aos controles;
- mensagens de erro e estado compreensíveis por tecnologia assistiva;
- imagens decorativas com texto alternativo vazio e imagens informativas com alternativa adequada;
- nenhuma informação comunicada exclusivamente por cor, posição ou ícone.

Não adicione ARIA para substituir semântica HTML nativa. Prefira o elemento nativo correto.

## 18. Protocolo para Codex e agentes de código

Antes de alterar uma interface administrativa:

1. leia os quatro documentos obrigatórios indicados na seção 1;
2. inspecione o layout, as partials, as Views e o CSS que já implementam o padrão;
3. identifique os componentes existentes que atendem à tarefa;
4. verifique desktop e mobile antes de propor um novo componente;
5. faça a menor mudança possível;
6. não misture redesign, regra de negócio ou refatoração não solicitada;
7. se houver divergência necessária, explique e documente o motivo antes da implementação;
8. atualize testes afetados e execute as validações do repositório.

Uma solicitação de nova tela não autoriza criar novas funcionalidades, rotas, permissões, consultas ou estruturas de banco além do escopo explicitamente pedido.

## 19. Filtros e consultas por período

Listagens que dependem de intervalo de datas devem usar o padrão `.bfa-filter-panel`.

**Referência oficial:** Tela Aulas (`/unidade/{unidadeId}/aulas`) — `Views/Aulas/Index.cshtml`. Esta tela é o exemplo canônico de grid com filtros por período, paginação server-side, mobile cards e date picker. Novas telas com filtro de data devem seguir este modelo.

### Estrutura

```html
<section class="bfa-filter-panel" aria-labelledby="filtros-aulas">
  <div class="bfa-filter-panel__header">
    <div class="bfa-filter-panel__icon" aria-hidden="true"><!-- SVG calendar --></div>
    <div>
      <h2 id="filtros-aulas">Filtros</h2>
      <p>Selecione um período para visualizar os dados.</p>
    </div>
  </div>

  <div class="bfa-quick-filters" aria-label="Períodos rápidos">
    <a class="bfa-chip bfa-chip--active" href="?dataInicio=...&dataFim=...">Hoje</a>
    <a class="bfa-chip" href="?dataInicio=...&dataFim=...">Esta semana</a>
    <a class="bfa-chip" href="?dataInicio=...&dataFim=...">Este mês</a>
    <span class="bfa-chip" style="pointer-events:none;opacity:.5">Personalizado</span>
  </div>

  <div class="bfa-filter-divider"></div>

  <form class="bfa-period-filter" method="get">
    <div class="bfa-field">
      <label for="dataInicio">Data inicial</label>
      <div class="bfa-date-input">
        <input id="dataInicio" name="dataInicio" type="text"
               inputmode="numeric" placeholder="dd/mm/aaaa" maxlength="10"
               value="dd/MM/yyyy" />
        <button type="button" class="bfa-date-button"
                aria-label="Abrir calendário da data inicial"
                data-bfa-date-trigger><!-- SVG calendar --></button>
      </div>
    </div>

    <div class="bfa-period-separator" aria-hidden="true">até</div>

    <div class="bfa-field">
      <label for="dataFim">Data final</label>
      <div class="bfa-date-input">
        <input id="dataFim" name="dataFim" type="text"
               inputmode="numeric" placeholder="dd/mm/aaaa" maxlength="10"
               value="dd/MM/yyyy" />
        <button type="button" class="bfa-date-button"
                aria-label="Abrir calendário da data final"
                data-bfa-date-trigger><!-- SVG calendar --></button>
      </div>
    </div>

    <div class="bfa-filter-actions">
      <button class="bfa-btn-primary bfa-admin-button" type="submit">Filtrar</button>
      <a class="bfa-btn-secondary bfa-admin-button" href="?">Limpar</a>
    </div>
  </form>
</section>
```

### Regras

- **Formato pt-BR**: Datas visíveis ao usuário usam `dd/MM/yyyy`. A UI nunca mostra `yyyy-MM-dd`.
- **Formato técnico**: Querystring e model binding usam `dd/MM/yyyy` com parsing no controller via `DateOnly.TryParseExact` com cultura `pt-BR`.
- **Filtros rápidos**: Links (`<a>`) que navegam com query string. "Hoje", "Esta semana", "Este mês" são links. "Personalizado" é um `<span>` inativo quando nenhum filtro rápido corresponde.
- **Preservação**: Filtros são preservados entre páginas via `BaseQueryString` no `PaginacaoViewModel`.
- **Reset de página**: Ao alterar período (formulário ou filtro rápido), a paginação volta para página 1.
- **Validação server-side**: `DataFim >= DataInicio` é validado no controller. Mensagem: "A data final deve ser igual ou posterior à data inicial."
- **Layout desktop**: Grid com 4 colunas — campo data inicial, separador "até", campo data final, ações.
- **Layout tablet** (`max-width: 56rem`): Grid com 3 colunas, ações em linha separada.
- **Layout mobile** (`max-width: 42.5rem`): Campos empilhados, separador oculto, ações em 2 colunas.
- **Layout compacto** (`max-width: 24.375rem`): Filtros rápidos em grid 2x2, ações em coluna.
- **Acessibilidade**: Labels reais associados, `aria-label` na seção e nos botões de calendário, navegação por teclado.
- **Date picker**: Usar `bfa-date-field.js` com a estrutura `bfa-date-input` + `data-bfa-date-field`. Calendário dark, pt-BR, com navegação mês anterior/próximo, dia selecionado em amarelo BFA, "Hoje" e "Limpar" no rodapé.

### Componentes CSS

| Classe | Uso |
|---|---|
| `.bfa-filter-panel` | Container do painel de filtros |
| `.bfa-filter-panel__header` | Cabeçalho com ícone + título + descrição |
| `.bfa-filter-panel__icon` | Ícone do cabeçalho (accent subtle) |
| `.bfa-quick-filters` | Container dos filtros rápidos (chips) |
| `.bfa-chip` | Botão/link de filtro rápido |
| `.bfa-chip--active` / `.bfa-chip.is-active` | Filtro rápido ativo |
| `.bfa-filter-divider` | Divisor horizontal |
| `.bfa-period-filter` | Grid do formulário de período |
| `.bfa-field` | Campo individual (label + input) |
| `.bfa-date-input` | Wrapper do input de data + botão calendário |
| `.bfa-date-button` | Botão do calendário |
| `.bfa-period-separator` | Separador "até" |
| `.bfa-filter-actions` | Botões Filtrar/Limpar |
| `.bfa-list-card` | Card da listagem (cabeçalho + tabela + footer) |
| `.bfa-list-card__head` | Cabeçalho da listagem |
| `.bfa-list-footer` | Footer com contagem + paginação + itens por página |
| `.bfa-page-size` | Seletor de itens por página |

### Calendários

Campos de data em formulários e filtros administrativos devem reutilizar o componente `bfa-date-field.js` com a estrutura `bfa-date-input`. O calendário é dark, pt-BR, com navegação por teclado, "Hoje" e "Limpar" no rodapé. Não criar calendários customizados para cada tela.

### Reutilização

Este padrão deve ser reutilizado em: Financeiro, Relatórios, Frequência, Presenças e qualquer tela com intervalo de datas.

## 20. Checklist obrigatório de revisão

Antes de considerar uma interface administrativa concluída, confirme:

- [ ] Usa o Admin Shell existente.
- [ ] Sidebar desktop consistente.
- [ ] Drawer/hambúrguer mobile consistente.
- [ ] Page Header padrão.
- [ ] Ação principal segue padrão BFA.
- [ ] Desktop validado.
- [ ] Tablet validado.
- [ ] Mobile 390px validado.
- [ ] Mobile 360px validado.
- [ ] Sem overflow horizontal.
- [ ] Listagem mobile usa cards quando necessário.
- [ ] Ícones possuem `aria-label`.
- [ ] Forms usam padrão BFA.
- [ ] Status usam badges padrão.
- [ ] Empty state implementado.
- [ ] Não duplica CSS/componente existente.
- [ ] Build sem warnings.
- [ ] Testes passando.

Verificações complementares:

- [ ] Li `AGENTS.md`, `docs/ARCHITECTURE.md`, `brand/guide/brand-guide.md` e `docs/UI-ADMIN-STANDARDS.md`.
- [ ] Reutilizei componentes, partials e tokens antes de criar novos padrões.
- [ ] Ações por ícone têm `title`, foco visível e alvo de toque adequado.
- [ ] Mudanças de estado usam `POST`, antiforgery e autorização no servidor.
- [ ] A navegação contém somente funcionalidades implementadas e mantém paridade entre desktop e mobile.
- [ ] “Sair” permanece separado da navegação funcional.
- [ ] O drawer usa no máximo `320px`/`85vw`, `100dvh`, overlay, scroll interno e itens compactos sem distribuição vertical artificial.
- [ ] A interface funciona com zoom de `200%` e navegação somente por teclado.
- [ ] Nenhuma informação depende somente de cor, ícone ou posição.
- [ ] Views continuam sem regra de negócio, acesso a banco ou decisão de autorização.
- [ ] Nenhuma rota, funcionalidade, migration ou dependência foi criada sem autorização explícita.

Esse checklist é parte do Definition of Done de qualquer trabalho futuro em interface administrativa.
