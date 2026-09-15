# Design

## Referência

`backend/src/BFA.Web/Areas/Unidade/Views/Inicio/Index.cshtml`, usando os seletores existentes de `admin.css`.

## Composição

1. Cabeçalho atual da Franqueadora, sem alteração de mensagem ou fluxo.
2. Seção `Métricas operacionais` com cinco cards compactos e ícones semânticos.
3. Seção `Métricas financeiras` com três cards monetários, destaque para receita e atraso.
4. Seção `Unidades da Rede` com `bfa-admin-desktop-list` e `bfa-mobile-card-list` já existentes.
5. Estado vazio atual quando não houver unidades.

## Decisões

- Não adicionar dados à consulta: os dados atuais atendem ao escopo.
- Centralizar a cultura monetária no mapper do ViewModel para evitar dependência da cultura do processo.
- Não criar CSS de componente novo; reutilizar Admin Shell e classes de listagem responsiva existentes.
- Preservar a rota e o texto do link de cada unidade.
