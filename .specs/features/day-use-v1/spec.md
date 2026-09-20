# Day Use V1

## Problem Statement

A Unidade precisa registrar utilização avulsa em uma data para alunos cadastrados ou participantes avulsos, sem acoplar a operação a Aula, Matrícula ou Financeiro existente.

## Escopo

Day Use registra o uso avulso da estrutura de uma Unidade em uma data. Não possui relação com Aula, Turma, Presenca, Frequencia, Matricula ou Portal Aluno.

## Requisitos

- DU-001: O sistema deve permitir registrar Day Use para Aluno cadastrado ou participante avulso.
- DU-002: Participante avulso deve exigir nome e aceitar telefone/e-mail opcionais.
- DU-003: O sistema deve preservar a invariável Aluno OU Avulso.
- DU-004: AdminUnidade deve configurar o valor sugerido da própria Unidade.
- DU-005: Professor autorizado deve registrar Day Use sem alterar a configuração da Unidade.
- DU-006: O registro deve persistir DataUso, ValorSugerido, ValorCobrado e auditoria de criação.
- DU-007: ValorCobrado deve aceitar zero e representar cortesia sem criar Cobranca.
- DU-008: O sistema deve registrar Pago/Pendente no próprio Day Use, sem alterar Cobranca.
- DU-009: Deve existir no máximo um Day Use por Aluno, Unidade e DataUso.
- DU-010: Participantes avulsos não devem ser deduplicados por dados pessoais.
- DU-011: Todas as operações devem respeitar Organização, Unidade e perfil autorizado.
- DU-012: V025 deve ser SQL manual e V001–V024 devem permanecer intactas.
- DU-013: O filtro de participante deve ignorar maiúsculas/minúsculas para nomes reais de Aluno e NomeAvulso, no banco, com trim e paginação server-side preservada.
- DU-014: AdminUnidade pode excluir fisicamente Day Uses da própria Unidade; Professor pode excluir somente os criados por si na Unidade vinculada. A exclusão exige POST com antiforgery e não altera outros domínios.
- DU-015: A listagem deve apresentar o nome real do usuário registrador quando houver PerfilUsuario e nunca expor identificadores técnicos.

## Fora do escopo

Horário, Quadra, Reserva, Aula, Presenca, Frequencia, Matricula, Portal Aluno, cancelamento, cobrança e integração com gateway.

## Out of Scope

Horário, Quadra, Reserva, Aula, Presenca, Frequencia, Matricula, Portal Aluno, cancelamento, cobrança e integração com gateway.

## Assumptions & Open Questions

- Open questions resolved: nenhuma para a implementação aprovada.
- O valor sugerido precisa estar configurado para permitir registro.
- O pagamento é apenas operacional no Day Use e não baixa uma Cobranca.
- A política de cancelamento será definida em feature posterior.

## User Stories

- Como AdminUnidade, quero configurar o valor sugerido da minha Unidade.
- Como operador autorizado, quero registrar Day Use para aluno ou participante avulso.
- Como operador autorizado, quero consultar e marcar registros como pagos.

## Requirement Traceability

| ID | Implementação planejada | Teste planejado |
| --- | --- | --- |
| DU-001 | Domain/Application DayUse | Unit/Application |
| DU-002 | Domain/Application DayUse | Unit/Application |
| DU-003 | Domain/Application DayUse | Unit/Application |
| DU-004 | Unidade e DayUse service | Unit/Integration |
| DU-005 | Unidade e DayUse service | Unit/Integration |
| DU-006 | Unidade e DayUse service | Unit/Integration |
| DU-007 | Unidade e DayUse service | Unit/Integration |
| DU-008 | Unidade e DayUse service | Unit/Integration |
| DU-009 | Constraints e autorização | PostgreSQL/Endpoint |
| DU-010 | Constraints e autorização | PostgreSQL/Endpoint |
| DU-011 | Constraints e autorização | PostgreSQL/Endpoint |
| DU-012 | V025 e testes arquiteturais | Migration tests |
| DU-013 | Filtro ILIKE na consulta PostgreSQL | PostgreSQL integration: aluno, avulso, caixa, trim e isolamento |
| DU-014 | Caso de uso compartilhado, exclusão escopada e POST antiforgery | Unit, PostgreSQL e controller security tests |
| DU-015 | Projeção de PerfilUsuario na listagem | PostgreSQL integration |
