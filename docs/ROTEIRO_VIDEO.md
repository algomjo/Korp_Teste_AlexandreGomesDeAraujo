# Roteiro sugerido para o vídeo (6-8 minutos)

## 1. Visão geral (40s)

Apresente a tela e explique Angular + dois microsserviços C#/.NET, cada um com banco próprio.

## 2. Produtos (1min)

Cadastre dois produtos, mostre validação e saldo persistido. Reinicie um container se quiser evidenciar a persistência física.

## 3. Nota fiscal (1min30s)

Monte uma nota com múltiplos produtos. Mostre a numeração automática, o status Aberta e o botão de impressão.

## 4. Impressão e estoque (1min)

Clique em imprimir, destaque o indicador de processamento, o status Fechada e a redução dos saldos. Mostre que o botão fica indisponível após o fechamento.

## 5. Falha e recuperação (1min30s)

Crie outra nota, ative a falha simulada e tente imprimir. Mostre o feedback, a nota ainda aberta e o saldo intacto. Desative a falha, repita e mostre o sucesso.

## 6. Detalhamento técnico (2min)

Mostre rapidamente a separação dos projetos, bancos, chamada HTTP resiliente, transação e chave de idempotência. Cite `ngOnInit`, `ngOnDestroy`, `forkJoin`, `takeUntil`, `finalize`, ASP.NET Core, EF Core, SQLite e os usos de LINQ documentados.
