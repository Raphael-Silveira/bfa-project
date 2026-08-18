# Padrão visual administrativo BFA

O design system administrativo usa `wwwroot/css/admin.css` como base reutilizável. Cada Área pode adicionar um CSS próprio apenas para detalhes do seu domínio, como `franqueadora.css`.

## Shell

O shell é composto por:

- `.bfa-admin-header`: header compacto com marca e ações globais;
- `.bfa-admin-shell`: grade que organiza navegação e conteúdo;
- `.bfa-admin-sidebar`: navegação lateral estável no desktop;
- `.bfa-admin-main`: área principal da página;
- `.bfa-admin-drawer`: menu mobile baseado no offcanvas do Bootstrap;
- `.bfa-admin-footer`: rodapé administrativo discreto.

No desktop e notebook, a navegação permanece na lateral. Em tablet e mobile, a sidebar é ocultada e o botão hambúrguer abre o drawer sobre o conteúdo, com overlay escuro. O painel usa no máximo `320px` ou `85vw`, ocupa `100dvh` e mantém scroll interno independente. O drawer permite fechamento pelo botão próprio, tecla `Esc` e clique no backdrop, mantendo o gerenciamento de foco do Bootstrap.

Os links mobile têm aproximadamente `54px` de altura, com ícone e texto e espaçamento vertical de `10px`. O estado ativo usa somente fundo sutil e acento amarelo na lateral; os links não devem assumir aparência de cards. A ação `Sair` aparece logo após a navegação, separada por uma linha e cerca de `22px`, sem ser empurrada para o rodapé.

## Componentes reutilizáveis

- Cabeçalho de página: `.bfa-admin-page-header`, `__eyebrow`, `__title`, `__description` e `__actions`.
- Cards e grids: `.bfa-admin-card`, `.bfa-admin-card-grid` e `.bfa-admin-metric-card`.
- Listagens: `.bfa-admin-table-container`, `.bfa-admin-table`, `.bfa-admin-desktop-list` e `.bfa-admin-mobile-list`.
- Status: `.bfa-admin-badge` com modificadores como `.is-active` e `.is-inactive`.
- Ações: `.bfa-admin-actions`, `.bfa-admin-icon-action` e `.bfa-admin-button`.
- Formulários: `.bfa-admin-form`, `.bfa-admin-form-card`, `.bfa-admin-form-help` e `.bfa-admin-form-actions`.
- Feedback: `.bfa-admin-empty-state` e `.bfa-admin-notice`.

Ícones administrativos são SVG inline, decorativos quando acompanhados de texto e identificados por `aria-label` e `title` quando constituem a ação inteira. Todo controle deve manter foco visível e área de toque adequada.

## Novas páginas

Novas páginas administrativas devem usar o shell da sua Área, iniciar com o cabeçalho padrão e compor conteúdo com os componentes acima. Regras de apresentação específicas ficam no CSS da Área; regras de negócio, autorização e acesso a dados continuam fora de layouts e Views.

Listagens devem manter tabela em telas largas e fornecer uma lista vertical em cards no mobile quando a tabela exigir rolagem horizontal para a operação principal.
