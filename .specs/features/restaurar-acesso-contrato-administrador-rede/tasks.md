# Tasks — Restaurar acesso a Contrato para AdministradorRede

## Test Coverage Matrix

| Requirement | Unit | Integration / HTTP | Manual QA |
|---|---|---|---|
| NAV-01 | N/A | Dashboard contém resumo e link | Desktop + mobile |
| AUTH-01 | N/A | AdminRede com/sem vínculo ativo | URL direta + leitura |
| AUTH-02 | Existente | Existente para AdminUnidade; confirmar não regressão | Leitura somente |
| AUTH-03 | Handler existente | Negativos existentes; confirmar suíte | Professor e cross-tenant |
| DATA-01 | N/A | Documento e tenant isolation existentes | Não expor storage key |
| UI-01 | N/A | Asserts semânticos mínimos | 1440/1024/768/390/360 e teclado |
| DB-01 | N/A | N/A | Diff de migrations vazio |

## Gate Check Commands

```powershell
python C:\Users\rapha\.codex\skills\tlc-spec-driven\scripts\validate_spec.py .specs/features/restaurar-acesso-contrato-administrador-rede/spec.md --strict
python C:\Users\rapha\.codex\skills\tlc-spec-driven\scripts\validate_tasks.py .specs/features/restaurar-acesso-contrato-administrador-rede/tasks.md --strict
dotnet test backend/BFA.sln --filter "FullyQualifiedName~AreaUnidadeEndpointTests|FullyQualifiedName~ContratosFranquiaEndpointTests"
dotnet build backend/BFA.sln
dotnet test backend/BFA.sln
git diff --check
git diff -- database/migrations
```

## Execution Plan

### Phase 1 — Regressão executável

```text
T1 -> T2
```

### T1: Adicionar cenário HTTP de AdministradorRede com Unidade franqueada

**Depends on**: none  
**Where**: `backend/tests/BFA.IntegrationTests/AreaUnidadeEndpointTests.cs`  
**Change**: Preparar AdministradorRede, vínculo comercial/contrato ativo e afirmar resumo + link no dashboard, GET direto 200 e ausência de ações mutáveis.  
**Tests**: O próprio teste deve falhar antes da correção porque o painel não é renderizado.  
**Gate**: Executar o teste isolado e registrar a falha esperada na ausência do conteúdo contratual.

### T2: Adicionar cenário HTTP de AdministradorRede sem vínculo comercial ativo

**Depends on**: T1  
**Where**: `backend/tests/BFA.IntegrationTests/AreaUnidadeEndpointTests.cs`  
**Change**: Afirmar dashboard e rota GET com estado vazio controlado e sem ação de criação na Área da Unidade.  
**Tests**: O próprio cenário de ausência controlada para Rede.  
**Gate**: Executar os dois novos testes; o cenário do painel deve continuar vermelho antes da correção.

### Phase 2 — Correção mínima

```text
T3
```

### T3: Restaurar painel contratual somente leitura

**Depends on**: T2  
**Where**: `backend/src/BFA.Web/Areas/Unidade/Views/Inicio/Index.cshtml`  
**Change**: Renderizar resumo vigente ou estado vazio e link GET para a página de Contrato, sem qualquer ação de mutação.  
**Tests**: Novos testes de T1/T2 e testes existentes de `AreaUnidadeEndpointTests`.  
**Gate**: Testes direcionados verdes; inspeção do HTML confirma um único link na navegação e nenhuma ação mutável no painel.

### Phase 3 — Verificação

```text
T4 -> T5
```

### T4: Executar suíte completa e gates estruturais

**Depends on**: T3  
**Where**: `.specs/features/restaurar-acesso-contrato-administrador-rede/tasks.md`  
**Change**: Registrar evidências de validação sem alterar código de produto.  
**Tests**: `dotnet build backend/BFA.sln`, `dotnet test backend/BFA.sln`, `git diff --check`.  
**Gate**: 0 erros, 0 warnings; total unitário, integração e global registrados; diff de migrations vazio.

### T5: Executar QA no navegador

**Depends on**: T4  
**Where**: `.specs/features/restaurar-acesso-contrato-administrador-rede/tasks.md`  
**Change**: Registrar resultados A–H: Rede sem/com franqueado, AdminUnidade, Professor, desktop, mobile, URL direta e leitura somente.  
**Tests**: Inspeção visual e funcional nos viewports definidos, sem submissão de mutações.  
**Gate**: Todos os cenários documentados como aprovados ou com evidência objetiva de bloqueio.

## Task Breakdown

- [x] T1 — teste vermelho: Rede + Unidade franqueada.
- [x] T2 — teste de estado vazio para Rede.
- [x] T3 — restaurar painel somente leitura.
- [x] T4 — build, suíte completa e gates.
- [x] T5 — QA A–H.

## Restrições de execução

- Não criar commit nem push, por solicitação explícita do usuário.
- Não criar migration nem modificar V001–V014 ou qualquer migration posterior.
- Não alterar governança ou autorização sem nova evidência que contradiga o diagnóstico.
- Parar após este plano e aguardar aprovação antes de T1.

## Evidências de execução

- T1/T2 (vermelho): 2 testes executados, 2 falhas esperadas em `Contrato da franquia` ausente no dashboard; as rotas de detalhe responderam antes dessas asserções.
- T3 (verde): 2/2 novos cenários e 123/123 testes direcionados de Unidade/Contratos aprovados.
- T4: build final com 0 erros e 2 warnings NU1903 preexistentes; 484/484 testes unitários e 697/697 testes de integração aprovados (1.181 total); validadores TLC em 0/0; `git diff --check` aprovado; migrations sem diff.
- T5: AdministradorUnidade aprovado no navegador com contrato ativo, rota direta 200 e ausência de ações mutáveis; drawer contém `Contrato`; 1440/1024/768/390/360 sem overflow horizontal. AdministradorRede com/sem vínculo comercial, Professor e cross-tenant aprovados por testes HTTP; QA manual desses perfis ficou objetivamente bloqueado por ausência de credenciais/dados seguros, sem criar nem alterar registros. Aplicação iniciada com `Hangfire__Enabled=false` e encerrada após o QA.
