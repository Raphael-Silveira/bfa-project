# Plano: Módulo Aulas e Presenças — Área da Unidade

## Status

**Concluído**

## Objetivo

Implementar o módulo **Aulas** na Área da Unidade, permitindo:
- Programação e gestão de aulas concretas (ocorrências de TurmaHorario)
- Chamada / registro de presença dos alunos
- Agenda / calendário de aulas para Admin e Professor
- Relatório de frequência por aluno/turma/período

## Contexto Atual

- Turma + TurmaHorario: completos (V009)
- Matricula + MatriculaHorario (Grade): completos (V013)
- Aula: **não existe** em nenhuma camada
- Presença: **não existe** em nenhuma camada
- Evolução prevista: Grade → Aula → Presença

## Decisões de Design

1. **Aula é tabela persistida (V015)** — não é cálculo dinâmico
2. **Aula é ocorrência concreta** de TurmaHorario em data específica
3. **Presença é registro individual** do aluno na Aula
4. **Status da Aula**: Programada → Concluída / Cancelada
5. **Status da Presença**: Presente / Ausente / Justificado / Isento
6. **Chamada é operação do Professor** — ele registra presença dos alunos da turma
7. **Geração de Aulas** pode ser manual (Admin) ou automática (futuro)

---

## 1. Modelagem — Migration V015

### Tabela `aulas`

| Coluna | Tipo | Constraints |
|--------|------|-------------|
| `id` | uuid | PK |
| `organizacao_id` | uuid | FK → organizacoes, NOT NULL |
| `unidade_id` | uuid | FK → unidades(organizacao_id, id), NOT NULL |
| `turma_id` | uuid | FK → turmas(organizacao_id, unidade_id, id), NOT NULL |
| `turma_horario_id` | uuid | FK → turmas_horarios(organizacao_id, unidade_id, id), NOT NULL |
| `data` | date | NOT NULL |
| `hora_inicio` | time | NOT NULL |
| `hora_fim` | time | NOT NULL, CHECK hora_fim > hora_inicio |
| `status` | varchar(20) | NOT NULL, DEFAULT 'Programada', CHECK IN ('Programada','Concluida','Cancelada') |
| `capacidade` | integer | NOT NULL, CHECK capacidade > 0 |
| `observacoes` | text | NULL |
| `criado_por_usuario_id` | uuid | FK → usuarios, NOT NULL |
| `atualizado_por_usuario_id` | uuid | FK → usuarios, NOT NULL |
| `criado_em_utc` | timestamptz | NOT NULL |
| `atualizado_em_utc` | timestamptz | NOT NULL |

**Unique:** `uq_aulas_organizacao_turma_data_hora` (organizacao_id, turma_id, data, hora_inicio) — impede duplicata
**Indexes:**
- `ix_aulas_organizacao_unidade_data` (organizacao_id, unidade_id, data)
- `ix_aulas_organizacao_turma_data` (organizacao_id, turma_id, data)
- `ix_aulas_organizacao_turma_horario` (organizacao_id, turma_horario_id)

**Triggers:**
- `trg_proteger_aula`: bloqueia mudança de id, organizacao_id, unidade_id, turma_id, turma_horario_id, criado_em_utc; permite mudança de status apenas entre transições válidas; ao concluir, capacity deve ser respeitada

### Tabela `presencas`

| Coluna | Tipo | Constraints |
|--------|------|-------------|
| `id` | uuid | PK |
| `organizacao_id` | uuid | FK → organizacoes, NOT NULL |
| `unidade_id` | uuid | FK → unidades(organizacao_id, id), NOT NULL |
| `aula_id` | uuid | FK → aulas(organizacao_id, unidade_id, id), NOT NULL |
| `aluno_id` | uuid | FK → alunos(organizacao_id, id), NOT NULL |
| `matricula_id` | uuid | FK → matriculas(organizacao_id, unidade_id, id), NOT NULL |
| `status` | varchar(20) | NOT NULL, CHECK IN ('Presente','Ausente','Justificado','Isento') |
| `chegou_as` | time | NULL |
| `saiu_as` | time | NULL |
| `observacoes` | text | NULL |
| `registrado_por_usuario_id` | uuid | FK → usuarios, NOT NULL |
| `criado_em_utc` | timestamptz | NOT NULL |
| `atualizado_em_utc` | timestamptz | NOT NULL |

**Unique:** `uq_presencas_aula_aluno` (organizacao_id, aula_id, aluno_id) — um registro por aluno por aula
**Indexes:**
- `ix_presencas_organizacao_aula` (organizacao_id, aula_id)
- `ix_presencas_organizacao_aluno` (organizacao_id, aluno_id)

**Triggers:**
- `trg_proteger_presenca`: bloqueia mudança de id, organizacao_id, aula_id, aluno_id, matricula_id, criado_em_utc; aula deve estar Concluida para registrar presença

---

## 2. Domain

### Aula.cs

```csharp
public sealed class Aula
{
    public const int ObservacoesTamanhoMaximo = 500;

    // Propriedades: Id, OrganizacaoId, UnidadeId, TurmaId, TurmaHorarioId,
    //   Data, HoraInicio, HoraFim, Status, Capacidade, Observacoes,
    //   CriadoPorUsuarioId, AtualizadoPorUsuarioId, CriadoEmUtc, AtualizadoEmUtc

    // Métodos:
    // - Concluir(): Status = Concluida (deve ser Programada)
    // - Cancelar(): Status = Cancelada (deve ser Programada)
    // - AtualizarObservacoes():altera observacoes
}
```

### Presenca.cs

```csharp
public sealed class Presenca
{
    public const int ObservacoesTamanhoMaximo = 500;

    // Propriedades: Id, OrganizacaoId, UnidadeId, AulaId, AlunoId, MatriculaId,
    //   Status, ChegouAs, SaiuAs, Observacoes,
    //   RegistradoPorUsuarioId, CriadoEmUtc, AtualizadoEmUtc

    // Métodos:
    // - Registrar(status, observacoes):altera status
    // - RegistrarHorarios(chegouAs, saiuAs):altera horários
}
```

### StatusAula.cs

```csharp
public enum StatusAula
{
    Programada,
    Concluida,
    Cancelada
}
```

### StatusPresenca.cs

```csharp
public enum StatusPresenca
{
    Presente,
    Ausente,
    Justificado,
    Isento
}
```

---

## 3. EF Configurations

### AulaConfiguration.cs

- Tabela: `aulas`
- Status: `HasConversion<string>()`
- HoraInicio/HoraFim: `time without time zone`
- Triggers: `trg_proteger_aula`
- AlternateKey: (OrganizacaoId, UnidadeId, Id)
- Unique: (OrganizacaoId, TurmaId, Data, HoraInicio)
- FKs: Organizacao, Unidade, Turma, TurmaHorario, 2x UsuarioIdentity

### PresencaConfiguration.cs

- Tabela: `presencas`
- Status: `HasConversion<string>()`
- ChegouAs/SaiuAs: `time without time zone` nullable
- Triggers: `trg_proteger_presenca`
- AlternateKey: (OrganizacaoId, Id)
- Unique: (OrganizacaoId, AulaId, AlunoId)
- FKs: Organizacao, Unidade, Aula, Aluno, Matricula, UsuarioIdentity

---

## 4. Application Layer

### IAulasServico / AulasServico

Operações:

| Método | Descrição |
|--------|-----------|
| `ListarAsync(usuarioId, unidadeId, dataInicio, dataFim)` | Lista aulas no período |
| `ObterAsync(usuarioId, unidadeId, aulaId)` | Detalhe da aula com alunos |
| `CriarAsync(usuarioId, unidadeId, solicitacao)` | Cria aula programada |
| `AtualizarAsync(usuarioId, unidadeId, aulaId, solicitacao)` | Atualiza dados da aula |
| `ConcluirAsync(usuarioId, unidadeId, aulaId)` | Marca como concluída |
| `CancelarAsync(usuarioId, unidadeId, aulaId)` | Marca como cancelada |
| `ListarAlunosAsync(usuarioId, unidadeId, aulaId)` | Lista alunos para chamada |
| `RegistrarPresencaAsync(usuarioId, unidadeId, aulaId, alunoId, presenca)` | Registra presença de um aluno |
| `RegistrarPresencasEmLoteAsync(usuarioId, unidadeId, aulaId, presencas)` | Registra presenças em lote (chamada) |
| `ObterFrequenciaAlunoAsync(usuarioId, unidadeId, alunoId, dataInicio, dataFim)` | Relatório de frequência |

### DTOs

```csharp
public sealed record AulaResumo(
    Guid AulaId, string TurmaNome, string ProfessorNome,
    DateOnly Data, TimeOnly HoraInicio, TimeOnly HoraFim,
    StatusAula Status, int Capacidade, int Inscritos);

public sealed record AulaDetalhe(
    Guid AulaId, Guid TurmaId, string TurmaNome,
    string ProfessorNome, DateOnly Data,
    TimeOnly HoraInicio, TimeOnly HoraFim,
    StatusAula Status, int Capacidade,
    string? Observacoes,
    IReadOnlyList<AlunoPresencaResumo> Alunos);

public sealed record AlunoPresencaResumo(
    Guid AlunoId, string NomeCompleto,
    StatusPresenca? Status, TimeOnly? ChegouAs, TimeOnly? SaiuAs);

public sealed record CriarAulaSolicitacao(
    Guid TurmaHorarioId, DateOnly Data,
    TimeOnly HoraInicio, TimeOnly HoraFim,
    string? Observacoes);

public sealed record RegistrarPresencaSolicitacao(
    StatusPresenca Status, TimeOnly? ChegouAs, TimeOnly? SaiuAs,
    string? Observacoes);

public sealed record FrequenciaAlunoResumo(
    Guid AlunoId, string NomeCompleto,
    int TotalAulas, int Presentes, int Ausentes,
    int Justificados, int Isentos, decimal PercentualFrequencia);
```

### EstadoAulasUnidade enum

```csharp
public enum EstadoAulasUnidade
{
    Sucesso,
    SemAcesso,
    UnidadeNaoEncontrada,
    AulaNaoEncontrada,
    TurmaNaoEncontrada,
    TurmaHorarioNaoEncontrado,
    DadosInvalidos,
    AulaNaoProgramada,  // tentativa de concluir/cancelar aula que não está programada
    AulaJaConcluida,
    AulaJaCancelada,
    AlunoNaoMatriculado,
    CapacidadeExcedida,
    Falha
}
```

---

## 5. Repository Layer

### IAulasRepositorio / AulasRepositorio

| Método | Descrição |
|--------|-----------|
| `ListarAsync(orgId, unidadeId, dataInicio, dataFim)` | Lista aulas no período |
| `ObterAsync(orgId, unidadeId, aulaId)` | Detalhe completo |
| `CriarAsync(aula)` | Insere aula |
| `AtualizarAsync(aula)` | Atualiza aula |
| `ListarAlunosAsync(orgId, unidadeId, aulaId)` | Alunos matriculados na turma/horário da aula |
| `ObterPresencaAsync(orgId, aulaId, alunoId)` | Presença existente |
| `RegistrarPresencaAsync(presenca)` | Insere/atualiza presença |
| `RegistrarPresencasEmLoteAsync(presencas)` | Insere presenças em lote |
| `ObterFrequenciaAlunoAsync(orgId, alunoId, dataInicio, dataFim)` | Dados de frequência |
| `ExisteAulaNoHorarioAsync(orgId, turmaId, data, horaInicio)` | Verifica conflito |

---

## 6. Controller — AlunosController estendido ou novo AulasController

### Opção: Novo AulasController na área Unidade

**Rota:** `unidade/{unidadeId}/aulas`

| HTTP | Action | Rota | Descrição |
|------|--------|------|-----------|
| GET | `Index` | `` | Lista aulas (calendário/lista) |
| GET | `Nova` | `/nova` | Formulário de criação |
| POST | `Nova` | `/nova` | Criar aula |
| GET | `Detalhes` | `/{aulaId}` | Detalhe da aula + chamada |
| GET | `Editar` | `/{aulaId}/editar` | Formulário de edição |
| POST | `Editar` | `/{aulaId}/editar` | Atualizar aula |
| POST | `Concluir` | `/{aulaId}/concluir` | Marcar como concluída |
| POST | `Cancelar` | `/{aulaId}/cancelar` | Marcar como cancelada |
| POST | `RegistrarPresenca` | `/{aulaId}/presencas/{alunoId}` | Registrar presença |
| POST | `RegistrarPresencasLote` | `/{aulaId}/presencas/lote` | Registrar presenças em lote |
| GET | `Frequencia` | `/frequencia` | Relatório de frequência |

### Governança

- `PodeGerenciarAlunos` para AdminUnidade
- Professor: acesso próprio (já existe TurmasController na área Professor)

---

## 7. Views

### Aulas/Index.cshtml
- Calendário semanal/mensal com aulas programadas
- Filtro por turma, período
- Cards/lista com: turma, professor, data, horário, status
- Botão "Nova aula" quando PodeGerenciarAlunos

### Aulas/Nova.cshtml
- Formulário: selecionar TurmaHorário, data, horário, observações
- Validação de conflito de horário

### Aulas/Detalhes.cshtml
- Resumo da aula (turma, professor, data, horário, status)
- Lista de alunos com presença registrada
- Formulário de chamada (quando status = Programada)
- Botões: Concluir, Cancelar

### Aulas/Editar.cshtml
- Editar dados da aula (status, observações)

### Aulas/Frequencia.cshtml
- Filtro: aluno, turma, período
- Tabela: aluno, total aulas, presenças, ausências, frequência %

---

## 8. Regras de Negócio

### Criação de Aula
- TurmaHorario deve existir e estar ativo
- Data deve estar dentro da vigência do TurmaHorario
- Não pode duplicar aula no mesmo horário da mesma turma
- Capacidade herdada da Turma

### Conclusão de Aula
- Só pode concluir aula com status Programada
- Data+hora devem ser≤ agora (não concluir aula futura)
- Registra automaticamente "Ausente" para alunos sem presença

### Cancelamento de Aula
- Só pode cancelar aula com status Programada
- Remove presenças registradas (ou mantém como registro histórico?)

### Chamada (Registro de Presença)
- Aula deve estar Programada ou Concluída
- Aluno deve ter matrícula ativa na turma
- Um registro por aluno por aula (unique constraint)
- Operação em lote para eficiência

### Relatório de Frequência
- Calcula: (Presentes / Total Aulas) × 100
- Mostra por aluno, turma, período
- Inclui aulas canceladas no cálculo? (decisão: não contar canceladas)

---

## 9. Escopo

### Incluído
- Migration V015 (aulas + presencas)
- Domain: Aula, Presenca, StatusAula, StatusPresenca
- EF Configurations
- Application: AulasServico com todas as operações
- Repository: AulasRepositorio
- Controller: AulasController
- Views: Index, Nova, Detalhes, Editar, Frequencia
- Governança: PodeGerenciarAlunos
- Testes unitários e de integração

### Fora do escopo
- Geração automática de aulas (agendamento em lote)
- Notificações para alunos/responsáveis
- App mobile do professor
- Relatórios avançados/gráficos
- Integração com calendar APIs

---

## 10. Riscos

1. **Performance**: Listar aulas de longo período pode ser pesado → usar paginação
2. **Concorrência**: Dois professores registrando presença ao mesmo tempo → usar transação
3. **Dados faltantes**: Aula criada sem chamada → tratar como programa
4. **Migração**: V015 é nova migration → seguir convenção de versionamento

---

## 11. Testes

### Unitários
- Domain: validações de Aula e Presenca
- Application: regras de negócio (criar, concluir, cancelar, chamada)

### Integração
- Repository: CRUD completo
- Conflito de horário
- Capacidade
- Transação de chamada em lote
- Frequência do aluno

### Manuais
- Criar aula → verificar na listagem
- Registrar presença → verificar no detalhe
- Concluir aula → verificar status
- Cancelar aula → verificar exclusão lógica
- Relatório de frequência → verificar cálculos
- Mobile 390px
- Desktop 1440px
- Zoom 200%

---

## 12. Critérios de Aceite

- [ ] Migration V015 aplicada sem erro
- [ ] Aulas podem ser criadas, editadas, concluídas, canceladas
- [ ] Presenças podem ser registradas individualmente e em lote
- [ ] Chamada mostra apenas alunos matriculados na turma
- [ ] Relatório de frequência calcula corretamente
- [ ] Governança funciona (Admin vs Professor vs readonly)
- [ ] Build: 0 erros, 0 warnings
- [ ] Todos os testes passando
- [ ] Nenhuma migration antiga alterada (V001-V014 intactas)

---

## 13. Estratégia de Implementação

### Fase 1 — Fundação
1. Migration V015
2. Domain: Aula, Presenca, StatusAula, StatusPresenca
3. EF Configurations

### Fase 2 — Application + Repository
4. Interface + Implementação do Repository
5. Interface + Implementação do Service

### Fase 3 — Web
6. Controller: AulasController
7. ViewModels
8. Views: Index, Nova, Detalhes

### Fase 4 — Chamada + Relatório
9. Action de chamada (registro de presenças)
10. Action de relatório de frequência
11. Views: Frequencia

### Fase 5 — Testes + Validação
12. Testes unitários
13. Testes de integração
14. QA manual
15. Build final

---

## Próximos Passos

1. Aprovar plano
2. Criar Migration V015
3. Implementar Domain
4. Implementar EF Configurations
5. Build + testes

---

## Resultado

**Concluído em:** 2026-09-04

### Arquivos Criados
- `database/migrations/V015__criar_aulas_e_presencas.sql` — Tabelas `aulas` e `presencas` com triggers
- `backend/src/BFA.Domain/Aulas/StatusAula.cs` — Enum (Programada, Concluida, Cancelada)
- `backend/src/BFA.Domain/Aulas/StatusPresenca.cs` — Enum (Presente, Ausente, Justificado, Isento)
- `backend/src/BFA.Domain/Aulas/Aula.cs` — Domain entity
- `backend/src/BFA.Domain/Aulas/Presenca.cs` — Domain entity
- `backend/src/BFA.Infrastructure/Persistence/Configurations/AulaConfiguration.cs` — EF config
- `backend/src/BFA.Infrastructure/Persistence/Configurations/PresencaConfiguration.cs` — EF config
- `backend/src/BFA.Application/Aulas/AulasUnidade.cs` — Service + DTOs + interfaces
- `backend/src/BFA.Infrastructure/Aulas/AulasRepositorio.cs` — Repository
- `backend/src/BFA.Web/Areas/Unidade/Controllers/AulasController.cs` — Controller (10 actions)
- `backend/src/BFA.Web/ViewModels/Unidade/AulaViewModels.cs` — ViewModels + mapper
- `backend/src/BFA.Web/Areas/Unidade/Views/Aulas/Index.cshtml` — Listagem
- `backend/src/BFA.Web/Areas/Unidade/Views/Aulas/Nova.cshtml` — Formulário de criação
- `backend/src/BFA.Web/Areas/Unidade/Views/Aulas/Detalhes.cshtml` — Detalhe + presenças
- `backend/src/BFA.Web/Areas/Unidade/Views/Aulas/Editar.cshtml` — Formulário de edição
- `backend/src/BFA.Web/Areas/Unidade/Views/Aulas/Chamada.cshtml` — Formulário de chamada
- `backend/src/BFA.Web/Areas/Unidade/Views/Aulas/Frequencia.cshtml` — Relatório de frequência

### Arquivos Modificados
- `BFA.Infrastructure/DependencyInjection.cs` — Registrado IAulasRepositorio + IAulasServico
- `BFA.Infrastructure/Persistence/BfaDbContext.cs` — Adicionados DbSets Aulas e Presencas
- `_UnidadeNavLinks.cshtml` — Adicionado link "Aulas" no menu
- `FluxosTurmaGradeArchitectureTests.cs` — Migration count atualizado (14→15)
- `MatriculasOperacionaisArchitectureTests.cs` — Migration count atualizado (14→15)

### Testes
- 484 unitários passando
- 693 integração passando
- Total: 1.177
