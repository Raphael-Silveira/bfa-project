# Validation — Melhorar Visão Geral da Área Franqueadora

## Resultado

**PASS** — os critérios de aceitação foram verificados por teste HTTP, inspeção do diff e gates completos.

## Evidências

- UI e métricas: `backend/src/BFA.Web/Areas/Franqueadora/Views/Inicio/Index.cshtml:35-91` separa métricas operacionais/financeiras e reutiliza cards, ícones e corpo do Admin Shell.
- Formatação monetária: `backend/src/BFA.Web/ViewModels/Franqueadora/FranqueadoraDashboardViewModel.cs:36-52` usa cultura `pt-BR` para os três valores.
- Lista responsiva e rota preservada: `backend/src/BFA.Web/Areas/Franqueadora/Views/Inicio/Index.cshtml:94-142` mantém `/unidade/{id}` e os padrões desktop/mobile existentes.
- Regressão: `FranqueadoraEndpointTests.Dashboard_da_rede_exibe_metricas_reorganizadas_valores_em_reais_e_unidades_responsivas` passou.
- Teste direcionado: 58/58 integrações Franqueadora passaram.
- Sensor discriminativo: a mutação que removeu a cultura `pt-BR` falhou no assert de `R$ 49.440,00`; mutação eliminada.
- Validadores TLC: `validate_spec.py --strict` e `validate_tasks.py --strict` passaram sem erros ou warnings.
- Build: `dotnet build backend/BFA.sln` passou com 0 erros e 4 warnings preexistentes `NU1903` sobre `Newtonsoft.Json` 11.0.1.
- Suíte completa: 484/484 testes unitários e 707/707 testes de integração passaram; total 1.191/1.191.
- `git diff --check`: passou; apenas avisos normais de conversão LF/CRLF do Git.
- Banco: `git status --short -- database/migrations` e `git diff -- database/migrations` sem alterações.
- Hangfire: nenhum arquivo de configuração/startup foi alterado e nenhum job foi executado.

## Escopo

O diff atual contém somente a melhoria do dashboard e seus artefatos TLC. Não foram alteradas consultas de aplicação, autorização, rotas, módulos preservados, banco ou migrations.

Não foi criado commit e nenhum push foi realizado.
