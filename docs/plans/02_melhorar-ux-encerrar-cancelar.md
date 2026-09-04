# Plano: Melhorar UX de Encerrar/Cancelar Matrícula

## Status

**Concluído**

## Contexto

O fluxo de Encerrar/Cancelar Matrícula não possuía validação client-side de data mínima, permitindo que usuários enviassem formulários com datas inválidas que seriam rejeitadas pelo servidor com mensagens genéricas.

## Mudanças Implementadas

### 1. ViewModel (`MatriculaViewModels.cs`)

Adicionada propriedade `DataMinimaEncerramento` ao `FinalizarMatriculaViewModel`:

```csharp
[BindNever, ValidateNever]
public required string DataMinimaEncerramento { get; init; }
```

### 2. Mapper (`MatriculasViewModelMapper.Finalizar`)

Calcula data mínima como o maior `VigenciaInicio` entre as grades abertas:

```csharp
DataMinimaEncerramento = FormatarData(
    matricula.GradeAtual
        .Where(item => item.VigenciaFim is null)
        .Select(item => item.VigenciaInicio)
        .DefaultIfEmpty(matricula.DataInicio)
        .Max())
```

### 3. Views (`Encerrar.cshtml` e `Cancelar.cshtml`)

- Adicionado atributo `data-min-date` ao input de data
- Adicionada mensagem explicativa mostrando a data mínima
- Adicionado atributo `data-bfa-matricula-finalizar` ao formulário

### 4. JavaScript (`bfa-matricula-wizard.js`)

Adicionada função `iniciarFinalizar` que:
- Valida data mínima no submit
- Mostra mensagem de erro amigável
- Limpa erro quando data é corrigida

### 5. Mensagem de Erro Server-side (`MatriculasController.cs`)

Adicionado caso específico para `DataInvalida` no método `MensagemErro`:

```csharp
EstadoMatriculas.DataInvalida =>
    "A data final não pode ser anterior ao início da grade atual.",
```

## Arquivos Modificados

| Arquivo | Mudança |
|---------|---------|
| `MatriculaViewModels.cs` | Adicionada `DataMinimaEncerramento` |
| `MatriculasViewModelMapper.cs` | Calcula data mínima no mapper |
| `Encerrar.cshtml` | Adicionado `data-min-date` e mensagem |
| `Cancelar.cshtml` | Adicionado `data-min-date` e mensagem |
| `bfa-matricula-wizard.js` | Adicionada `iniciarFinalizar` |
| `MatriculasController.cs` | Melhorada mensagem de erro |

## Validação

- **Build:** 0 erros, 0 warnings
- **Testes Unitários:** 484 aprovados
- **Testes de Integração:** 148 aprovados
- **Migrations:** Nenhuma alteração

## Comportamento

### Antes
1. Usuário selecionava data anterior à grade aberta
2. Formulário era enviado
3. Servidor rejeitava com erro genérico
4. Mensagem aparecia: "Não foi possível concluir a matrícula."

### Agora
1. Tela exibe data mínima (ex: "A data mínima é 03/09/2026")
2. Se usuário digitar data < data mínima, mensagem aparece antes do submit
3. Submit é bloqueado até data ser válida
4. Se server-side rejeitar, mensagem é mais específica

## Próximos Passos

1. Teste manual completo dos fluxos de Encerrar e Cancelar
2. Verificar se há outros fluxos que precisam de validação similar
