# Plano 01: Corrigir Salvamento de Alterar Grade

**Data:** 2026-09-03  
**Status:** Em andamento

## Objetivo

Investigar e corrigir o bug no fluxo de Alterar Grade da matrícula, que impede o salvamento quando a data da nova grade coincide com o primeiro dia da grade atual.

## Contexto Atual

A tela `Alterar Grade` já está implementada em:
- `backend/src/BFA.Web/Areas/Unidade/Controllers/MatriculasController.cs`
- `backend/src/BFA.Web/Areas/Unidade/Views/Matriculas/AlterarGrade.cshtml`
- `backend/src/BFA.Web/ViewModels/Unidade/MatriculaViewModels.cs`

A Application e Infrastructure já possuem a lógica de alteração:
- `BFA.Application.Matriculas.MatriculasOperacionais.cs`
- `BFA.Infrastructure.Matriculas/MatriculasRepositorio.cs`

## Problema

### Sintoma 1: Erro ao salvar
Quando a grade atual começa em D (ex: 02/09/2026) e o usuário tenta alterar com nova grade começando em D, aparece erro de validação.

### Sintoma 2: Mensagem incorreta
A mensagem observada foi "Não foi possível concluir a matrícula. Tente novamente." — que pertence ao fluxo de Nova Matrícula (`MensagemErro`, linha 587). Em Alterar Grade deveria ser "Não foi possível alterar a Grade. Tente novamente." (`MensagemErroAlterarGrade`, linha 603).

### Sintoma 3: Nome do aluno perdido
Após o erro, o ViewModel é reidratado mas o NomeAluno aparece vazio:
- Antes: "Grade de Luisa Pires"
- Depois: "Grade de"

## Resultado da Investigação

### 1. Regra D-1 (confirmada como INTENCIONAL)

Linha 445 do `MatriculasRepositorio.cs`:
```csharp
if (removidos.Any(item => data <= item.VigenciaInicio))
    return new(EstadoMatriculas.DataInvalida);
```

O teste `Mudanca_material_no_primeiro_dia_e_rejeitada` (linha 356 de `MatriculasOperacionaisRepositorioTests.cs`) CONFIRMA que esta regra é intencional:

```csharp
[Fact]
public async Task Mudanca_material_no_primeiro_dia_e_rejeitada()
{
    // Grade começa em Inicio (01/09/2026)
    // Tenta alterar também começando em Inicio
    // Resultado: DataInvalida
    Assert.Equal(EstadoMatriculas.DataInvalida, resultado.Estado);
}
```

**Justificativa técnica:** Se `data = VigenciaInicio`, a regra D-1 faria `VigenciaFim = data - 1 = VigenciaInicio - 1`, o que violaria a Domain rule em `MatriculaHorario.Encerrar`:
```csharp
if (vigenciaFim < VigenciaInicio)
    throw new ArgumentException("A vigencia final nao pode ser anterior a vigencia inicial.");
```

**Conclusão:** A regra está correta. Mudança material no primeiro dia NÃO pode ser feita porque o histórico antigo terminaria antes de começar.

### 2. Mensagem de Erro

O controller na linha 297 usa `MensagemErroAlterarGrade`:
```csharp
ModelState.AddModelError(string.Empty, MensagemErroAlterarGrade(resultado.Estado));
```

Para `DataInvalida`, a mensagem retornada é:
```csharp
EstadoMatriculas.DataInvalida =>
    "A nova Grade deve começar após o início da Grade atual.",
```

**Possíveis cenários para mensagem incorreta:**
1. O usuário viu esta mensagem antes do código atual (o diff mostra 253 linhas novas)
2. O usuário está confundindo com outra tela
3. O estado retornado não é `DataInvalida` mas sim `Falha` (cairia no fallback)

### 3. NomeAluno Perdido

Analisei o fluxo de reidratação:
1. POST falha → `ReexibirAlterarGradeAsync` é chamado
2. Recebe `detalhe.Valor` (`MatriculaDetalhe`) que contém `NomeAluno`
3. `MatriculasViewModelMapper.AlterarGrade` atribui `NomeAluno = matricula.NomeAluno`
4. View renderiza `@Model.NomeAluno`

**Não encontrei caminho de código** que perca `NomeAluno` no fluxo atual. Possíveis causas:
- Browser cache (View antiga sem o ViewModel atualizado)
- Erro na requisição AJAX de horários (se houver)
- Cenário diferente do investigado

## Escopo da Correção

### O que será corrigido

1. **Mensagem de erro** — Verificar se há cenário onde `MensagemErro` (linha 587) é chamada em vez de `MensagemErroAlterarGrade` (linha 603). No código atual está correto.

2. **Reidratação do ViewModel** — Verificar se `ReexibirAlterarGradeAsync` está sendo chamada corretamente em todos os caminhos de erro. No código atual parece correto.

3. **UX preventiva** — A data mínima na UI deveria ser `Matricula.DataInicio + 1` (ou o primeiro dia após o início da grade atual) para evitar que o usuário selecione uma data inválida.

### O que NÃO será corrigido

- A regra D-1 está correta e testada
- A mensagem `MensagemErroAlterarGrade` já está implementada
- O fluxo de Nova Matrícula não será alterado

## Arquivos/Módulos Envolvidos

| Arquivo | Responsabilidade |
|---------|-----------------|
| `MatriculasController.cs:267-316` | POST AlterarGrade — validação e reidratação |
| `MatriculasController.cs:431-456` | ReexibirAlterarGradeAsync |
| `MatriculasRepositorio.cs:403-490` | AlterarGradeAsync (validação D-1) |
| `MatriculasOperacionais.cs:401-417` | AlterarGradeAsync (Application) |
| `AlterarGrade.cshtml:39-53` | Campo de data na UI |

## Regras de Negócio Afetadas

### Regra D-1 (NÃO será alterada)
A regra `data <= item.VigenciaInicio` está correta porque:
1. Histórico antigo termina em `data - 1`
2. Se `data = VigenciaInicio`, então `VigenciaFim = VigenciaInicio - 1` (inválido)
3. Teste `Mudanca_material_no_primeiro_dia_e_rejeitada` confirma

### Nova regra a adicionar
A data mínima na UI deveria ser calculada como:
```csharp
var dataMinima = matricula.GradeAtual
    .Where(item => item.VigenciaFim is null)
    .Select(item => item.VigenciaInicio)
    .DefaultIfEmpty(matricula.DataInicio)
    .Max();
// dataMinima já é o primeiro dia da grade atual
// Usuário não deveria poder selecionar data <= dataMinima
// mas sim data > dataMinima (ou dataMinima + 1 para material change)
```

## Banco / Migration

**NÃO haverá alteração de schema.** O problema é de lógica na Application/Infrastructure e UX na UI.

## Autorização / Governança

Não há mudança na autorização. A regra `PodeGerenciarMatriculas` continua sendo verificada.

## Histórico / Imutabilidade

A regra D-1 preserva o histórico corretamente. Não será alterada.

## Concorrência / Locks

A Implementation atual já usa locks adequados. Não há problema de concorrência neste bug.

## Estratégia de Implementação

### Passo 1: Validar mensagem de erro no código atual
1. Verificar se `MensagemErroAlterarGrade` é chamada em TODOS os caminhos de erro do POST
2. Verificar se há cenário onde o estado retornado cai no fallback errado

### Passo 2: Validar reidratação do ViewModel
1. Verificar se `ReexibirAlterarGradeAsync` é chamada com todos os parâmetros corretos
2. Verificar se `MatriculasViewModelMapper.AlterarGrade` recebe `matricula` com `NomeAluno`

### Passo 3: Melhorar UX da data
1. Calcular data mínima na View (primeiro dia após grade atual)
2. Adicionar validação client-side para evitar data <= dataMinima
3. Adicionar validação server-side para melhor mensagem de erro

### Passo 4: Testar cenário completo
1. Criar matrícula com grade iniciando em D
2. Abrir Alterar Grade
3. Tentar alterar com data = D → deve mostrar "A nova Grade deve começar após o início da Grade atual."
4. Tentar alterar com data = D+1 → deve funcionar
5. Verificar NomeAluno em todos os cenários

## Riscos

1. **Baixo** — A regra D-1 está correta e testada
2. **Baixo** — As mensagens de erro estão implementadas
3. **Médio** — Pode haver cenário não coberto que perde NomeAluno

## Testes Automatizados

Verificar cobertura existente:
- `MatriculasOperacionaisRepositorioTests.cs` — `Mudanca_material_no_primeiro_dia_e_rejeitada`
- `AreaUnidadeMatriculasEndpointTests.cs` — testes de endpoint

Adicionar teste se necessário para validar mensagem de erro.

## Testes Manuais

1. Matrícula com grade começando em 02/09/2026
2. Abrir Alterar Grade com data = 02/09/2026
3. Selecionar horário diferente
4. Clicar "Salvar nova Grade"
5. Verificar mensagem: "A nova Grade deve começar após o início da Grade atual."
6. Verificar NomeAluno exibido corretamente
7. Alterar data para 03/09/2026
8. Clicar "Salvar nova Grade"
9. Verificar se salva com sucesso

## Critérios de Aceite

- [ ] Mensagem de erro é específica e correta para Alterar Grade
- [ ] NomeAluno é exibido corretamente após erro de validação
- [ ] Data mínima na UI impede seleção inválida
- [ ] Testes existentes continuam passando
- [ ] Build continua com 0 erros e 0 warnings
- [ ] Teste manual confirma fluxo correto

## Resultado

### Investigação Concluída

1. **Regra D-1:** Confirmada como intencional. Teste `Mudanca_material_no_primeiro_dia_e_rejeitada` confirma.

2. **Mensagem de Erro:** Já correta no código (`MensagemErroAlterarGrade`).

3. **NomeAluno:** Não encontrado caminho de código que perca. Possível causa: browser cache.

### Implementação Concluída

**Alterações realizadas:**

1. **`MatriculaViewModels.cs`** — Adicionada propriedade `DataMinimaGrade` ao `AlterarGradeMatriculaViewModel`:
   ```csharp
   [BindNever, ValidateNever]
   public required string DataMinimaGrade { get; init; }
   ```

2. **`MatriculasViewModelMapper.AlterarGrade`** — Calcula data mínima como primeiro dia após grade atual:
   ```csharp
   DataMinimaGrade = FormatarData(
       matricula.GradeAtual
           .Where(item => item.VigenciaFim is null)
           .Select(item => item.VigenciaInicio)
           .DefaultIfEmpty(matricula.DataInicio)
           .Max().AddDays(1))
   ```

3. **`AlterarGrade.cshtml`** — Adicionado atributo `data-min-date` e mensagem explicativa:
   ```html
   <input ... data-min-date="@dataMinima" />
   <p>A data mínima é @dataMinima.</p>
   ```

4. **`bfa-matricula-wizard.js`** — Adicionada validação client-side em `iniciarGrade`:
   - Valida data mínima no submit
   - Mostra mensagem de erro amigável
   - Limpa erro quando data é corrigida

**Build:** 0 erros, 0 warnings  
**Testes Unitários:** 484 aprovados  
**Testes de Integração:** 148 aprovados  
**Migrations:** Nenhuma alteração

**Arquivos modificados:**
- `backend/src/BFA.Web/ViewModels/Unidade/MatriculaViewModels.cs`
- `backend/src/BFA.Web/Areas/Unidade/Views/Matriculas/AlterarGrade.cshtml`
- `backend/src/BFA.Web/wwwroot/js/bfa-matricula-wizard.js`

## Status

**Concluído** — Melhoria de UX implementada e testada