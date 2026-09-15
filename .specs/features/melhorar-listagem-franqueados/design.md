# Design

## Componentes

- `IFranqueadosConsulta` recebe busca, página e tamanho e retorna `PaginaResultado<FranqueadoResumo>`.
- `FranqueadosServico` mantém a resolução de contexto e repassa os parâmetros.
- `FranqueadosRepositorio` aplica filtro case-insensitive, ordenação por nome/id, count e paginação.
- `FranqueadosIndexViewModel` carrega termo e metadados da página.
- `FranqueadosController` mantém a policy e monta a URL de paginação.
- `Franqueados/Index.cshtml` usa header com ação, toolbar de busca, estados vazios, tabela desktop, cards mobile e `_Paginacao`.

## Decisões

- O botão “Novo Franqueado” aponta para `/franqueadora/usuarios/novo`; o fluxo continua exigindo a seleção manual de “Franqueado”.
- O filtro não usa JavaScript nem lista completa no browser.
- Não se adiciona filtro por status, tipo ou Unidade nesta etapa.
- A tabela reduz a leitura visual agrupando nome fantasia como informação secundária, documento/tipo no mesmo bloco e mantendo unidades/status/ações.
- Nenhuma regra de negócio ou autorização será movida para View ou Controller.
