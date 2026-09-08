# PROJECT-STATE.md — BFA Platform

**Última atualização:** 2026-09-07  
**Status:** Em desenvolvimento ativo  
**Branch:** feature/login-mvc  
**Testes:** 1.178 aprovados (484 unitários + 694 integração)  
**Build:** 0 erros

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
- Cobranças (`CobrancasController`) — listagem agrupada, detalhe consolidado e registro de pagamento
- Relatórios (`RelatoriosController`)
- Contrato (read-only)

### Professor (Area: `/professor`)
- Dashboard (`InicioController`)
- Turmas (`TurmasController`)

### Aluno (Area: `/aluno`)
- Área implementada, mas ainda em transição de UX/escopo

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
| V016 | Cobranças e Pagamentos |
| V017 | Hangfire schema |
| V018 | Permissão CREATE no schema Hangfire |
| V019 | Tabelas do Hangfire |
| V020 | Nullable em colunas de auditoria de cobranças |

**Regra:** Migrations são imutáveis. Correções são novas migrations.

### Roles PostgreSQL

```text
bfa_app_role (NOLOGIN) → role de runtime
bfa_dev_app (LOGIN) → membro de bfa_app_role
```

DDL exclusivamente por `bfa_dev_deploy`. Aplicação é apenas DML.

## Estado recente

- Área do Aluno implementada, mas ainda em transição de UX/escopo.
- Área do Professor implementada, ainda em transição.
- Fluxos centrais da operação da unidade já estão ativos: turmas, matrículas, aulas, alunos e financeiro.
- A documentação precisa acompanhar o código com mais frequência, pois o código é a fonte de verdade.

## Funcionalidades em Andamento

- Consolidação visual do padrão administrativo.
- Refinamento contínuo da Área do Aluno.
- Refinamento contínuo da Área do Professor.
- Evolução do Financeiro e dos detalhes de cobrança.

## Próximos Passos (Roadmap)

1. ~~Aulas~~ ✅
2. ~~Presença~~ ✅ (incluída no módulo Aulas)
3. ~~Financeiro Unidade x Aluno~~ ✅
4. ~~Cobrança~~ ✅ (incluída no módulo Financeiro)
5. ~~Pagamento~~ ✅ (incluído no módulo Financeiro)
6. ~~Relatórios~~ ✅
7. ~~Inadimplência~~ ✅ (incluída no módulo Relatórios)
8. ~~Área do Aluno~~ ✅
9. Login do Aluno (Email/CPF) — Plano 12
10. Área do Professor (Dashboard, Aulas, Frequência) — Plano 12
11. Franqueadora — Alunos da Rede + Controle de Padrão — Plano 12
12. Franqueadora — Relatórios + Financeiro consolidado — Plano 12
13. Aluno — Pagamento Online (Pagar.me) — Plano 11
14. Campeonatos (deferido)
15. Comércio (deferido)

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
| 07 | Financeiro Unidade x Aluno | Concluído |
| 08 | Relatórios | Concluído |
| 09 | Área do Aluno | Concluído (em transição) |
| 10 | Melhoria Visual Monitores Grandes | Concluído |
| 11 | Pagamento Online Split Pagar.me | Planejado |
| 12 | Incremento das 4 Visões | Planejado |

## Documentos de Referência

| Documento | Propósito |
|-----------|-----------|
| `AGENTS.md` | Constituição operacional |
| `docs/PRODUCT-VISION.md` | Visão de produto |
| `docs/ARCHITECTURE.md` | Arquitetura técnica |
| `docs/WEB-ARCHITECTURE-CHECKLIST.md` | Auditoria prática do BFA.Web |
| `docs/UI-ADMIN-STANDARDS.md` | Padrão visual administrativo |
| `brand/guide/brand-guide.md` | Identidade visual |
| `docs/ENVIRONMENTS.md` | Configuração de ambientes |
| `docs/BOOTSTRAP-INICIAL.md` | Bootstrap de desenvolvimento |
| `docs/adr/` | Decisões arquiteturais |
| `docs/plans/` | Planos de implementação |
