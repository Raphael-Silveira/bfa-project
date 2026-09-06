# BFA Project Context

BFA — Brazilian Footvolley Academy.
Plataforma de gestão de rede de franquias de futevôlei.
Monólito modular .NET 10 / ASP.NET Core MVC + Razor / PostgreSQL 17.

---

## Leitura obrigatória antes de qualquer implementação não trivial

1. `AGENTS.md` — constituição operacional completa (regras de arquitetura, banco, logging, git, testes, deploy).
2. `docs/PROJECT-STATE.md` — estado atual, módulos implementados, migrations aplicadas, planos ativos.
3. `docs/PRODUCT-VISION.md` — propósito do produto, públicos, módulos previstos, roadmap conceitual.
4. `docs/ARCHITECTURE.md` — arquitetura técnica detalhada (camadas, multi-tenancy, Identity, autorização, módulos).
5. `docs/UI-ADMIN-STANDARDS.md` — padrão normativo de interfaces administrativas (shell, componentes, tokens, responsividade).
6. `brand/guide/brand-guide.md` — paleta, tipografia e uso correto da marca.
7. Plano relevante em `docs/plans/` (ver tabela abaixo).
8. `git status` e `git diff` antes de modificar qualquer arquivo.

---

## Regras críticas (resumo)

- Migrations SQL em `database/migrations/` sao **imutaveis**. Correcoes = nova migration.
- **Nunca** executar `EnsureCreated`, `EnsureDeleted` ou `Database.Migrate` na inicializacao.
- `OrganizacaoId` e `UnidadeId` postados pelo browser **nunca** sao autorizacao — sempre resolver do contexto autenticado.
- Controllers sao finos; regras de negocio ficam em Domain/Application.
- Perfis de acesso via `VinculoAcesso`, **nao** `IdentityRole`.
- Franqueado = entidade comercial, nao perfil de acesso.
- Toda interface administrativa usa o Admin Shell e as classes de `admin.css`.
- Nomes de negocio em portugues (sem acentos em identificadores); nomes tecnicos em ingles.
- Build deve ter 0 erros e 0 warnings antes de qualquer entrega.
- Nao commitar nem fazer push sem instrucao explicita do usuario.
- Nunca usar comandos Git destrutivos sem autorizacao.

---

## Migrations aplicadas (V001-V016, todas imutaveis)

V001 Organizacoes/Unidades | V002 Identity | V003 VinculosAcesso | V004 Usuarios/Franqueados
V005 CNPJ alfanumerico | V006 Localidades | V007 Contratos | V008 Professores/Remuneracoes
V009 Turmas/Horarios | V010 Planos | V011 Alunos/Responsaveis | V012 Disponibilidades/Matriculas
V013 Grade | V014 Correcao validacao unidade | V015 Aulas/Presencas | V016 Cobrancas/Pagamentos

---

## Planos de implementacao (docs/plans/)

| No  | Plano                                | Status     |
|-----|--------------------------------------|------------|
| 01-10 | Varios modulos concluidos          | Concluido  |
| 11  | Pagamento Online Split Pagar.me      | Planejado  |
| 12  | Incremento das 4 Visoes              | Planejado  |
| 13  | Refresh Visual BFA Admin             | (ver doc)  |
| 14  | Padronizacao Grids/Listagens         | (ver doc)  |

---

## Estrutura rapida

```
backend/src/
  BFA.Domain/          # entidades e invariantes (sem EF, MVC, HTTP)
  BFA.Application/     # casos de uso e orquestracao
  BFA.Infrastructure/  # EF Core, Npgsql, Identity, armazenamento
  BFA.Web/Areas/
    Franqueadora/      # gestao global da rede (/franqueadora)
    Unidade/           # operacao local (/unidade/{unidadeId})
    Professor/         # area do professor (/professor/...)
    Aluno/             # area do aluno (/aluno/...)
database/migrations/   # SQL versionado imutavel
docs/plans/            # planos de implementacao sequenciais
```

---

## Atualizacao do estado

Sempre que alterar migrations, modulos, planos ou estado do projeto, atualize `docs/PROJECT-STATE.md`.

---

*Este arquivo e um indice. Consulte as fontes canonicas acima para detalhes.*
