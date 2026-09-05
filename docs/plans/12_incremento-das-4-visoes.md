# Plano: Incremento das 4 Visões (Áreas)

## Status

**Planejado**

## Contexto

O BFA possui 4 áreas de atuação com diferentes públicos:
1. **Franqueadora** — Rede BFA (acesso total)
2. **Unidade** — Franqueado (sua unidade)
3. **Aluno** — Alunos (próprios dados)
4. **Professor** — Professores (próprias turmas/auntas)

## Decisões tomadas

- **Login do aluno:** Email ou CPF (campo único aceita os dois)
- **Professor:** Área separada com suas turmas, aulas, alunos, frequência
- **Franqueadora:** Acesso total a todas as unidades, controle de padrão da rede

---

## 1. Login do Aluno — Email ou CPF

### O que mudar

| Componente | Mudança |
|------------|---------|
| `ContaController` | Campo de login aceita email ou CPF |
| `DestinoPosLogin` | Aluno logado via email/CPF → `/aluno/{unidadeId}` |
| `UsuarioIdentity` | Aluno pode ter `NormalizedEmail = CPF` se não tiver email |
| View de login | Campo "Email ou CPF" em vez de "Email" |

### Fluxo

```
Aluno digita "joao@email.com" ou "123.456.789-00"
        │
Backend tenta:
1. Buscar por email → se encontrar, faz login
2. Se não, buscar por CPF → se encontrar, faz login
3. Se não, erro "Conta não encontrada"
        │
Redireciona para /aluno/{unidadeId}
```

### Regras

- CPF deve ser aceito com ou sem máscara (12345678900 ou 123.456.789-00)
- Backend normaliza CPF (remove pontuação) antes de buscar
- Aluno pode ter email E CPF, mas login aceita qualquer um dos dois
- Se aluno não tem email, o campo email fica vazio e ele loga só com CPF

---

## 2. Área do Professor — Expansão

### Hoje existe

- Selecionar Unidade
- Minhas Turmas (lista básica)

### O que criar

| Funcionalidade | Prioridade | Descrição |
|----------------|-----------|-----------|
| Dashboard | Alta | Resumo do dia: aulas de hoje, próximas aulas, total de alunos |
| Minhas Aulas | Alta | Lista de aulas com data, horário, turma, status |
| Registrar Frequência | Alta | Chamada dos alunos em cada aula |
| Alunos por Turma | Média | Ver lista de alunos de cada turma |
| Meu Perfil | Média | Dados pessoais, remuneração, horários |
| Horário Semanal | Média | Grade horária do professor |

### Controllers

```
BFA.Web.Areas.Professor.Controllers/
├── InicioController.cs          (já existe — Dashboard)
├── AulasController.cs           (NOVO — minhas aulas, frequência)
├── TurmasController.cs          (já existe — expandir)
└── PerfilController.cs          (NOVO — dados pessoais)
```

### Views

```
BFA.Web.Areas.Professor.Views/
├── Inicio/
│   ├── Dashboard.cshtml         (NOVO)
│   └── SelecionarUnidade.cshtml (já existe)
├── Aulas/
│   ├── Index.cshtml             (NOVO — lista de aulas)
│   ├── Detalhe.cshtml           (NOVO — detalhe da aula)
│   └── Chamada.cshtml           (NOVO — registrar frequência)
├── Turmas/
│   ├── Index.cshtml             (já existe — expandir)
│   ├── Detalhe.cshtml           (já existe — expandir com alunos)
│   └── _Horarios.cshtml         (já existe)
├── Perfil/
│   └── Index.cshtml             (NOVO — dados pessoais)
└── Shared/
    ├── _ProfessorLayout.cshtml  (já existe)
    └── _ProfessorNavLinks.cshtml (atualizar)
```

### NavLinks atualizado

1. Dashboard → `/professor/unidade/{unidadeId}`
2. Minhas Aulas → `/professor/unidade/{unidadeId}/aulas`
3. Minhas Turmas → `/professor/unidade/{unidadeId}/turmas`
4. Meu Perfil → `/professor/unidade/{unidadeId}/perfil`

---

## 3. Área da Franqueadora — Controle da Rede

### Hoje existe

- Unidades (CRUD)
- Franqueados (CRUD)
- Planos da Rede (CRUD)
- Contratos (CRUD)
- Usuários (CRUD)
- Acessos por Unidade (CRUD)

### O que criar

| Funcionalidade | Prioridade | Descrição |
|----------------|-----------|-----------|
| Dashboard da Rede | Alta | Visão consolidada: total de alunos, unidades ativas, faturamento |
| Controle de Padrão | Alta | Verificar se unidades seguem padrão da rede (grade, planos, etc.) |
| Alunos da Rede | Alta | Ver todos os alunos de todas as unidades |
| Relatórios da Rede | Média | Inadimplência consolidada, crescimento, ranking de unidades |
| Financeiro da Rede | Média | Repasse de royalties, faturamento por unidade |
| Configurações | Baixa | Percentuais de royalty, regras gerais |

### Controllers

```
BFA.Web.Areas.Franqueadora.Controllers/
├── InicioController.cs          (já existe — expandir com dashboard consolidado)
├── UnidadesController.cs        (já existe)
├── FranqueadosController.cs     (já existe)
├── PlanosController.cs          (já existe)
├── ContratosController.cs       (já existe)
├── UsuariosController.cs        (já existe)
├── AcessosUnidadeController.cs  (já existe)
├── AlunosController.cs          (NOVO — ver alunos da rede)
├── RelatoriosController.cs      (NOVO — relatórios consolidados)
└── FinanceiroController.cs      (NOVO — repasse royalties)
```

### Views novas

```
BFA.Web.Areas.Franqueadora.Views/
├── Alunos/
│   ├── Index.cshtml             (NOVO — lista consolidada)
│   └── Detalhes.cshtml          (NOVO — detalhe do aluno)
├── Relatorios/
│   ├── Index.cshtml             (NOVO — painel de relatórios)
│   └── Rede.cshtml              (NOVO — métricas da rede)
├── Financeiro/
│   ├── Index.cshtml             (NOVO — repasse royalties)
│   └── Detalhes.cshtml          (NOVO — detalhe por unidade)
```

### NavLinks atualizado

1. Visão Geral → `/franqueadora`
2. Usuários → `/franqueadora/usuarios`
3. Unidades → `/franqueadora/unidades`
4. Franqueados → `/franqueadora/franqueados`
5. Alunos da Rede → `/franqueadora/alunos` (NOVO)
6. Planos da Rede → `/franqueadora/planos`
7. Relatórios → `/franqueadora/relatorios` (NOVO)
8. Financeiro → `/franqueadora/financeiro` (NOVO)

---

## 4. Área do Aluno — Melhorias

### Hoje existe

- Dashboard (read-only)
- Perfil (read-only)
- Matrículas (read-only)
- Agenda (read-only)
- Frequência (read-only)
- Financeiro (read-only)

### O que criar

| Funcionalidade | Prioridade | Descrição |
|----------------|-----------|-----------|
| Pagamento Online | Alta | Pix/cartão via Pagar.me |
| Alterar Senha | Média | Trocar senha atual |
| Notificações | Média | Mensagens da unidade |
| Sair / Trocar Unidade | Baixa | Se aluno estiver em mais de uma unidade |

---

## 5. Ordem de implementação sugerida

| Fase | Escopo | Status |
|------|--------|--------|
| **1** | Login do Aluno (Email/CPF) | Pendente |
| **2** | Área do Professor (Dashboard, Aulas, Frequência) | Pendente |
| **3** | Franqueadora — Dashboard consolidado + Alunos da Rede (paginação) | Concluído |
| **4** | Franqueadora — Relatórios consolidados | Pendente |
| **4b** | Franqueadora — Financeiro (repasse royalties) | Adiado — definir modelo depois |
| **5** | Aluno — Pagamento Online | Pendente |
| **6** | Aluno — Notificações, Alterar Senha | Pendente |

---

## Critérios de aceite

- [ ] Aluno consegue logar com email ou CPF
- [ ] Professor tem área completa com aulas, turmas e frequência
- [ ] Franqueadora vê todos os alunos da rede
- [ ] Franqueadora vê relatórios consolidados
- [ ] `dotnet build` sem erros
- [ ] `dotnet test` passando
