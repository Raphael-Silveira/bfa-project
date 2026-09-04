# Plano: Módulo Alunos e Responsáveis — Área da Unidade

## Status

**Em andamento — Etapa 3**

## Objetivo

Criar o módulo **Alunos** na Área da Unidade, permitindo:
- Listar alunos da Unidade (via Matrículas)
- Abrir detalhes do aluno
- Editar dados cadastrais permitidos
- Gerenciar Responsáveis
- Visualizar Matrículas atuais e históricas

## Estado Atual

- Alunos, Responsáveis e AlunoResponsavel já existem no banco (V011)
- Domínio completo com validações (Aluno.cs, Responsavel.cs, AlunoResponsavel.cs)
- EF Core configurado com triggers e constraints
- Nenhum controller/view de Alunos na Área da Unidade
- Nenhuma governança `PodeGerenciarAlunos`
- Menu atual: Visão Geral → Professores → Turmas → Planos → Matrículas → Contrato

## Decisões da Etapa 1

1. **PodeGerenciarAlunos**: Criado como capacidade explícita, separada de `PodeGerenciarMatriculas`, mas com mesma regra de governança nesta versão
2. **CPF**: Não editável nesta etapa. Domain não oferece alteração via `AtualizarDados()`. LGPD exige proteção/minimização, não implica imutabilidade
3. **Professor**: Não deve ganhar acesso administrativo ao módulo Alunos
4. **Aluno sem matrícula ativa**: Aparece na listagem se possui histórico na unidade, com indicação "Sem matrícula ativa"
5. **Situação do aluno**: Usar dados reais (Aluno.Ativo + Status da Matrícula), não inventar novo StatusAluno
6. **Grid desktop**: Usar layout em grid para detalhe do aluno em telas >= 1440px

---

## 1. Modelagem de Aluno

### Tabela `alunos` (V011)

| Coluna | Tipo | Editável? | Observação |
|--------|------|-----------|------------|
| `id` | uuid | ❌ Imutável | PK, trigger bloqueia |
| `organizacao_id` | uuid | ❌ Imutável | trigger bloqueia |
| `usuario_id` | uuid | ⚠️ Limitado | Só via `AlterarUsuario()` |
| `nome_completo` | text | ✅ Sim | CK: não vazio |
| `data_nascimento` | date | ✅ Sim | CK: não futura |
| `cpf` | text | ⚠️ Cuidado | Unique parcial, máscara |
| `telefone` | text | ✅ Sim | CK: não vazio se informado |
| `email` | text | ✅ Sim | CK: não vazio se informado |
| `ativo` | boolean | ✅ Sim | Via `Ativar()`/`Desativar()` |
| `criado_em_utc` | timestamptz | ❌ Imutável | trigger bloqueia |
| `atualizado_em_utc` | timestamptz | ✅ Automático | Atualizado pelo domínio |

### Triggers (V011)

| Trigger | O que faz |
|---------|-----------|
| `trg_proteger_aluno` | Bloqueia mudança em `id`, `organizacao_id`, `criado_em_utc`. **Impede desativação** se houver `alunos_responsaveis` ativos |
| `trg_proteger_aluno_matriculas` | (Referenciado no EF mas não criado em V011 — trigger futuro) |

### Validações do Domínio (`Aluno.cs`)

- CPF: 11 dígitos ou nulo
- Nome: trim automático
- Data nascimento: não futura (usa `dataCivilAtual` como parâmetro)
- `AtualizarDados()`: altera NomeCompleto, DataNascimento, Telefone, Email
- `Desativar()`: valida se há vínculos ativos (trigger também bloqueia)

### Campos Realmente Editáveis

| Campo | Via | Observação |
|-------|-----|------------|
| NomeCompleto | `AtualizarDados()` | ✅ |
| DataNascimento | `AtualizarDados()` | ✅ |
| Telefone | `AtualizarDados()` | ✅ |
| Email | `AtualizarDados()` | ✅ |
| CPF | ⚠️ | Não expor na edição — LGPD. Se necessário, criar fluxo separado |
| Ativo | `Desativar()`/`Ativar()` | Com validação de vínculos |
| UsuarioId | `AlterarUsuario()` | Interno, não expor |

---

## 2. Modelagem de Responsável

### Tabela `responsaveis` (V011)

| Coluna | Tipo | Editável? | Observação |
|--------|------|-----------|------------|
| `id` | uuid | ❌ Imutável | |
| `organizacao_id` | uuid | ❌ Imutável | |
| `nome_completo` | text | ✅ Sim | CK: não vazio |
| `cpf` | text | ⚠️ Cuidado | Unique parcial |
| `telefone` | text | ✅ Sim | |
| `email` | text | ✅ Sim | |
| `usuario_id` | uuid | ⚠️ Limitado | |
| `ativo` | boolean | ✅ Sim | |
| `criado_em_utc` | timestamptz | ❌ Imutável | |
| `atualizado_em_utc` | timestamptz | ✅ Automático | |

### Triggers (V011)

| Trigger | O que faz |
|---------|-----------|
| `trg_proteger_responsavel` | Bloqueia mudança em `id`, `organizacao_id`, `criado_em_utc`. Impede desativação se houver vínculos ativos |

### Validações do Domínio (`Responsavel.cs`)

- Pelo menos um de telefone ou email obrigatório (`ck_responsaveis_contato_obrigatorio`)
- CPF: 11 dígitos ou nulo
- `AtualizarDados()`: altera NomeCompleto, CPF, Telefone, Email
- `Desativar()`: valida vínculos ativos

---

## 3. Modelagem de AlunoResponsavel

### Tabela `alunos_responsaveis` (V011)

| Coluna | Tipo | Editável? | Observação |
|--------|------|-----------|------------|
| `id` | uuid | ❌ Imutável | |
| `organizacao_id` | uuid | ❌ Imutável | |
| `aluno_id` | uuid | ❌ Imutável | FK |
| `responsavel_id` | uuid | ❌ Imutável | FK |
| `tipo_relacao` | text | ✅ Sim | CK: enum válido |
| `descricao_relacao` | text | ✅ Sim | CK: obrigatório se Outro |
| `principal_contato` | boolean | ✅ Sim | Unique parcial: 1 por aluno |
| `responsavel_financeiro` | boolean | ✅ Sim | |
| `ativo` | boolean | ✅ Sim | Via `Ativar()`/`Desativar()` |
| `criado_em_utc` | timestamptz | ❌ Imutável | |
| `atualizado_em_utc` | timestamptz | ✅ Automático | |

### Triggers (V011)

| Trigger | O que faz |
|---------|-----------|
| `trg_proteger_aluno_responsavel` | Bloqueia mudança em colunas de identidade. Valida que ambos (Aluno e Responsável) estão ativos antes de permitir vínculo ativo. Usa `FOR UPDATE` locks |

### Restrições Importantes

- **Unique parcial**: Um único `principal_contato = true` por aluno
- **Unique**: `(organizacao_id, aluno_id, responsavel_id)` — não duplicar vínculo
- **Validação**: `descricao_relacao` obrigatória quando `tipo_relacao = 'Outro'`
- **Validação**: `descricao_relacao` deve ser nula para outros tipos
- **Desativação**: Não desativar se for o único `principal_contato` ativo (via trigger)

### Operações de UI Necessárias

| Operação | Descrição |
|----------|-----------|
| Adicionar responsável | Criar novo Responsável + vincular ao Aluno |
| Vincular responsável existente | Vincular Responsável já cadastrado na Organização |
| Alterar classificação | Mudar TipoRelacao, DescricaoRelacao |
| Trocar PrincipalContato | Marcar/desmarcar (respeitando unique parcial) |
| Marcar/desmarcar ResponsavelFinanceiro | Toggle simples |
| Inativar vínculo | `Desativar()` no AlunoResponsavel |
| Reativar vínculo | `Ativar()` no AlunoResponsavel |

**Importante**: Editar a pessoa Responsável ≠ Editar o vínculo. São telas/fluxos diferentes.

---

## 4. Autorização

### Regra Atual

`PodeGerenciarMatriculas` em `GovernancaOperacionalUnidade`:
```csharp
public bool PodeGerenciarMatriculas =>
    EhAdministradorUnidade || EhAdministradorRede && !PossuiFranqueadoAtivo;
```

### Análise: Criar `PodeGerenciarAlunos`?

**Argumentos a favor:**
- Semântica diferente: "gerenciar cadastro" ≠ "gerenciar contrato"
- Futuro: secretária pode alterar telefone mas não negociar matrícula
- Clareza no código

**Argumentos contra:**
- Hoje, quem pode gerenciar matrículas é exatamente quem pode gerenciar alunos
- Adicionar mais uma propriedade aumenta complexidade sem benefício imediato
- Pode ser adicionado depois quando surgir necessidade real

**Recomendação: NÃO criar `PodeGerenciarAlunos` agora.**
Reutilizar `PodeGerenciarMatriculas` para o módulo Alunos. Documentar que, se no futuro surgir a necessidade de permissão separada, basta adicionar uma nova propriedade ao `GovernancaOperacionalUnidade`.

### Regra de Dados

Aluno pertence à **Organização**, não à Unidade.
A listagem local nascerá de:
```
Unidade → Matrícula → Aluno
```

`AdministradorUnidade` NÃO pode enumerar todos os Alunos da Organização.
`AdministradorRede` pode ter visão organizacional ampla, mas isso não autoriza busca global para AdminUnidade.

---

## 5. Menu da Unidade

### Ordem Atual

```
Visão Geral
Professores
Turmas
Planos
Matrículas
Contrato
```

### Ordem Proposta

```
Visão Geral
Professores
Turmas
Alunos        ← NOVO
Matrículas
Planos
Contrato
```

**Justificativa:**
- Alunos fica entre Turmas e Matrículas porque é o conceito "meio": aluno existe porque tem matrícula, e matrícula é de um aluno
- Professores → Turmas → Alunos segue a hierarquia operacional: quem ensina → o que ensina → para quem
- Matrículas → Planos → Contrato segue a hierarquia comercial: vínculo → condições → documento
- Planos sobe antes de Contrato porque plano é condição comercial, contrato é o documento final

---

## 6. Listagem de Alunos

### Fonte de Dados

```sql
SELECT DISTINCT ON (a.id)
    a.id, a.nome_completo, a.data_nascimento, a.telefone, a.email,
    m.id AS matricula_id, m.status, m.data_inicio, m.data_fim_prevista,
    pv.nome AS plano, pv.frequencia_semanal
FROM matriculas m
JOIN alunos a ON a.id = m.aluno_id AND a.organizacao_id = m.organizacao_id
JOIN planos_versoes pv ON pv.id = m.plano_versao_id
WHERE m.unidade_id = @unidadeId
  AND m.organizacao_id = @organizacaoId
ORDER BY a.id, m.data_inicio DESC;
```

**Regra**: Um aluno aparece apenas uma vez, mesmo com várias matrículas.

### Colunas da Listagem

| Coluna | Observação |
|--------|------------|
| Nome | `nome_completo` |
| Idade | Calculada de `data_nascimento` |
| Contato | Telefone ou Email (máscara) |
| Situação | Badge: Ativo/Inativo |
| Matrícula Ativa | Se houver, mostrar plano e status |
| Ação | Ver detalhes |

### Padrão Visual

Seguir `UI-ADMIN-STANDARDS.md`:
- Desktop: tabela com colunas
- Mobile: cards
- Filtro por texto (nome)
- Badge de status
- Ação "Ver detalhes"

---

## 7. Detalhe do Aluno

### Estrutura Proposta

```
┌─────────────────────────────────────────────────────────┐
│ Aluno · Luisa Pires                                     │
│ Dados cadastrais e vínculos                             │
├─────────────────────────────────────────────────────────┤
│                                                         │
│ [ DADOS DO ALUNO ]                    [ Editar dados ]  │
│ Nome: Luisa Pires                                       │
│ Nascimento: 15/03/2010 (16 anos)                        │
│ CPF: ***.***.***-78                                      │
│ Telefone: (15) 99999-0000                               │
│ Email: luisa@email.com                                  │
│ Situação: Ativo                                         │
│                                                         │
│ [ RESPONSÁVEIS ]                   [ Gerenciar ]        │
│ Maria da Silva — Mãe · Principal contato · Financeiro   │
│电话: (15) 98888-0000 · Email: maria@email.com          │
│                                                         │
│ José da Silva — Pai · Financeiro                        │
│ Telefone: (15) 97777-0000                               │
│                                                         │
│ [ MATRÍCULA ATUAL ]                  [ Ver matrícula ]  │
│ Plano: Trimestral 2x                                    │
│ Status: Ativa                                           │
│ Início: 01/09/2026 · Término: 30/11/2026               │
│                                                         │
│ [ HISTÓRICO DE MATRÍCULAS ]                             │
│ • 01/03/2026 a 31/08/2026 — Mensal 2x — Encerrada      │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### Seções

| Seção | Dados | Ação |
|-------|-------|------|
| Dados do Aluno | Nome, Nascimento, CPF (mascarado), Telefone, Email, Situação | Editar dados |
| Responsáveis | Lista de vínculos ativos com classificação | Gerenciar responsáveis |
| Matrícula Atual | Plano, Status, Datas | Ver matrícula (link para Detalhes da Matrícula) |
| Histórico de Matrículas | Lista ordenada por data | Ver matrícula |

### Regra Importante

A tela do Aluno **não substitui** o Detalhe da Matrícula.
São conceitos diferentes:
- **Aluno** = pessoa/cadastro
- **Matrícula** = contrato/vínculo com a Unidade

---

## 8. Editar Aluno

### Campos Editáveis

| Campo | Editável | Observação |
|-------|----------|------------|
| NomeCompleto | ✅ | |
| DataNascimento | ✅ | |
| Telefone | ✅ | |
| Email | ✅ | |
| CPF | ❌ LGPD | Não expor na edição |

### Campos NÃO Editáveis (nunca pela tela do Aluno)

- Plano
- Preço contratado
- Taxa
- Grade
- DataInicio da Matrícula
- DataFimPrevista da Matrícula

Esses campos são editáveis apenas pelas telas de Matrícula.

---

## 9. LGPD

| Regra | Implementação |
|-------|---------------|
| CPF mascarado | Exibir `***.***.***-XX` fora da edição |
| CPF em querystring | Nunca — usar GUID |
| PII em logs | Nunca logar CPF, telefone, email |
| Cross-unit | Validação server-side: aluno só aparece via matrícula da unidade |
| Busca local | Sem enumeração organizacional |
| Mínimo necessário | Só expor dados necessários para a operação |

---

## 10. Necessidade de Migration

**NENHUMA migration necessária.**

Todas as tabelas, triggers, constraints e índices já existem em V011.
O módulo Alunos é puramente de aplicação (controllers, views, services, queries).

Se no futuro for necessário algum campo novo (ex: `foto_perfil`), será criada nova migration.

---

## 11. Rotas Propostas

| Método | Rota | Ação |
|--------|------|------|
| GET | `/unidade/{unidadeId}/alunos` | Listar alunos |
| GET | `/unidade/{unidadeId}/alunos/{alunoId}` | Detalhes do aluno |
| GET | `/unidade/{unidadeId}/alunos/{alunoId}/editar` | Formulário de edição |
| POST | `/unidade/{unidadeId}/alunos/{alunoId}/editar` | Salvar edição |
| GET | `/unidade/{unidadeId}/alunos/{alunoId}/responsaveis` | Gerenciar responsáveis |
| POST | `/unidade/{unidadeId}/alunos/{alunoId}/responsaveis/adicionar` | Adicionar/vincular responsável |
| POST | `/unidade/{unidadeId}/alunos/{alunoId}/responsaveis/{vinculoId}/editar` | Editar classificação |
| POST | `/unidade/{unidadeId}/alunos/{alunoId}/responsaveis/{vinculoId}/inativar` | Inativar vínculo |
| POST | `/unidade/{unidadeId}/alunos/{alunoId}/responsaveis/{vinculoId}/reativar` | Reativar vínculo |

---

## 12. Riscos

| Risco | Mitigação |
|-------|-----------|
| Trigger `trg_proteger_aluno` bloqueia desativação se houver vínculos ativos | UI deve listar vínculos antes de permitir desativação |
| Trigger `trg_proteger_aluno_responsavel` valida que ambos estão ativos | UI deve validar antes de submeter |
| Unique parcial `principal_contato` | UI deve sugerir troca, não criar duplicata |
| Aluno sem matrícula na unidade não deve aparecer | Query filtra por `matriculas.unidade_id` |
| CPF único por organização | Tratar erro de constraint com mensagem amigável |

---

## 13. Testes

### Unitários

- Validação de domínio: Aluno, Responsavel, AlunoResponsavel
- ViewModelMapper: mapeamento correto

### Integração

- Listagem de alunos respeita filtro por unidade
- Detalhe do aluno retorna dados corretos
- Edição de dados cadastrais
- CRUD de responsáveis com validação de triggers
- Cross-unit: aluno de outra unidade não aparece

### Manuais

- Navegação completa: Listar → Detalhes → Editar
- Gerenciar responsáveis: adicionar, editar, inativar, reativar
- Validação de triggers: desativar com vínculos ativos
- LGPD: CPF mascarado, sem PII em URLs

---

## 14. Critérios de Aceite

- [ ] Menu "Alunos" visível para AdministradorUnidade
- [ ] Listagem mostra apenas alunos com matrícula na unidade
- [ ] Um aluno aparece apenas uma vez (DISTINCT)
- [ ] Detalhes mostra todas as seções (Dados, Responsáveis, Matrícula, Histórico)
- [ ] Edição altera apenas campos permitidos
- [ ] CPF mascarado na listagem e detalhes
- [ ] Gerenciar responsáveis permite todas as operações
- [ ] Validações de trigger funcionam (sem erro 500)
- [ ] LGPD respeitada (sem PII em logs/URLs)
- [ ] Build 0 erros, 0 warnings
- [ ] Todos os testes existentes continuam passando

---

## 15. Divisão em Etapas

### ETAPA 1 — Menu + Listagem + Detalhe do Aluno

**Status:** ✅ Concluído (2026-09-03)

**Arquivos criados/modificados:**
- `GovernancaOperacionalUnidade.cs` — Adicionada propriedade `PodeGerenciarAlunos`
- `_UnidadeNavLinks.cshtml` — Adicionado menu "Alunos" entre Turmas e Matrículas
- `AlunosUnidade.cs` (Application) — Serviço de consulta com `ListarAsync` e `ObterAsync`
- `AlunosRepositorio.cs` (Infrastructure) — Consultas com dedup por aluno e cross-unit validation
- `AlunoViewModels.cs` (Web) — ViewModels e mapper com mascaramento de CPF
- `AlunosController.cs` (Web) — Controller thin com GET Index e GET Detalhes
- `Index.cshtml` — Listagem desktop (tabela) e mobile (cards)
- `Detalhes.cshtml` — Detalhe com seções: Dados, Responsáveis, Matrícula Atual, Histórico

**Build:** 0 erros, 0 warnings
**Testes:** 484 unitários + 187 integração aprovados

**Escopo:**
- Adicionar "Alunos" ao menu (`_UnidadeNavLinks.cshtml`)
- Criar `AlunosController.cs`
- Criar `AlunosListaViewModel` e `AlunoDetalheViewModel`
- Criar service de consulta `AlunosUnidadeConsulta`
- Criar repositório de consulta
- Criar `Index.cshtml` (listagem)
- Criar `Detalhes.cshtml` (detalhe)
- Testes unitários e de integração

**Arquivos:**
- `AlunosController.cs` (novo)
- `AlunoViewModels.cs` (novo)
- `AlunosUnidade.cs` (novo — Application)
- `AlunosRepositorio.cs` (novo — Infrastructure)
- `_UnidadeNavLinks.cshtml` (modificado)
- `Index.cshtml` (novo)
- `Detalhes.cshtml` (novo)
- Testes correspondentes

### ETAPA 2 — Editar Dados Cadastrais

**Status:** ✅ Concluído (2026-09-03)

**Decisões desta tarefa:**
- CPF é dado protegido; aparece mascarado como informação somente leitura; o Domain não oferece correção de CPF via `AtualizarDados()`; eventual correção futura exige decisão explícita
- Alteração de DataNascimento valida se o aluno permanece adulto OU possui Responsável ativo para cada Matrícula ativa da Organização; histórico não bloqueia correção legítima
- `PodeGerenciarAlunos` é a governança utilizada; mesma regra de `PodeGerenciarMatriculas`
- Tenant-safe: Aluno validado via Matrícula (Unidade → Matrícula → Aluno); cross-unit rejeitado
- PRG: POST → Redirect Detalhes; TempData["Sucesso"] para mensagem

**Escopo:**
- Criar `EditarAlunoViewModel` (campos editáveis + CPF mascarado read-only)
- Criar `AtualizarDadosAlunoAsync` no `AlunosServico`
- Adicionar `ObterParaEdicaoAsync`, `VerificarMatriculasAtivasComAlunoAsync`, `VerificarResponsaveisAtivosAsync` no `AlunosRepositorio`
- Adicionar actions GET/POST `Editar` no `AlunosController`
- Criar `Editar.cshtml` (formulário)
- Adicionar botão [ Editar dados ] no `Detalhes.cshtml`
- Testes unitários e de integração

**Arquivos:**
- `AlunosUnidade.cs` (modificado — Application)
- `AlunosRepositorio.cs` (modificado — Infrastructure)
- `AlunosController.cs` (modificado — Web)
- `AlunoViewModels.cs` (modificado — Web)
- `Editar.cshtml` (novo — Views/Alunos)
- `Detalhes.cshtml` (modificado — botão Editar)
- Testes correspondentes

**Regras de DataNascimento:**
- Se nova DataNascimento mantém aluno adulto para todas as Matrículas ativas → permitir
- Se nova DataNascimento torna aluno menor para alguma Matrícula ativa → exigir AlunoResponsavel ativo + Responsavel ativo
- Se não possui Responsável ativo → rejeitar com mensagem amigável
- Matrículas encerradas/canceladas não bloqueiam correção

### ETAPA 3 — Gerenciar Responsáveis

**Escopo:**
- Criar `Responsaveis.cshtml` (gestão de vínculos)
- Adicionar actions de CRUD no controller
- Adicionar operações no service
- Validações de trigger
- Testes

**Arquivos:**
- `AlunosController.cs` (modificado)
- `AlunoViewModels.cs` (modificado)
- `AlunosUnidade.cs` (modificado)
- `Responsaveis.cshtml` (novo)
- Testes correspondentes

---

## 16. Próximos Passos

1. Aprovar este plano
2. Iniciar ETAPA 1
3. Build + testes
4. Revisão
5. Commit
6. Avançar para ETAPA 2
