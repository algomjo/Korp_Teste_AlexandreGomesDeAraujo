# Korp - Sistema de Emissão de Notas Fiscais

Aplicação full stack desenvolvida como desafio técnico, com **Angular 20** no frontend e **dois microsserviços em C#/.NET 10**. O sistema gerencia produtos, estoque e emissão de notas fiscais, com bancos SQLite separados por serviço e tratamento de falhas entre APIs.

## Stack

- C# / .NET 10
- ASP.NET Core Web API
- Angular 20
- TypeScript
- SQLite
- Docker / Docker Compose
- Comunicação HTTP entre microsserviços
- Resiliência com retry e timeout

## Arquitetura

```text
Angular
   │
   ├──────────────► Inventory.Api
   │                 │
   │                 ▼
   │              SQLite
   │
   └──────────────► Billing.Api
                     │
                     ├──── HTTP resiliente ────► Inventory.Api
                     │
                     ▼
                  SQLite
```

Cada microsserviço é responsável pelo próprio banco de dados. Não existe acesso direto às tabelas do outro serviço.

## Funcionalidades

- Cadastro de produtos.
- Controle de saldo em estoque.
- Criação de notas fiscais com múltiplos itens.
- Numeração sequencial das notas.
- Fechamento da nota após baixa do estoque.
- Validação de saldo antes da movimentação.
- Proteção contra fechamento repetido da mesma nota.
- Baixa de estoque idempotente.
- Simulação de indisponibilidade do serviço de estoque.
- Retry e timeout na comunicação entre serviços.
- Feedback de erros de validação, conflito e indisponibilidade no frontend.

## Executar com Docker

Pré-requisito: **Docker Desktop**.

```bash
git clone https://github.com/algomjo/Korp_Teste_AlexandreGomesDeAraujo.git
cd Korp_Teste_AlexandreGomesDeAraujo
docker compose up --build
```

Depois acesse:

- Frontend: `http://localhost:4200`
- Inventory API: `http://localhost:5101`
- Billing API: `http://localhost:5102`

## Executar em desenvolvimento

Execute os serviços em terminais separados:

```bash
dotnet run --project src/Inventory.Api --urls http://localhost:5101
```

```bash
dotnet run --project src/Billing.Api --urls http://localhost:5102
```

```bash
cd src/web
npm install
npm start
```

## Fluxo principal

1. Cadastre um ou mais produtos com código, descrição e saldo.
2. Crie uma nota fiscal e adicione os produtos desejados.
3. A nota é criada no estado `Open` e recebe numeração sequencial.
4. Ao usar **Imprimir e fechar**, o serviço de faturamento solicita a baixa ao serviço de estoque.
5. Se a operação for concluída, a nota é fechada.
6. Uma nota fechada não pode ser processada novamente.

## Resiliência e idempotência

O projeto inclui um cenário explícito para demonstrar falha e recuperação entre microsserviços.

Ao ativar **Simular falha no estoque**, o serviço de estoque responde com `503`. A comunicação utiliza políticas de retry e timeout, mas a nota permanece aberta e o saldo não é alterado enquanto a operação não for concluída.

A baixa utiliza uma chave no formato `invoice-{id}` para garantir idempotência. Se uma chamada precisar ser repetida por perda de resposta ou falha de comunicação, o estoque não é descontado duas vezes.

## Decisões técnicas

- `Inventory.Api` é responsável por produtos, saldo e movimentação de estoque.
- `Billing.Api` é responsável por notas, itens, numeração e fechamento.
- Cada serviço possui seu próprio banco SQLite.
- A baixa de estoque utiliza transação serializável.
- Itens repetidos são consolidados antes da alteração do saldo.
- Erros de validação retornam `400`.
- Conflitos de regra de negócio retornam `409`.
- Indisponibilidade de serviço retorna `503`.

## Documentação adicional

- [Detalhamento técnico](docs/DETALHAMENTO_TECNICO.md)
- [Roteiro de apresentação](docs/ROTEIRO_VIDEO.md)

---

Desenvolvido por [Alexandre Gomes de Araújo](https://github.com/algomjo).