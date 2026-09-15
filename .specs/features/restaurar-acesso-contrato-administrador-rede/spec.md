# Restaurar acesso a Contrato para AdministradorRede

**Status**: verified

## Problem Statement

O refresh visual do dashboard da Unidade removeu o painel e o atalho de Contrato. A rota, a sidebar, a autorização e a consulta continuam funcionais, mas a capacidade deixou de estar representada no painel operacional, contrariando a documentação que mantém o Contrato como consulta da Unidade e como informação central para a Rede.

## Out of Scope

- Alterar o domínio, os estados, versões, documentos ou histórico de contratos.
- Criar uma listagem global de contratos na Área Franqueadora.
- Alterar permissões de mutação contratual.
- Criar uma nova capacidade em `IGovernancaOperacionalUnidade`.
- Alterar migrations, schema, armazenamento privado ou dependências.
- Refatorar o dashboard, menus ou módulos não relacionados.
- Fazer commit ou push.

## Assumptions & Open Questions

| Assumption | Chosen default | Rationale |
|---|---|---|
| Superfície a restaurar | Painel contratual no dashboard da Unidade | O histórico Git identifica sua remoção como a mudança que causou a perda de descoberta; menu e rota ainda existem. |
| Perfis de consulta | AdministradorRede da Organização e AdministradorUnidade autorizado | Arquitetura, handler, controller e consulta persistente já implementam essa regra. |
| Unidade sem vínculo/contrato ativo | Exibir estado vazio controlado com acesso à consulta | A rota atual retorna 200 com ausência controlada e não oferece criação na Área da Unidade. |
| Gestão contratual | Permanecer exclusiva da Área Franqueadora para AdministradorRede | A gestão exige o contexto Franqueado–Unidade e a policy `AdministradorRede`. |
| Documentação arquitetural | Não alterar | A regra já está documentada corretamente; o desvio está somente na View. |
| Git | Não criar commits | Restrição explícita do usuário, prevalecendo sobre o padrão opcional de commits atômicos do TLC. |

**Open questions: none**

## User Stories

### US-01 — Descobrir e consultar o Contrato como Rede

Como AdministradorRede, quero visualizar no dashboard da Unidade o estado do contrato e acessar seus detalhes para acompanhar a relação comercial mesmo quando a operação local está sob responsabilidade do franqueado.

**Acceptance Criteria**:

1. WHEN an AdministradorRede opens an active Unit in the same Organization with an active franchise contract, the dashboard SHALL display the current contract summary and a link to `/unidade/{unidadeId}/contrato`.
2. WHEN that AdministradorRede follows or directly requests the Unit contract URL, the system SHALL return the read-only contract page without edit, upload, formalize, cancel, or terminate actions.
3. WHEN the Unit has an active `FranqueadoUnidade`, the system SHALL keep contract consultation visible to AdministradorRede and SHALL not apply operational edit restrictions as a reason to hide it.
4. WHEN the Unit has no active franchise relationship or active contract, the dashboard and contract page SHALL present a controlled empty state and SHALL not offer contract creation from the Unit Area.

### US-02 — Preservar a consulta local autorizada

Como AdministradorUnidade autorizado, quero continuar consultando o contrato da minha Unidade sem receber permissões de gestão contratual.

**Acceptance Criteria**:

1. WHEN an authorized AdministradorUnidade opens the Unit dashboard, the system SHALL display the same read-only contract summary or controlled empty state.
2. WHEN an authorized AdministradorUnidade opens the contract URL, the system SHALL return 200 and SHALL omit every contract mutation action.

### US-03 — Preservar isolamento e perfis

Como responsável pela segurança multi-tenant, quero que a restauração visual não amplie o conjunto de usuários autorizados.

**Acceptance Criteria**:

1. WHEN a Professor, Aluno, Responsavel, or other user without Unit administrative access requests the Unit contract URL, the system SHALL deny access.
2. WHEN a user from another Organization requests the Unit contract URL or a contract document, the system SHALL deny access without exposing contract data.
3. WHILE rendering the restored dashboard panel, the system SHALL use only the already authorized tenant-scoped contract result supplied by the controller.
4. WHEN rendering desktop sidebar or mobile drawer for an authorized Unit administrator, the system SHALL continue to expose exactly one `Contrato` navigation link.

## Requirement Traceability

| ID | Requirement | Evidence / planned verification |
|---|---|---|
| NAV-01 | Restaurar resumo e atalho contratual no dashboard | Teste HTTP do dashboard + QA desktop/mobile |
| AUTH-01 | AdminRede consulta Unidade própria com ou sem franqueado ativo | Testes HTTP dos dois cenários |
| AUTH-02 | AdminUnidade mantém consulta somente leitura | Teste HTTP existente ampliado apenas se necessário |
| AUTH-03 | Professor/outros perfis e outro tenant permanecem bloqueados | Testes negativos existentes + regressão direcionada |
| DATA-01 | Consulta continua filtrada por Organização, Unidade e cadeia contratual ativa | Testes de endpoint/documento existentes; nenhuma alteração em Infrastructure |
| UI-01 | Painel segue Admin Shell e componentes atuais | Inspeção Razor e QA nos viewports definidos |
| DB-01 | Nenhuma migration ou mudança de schema | `git diff -- database/migrations` vazio |
