# Plano: Área do Aluno

## Status

**Concluído**

## Objetivo

Implementar a **Área do Aluno** — portal self-service onde o aluno acessa seus dados, matrícula, agenda, frequência e situação financeira.

## Contexto Atual

- Área Aluno existe como esqueleto vazio (`.gitkeep` apenas)
- `PerfilAcesso.Aluno` (valor 4) já existe no enum
- `Aluno.UsuarioId` vincula o aluno ao Identity (login possível)
- `DestinoPosLogin` NÃO redireciona alunos (caem em `/acesso-negado`)
- Domain层 completo: Aluno, Matricula, Aula, Presenca, Cobranca, Pagamento
- Services admin existem (AlunosServico, MatriculasServico, etc.) — reutilizáveis read-only

## Regras de Design

1. **Aluno vê APENAS seus próprios dados** — nunca de outros alunos
2. **Read-only** — aluno não cria/edita/deleta nada (admin faz isso)
3. **Multi-tenancy** — aluno vinculado a Organizacao + Unidade
4. **Layout próprio** — mais simples que o admin shell
5. **Reutilizar services existentes** — com novo repositório read-only

---

## 1. Login Routing

### DestinoPosLogin
- Adicionar `DestinoAcesso.Aluno` ao enum
- Adicionar redirect: `/aluno/{unidadeId}`
- Aluno logado é redirecionado para seu dashboard

### Autorização
- Policy `AlunoAcesso` — verifica `PerfilAcesso.Aluno` no VinculoAcesso
- Controllers verificam que o UsuarioId logado é o próprio Aluno

---

## 2. Layout + Navigation

### _AlunoLayout.cshtml
- Shell simplificado (sem sidebar admin)
- Header com logo BFA + nome do aluno + logout
- Navegação horizontal ou sidebar leve
- Responsivo (mobile-first — aluno acessa pelo celular)

### _AlunoNavLinks.cshtml
- Perfil
- Minha Matrícula
- Agenda
- Aulas
- Frequência
- Financeiro

---

## 3. Application — AlunoAreaServico

### IAlunoAreaServico

| Método | Descrição |
|--------|-----------|
| `ObterPerfilAsync(usuarioId)` | Dados cadastrais do aluno |
| `ObterMatriculasAsync(usuarioId, unidadeId)` | Matrículas do aluno na unidade |
| `ObterAgendaAsync(usuarioId, unidadeId, dataInicio, dataFim)` | Aulas programadas do aluno |
| `ObterFrequenciaAsync(usuarioId, unidadeId, dataInicio, dataFim)` | Frequência do aluno |
| `ObterFinanceiroAsync(usuarioId, unidadeId)` | Cobranças e pagamentos do aluno |

### IAlunoAreaRepositorio (read-only)

| Método | Descrição |
|--------|-----------|
| `ObterAlunoPorUsuarioAsync(usuarioId)` | Busca Aluno por UsuarioId |
| `ListarMatriculasAsync(orgId, unidadeId, alunoId)` | Lista matrículas |
| `ListarAulasAsync(orgId, unidadeId, alunoId, dataInicio, dataFim)` | Aulas do aluno |
| `ListarPresencasAsync(orgId, unidadeId, alunoId, dataInicio, dataFim)` | Presenças |
| `ListarCobrancasAsync(orgId, unidadeId, alunoId)` | Cobranças |

---

## 4. Controller — AlunoController

**Rota:** `aluno/{unidadeId:guid}`

| HTTP | Action | Descrição |
|------|--------|-----------|
| GET | `Dashboard` | Painel principal |
| GET | `Perfil` | Dados cadastrais |
| GET | `Matriculas` | Lista de matrículas |
| GET | `Agenda` | Aulas programadas |
| GET | `Frequencia` | Registro de presença |
| GET | `Financeiro` | Cobranças e pagamentos |

---

## 5. Views

### Dashboard.cshtml
- Card: Minha Matrícula (status, plano, unidade)
- Card: Próximas Aulas (top 3)
- Card: Situação Financeira (resumo)
- Card: Frequência (percentual)

### Perfil.cshtml
- Nome, CPF (máscara), Email, Telefone
- Data de nascimento
- Responsáveis vinculados

### Matriculas.cshtml
- Lista de matrículas: plano, status, período, valor
- Detalhe: horários vinculados

### Agenda.cshtml
- Calendário/lista de aulas programadas
- Filtros por período
- Status: Programada, Concluída, Cancelada

### Frequencia.cshtml
- Tabela: data, turma, horário, status (Presente/Ausente/Justificado/Isento)
- Percentual geral de frequência

### Financeiro.cshtml
- Lista de cobranças: descrição, tipo, valor, vencimento, status
- Histórico de pagamentos

---

## 6. Escopo

### Incluído
- Login routing para Aluno
- Layout + Navigation
- Application + Repository (read-only)
- Controller (6 actions)
- Views (6 páginas)
- Authorization policy

### Fora do escopo
- Aluno cria/edita dados (admin faz)
- Pagamento online
- Notificações push
- App mobile (fase 5)
- Chat com professor
- Acesso do Responsável

---

## 7. Resultado

**Concluído em:** 2026-09-04

### Arquivos Criados
- `backend/src/BFA.Web/Areas/Aluno/Controllers/AlunoController.cs`
- `backend/src/BFA.Web/Areas/Aluno/Views/Shared/_AlunoLayout.cshtml`
- `backend/src/BFA.Web/Areas/Aluno/Views/Shared/_AlunoNavLinks.cshtml`
- `backend/src/BFA.Web/Areas/Aluno/Views/_ViewImports.cshtml`
- `backend/src/BFA.Web/Areas/Aluno/Views/Dashboard.cshtml`
- `backend/src/BFA.Web/Areas/Aluno/Views/Perfil.cshtml`
- `backend/src/BFA.Web/Areas/Aluno/Views/Matriculas.cshtml`
- `backend/src/BFA.Web/Areas/Aluno/Views/Agenda.cshtml`
- `backend/src/BFA.Web/Areas/Aluno/Views/Frequencia.cshtml`
- `backend/src/BFA.Web/Areas/Aluno/Views/Financeiro.cshtml`
- `backend/src/BFA.Web/ViewModels/Aluno/AlunoAreaViewModels.cs`
- `backend/src/BFA.Application/AlunoArea/IAlunoAreaServico.cs`
- `backend/src/BFA.Application/AlunoArea/IAlunoAreaRepositorio.cs`
- `backend/src/BFA.Application/AlunoArea/AlunoAreaServico.cs`
- `backend/src/BFA.Infrastructure/AlunoArea/AlunoAreaRepositorio.cs`
- `backend/docs/plans/09_area-do-aluno.md`

### Arquivos Modificados
- `DestinoPosLogin.cs` — Adicionado redirect para Aluno
- `DestinoPosLoginResultado.cs` — Adicionado caso Aluno
- `DependencyInjection.cs` — Registrado IAlunoAreaServico + IAlunoAreaRepositorio
- `PROJECT-STATE.md` — Atualizado roadmap
- `docs/plans/README.md` — Adicionado plano 09

### Testes
- 484 unitários passando
- 693 integração passando
- Total: 1.177
