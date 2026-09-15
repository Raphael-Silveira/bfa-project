# Design — Filtrar Unidades da Franqueadora

## Decision

Estender a consulta existente de Unidades da Franqueadora com um contrato de filtros. O serviço de aplicação mantém a resolução da Organização autorizada e passa o filtro ao repositório. O repositório aplica os predicados de nome e vínculo ativo antes da ordenação determinística por `Nome` e da materialização.

## Components

- Application: enum/record do filtro e assinaturas da consulta.
- Infrastructure: predicados tenant-scoped sobre `Unidades.Nome` e `FranqueadosUnidades.Ativo`.
- Controller: bind da querystring, normalização do tipo e passagem do filtro, sem alterar autorização.
- ViewModel: estado de `busca` e `tipo` submetido e indicador de empty state filtrado.
- View: formulário GET compacto, valores selecionados, empty state filtrado e markup existente preservado.
- Tests: cenários HTTP de filtros, tenant e ação de contrato.

## Query flow

```text
GET querystring -> Controller normaliza -> Application resolve Organização
-> Repository aplica tenant + nome + vínculo ativo
-> OrderBy(Nome) -> materializa -> ViewModel
```

Não há paginação no endpoint atual, portanto nenhuma será introduzida. `Limpar` será um link simples para a raiz da rota e não reterá parâmetros antigos.

## Security and compatibility

A policy `AdministradorRede` e a resolução de Organização permanecem inalteradas. Toda consulta continua limitada por `organizacaoId`. `FranqueadoIdAtivo` continuará sendo projetado para cada resultado, mantendo a condição já aprovada da partial `_UnidadeAcoes`.

## UI

Reutilizar as classes compactas `bfa-admin-filters` já usadas nas listas da Franqueadora. O formulário GET terá os textos PT-BR `Buscar unidade`, `Tipo`, `Todos`, `Franqueadas`, `Rede`, `Filtrar` e `Limpar`. Adicionar apenas ajustes locais de layout se as classes existentes não fornecerem o campo de busca maior e o empilhamento mobile.
