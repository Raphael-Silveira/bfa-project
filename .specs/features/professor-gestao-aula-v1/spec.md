# PROFESSOR — GESTÃO DA AULA V1

## Problem Statement

A tela do Professor exibe confirmações de participação, mas ainda não permite registrar a chamada oficial nem cancelar uma aula com auditoria. Participação e presença precisam continuar independentes.

## Out of Scope

Financeiro, perfil, matrícula, login, Franqueadora, integração futura com Professor e remoção de `Justificado` do domínio.

## Assumptions & Open Questions

| Assumption | Chosen default | Rationale |
|---|---|---|
| Professor autorizado | vínculo ativo na Organização/Unidade/Turma já usado pela Área Professor | preserva o isolamento existente |
| Chamada não marcada | ausência de registro `Presenca` | mantém o modelo atual e permite chamada parcial |
| Auditoria de responsável | FK para `usuarios(id)` | segue o padrão das auditorias de Aula |
| Instante do cancelamento | UTC gerado no servidor | impede confiança em timestamp do cliente |

Open questions: none

## User Stories

- Como Professor autorizado, quero registrar Presente ou Ausente para os alunos válidos da aula.
- Como Professor autorizado, quero cancelar uma aula informando o motivo.
- Como Aluno, quero ver uma aula cancelada sem poder confirmá-la novamente.

## Acceptance Criteria

1. WHEN uma aula programada for cancelada, o sistema SHALL persistir motivo, instante UTC e usuário autenticado responsável.
2. IF o motivo do cancelamento estiver vazio, em branco ou exceder 500 caracteres, THEN o sistema SHALL rejeitar a operação.
3. IF existir qualquer Presenca para a aula, THEN o sistema SHALL impedir o cancelamento e preservar as presenças.
4. WHEN o Professor autorizado salvar a chamada, o sistema SHALL aceitar somente Presente ou Ausente e SHALL manter aluno não marcado sem Presenca.
5. WHEN a chamada for salva novamente, o sistema SHALL atualizar a Presenca existente sem duplicá-la.
6. IF a URL apontar para Organização, Unidade, Turma ou Professor não autorizado, THEN o sistema SHALL negar a operação sem alterar dados.
7. WHILE a aula estiver cancelada, o sistema SHALL exibir o motivo, SHALL bloquear chamada e cancelamento e SHALL impedir nova confirmação do aluno.
8. WHEN a agenda, próximas aulas ou frequência forem consultadas, o sistema SHALL excluir aula cancelada das próximas aulas e dos denominadores de frequência.
9. WHEN a participação for exibida na tela do Professor, o sistema SHALL mantê-la separada da chamada e SHALL impedir que ela altere Presenca.

## Requirement Traceability

| ID | Requirement | Evidence target |
|---|---|---|
| PGA-01 | Auditoria V024 | migration, entidade e teste unitário |
| PGA-02 | Motivo obrigatório | domínio, aplicação e teste unitário |
| PGA-03 | Bloqueio com Presenca | repositório e teste de integração |
| PGA-04 | Chamada Presente/Ausente/Pendente | aplicação, Razor e testes |
| PGA-05 | Idempotência | repositório e teste de integração |
| PGA-06 | Autorização tenant-aware | consulta, controller e testes |
| PGA-07 | Aula cancelada somente leitura | consulta, Portal Aluno e testes |
| PGA-08 | Exclusão de próximas aulas/frequência | repositório e regressões |
| PGA-09 | Separação confirmação/presença | DTO, view e teste |
