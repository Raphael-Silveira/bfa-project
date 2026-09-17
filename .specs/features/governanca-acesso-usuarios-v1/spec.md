# Spec — Governança de Acesso de Usuários V1

## Problem Statement

Permitir que a Área Franqueadora governe vínculos de acesso sem confundir conta técnica, dados pessoais, autorização e relação comercial, preservando histórico e isolamento por Organização.

## Out of Scope

- Alterar `FranqueadoUsuario` ou redesenhar sua relação comercial.
- Alterar o módulo Franqueados.
- Introduzir `IdentityRole`.
- Bloquear globalmente `UsuarioIdentity` para retirar acesso de um tenant.
- Criar migration antes de comprovar necessidade.
- Redesign amplo da Área Franqueadora.

## Assumptions & Open Questions

Open questions: none for starting Design; the lock strategy only needs technical confirmation in the next stage.

- A conta técnica é válida para reativação quando o registro `UsuarioIdentity` ainda existe e o vínculo histórico respeita as restrições atuais de Organização, perfil e escopo.
- O estado global de bloqueio do Identity permanece fora da governança contextual de tenant.
- A estratégia de lock transacional por Organização deve ser confirmada contra o provider PostgreSQL disponível antes da implementação. Se não for segura e testável no padrão atual, a criação de constraint/migration será submetida para aprovação separada.
- A decisão futura sobre sincronizar desativação comercial e acesso operacional permanece fora desta fase.

## User Stories

### US-01 — Governar administradores da Organização

Como AdministradorRede, quero desativar e reativar vínculos de AdministradorRede da minha Organização, para controlar autorização sem apagar histórico nem afetar outros tenants.

### US-02 — Preservar o último administrador

Como Organização, quero impedir a remoção do último AdministradorRede, inclusive em concorrência e auto-revogação, para nunca perder governança administrativa.

### US-03 — Governar acessos de Unidade ativa

Como AdministradorRede, quero adicionar e reativar AdministradorUnidade somente em Unidades ativas, para evitar conceder acesso operacional a uma Unidade indisponível.

## Semântica definitiva

### Conta e perfil

- `UsuarioIdentity` é a conta técnica global de autenticação. Seu bloqueio global não pode ser usado por um tenant para retirar autorização em apenas uma Organização.
- `PerfilUsuario` contém dados pessoais e seu `Ativo` representa o estado do perfil pessoal, não uma autorização. `PerfilUsuario.Ativo` não concede nem revoga acesso e não deve ser usado como status de autorização.
- O diagnóstico confirma que `PerfilUsuario.Ativo` é criado como `true`, não possui fluxo de alteração no módulo e não é consultado pelo pipeline de autenticação/autorização. A futura governança não usará esse campo isoladamente para bloquear login ou acesso.

### Autorização e relações comerciais

- `VinculoAcesso.Ativo` é a fonte de verdade para autorização dentro de uma Organização/Unidade.
- `AdministradorRede` é um `VinculoAcesso` com `OrganizacaoId` preenchido e `UnidadeId = null`.
- `AdministradorUnidade` é um `VinculoAcesso` com Organização e Unidade.
- `FranqueadoUsuario.Ativo` representa somente relação comercial e não autoriza, bloqueia login ou concede `AdministradorUnidade` sozinho.
- O comportamento existente de desativar/reativar o acesso operacional junto do vínculo comercial do Franqueado permanece congelado nesta fase e será tratado como decisão futura.

## Requisitos funcionais

### RF-01 — Gestão contextual de AdministradorRede

Quando um AdministradorRede autenticado operar sobre um vínculo, o sistema deve considerar somente a Organização resolvida a partir de seus vínculos ativos de AdministradorRede; nunca deve aceitar a Organização como autoridade vinda do cliente.

Quando o vínculo alvo pertencer à mesma Organização e tiver perfil `AdministradorRede`, o sistema deve permitir sua desativação lógica, preservando o registro e seu histórico.

Quando o vínculo alvo estiver inativo, pertencer à mesma Organização, tiver perfil `AdministradorRede` e a conta técnica ainda for válida segundo as regras existentes, o sistema deve reativar o mesmo vínculo histórico, sem criar outro.

### RF-02 — Proteção do último administrador

Quando a desativação atingir o último `AdministradorRede` ativo da Organização, o sistema deve bloquear a operação e retornar mensagem clara, mantendo o vínculo ativo.

Quando o AdministradorRede tentar revogar o próprio vínculo, o sistema deve permitir somente se existir pelo menos outro AdministradorRede ativo na mesma Organização.

Quando duas desativações concorrentes tentarem remover os dois últimos AdministradoresRede, no máximo uma poderá concluir; a Organização nunca poderá terminar sem AdministradorRede ativo.

### RF-03 — Concorrência sem alteração de schema

Quando a desativação de AdministradorRede for persistida em PostgreSQL, a operação deve serializar a decisão por Organização, recontar os AdministradoresRede ativos dentro da transação e tratar conflito concorrente de modo controlado.

A estratégia preferencial é lock transacional por Organização no repositório, usando mecanismo nativo do PostgreSQL, sem migration. Se a implementação comprovar que o padrão de infraestrutura não suporta isso de forma segura, a implementação deve parar antes de alterar schema e registrar a justificativa.

### RF-04 — AdministradorUnidade e Unidade inativa

Quando um AdministradorRede adicionar ou reativar um vínculo `AdministradorUnidade`, o sistema deve exigir que a Unidade pertença à Organização e esteja ativa.

Quando a Unidade estiver inativa, o sistema deve bloquear tanto a criação quanto a reativação do vínculo, preservando qualquer histórico existente.

Quando a Unidade estiver ativa, o sistema deve manter o comportamento atual de adicionar, reativar o mesmo vínculo histórico e impedir duplicidade ativa.

### RF-05 — Status e interface

Quando a listagem de Usuários apresentar estado de acesso, o texto deve representar autorização, por exemplo `Acesso ativo` ou `Sem acesso ativo`, e não usar `PerfilUsuario.Ativo` como se fosse autorização.

Quando a interface apresentar relações comerciais, deve diferenciá-las de perfis e acessos por Unidade, sem conceder qualquer permissão por mera apresentação visual.

## Requisitos de segurança

- Um AdministradorRede da Organização A não pode ler, ativar, desativar ou alterar vínculo da Organização B.
- Operações sobre Unidade devem validar tenant e perfil do vínculo alvo na Application/Infrastructure.
- Nenhum campo de Organização, Unidade, perfil ou estado enviado pelo cliente pode substituir a autorização resolvida no servidor.
- Desativar acesso de um tenant não pode alterar `UsuarioIdentity` ou vínculos de outro tenant.

## Testes de aceitação

- AT-01: AdministradorRede A desativa outro AdministradorRede da Organização A e o vínculo fica inativo.
- AT-02: reativação reutiliza o vínculo histórico.
- AT-03: desativação do último AdministradorRede é bloqueada.
- AT-04: auto-revogação é permitida quando há outro AdministradorRede ativo.
- AT-05: auto-revogação do último AdministradorRede é bloqueada.
- AT-06: usuário com Organizações A e B perde apenas o vínculo de A.
- AT-07: operação cross-tenant não altera vínculo de B.
- AT-08: AdministradorUnidade pode ser adicionado e reativado em Unidade ativa.
- AT-09: adicionar ou reativar AdministradorUnidade em Unidade inativa é bloqueado.
- AT-10: testes demonstram que `PerfilUsuario.Ativo` não concede nem revoga autorização.
- AT-11: duas operações concorrentes não deixam a Organização sem AdministradorRede ativo.
- AT-12: `FranqueadoUsuario.Ativo` continua sem efeito de autorização isoladamente.

## Alterações esperadas após aprovação desta spec

- Application: casos de uso e resultados explícitos para desativar/reativar AdministradorRede, proteção do último administrador e validação de Unidade ativa.
- Infrastructure: persistência transacional contextual e serialização por Organização, sem migration se o mecanismo nativo for suficiente.
- Web: controles mínimos para gestão de acesso, separados de dados pessoais e relação comercial.
- Testes unitários e de integração para todos os ATs, incluindo concorrência PostgreSQL.
- Possível alteração textual da listagem de Usuários somente após a semântica de status estar coberta por testes.

## Banco e Git

- Nenhuma migration é necessária na especificação inicial.
- V001–V014 permanecem imutáveis.
- Nenhum commit ou push nesta etapa.

## Requirement Traceability

| ID | Requisito |
|---|---|
| AUTH-01 | RF-01 e segurança contextual por Organização |
| AUTH-02 | RF-02, último administrador e auto-revogação |
| CONC-01 | RF-03 e AT-11 |
| UNIT-01 | RF-04 e AT-08/AT-09 |
| SEM-01 | Semântica de `UsuarioIdentity`, `PerfilUsuario.Ativo` e `VinculoAcesso.Ativo` |
| COMM-01 | RF-05, AT-10 e AT-12 |
| UI-01 | RF-05, separação visual sem redesign amplo |
| DB-01 | Nenhuma migration e V001–V014 intactas |
