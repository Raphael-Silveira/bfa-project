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

Utilize os tokens existentes em `bfa-theme.css` e `admin.css`. Não espalhe valores hexadecimais duplicados pelas Views ou por novos arquivos CSS quando já houver uma variável apropriada.

O amarelo deve identificar principalmente:

- ação primária;
- item ativo de navegação;
- foco e estados interativos relevantes;
- pequenos acentos de hierarquia;
- informações de destaque coerentes com a semântica da tela.

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

### 4.1 Desktop

Em desktop:

- o cabeçalho permanece consistente entre todas as páginas da área;
- a barra lateral permanece fixa/lateral no shell e apresenta somente destinos implementados e autorizados;
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
- não dependa apenas de scroll horizontal quando uma representação em cartões for mais adequada no mobile.

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

- conter texto explícito, como “Ativa” ou “Inativa”;
- usar cores com contraste adequado;
- manter tamanho compacto;
- representar estado, não funcionar como botão sem affordance de botão;
- preservar a mesma terminologia em todas as telas.

Não introduza uma nova cor de estado sem necessidade semântica e sem verificar sua convivência com a paleta oficial.

## 10. Cards, métricas e avisos

Use `.bfa-admin-card` como superfície base e `.bfa-admin-card-grid` para coleções responsivas. Métricas usam `.bfa-admin-metric-card`; avisos operacionais usam `.bfa-admin-notice`.

Regras:

- cards agrupam conteúdo relacionado, não cada linha ou ação isolada;
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

## 19. Checklist obrigatório de revisão

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
