# Diagnóstico — Restaurar acesso a Contrato para AdministradorRede

## PROBLEMA

O dashboard da Área da Unidade deixou de apresentar o painel contratual e o atalho `Ver contrato`. Para um `AdministradorRede` que entra no contexto de uma Unidade, isso reduz a descoberta de uma função relevante do relacionamento Franqueadora/Franquia.

## COMPORTAMENTO ESPERADO

| Cenário | Menu da Unidade | Dashboard | GET `/unidade/{unidadeId}/contrato` | Operações contratuais |
|---|---|---|---|---|
| A. AdministradorRede, Unidade sem `FranqueadoUnidade` ativo | Visível | Estado vazio controlado e atalho de consulta | 200 com estado vazio | Gerenciamento somente pela Área Franqueadora quando existir contexto comercial válido |
| B. AdministradorRede, Unidade com `FranqueadoUnidade` ativo | Visível | Resumo do contrato ativo e atalho | 200, somente leitura | Criar/editar/ativar/formalizar/substituir/cancelar/encerrar pela Área Franqueadora |
| C. AdministradorUnidade autorizado | Visível | Resumo ou estado vazio e atalho | 200, somente leitura | Nenhuma mutação contratual |
| D. Professor | Ausente em sua Área | Ausente | 403 lógico, traduzido pelo cookie para `/acesso-negado` | Nenhuma |
| E. Usuário de outra Organização | Não recebe a página autorizada | Não recebe dados | 403 lógico, traduzido pelo cookie para `/acesso-negado` | Na rota Franqueadora contextual, cadeia inexistente no tenant retorna 404 |

`Franqueado` permanece entidade comercial e nunca é usado como perfil de acesso.

## COMPORTAMENTO ATUAL

- `_UnidadeNavLinks.cshtml` ainda renderiza `Contrato` sem condicional de governança; a mesma partial alimenta sidebar desktop e drawer mobile.
- `ContratoController` mantém a rota GET somente leitura e exige acesso administrativo à Unidade. O handler concede superacesso a `AdministradorRede` apenas dentro da própria Organização.
- `ContratoUnidadeConsulta` refaz a autorização no acesso a dados e restringe por `OrganizacaoId`, `UnidadeId`, vínculo comercial ativo, contrato ativo e versão vigente.
- O dashboard ainda chama `IContratoUnidadeConsulta.ObterAtivoAsync` e preenche `PainelUnidadeViewModel.Contrato`, mas `Views/Inicio/Index.cshtml` não usa mais a propriedade.
- A Área Franqueadora não possui uma listagem global de contratos. O gerenciamento é contextual em `/franqueadora/franqueados/{franqueadoId}/unidades/{unidadeId}/contrato`, alcançado pelo detalhe do Franqueado.

## CAUSA RAIZ

O commit `aff3a0e7fc799c6d38336adffcb1ca9ec43455a7` (`feat(ui): refresh visual admin - shell, sidebar, header, dashboard unidade`) removeu integralmente o painel `bfa-unidade-contract-card` de `Areas/Unidade/Views/Inicio/Index.cshtml`. A própria mensagem do commit registra `Remove painel contrato do dashboard`.

A remoção foi apenas de Razor/CSS visual: controller, ViewModel, consulta, rota, autorização e item de navegação permaneceram. O commit posterior `9ff31d3` transformou Relatórios em grupo expansível, mas preservou o link `Contrato` depois do grupo.

## ARQUIVOS ENVOLVIDOS

- `backend/src/BFA.Web/Areas/Unidade/Views/Inicio/Index.cshtml` — ponto da regressão.
- `backend/tests/BFA.IntegrationTests/AreaUnidadeEndpointTests.cs` — cobertura incompleta do dashboard/rota para `AdministradorRede` com Unidade franqueada.
- `backend/src/BFA.Web/Areas/Unidade/Views/Shared/_UnidadeNavLinks.cshtml` — correto; não requer alteração.
- `backend/src/BFA.Web/Areas/Unidade/Controllers/InicioController.cs` — correto; já entrega o contrato.
- `backend/src/BFA.Web/Areas/Unidade/Controllers/ContratoController.cs` — correto; GET somente leitura.
- `backend/src/BFA.Infrastructure/Unidades/ContratoUnidadeConsulta.cs` — correto; autorização em profundidade e filtro tenant-safe.
- `backend/src/BFA.Web/Authorization/AcessoUnidadePorPerfilHandler.cs` — correto; superacesso de Rede na própria Organização.
- `backend/src/BFA.Web/Areas/Franqueadora/Controllers/ContratosController.cs` e `BFA.Application/Franqueadora/Contratos/ContratosFranquiaServico.cs` — gerenciamento exclusivo de `AdministradorRede`.

## PROPOSTA

Restaurar no dashboard o painel somente leitura com resumo/estado vazio e link para a rota existente, adaptado ao design system atual. Não adicionar item global ao menu da Franqueadora porque não existe recurso global desacoplado do par Franqueado–Unidade. Não criar capacidade de governança: consulta contratual já possui semântica e autorização próprias.

## RISCOS

- Reintroduzir ações mutáveis na Área da Unidade por engano.
- Quebrar responsividade ao restaurar markup antigo sem adaptação.
- Testar apenas a presença textual e deixar rota/autorização sem proteção.

## TESTES

- Novo teste HTTP para `AdministradorRede` + Unidade com `FranqueadoUnidade` ativo: dashboard contém resumo e link; GET direto retorna 200 e permanece sem ações mutáveis.
- Novo teste HTTP para `AdministradorRede` + Unidade sem vínculo ativo: dashboard e GET exibem estado vazio controlado.
- Preservar testes de `AdministradorUnidade`, ausência de contrato, outra Unidade/tenant e policy exclusiva da Franqueadora.
- QA desktop/mobile: sidebar/drawer, painel, link, acesso direto e leitura somente.

## Evidências

- Documentação: `docs/ARCHITECTURE.md`, seção de contratos versionados e seção da Área Unidade.
- Plano histórico: `docs/plans/04_modulo-alunos-e-responsaveis.md`, ordem do menu incluindo Contrato.
- Plano visual: `docs/plans/13_refresh-visual-bfa.md`, que declarava UI/UX sem alterar funcionalidade.
- Git: `aff3a0e` removeu o painel; `9ff31d3` preservou o item da sidebar.
- Reprodução local: conta de Unidade exibiu o link `Contrato`; GET abriu contrato vigente em modo somente leitura.
