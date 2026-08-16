# Korp - Sistema de emissão de notas fiscais

Aplicação do desafio técnico com frontend Angular 20 e dois microsserviços em C#/.NET 10. O projeto persiste produtos, estoque e notas em bancos SQLite reais e separados.

## Executar

Pré-requisito: Docker Desktop.

```bash
docker compose up --build
```

Acesse `http://localhost:4200`. As APIs ficam em `http://localhost:5101` (estoque) e `http://localhost:5102` (faturamento).

Para desenvolvimento sem Docker, execute em três terminais:

```bash
dotnet run --project src/Inventory.Api --urls http://localhost:5101
dotnet run --project src/Billing.Api --urls http://localhost:5102
cd src/web && npm install && npm start
```

## Fluxo funcional

1. Cadastre produtos com código, descrição e saldo.
2. Em **Notas fiscais**, adicione um ou mais produtos e emita a nota. Ela nasce `Open` (Aberta) e recebe numeração sequencial.
3. Use **Imprimir e fechar**. A interface mostra o processamento, o faturamento pede a baixa ao estoque e, após sucesso, fecha a nota.
4. Uma nota fechada não pode ser impressa novamente.

## Demonstração de falha e recuperação

Ative **Simular falha no estoque** no topo da interface e tente imprimir uma nota aberta. O serviço retorna 503, a política de resiliência tenta novamente e a interface informa que o estoque está indisponível. A nota permanece aberta e nenhum saldo é alterado. Desative a simulação e tente novamente: a operação conclui normalmente.

## Decisões técnicas

- `Inventory.Api`: cadastro de produtos, saldo e baixa idempotente.
- `Billing.Api`: notas, itens, numeração, fechamento e comunicação HTTP resiliente.
- Cada microsserviço é dono de seu banco SQLite. Não há acesso cruzado a tabelas.
- A baixa usa transação serializável, valida o saldo antes da alteração e consolida itens repetidos.
- A chave `invoice-{id}` torna a baixa idempotente. Se a resposta se perder e o faturamento repetir a chamada, o estoque não é descontado duas vezes.
- Retry com atraso, timeout por tentativa e timeout total são fornecidos pelo resilience handler do .NET.
- Erros de validação usam 400, conflitos de regra usam 409 e indisponibilidade usa 503, sempre com feedback legível no frontend.

Detalhes para a apresentação estão em [docs/DETALHAMENTO_TECNICO.md](docs/DETALHAMENTO_TECNICO.md) e [docs/ROTEIRO_VIDEO.md](docs/ROTEIRO_VIDEO.md).
