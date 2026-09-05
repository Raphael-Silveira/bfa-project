# Plano: Pagamento Online com Split Payment (Pagar.me)

## Status

**Planejado**

## Objetivo

Implementar pagamento online para o aluno com split automático entre franqueadora, unidade/franqueado e professor.

## Contexto

- Aluno paga mensalidade, taxa de matrícula ou avulso
- Gateway divide automaticamente entre os recipients
- Franqueadora recebe royalties, franqueado recebe proporcional, professor recebe comissão
- Webhook atualiza status de pagamento no sistema

## Gateway: Pagar.me (Stone)

### Por quê Pagar.me
- Split nativo via API (Recipient + Split Rules)
- Melhor DevEx para contexto brasileiro
- Antecipação D+1 nativa
- Suporte a Pix, cartão, boleto
- Webhooks idempotentes
- Circular BACEN 3.886 compliance

### Conceitos-chave do Pagar.me

| Conceito | Descrição |
|----------|-----------|
| **Recipient** | Conta de destino do dinheiro (rp_XXXXXXXX) |
| **Split Rule** | Regra de divisão por recipient (% ou flat) |
| **Order** | Pedido com pagamento + split |
| **Customer** | Dados do aluno (CPF, nome, email) |
| **Webhook** | Notificação de status (order.paid, etc.) |

### Split Rules — Parâmetros

```json
{
  "recipient_id": "rp_XXXXXXXX",
  "type": "percentage",
  "amount": 15,
  "options": {
    "liable": true,
    "charge_processing_fee": true,
    "charge_remainder_fee": true
  }
}
```

- `liable`: responsável por chargeback
- `charge_processing_fee`: paga taxas da transação
- `charge_remainder_fee`: absorve centavos de arredondamento

## Modelo de Split para BFA

### Estrutura de recipients

```
BFA (Marketplace/Plataforma)
├── Unidade BFA Tietê (Franqueado)
│   ├── Professor João (Professor)
│   └── Professor Maria (Professor)
├── Unidade BFA Sorocaba (Franqueado)
│   └── Professor Pedro (Professor)
└── ...
```

### Regra de divisão padrão

| Participante | Percentual | Liable | Charge Fee |
|-------------|-----------|--------|------------|
| Franqueadora (BFA) | 15% | Sim | Sim |
| Franqueado/Unidade | 75% | Sim | Sim |
| Professor | 10% | Não | Não |

**Nota:** Percentuais configuráveis por unidade/contrato.

### Exemplo: Mensalidade R$ 200,00

| Destino | Cálculo | Valor |
|---------|---------|-------|
| BFA (royalties) | 15% | R$ 30,00 |
| Franqueado | 75% | R$ 150,00 |
| Professor | 10% | R$ 20,00 |

## Arquitetura

### Fluxo de pagamento

```
1. Aluno clica "Pagar" na área Aluno
         │
2. BFA cria Customer no Pagar.me (se não existe)
         │
3. BFA cria Order com split rules
         │
4. Pagar.me mostra checkout (Pix/cartão/boleto)
         │
5. Aluno confirma pagamento
         │
6. Pagar.me processa + aplica split
         │
7. Webhook notifica BFA (order.paid)
         │
8. BFA atualiza Cobranca → Paga
         │
9. BFA cria Pagamento registro
```

### Componentes novos

| Camada | Componente | Descrição |
|--------|-----------|-----------|
| **Domain** | `PagamentoOnline` | Registro de transação com Pagar.me |
| **Domain** | `RecipientPagarMe` | Vinculação Unidade→Recipient |
| **Application** | `IPagamentoServico` | Criar pagamento, consultar status |
| **Application** | `IPagamentoRepositorio` | CRUD + consultas |
| **Infrastructure** | `PagarMeClient` | HTTP client para API Pagar.me |
| **Infrastructure** | `PagamentoRepositorio` | Repositório EF Core |
| **Web** | `PagamentoController` | Ações de pagamento (Aluno area) |
| **Web** | `WebhookController` | Receber notificações do Pagar.me |

### Tabelas novas

```sql
-- Vincula unidade a um recipient no Pagar.me
CREATE TABLE recipientes_pagarme (
    id UUID PRIMARY KEY,
    organizacao_id UUID NOT NULL REFERENCES organizacoes(id),
    unidade_id UUID NOT NULL REFERENCES unidades(id),
    recipient_id VARCHAR(50) NOT NULL, -- rp_XXXXXXXX
    nome VARCHAR(150) NOT NULL,
    tipo VARCHAR(20) NOT NULL, -- franqueadora | unidade | professor
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    criado_em_utc TIMESTAMPTZ NOT NULL,
    atualizado_em_utc TIMESTAMPTZ NOT NULL
);

-- Registro de transação com Pagar.me
CREATE TABLE pagamentos_online (
    id UUID PRIMARY KEY,
    organizacao_id UUID NOT NULL REFERENCES organizacoes(id),
    unidade_id UUID NOT NULL REFERENCES unidades(id),
    cobranca_id UUID NOT NULL REFERENCES cobrancas(id),
    aluno_id UUID NOT NULL REFERENCES alunos(id),
    pedido_id VARCHAR(50), -- order id do Pagar.me
    transacao_id VARCHAR(50), -- transaction id
    valor_centavos INTEGER NOT NULL,
    meio_pagamento VARCHAR(20) NOT NULL, -- pix | credit_card | boleto
    status VARCHAR(30) NOT NULL, -- pendente | processando | pago | falha | cancelado
    split_json JSONB NOT NULL, -- snapshot das split rules
    metadados_json JSONB,
    criado_em_utc TIMESTAMPTZ NOT NULL,
    atualizado_em_utc TIMESTAMPTZ NOT NULL
);
```

### Endpoints novos

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/aluno/{unidadeId}/pagamento/{cobrancaId}` | Tela de pagamento |
| POST | `/aluno/{unidadeId}/pagamento/criar` | Criar pedido no Pagar.me |
| GET | `/aluno/{unidadeId}/pagamento/{pedidoId}/status` | Consultar status |
| POST | `/webhook/pagarme` | Receber notificações |

### Configuração necessária

```json
// appsettings.json
{
  "PagarMe": {
    "ApiKey": "sk_live_XXXXX",
    "EncryptionKey": "ek_XXXXX",
    "WebhookSecret": "whsec_XXXXX",
    "SplitDefaults": {
      "FranqueadoraPercentual": 15,
      "ProfessorPercentual": 10
    }
  }
}
```

## Regras de negócio

1. **Split configurável por unidade** — franqueadora e professor podem ter % diferentes
2. **Chargeback** — Franqueado é o `liable` principal (responsável financeiro)
3. **Taxas de processamento** — Franqueado absorve (`charge_processing_fee: true`)
4. **Resto de divisão** — Franqueado absorve centavos (`charge_remainder_fee: true`)
5. **Idempotência** — Webhook deve ser idempotente (evitar duplicação)
6. **Conciliação** — Pagar.me gera relatório por transação para conciliação

## Escopo

### MVP (Fase 1)
- Criar recipients para unidade (franqueadora + franqueado)
- Checkout Pix (mais simples, liquidação imediata)
- Webhook para atualizar status
- Dashboard de conciliação básico

### Fase 2
- Checkout cartão de crédito
- Checkout boleto
- Split com professor
- Antecipação de recebíveis
- Relatórios de conciliação

### Fora do escopo
- Pagamento recorrente automático (assinatura)
- Notificação push para franqueado
- App mobile de pagamento

## Riscos

1. **Onboarding KYB** — Cada recipient precisa de dados válidos (CPF/CNPJ)
2. **Webhook reliability** — Pode falhar; precisa de retry + reconciliação
3. **Taxas variáveis** — Pagar.me cobra % por transação; precisa considerar no modelo
4. **PIX vs Cartão** — Liquidação diferente (PIX: same-day, Cartão: D+2 a D+30)

## Critérios de aceite

- [ ] Aluno consegue pagar via Pix na área do aluno
- [ ] Split automático entre franqueadora e franqueado
- [ ] Webhook atualiza status de pagamento corretamente
- [ ] Cobrança é marcada como paga após confirmação
- [ ] Dashboard de conciliação mostra transações
- [ ] `dotnet build` sem erros
- [ ] `dotnet test` passando
