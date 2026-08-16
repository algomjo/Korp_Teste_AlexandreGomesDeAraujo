# Detalhamento técnico

## Arquitetura

O frontend Angular consome dois microsserviços independentes. O serviço de Faturamento não manipula o banco de Estoque: ao imprimir uma nota, solicita a baixa pela API HTTP. Cada serviço possui seu próprio banco SQLite persistido em volume Docker.

```text
Angular :4200 -> Inventory.Api :5101 -> inventory.db
       `------> Billing.Api   :5102 -> billing.db
                         `--HTTP resiliente--> Inventory.Api
```

## Angular

A aplicação usa componentes standalone e os ciclos de vida `ngOnInit` e `ngOnDestroy`. No `ngOnInit`, carrega produtos e notas. No `ngOnDestroy`, encerra o `Subject` usado por `takeUntil`, evitando inscrições pendentes.

RxJS é usado em:

- `forkJoin`, para buscar os dois conjuntos em paralelo;
- `takeUntil`, para vincular o tempo de vida das inscrições ao componente;
- `finalize`, para desligar indicadores de carregamento e impressão tanto no sucesso quanto no erro;
- `Observable`, como contrato assíncrono do serviço HTTP.

Não foi adotada biblioteca visual externa: a interface usa HTML e CSS próprios, o que reduz dependências e demonstra domínio de layout responsivo. `FormsModule` atende os formulários e `HttpClient` realiza a integração.

## C# e frameworks

Os serviços usam ASP.NET Core Minimal APIs, Entity Framework Core e o provider SQLite. O `Microsoft.Extensions.Http.Resilience` implementa retry e timeouts na comunicação entre os microsserviços.

LINQ aparece de forma intencional para:

- ordenar e projetar consultas (`OrderBy`, `OrderByDescending`, `Select`);
- agrupar itens repetidos e somar quantidades (`GroupBy`, `Sum`, `ToDictionary`);
- filtrar produtos e detectar saldo insuficiente (`Where`, `Any`);
- calcular a próxima numeração com `MaxAsync`.

## Regras e consistência

A nota nasce aberta. Somente uma nota aberta pode ser impressa. A baixa de estoque ocorre antes da mudança para fechada. Se o Estoque falhar ou recusar saldo, o Faturamento mantém a nota aberta.

O Estoque executa a baixa em uma transação serializável. Uma restrição única protege a chave de idempotência e outra protege o código do produto. A numeração da nota também tem índice único.

Em sistemas de maior escala, SQLite pode ser trocado por PostgreSQL e a operação distribuída pode evoluir para Saga/Outbox com mensageria. Para este escopo, a chamada síncrona idempotente torna o comportamento simples de executar e demonstrar.

## Tratamento de erros

Validações de entrada retornam `ValidationProblem`; duplicidade, saldo insuficiente e mudança inválida de estado retornam conflito; indisponibilidade retorna `ProblemDetails` com status 503. Exceções de comunicação são registradas com logging estruturado sem expor detalhes internos ao usuário.

## Requisitos opcionais cobertos

- Concorrência: transação serializável e restrições únicas.
- Idempotência: chave estável por nota na baixa de estoque.
- IA: não implementada, para priorizar consistência transacional e recuperação de falhas.
