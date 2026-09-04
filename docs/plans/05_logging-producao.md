# Plano: Logging Informativo para Produção

## Status

**Aprovado — Aguardando implementação**

## Objetivo

Adicionar logs informativos em toda a aplicação para que, em ambiente de produção, seja possível identificar:
- O que está acontecendo ( fluxo de requests )
- Quem fez o quê ( usuários e ações )
- Onde erros ocorreram ( com contexto suficiente para diagnóstico )
- Quanto tempo operações levaram ( performance )

## Estado Atual

- **ILogger usado em apenas 2 de 23 controllers** (apenas para `LogError` em Contratos)
- **Zero logs na Application layer** (~15 serviços concretos)
- **Zero logs na Infrastructure layer** (~25 implementações)
- **Zero logs em authorization handlers** (7 handlers)
- **Nenhum middleware customizado** de request logging
- **Production log level = Warning** — suprime todos os logs Information
- Total de chamadas `LogWarning/LogInformation/LogDebug` no código: **0**
- Total de chamadas `LogError` no código: **2**

## Decisões

1. **ILogger nativo** — sem Serilog/NLog. Usar `Microsoft.Extensions.Logging` que já vem no ASP.NET Core
2. **Levels padronizados**: `LogDebug` para tracing interno, `LogInformation` para operações normais, `LogWarning` para violações de regra de negócio e Forbid, `LogError` para falhas
3. **NÃO logar**: senhas, tokens, connection strings, CPF completo, dados sensíveis
4. **Request logging via middleware** — timing e status code de cada request
5. **Log enrichment manual** — incluir UserId, UnidadeId, OrganizacaoId quando disponível (sem package extra)

---

## Fase 0 — Fundação (appsettings + middleware)

### 0.1 Ajustar log levels por namespace

**Arquivo:** `appsettings.Production.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "BFA": "Information"
    }
  }
}
```

Mantém framework em Warning, mas libera Information para o namespace `BFA.*`.

### 0.2 Criar middleware de request logging

**Arquivo novo:** `BFA.Web/Infrastructure/RequestLoggingMiddleware.cs`

Loga para cada request:
- Method + Path + QueryString
- User ID (se autenticado)
- Status code da resposta
- Duração em ms
- Level: Information para 2xx/3xx, Warning para 4xx, Error para 5xx

**Registro:** `Program.cs` — adicionar `app.UseMiddleware<RequestLoggingMiddleware>()` antes de UseRouting.

### 0.3 Registrar no pipeline

**Arquivo:** `BFA.Web/Program.cs`

```csharp
app.UseMiddleware<RequestLoggingMiddleware>();
// antes de UseRouting
```

---

## Fase 1 — Controllers (23 controllers)

### Padrão por controller

Cada controller receberá:
- `ILogger<T>` via constructor injection
- `LogInformation` no entry de actions state-changing (POST)
- `LogWarning` em caminhos de falha (Forbid, business rule violations)
- `LogError` em Exception paths

### 1.1 Controllers Franqueadora (8)

| Controller | Ações a logar | Level |
|---|---|---|
| `InicioController` | — (read-only simples) | — |
| `UsuariosController` | Criar/Editar usuário | Info |
| `UnidadesController` | Criar/Editar/Ativar/Desativar unidade | Info |
| `FranqueadosController` | Editar, Adicionar/Desativar unidade | Info |
| `ContratosController` | Já tem LogError. Adicionar: Criar/Editar/Ativar/Cancelar/Encerrar contrato | Info |
| `PlanosController` | Criar/Editar/Ativar/Inativar plano | Info |
| `LocalidadesController` | — (cache/read-only) | — |
| `AcessosUnidadeController` | Adicionar/Ativar/Desativar acesso | Info |

### 1.2 Controllers Unidade (7)

| Controller | Ações a logar | Level |
|---|---|---|
| `InicioController` | — (dashboard) | — |
| `AlunosController` | Editar dados do aluno | Info |
| `TurmasController` | Criar/Editar turma, Trocar professor, Ajustar horários | Info |
| `ProfessoresController` | Criar/Vincular/Editar/Encerrar, Acesso/Revogar | Info |
| `MatriculasController` | Nova matrícula, AlterarGrade, Encerrar, Cancelar | Info |
| `PlanosController` | Criar/Editar/Ativar/Inativar | Info |
| `ContratoController` | Já tem LogError | — |

### 1.3 Controllers Professor (2)

| Controller | Ações a logar | Level |
|---|---|---|
| `InicioController` | Selecionar unidade | Info |
| `TurmasController` | — (read-only) | — |

### 1.4 Controllers Públicos (4)

| Controller | Ações a logar | Level |
|---|---|---|
| `HomeController` | — | — |
| `ContaController` | Login (sucesso=falha), Logout | Info/Warning |
| `PrimeiroAcessoController` | Definir senha | Info |
| `SelecaoUnidadeController` | Selecionar unidade | Info |

### 1.5 Controllers API (2)

| Controller | Ações a logar | Level |
|---|---|---|
| `HealthController` | — | — |
| `DatabaseHealthController` | Falha de conexão | Error |

---

## Fase 2 — Application Services (~15 serviços)

### Padrão por serviço

Cada serviço concreto receberá `ILogger<T>` e logará:
- Entry point da operação (Debug)
- Sucesso da operação com IDs relevantes (Information)
- Violação de regra de negócio (Warning)
- Falha inesperada (Error)

### 2.1 Serviços a instrumentar

| Serviço | Operações-chave |
|---|---|
| `AlunosServico` | Listar, Obter, AtualizarDados |
| `MatriculasOperacionais` | Nova matrícula, AlterarGrade, Encerrar, Cancelar |
| `ProfessoresUnidadeServico` | Vincular, Editar, Encerrar |
| `TurmasUnidade` | Criar, Editar, TrocarProfessor |
| `AjusteHorariosTurma` | Ajustar horários |
| `TrocaProfessorTurma` | Trocar professor |
| `PlanosServico` | Criar, Editar, Ativar, Inativar |
| `ContratosFranquiaServico` | Criar, Editar, Ativar, Formalizar, Cancelar |
| `UnidadesFranqueadoraServico` | Criar, Editar, Ativar, Desativar |
| `UsuariosFranqueadoraServico` | Criar, Editar |
| `FranqueadosServico` | Editar, Vincular/Desvincular unidade |
| `AcessosUnidadeServico` | Adicionar, Ativar, Desativar |
| `LocalidadesSincronizacaoServico` | Sincronizar |
| `BootstrapInicialSolicitacao` | Bootstrap |
| `PrimeiroAcessoServico` | Definir senha |

---

## Fase 3 — Infrastructure (~25 implementações)

### 3.1 Repositories

Adicionar `ILogger<T>` nos repositories principais para logar:
- Operações de persistência (Insert/Update/Delete) com entity ID (Debug)
- Falhas de query (Error)

Repositories prioritários:
- `AlunosRepositorio`
- `MatriculasRepositorio`
- `ProfessoresUnidadeRepositorio`
- `TurmasUnidadeRepositorio`
- `PlanosRepositorio`
- `ContratosFranquiaRepositorio`

### 3.2 Integrações externas

| Componente | Log |
|---|---|
| `IbgeLocalidadesClient` | Request/response HTTP (Debug), falha (Error) |
| `ArmazenamentoLocalDocumentosContrato` | Upload/download arquivo (Debug), falha (Error) |
| `DatabaseConnectionProbe` | Resultado do health check (Info/Error) |

### 3.3 Identity / Auth

| Componente | Log |
|---|---|
| `PrimeiroAcessoServico` | Senha definida (Info), tentativa inválida (Warning) |
| `BootstrapInicial` | Bootstrap executado (Info), falha (Error) |

### 3.4 Filters

| Componente | Log |
|---|---|
| `GovernancaOperacionalUnidadeResultFilter` | Cache hit/miss (Debug) |

---

## Fase 4 — Authorization Handlers (7 handlers)

| Handler | Log |
|---|---|
| `AdministradorRedeHandler` | Acesso concedido/negado (Debug) |
| `PerfilAcessoHandler` | Acesso concedido/negado (Debug) |
| `AcessoUnidadeHandler` | Acesso concedido/negado (Debug) |
| `AcessoUnidadePorPerfilHandler` | Acesso concedido/negado (Debug) |

---

## Fase 5 — CLI Commands (3 commands)

| Command | Log |
|---|---|
| `BootstrapInicialCommand` | Substituir Console.Out por ILogger (Info) |
| `SincronizarLocalidadesIbgeCommand` | Substituir Console.Out por ILogger (Info) |
| `DiagnosticarVinculosFranqueadoCommand` | Substituir Console.Out por ILogger (Info) |

---

## Resumo de Escopo

| Camada | Arquivos a modificar | Arquivos novos |
|---|---|---|
| appsettings | 1 | 0 |
| Web (middleware) | 1 (Program.cs) | 1 (RequestLoggingMiddleware.cs) |
| Web (controllers) | ~19 controllers | 0 |
| Application (services) | ~15 serviços | 0 |
| Infrastructure (repos) | ~6 repositories | 0 |
| Infrastructure (externals) | ~3 clientes | 0 |
| Infrastructure (identity) | ~2 serviços | 0 |
| Infrastructure (filters) | 1 filter | 0 |
| Authorization (handlers) | ~4 handlers | 0 |
| CLI (commands) | 3 commands | 0 |
| **Total** | **~53 arquivos** | **1 arquivo novo** |

## Ordem de Implementação

1. **Fase 0** — appsettings + middleware (fundação, 1 dia)
2. **Fase 1** — Controllers (visibilidade de ações, 1-2 dias)
3. **Fase 2** — Application Services (lógica de negócio, 1-2 dias)
4. **Fase 3** — Infrastructure (repositories + externals, 1 dia)
5. **Fase 4** — Authorization Handlers (meio dia)
6. **Fase 5** — CLI Commands (meio dia)

## Critérios de Aceite

- [ ] Production log level emiti Information para namespace `BFA.*`
- [ ] Cada request loga method, path, status code e duração
- [ ] Cada ação state-changing (POST) loga operação + IDs
- [ ] Cada violação de regra de negócio loga Warning com contexto
- [ ] Cada falha de integração externa loga Error com endpoint
- [ ] Nenhum dado sensível (senha, token, CPF completo) aparece nos logs
- [ ] Build 0 erros, 0 warnings
- [ ] Todos os testes existentes continuam passando
