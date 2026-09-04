# PROJECT-STATE.md — BFA Platform

**Última atualização:** 2026-09-04  
**Status:** Em desenvolvimento ativo  
**Branch:** feature/login-mvc  
**Testes:** 1.177 aprovados (484 unitários + 693 integração)  
**Build:** 0 erros, 0 warnings

## Visão do Produto

BFA — Brazilian Footvolley Academy é uma plataforma de gestão de rede de franquias de futevôlei. Suporta dois grandes contextos:

1. **Franqueadora/Rede** — gestão global da organização
2. **Unidade/Franquia** — operação local

Para detalhes completos: `docs/PRODUCT-VISION.md`

## Arquitetura

- **Estilo:** Monólito modular
- **Backend:** .NET 10, ASP.NET Core MVC + Razor
- **Banco:** PostgreSQL 17
- **ORM:** EF Core 10 + Npgsql (apenas runtime)
- **Migrations:** SQL manual versionado (NÃO EF Migrations)

Para detalhes completos: `docs/ARCHITECTURE.md` e `docs/adr/`

## Estrutura do Projeto

```text
backend/
├── BFA.sln
├── src/
│   ├── BFA.Web/           # Executável principal
│   ├── BFA.Application/   # Casos de uso
│   ├── BFA.Domain/        # Entidades e regras
│   └── BFA.Infrastructure/ # Persistência e integrações
└── tests/
    ├── BFA.UnitTests/
    └── BFA.IntegrationTests/
```

## Módulos Implementados

### Franqueadora (Area: `/franqueadora`)
- Dashboard (`InicioController`)
- Unidades (`UnidadesController`)
- Usuários (`UsuariosController`)
- Franqueados (`FranqueadosController`)
- Acessos por Unidade (`AcessosUnidadeController`)
- Contratos (`ContratosController`)
- Planos da Rede (`PlanosController`)
- Localidades (`LocalidadesController`)

### Unidade (Area: `/unidade/{unidadeId}`)
- Dashboard (`InicioController`)
- Professores (`ProfessoresController`)
- Turmas (`TurmasController`)
- Planos Locais (`PlanosController`)
- Matrículas (`MatriculasController`) — CRUD completo com Grade
- Alunos (`AlunosController`) — Listagem + Detalhe + Editar dados + Gerenciar Responsáveis
- Aulas (`AulasController`) — CRUD + Chamada + Frequência
- Contrato (read-only)

### Professor (Area: `/professor`)
- Dashboard (`InicioController`)
- Turmas (`TurmasController`)

### Aluno (Area: `/aluno`)
- Apenas scaffold vazio

### Autenticação e Autorização
- Identity completo (login, registro, recuperação)
- Sistema de Vinculos de Acesso (multi-tenant)
- Governança de Unidade centralizada
- Destino pós-login (`/acessar`)

## Modelo de Autorização

Perfis (via `VinculoAcesso`, NÃO `IdentityRole`):
- `AdministradorRede` — acesso global da Organização
- `AdministradorUnidade` — acesso às Unidades vinculadas
- `Professor` — acesso operacional nas Unidades vinculadas
- `Aluno` — futuro
- `Responsavel` — futuro

Franqueado é entidade comercial, NÃO perfil de acesso.

Governança centralizada via `IGovernancaOperacionalUnidade`:
- `PodeGerenciarTurmas`
- `PodeGerenciarPlanoLocal`
- `PodeGerenciarMatriculas`
- `PodeGerenciarAlunos`

## Banco de Dados

### Migrations Aplicadas (IMUTÁVEIS)

| Versão | Descrição |
|--------|-----------|
| V001 | Organizações e Unidades |
| V002 | Identidade (Identity) |
| V003 | Vínculos de Acesso |
| V004 | Usuários e Franqueados |
| V005 | Adequação CNPJ alfanumérico |
| V006 | Catálogo de Localidades |
| V007 | Contratos de Franquia |
| V008 | Professores e Remunerações |
| V009 | Turmas e Horários |
| V010 | Planos |
| V011 | Alunos e Responsáveis |
| V012 | Disponibilidade de Planos e Matrículas |
| V013 | Grade das Matrículas |
| V014 | Correção de validação de unidade na matrícula |
| V015 | Aulas e Presenças |

**Regra:** Migrations são imutáveis. Correções são novas migrations.

### Roles PostgreSQL

```text
bfa_app_role (NOLOGIN) → role de runtime
bfa_dev_app (LOGIN) → membro de bfa_app_role
```

DDL exclusivamente por `bfa_dev_deploy`. Aplicação é apenas DML.

## Trabalho Não Commitado (2026-09-03)

### Arquivos Modificados
- `MatriculasController.cs` — ações de Alterar Grade, Encerrar, Cancelar
- `Detalhes.cshtml` — botões de ação
- `Index.cshtml` — ajustes na listagem
- `MatriculaViewModels.cs` — ViewModels para AlterarGrade e Finalizar
- `unidade.css` — estilos para matrículas
- `bfa-matricula-wizard.js` — scripts de grade
- `AreaUnidadeMatriculasEndpointTests.cs` — testes de endpoint
- `GovernancaOperacionalUnidade.cs` — adicionada `PodeGerenciarAlunos`
- `_UnidadeNavLinks.cshtml` — adicionado menu "Alunos"
- `Professores/Encerrar.cshtml` — validação `data-bfa-date-min`
- `AlunosUnidade.cs` — adicionados `AtualizarDadosAsync`, `ObterDadosEdicaoAsync`, DTOs
- `AlunosRepositorio.cs` — adicionados `ObterParaEdicaoAsync`, `PersistirAtualizacaoAsync`, etc.
- `AlunoViewModels.cs` — adicionados `EditarAlunoViewModel`, `EditarAlunoMapper`
- `AlunosController.cs` — adicionadas actions `Editar` GET/POST
- `Alunos/Detalhes.cshtml` — adicionado botão [ Editar dados ] e mensagem de sucesso

### Arquivos Novos (Não Rastreados)
- `AlterarGrade.cshtml` — tela de alteração de grade
- `Cancelar.cshtml` — tela de cancelamento
- `Encerrar.cshtml` — tela de encerramento
- `AlunosUnidade.cs` — Application layer (Alunos)
- `AlunosRepositorio.cs` — Infrastructure layer (Alunos)
- `AlunoViewModels.cs` — ViewModels (Alunos) + EditarAlunoViewModel + EditarAlunoMapper
- `AlunosController.cs` — Controller (Alunos) — Index, Detalhes, Editar GET/POST
- `Alunos/Index.cshtml` — Listagem de alunos
- `Alunos/Detalhes.cshtml` — Detalhe do aluno
- `Alunos/Editar.cshtml` — Formulário de edição de dados cadastrais

## Bug Conhecido: Alterar Grade

**Status:** RESOLVIDO (melhoria de UX)

**Causa confirmada:**
A regra D-1 na linha 445 do `MatriculasRepositorio.cs` é INTENCIONAL:
```csharp
if (removidos.Any(item => data <= item.VigenciaInicio))
    return new(EstadoMatriculas.DataInvalida);
```

O teste `Mudanca_material_no_primeiro_dia_e_rejeitada` confirma:
- Mudança material no primeiro dia da grade é rejeitada
- Isso preserva integridade histórica (VigenciaFim não pode ser anterior a VigenciaInicio)

**Mensagem de erro:** Melhorada para "A data final não pode ser anterior ao início da grade atual."

**Solução implementada:**
- Adicionada `DataMinimaGrade` ao ViewModel
- View exibe data mínima e mensagem explicativa
- JavaScript valida data mínima no submit
- Usuário recebe feedback antes de enviar formulário

## Funcionalidades em Andamento

- Alterar Grade (bug conhecido) — CONCLUÍDO
- Encerrar Matrícula — CONCLUÍDO
- Cancelar Matrícula — CONCLUÍDO
- Módulo Alunos — Etapa 1 (Listagem + Detalhe) CONCLUÍDO
- Módulo Alunos — Etapa 2 (Editar Dados) CONCLUÍDO
- Módulo Alunos — Etapa 3 (Gerenciar Responsáveis) CONCLUÍDO
- Módulo Aulas e Presenças CONCLUÍDO
- Módulo Financeiro (Cobranças + Pagamentos) CONCLUÍDO
- Módulo Relatórios (Financeiro, Inadimplência, Frequência) CONCLUÍDO

## Próximos Passos (Roadmap)

1. ~~Aulas~~ ✅
2. ~~Presença~~ ✅ (incluída no módulo Aulas)
3. ~~Financeiro Unidade x Aluno~~ ✅
4. ~~Cobrança~~ ✅ (incluída no módulo Financeiro)
5. ~~Pagamento~~ ✅ (incluído no módulo Financeiro)
6. ~~Relatórios~~ ✅
7. ~~Inadimplência~~ ✅ (incluída no módulo Relatórios)
8. Campeonatos
9. Comércio

## Identidade Visual

- **Paleta:** #0D0D0D, #1E1E1E, #FFC107, #BDBDBD, #FFFFFF
- **Estilo:** Dark, premium, esportiva, moderna
- **Referências:** `docs/UI-ADMIN-STANDARDS.md` e `brand/guide/brand-guide.md`

## Convenções Importantes

1. **Nomes de negócio em português** (sem acentos em identificadores C#)
2. **Nomes técnicos em inglês**
3. **History é princípio central** — não sobrescrever dados históricos
4. **Multi-tenancy** — sempre proteger por OrganizacaoId
5. **Teste manual obrigatório** para fluxos Web principais
6. **Build deve ter 0 erros e 0 warnings**

## Planos de Implementação

Planos sequenciais em `docs/plans/`:

| Nº | Plano | Status |
|----|-------|--------|
| 01 | Corrigir Salvamento de Alterar Grade | Concluído |
| 02 | Melhorar UX de Encerrar/Cancelar Matrícula | Concluído |
| 03 | Padronizar Validação de Datas e Mensagens | Concluído |
| 04 | Módulo Alunos e Responsáveis | Concluído |
| 05 | Logging Informativo para Produção | Concluído |
| 06 | Módulo Aulas e Presenças | Concluído |

## Documentos de Referência

| Documento | Propósito |
|-----------|-----------|
| `AGENTS.md` | Constituição operacional |
| `docs/PRODUCT-VISION.md` | Visão de produto |
| `docs/ARCHITECTURE.md` | Arquitetura técnica |
| `docs/UI-ADMIN-STANDARDS.md` | Padrão visual administrativo |
| `brand/guide/brand-guide.md` | Identidade visual |
| `docs/ENVIRONMENTS.md` | Configuração de ambientes |
| `docs/BOOTSTRAP-INICIAL.md` | Bootstrap de desenvolvimento |
| `docs/adr/` | Decisões arquiteturais |
| `docs/plans/` | Planos de implementação |