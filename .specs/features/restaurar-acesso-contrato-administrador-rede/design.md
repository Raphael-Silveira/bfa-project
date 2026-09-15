# Design — Restaurar acesso a Contrato para AdministradorRede

## Contexto confirmado

O dashboard já recebe `PainelUnidadeViewModel.Contrato`. A consulta retorna `null` de forma controlada quando não existe vínculo/contrato ativo. A rota de detalhe é independente de `PodeGerenciarTurmas`, `PodeGerenciarMatriculas` ou outras capacidades operacionais. A gestão completa continua contextual na Área Franqueadora.

## Abordagens consideradas

### A — Restaurar o painel somente leitura no dashboard da Unidade (recomendada)

Reintroduzir uma seção contratual em `Inicio/Index.cshtml`, usando o ViewModel existente e as classes do design system atual. Preserva arquitetura, resolve a regressão identificada e não cria nova autorização.

### B — Adicionar `Contratos` ao menu principal da Franqueadora

Não recomendada. O módulo não possui rota/listagem global; toda gestão exige `franqueadoId` e `unidadeId`. Um link novo exigiria ampliar escopo e desenhar um novo caso de uso.

### C — Manter apenas a sidebar da Unidade e adicionar testes

Não recomendada. A sidebar prova que a rota ainda existe, mas não restaura a superfície removida no commit causador nem atende à descoberta contratual no dashboard.

## Decisão técnica

Adotar A. A View renderiza exclusivamente dados já autorizados e oferece somente navegação GET. Nenhuma decisão será adicionada a `.specs/STATE.md`, pois esta é uma correção local e reversível, não uma decisão arquitetural transversal.

## Fluxo

```text
AdministradorRede/AdminUnidade
        |
        v
GET /unidade/{unidadeId}
        |
        +--> autorização de Unidade
        |
        +--> IContratoUnidadeConsulta (tenant-safe)
        |
        v
PainelUnidadeViewModel.Contrato
        |
        +--> contrato ativo: resumo + "Ver contrato"
        |
        +--> ausente: estado vazio + acesso à consulta
```

## Alterações previstas

1. Teste de regressão em `AreaUnidadeEndpointTests.cs` cobrindo o caso que faltava: AdministradorRede, Unidade franqueada, painel visível, link presente e GET direto somente leitura.
2. Teste de ausência controlada para AdministradorRede sem vínculo comercial ativo.
3. Painel contratual restaurado em `Areas/Unidade/Views/Inicio/Index.cshtml`, sem ações mutáveis.
4. Nenhuma alteração em Domain, Application, Infrastructure, controllers, governança, policies, menus, migrations ou documentação de produto.

## Segurança

- O dashboard só é renderizado após autorização resource-based da Unidade.
- A consulta refaz a autorização pelo `usuarioId`, `OrganizacaoId` e `UnidadeId`.
- Documentos permanecem servidos pelo controller, sem URL física ou chave de armazenamento.
- O painel não contém POST, upload nem links para mutações da Área Franqueadora.

## Responsividade e acessibilidade

- Reutilizar `bfa-admin-card`, cabeçalho semântico, `dl` e botão/link compartilhado.
- Preservar um único `h1`; o painel usa `h2` e `aria-labelledby`.
- Validar 1440, 1024, 768, 390 e 360 px; desktop e drawer mobile devem manter o link de Contrato.

## Compatibilidade

Mudança aditiva de Razor, sem alteração de URL, contrato de API, dados persistidos ou schema. Rollback é a remoção isolada do painel e dos novos testes.
