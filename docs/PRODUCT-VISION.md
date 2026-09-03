# Visão de Produto — BFA Platform

**Status:** referência obrigatória de produto e arquitetura  
**Produto:** BFA — Brazilian Footvolley Academy  
**Escopo:** visão conceitual, públicos, responsabilidades e evolução modular

---

## 1. Propósito e caráter normativo

Este documento define o que é a BFA Platform, qual problema ela resolve, quais públicos atende e como seus principais contextos de negócio se relacionam. Ele é referência obrigatória para decisões futuras de produto e arquitetura.

A BFA Platform é uma plataforma de gestão de rede de franquias de futevôlei. Seus dois grandes objetivos são:

1. permitir que a Franqueadora administre a rede como um todo;
2. permitir que cada franquia ou Unidade opere o próprio negócio utilizando a mesma plataforma.

O problema central resolvido é a fragmentação entre a governança da rede e a operação cotidiana das Unidades. A plataforma cria uma base comum para controlar relações comerciais, acessos e indicadores da Franqueadora sem perder o isolamento e as necessidades operacionais de cada Unidade.

Portanto, o produto não é apenas um gestor de alunos, um gestor de aulas ou um sistema de academia. A definição correta é: **plataforma de gestão da rede de franquias e de operação das Unidades**.

Este documento complementa, sem substituir:

- `docs/ARCHITECTURE.md`, que define a arquitetura técnica;
- `brand/guide/brand-guide.md`, que define a identidade da marca;
- `docs/UI-ADMIN-STANDARDS.md`, que define o padrão das interfaces administrativas.

As regras visuais e os elementos de marca permanecem exclusivamente em seus documentos próprios e não são duplicados aqui.

## 2. Visão geral do produto

A plataforma atende, progressivamente, a Franqueadora e seus administradores de rede, Franqueados, administradores de Unidade, Professores, Alunos e Responsáveis. Cada público possui responsabilidades e experiências próprias dentro da mesma rede:

```text
Organização / Rede BFA
├── Franqueadora
│   ├── Franqueados e contratos
│   ├── Unidades e acessos
│   └── visão consolidada da rede
│
└── Unidades / operações locais
    ├── administração local
    ├── Professores
    ├── Alunos e Responsáveis
    └── operação acadêmica e financeira local
```

Identidade técnica, autorização contextual e entidades de negócio têm responsabilidades distintas. Uma pessoa pode ocupar mais de um papel na rede, sem que esses conceitos sejam fundidos no modelo.

## 3. Franqueadora e gestão da rede

A Área da Franqueadora representa a visão global da Organização e deve apoiar, progressivamente:

- gestão das Unidades;
- gestão de Franqueados;
- gestão de usuários administrativos;
- gestão de acessos;
- contratos de franquia;
- documentos contratuais;
- royalties;
- mensalidades e taxas da franquia;
- vigência e situação contratual;
- visão e indicadores consolidados da rede;
- relatórios consolidados;
- acompanhamento financeiro da relação entre Franqueadora e franquia.

O perfil `AdministradorRede` possui visão transversal somente dentro da própria Organização. Esse perfil não concede acesso implícito a outra Organização.

## 4. Franqueado como entidade de negócio

`Franqueado` é uma entidade de negócio que representa a relação comercial e legal de uma pessoa ou empresa com a BFA. Não é apenas um `PerfilAcesso` e não deve ser modelado como sinônimo de usuário do sistema.

Dados futuros possíveis incluem:

- nome ou razão social;
- nome fantasia;
- classificação como pessoa física ou jurídica;
- CPF ou CNPJ;
- telefone;
- e-mail;
- e-mail financeiro;
- endereço;
- responsável legal;
- observações;
- status;
- Unidades relacionadas.

Um Franqueado poderá estar associado a uma ou mais Unidades. A modelagem detalhada dessa associação será definida quando o módulo for aprovado para implementação.

## 5. Usuário, acesso e Franqueado

Os três conceitos devem permanecer explicitamente separados:

| Conceito | Responsabilidade |
|---|---|
| `UsuarioIdentity` | Autenticação, credenciais e acesso técnico ao sistema. |
| `VinculoAcesso` | Contextos e perfis que o usuário pode acessar. |
| `Franqueado` | Relação comercial, legal e contratual com a rede BFA. |

Exemplos:

```text
João
├── UsuarioIdentity
├── Franqueado
└── VinculoAcesso: AdministradorUnidade

Maria
├── UsuarioIdentity
└── VinculoAcesso: AdministradorUnidade
```

João pode ser simultaneamente usuário, Franqueado e administrador de Unidade. Maria pode administrar uma Unidade sem ser Franqueada. Futuras implementações devem preservar essa distinção.

## 6. Unidade e operação local

Cada `Unidade` representa uma operação local da rede e pertence a uma `Organizacao`. A plataforma deverá apoiar a Unidade na operação de:

- usuários administrativos;
- Professores;
- Alunos;
- Responsáveis;
- Turmas;
- agenda;
- aulas;
- Presenças;
- Matrículas;
- planos;
- Cobranças;
- pagamentos on-line;
- Relatórios;
- informações operacionais da Unidade.

`AdministradorUnidade` possui acesso somente às Unidades indicadas por seus `VinculoAcesso` ativos. Um mesmo usuário pode administrar mais de uma Unidade, desde que cada contexto esteja autorizado.

### 6.1 Planos comerciais dos Alunos

Os planos comerciais destinados aos Alunos possuem identidade estável e condições versionadas.
Um `Plano` pode ser definido para toda a rede BFA ou exclusivamente para uma Unidade, enquanto
cada `PlanoVersao` preserva duração, frequência semanal, mensalidade, eventual taxa de matrícula
e período de vigência.

A frequência semanal representa o direito comercial contratado e não vincula automaticamente o
Aluno a uma Turma ou horário. A `Matricula` guarda a versão exata e os valores contratados para que
mudanças posteriores de preço ou condições não alterem o histórico do Aluno.

A Grade operacional pertence à `Matricula`. Cada `MatriculaHorario` representa uma sessão
recorrente semanal efetivamente escolhida e aponta para o snapshot de `TurmaHorario`. Assim, uma
frequência 2x permite dois slots, inclusive no mesmo dia quando os horários não conflitam; ela não
significa obrigatoriamente dois dias diferentes. A vigência de cada vínculo preserva trocas de
Grade sem reescrever o passado.

A capacidade é controlada por `TurmaHorario`, usando `Turma.Capacidade` como limite de cada slot.
O mesmo Aluno não pode ocupar horários recorrentes sobrepostos, mesmo quando suas Matrículas
pertencem a Unidades diferentes da mesma Organização. Inclusões concorrentes disputando a última
vaga e conflitos globais do Aluno são serializados no backend e definitivamente protegidos no
PostgreSQL.

Alterações materiais em horários exigem decisão explícita sobre os Alunos afetados. Ajustar a
Grade fecha o vínculo antigo e cria outro; não troca silenciosamente o horário de um registro
histórico. Uma troca apenas de Professor migra os vínculos abertos um a um quando o slot material
é preservado, sempre na mesma transação. A evolução acadêmica seguirá
`Grade -> Aula -> Presença`: a Grade define recorrência, Aula será uma ocorrência concreta e
Presença registrará participação nessa ocorrência.

A Franqueadora governa planos de rede, versões comerciais e a disponibilidade desses planos por
Unidade. A Unidade apenas consome uma disponibilidade ativa ao matricular. Plano, Matrícula e
Cobrança permanecem conceitos distintos: a existência de um plano não cria matrícula, parcela,
cobrança ou pagamento automaticamente.

## 7. Professor

`Professor` será uma entidade de negócio própria e poderá possuir um `UsuarioIdentity` associado quando precisar acessar o sistema.

Sua experiência prevista inclui:

- agenda;
- aulas do dia ou período;
- Turmas;
- Alunos participantes;
- confirmação de presença;
- chamada;
- demais informações necessárias à operação da aula.

O perfil `Professor` não concede, por padrão, acesso administrativo global à Unidade. A autorização deve permanecer limitada aos contextos e capacidades aprovados para sua atividade.

## 8. Aluno

`Aluno` será uma entidade de negócio própria. Poderá possuir um `UsuarioIdentity` associado para acessar a Área do Aluno, mas não deve ser tratado apenas como um usuário do Identity.

Sua experiência prevista inclui:

- dados cadastrais;
- Matrícula;
- plano;
- Unidade;
- Turma;
- agenda;
- aulas;
- histórico;
- pagamentos;
- situação financeira;
- Campeonatos e eventos futuramente.

## 9. Responsável

`Responsavel` é uma entidade de negócio ligada ao Aluno por `AlunoResponsavel`. Na data inicial da
Matrícula, um Aluno menor precisa de ao menos um vínculo ativo com Responsável também ativo.

Sua possível experiência inclui:

- consultar o Aluno vinculado;
- visualizar aulas;
- acompanhar pagamentos;
- receber Cobranças;
- acessar somente as informações permitidas.

## 10. Contratos de franquia

Contratos de franquia pertencem ao domínio comercial da rede. Informações previstas incluem:

- número ou identificador do contrato;
- Franqueado;
- uma ou mais Unidades relacionadas;
- data de início;
- data de término;
- status;
- percentual de royalties;
- mensalidade fixa;
- taxa de adesão, quando aplicável;
- dia de vencimento;
- observações;
- documento do contrato;
- histórico futuro.

Royalties e mensalidade fixa podem coexistir e não são opções mutuamente exclusivas. Um contrato poderá prever, por exemplo, royalties de `8%` e mensalidade fixa de `R$ 500,00`.

Contratos e seus documentos exigem rastreabilidade. Alterações relevantes devem preservar o histórico necessário para auditoria e operação da relação comercial.

## 11. Documentos privados

Documentos contratuais devem ser armazenados de forma privada:

- contratos não devem ser gravados diretamente em `wwwroot`;
- documentos não devem ser expostos por URL pública direta;
- o banco deve armazenar apenas metadados e a referência ao arquivo;
- o acesso ao conteúdo deve passar por autenticação e autorização apropriadas;
- a infraestrutura de arquivos deve ser abstraída para permitir a troca futura entre armazenamento local privado e object storage.

Esta definição é diretriz para o futuro módulo de documentos e não autoriza sua implementação nesta etapa.

## 12. Contextos financeiros

Existem dois contextos financeiros diferentes, que não devem ser misturados na modelagem:

| Contexto | Exemplos |
|---|---|
| Franqueadora × Franquia | Royalties, mensalidades da franquia, taxas e Cobranças contratuais. |
| Unidade × Aluno | Matrícula, mensalidade ou plano, aulas, Cobranças, pagamento on-line e inadimplência. |

Todo pagamento ou cobrança deve possuir origem e contexto explícitos. Regras, relatórios e autorizações de um contexto não devem ser aplicados implicitamente ao outro.

## 13. Níveis de relatórios

Os Relatórios também possuem dois níveis distintos:

### Franqueadora

- visão consolidada da rede;
- Unidades;
- Franqueados;
- contratos;
- indicadores;
- financeiro consolidado.

### Unidade

- Alunos;
- Professores;
- Turmas;
- aulas;
- Presenças;
- financeiro;
- pagamentos;
- operação local.

O nível consolidado respeita o contexto da Organização; o nível local respeita simultaneamente Organização e Unidade.

## 14. Multi-tenancy e isolamento

A hierarquia fundamental é:

```text
Organizacao  -> representa a rede
└── Unidade  -> representa a operação local ou franquia
```

Toda funcionalidade de Unidade deve aplicar isolamento por `OrganizacaoId` e `UnidadeId`. Identificadores recebidos do browser ou de outro cliente nunca são, isoladamente, prova de autorização.

A autorização continua baseada em `VinculoAcesso`, resolvida e validada no servidor. Uma consulta ou operação de uma Unidade não pode expor, alterar ou inferir dados de outro tenant.

## 15. Perfis de acesso

Os perfis atuais são:

| Perfil | Experiência autorizável |
|---|---|
| `AdministradorRede` | Visão global e transversal da própria Organização. |
| `AdministradorUnidade` | Administração de uma ou mais Unidades explicitamente vinculadas. |
| `Professor` | Experiência operacional do Professor nos contextos autorizados. |
| `Aluno` | Experiência do Aluno. |
| `Responsavel` | Experiência futura ligada a Aluno ou dependente. |

Esses perfis representam autorização contextual e não devem usar `IdentityRole`. A existência de um perfil também não substitui a entidade de negócio correspondente.

## 16. Princípios de arquitetura do produto

Decisões futuras devem preservar os seguintes princípios:

- autenticação não é domínio de negócio;
- Identity permanece técnico;
- autorização é contextual;
- perfis não substituem entidades de negócio;
- Franqueado não é sinônimo de usuário;
- Professor não é sinônimo de usuário;
- Aluno não é sinônimo de usuário;
- Unidade pertence a uma Organização;
- dados nunca devem cruzar tenants;
- histórico relevante não deve ser perdido por `DELETE` físico;
- contratos e documentos exigem rastreabilidade;
- pagamentos devem ter origem e contexto explícitos.

Esses princípios complementam a separação de camadas, a persistência e as demais regras estabelecidas em `docs/ARCHITECTURE.md`.

## 17. Módulos previstos

Esta é a visão macro atual dos módulos do produto. A presença nesta lista não significa que o módulo já exista ou esteja autorizado para implementação.

### Franqueadora

- Dashboard
- Unidades
- Usuários
- Franqueados
- Acessos
- Contratos
- Documentos
- Royalties
- Cobranças
- Relatórios

### Unidade

- Dashboard
- Usuários
- Professores
- Alunos
- Responsáveis
- Turmas
- Agenda
- Aulas
- Presença
- Matrículas
- Planos
- Pagamentos
- Relatórios

### Professor

- Agenda
- Turmas
- Aulas
- Presença

### Aluno

- Perfil
- Matrícula
- Plano
- Agenda
- Aulas
- Pagamentos
- Histórico

## 18. Roadmap conceitual

A ordem conceitual atual, sem datas, é:

1. Fundação técnica
2. Autenticação
3. Autorização
4. Franqueadora
5. Unidades
6. Usuários
7. Franqueados
8. Contratos
9. Área da Unidade
10. Professores
11. Alunos
12. Turmas
13. Aulas / Agenda / Presença
14. Matrículas
15. Pagamentos
16. Relatórios
17. Campeonatos / Loja / expansões futuras

O roadmap pode evoluir conforme novas decisões de produto. Mudanças de ordem, prioridade ou escopo devem continuar respeitando a arquitetura e ser aprovadas antes da implementação.

## 19. Limite do escopo imediato

Este documento descreve a visão de produto e não autoriza a implementação imediata de todos os módulos citados.

Não devem ser criados automaticamente módulos, entidades, tabelas, migrations, rotas, telas, APIs ou integrações apenas porque aparecem nesta visão. Toda implementação continua sendo realizada em etapas explicitamente aprovadas, com escopo próprio, validação arquitetural e testes adequados.
