# Respostas

## Parte 4: SQL e modelagem

### 1. Pedidos por TipoPedido criados em setembro de 2026
```sql
  SELECT TipoPedido, 
         COUNT(*) AS Quantidade
    FROM Pedido
   WHERE DataCriacao >= '2026-09-01' AND DataCriacao < '2026-10-01'
GROUP BY TipoPedido;
```
Usei início inclusivo e fim exclusivo (em vez de BETWEEN) porque DataCriacao é DATETIME: com BETWEEN ... AND '2026-09-30', os pedidos criados durante o dia 30 ficariam de fora.

### 2. Pedidos "travados" (PAGO há mais de 2 horas, sem notificação CONCLUIDO)
```sql
SELECT *
  FROM Pedido
 WHERE Pedido.Status = 'PAGO'
   AND Pedido.DataCriacao < DATEADD(HOUR, -2, GETDATE())
   AND NOT EXISTS (
       SELECT 1
         FROM ControleNotificacao
        WHERE ControleNotificacao.PedidoId = Pedido.Id
          AND ControleNotificacao.Status = 'CONCLUIDO');
```
Premissas: a tabela não tem data de pagamento, então usei DataCriacao como referência (o ideal seria uma coluna DataPagamento). Também assumi que as datas são gravadas no horário do servidor; se fossem em UTC, usaria GETUTCDATE().

### 3. As 5 lojas com mais notificações em FALHA
```sql
   SELECT TOP 5 Pedido.LojaId, COUNT(*) AS QtdFalhas
     FROM Pedido
     JOIN ControleNotificacao ON ControleNotificacao.PedidoId = Pedido.Id
    WHERE ControleNotificacao.Status = 'FALHA'
 GROUP BY Pedido.LojaId
 ORDER BY COUNT(*) DESC;
```

### 4. PedidoSVL duplicado
Sim, é um problema. O PedidoSVL é o número que todos os sistemas usam para identificar o pedido. Se existirem duas linhas com PedidoSVL = 1007, a API não sabe em qual delas salvar os links nem qual marcar como paga, e cada sistema pode agir sobre uma linha diferente.

Eu tornaria o PedidoSV` único:
```sql
ALTER TABLE Pedido ADD CONSTRAINT UQ_Pedido_PedidoSVL UNIQUE (PedidoSVL);
```
Só validar no código não basta: se a mesma mensagem chegar duas vezes ao mesmo tempo, as duas requisições podem passar pela verificação "o pedido já existe?" antes de qualquer uma gravar. Com a constraint, o banco rejeita o segundo insert e garante que a duplicata não existe.

### 5. Índices para a consulta 2 (rodando a cada 5 minutos)
Sem índice, a consulta faz um *table scan*, lendo todas as linhas da tabela a cada execução, o que fica lento e pesado com milhões de registros. Eu criaria dois índices:
```sql
CREATE INDEX IX_Pedido_Status_DataCriacao
    ON Pedido (Status, DataCriacao);

CREATE INDEX IX_ControleNotificacao_PedidoId_Status
    ON ControleNotificacao (PedidoId, Status);
```
- Em Pedido, a coluna comparada com igualdade (Status) vem primeiro e a de intervalo (DataCriacao) depois, para o banco chegar direto nos pedidos PAGO e, dentro deles, nos mais antigos que 2 horas.
- Em ControleNotificacao, o índice permite que o NOT EXISTS verifique rápido, para cada pedido, se existe uma notificação CONCLUIDO.