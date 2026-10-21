# Respostas

## Parte 1: Questões conceituais

### 1. Síncrono x assíncrono
Em uma chamada HTTP síncrona, quem chama espera a resposta antes de continuar. Em uma integração assíncrona (fila, webhook), o sistema manda a mensagem, recebe um "recebi" e segue, e o resultado volta depois. Neste projeto os sistemas são de times e fornecedores diferentes (SVL, Digibee, CorteCloud, ERP). Se fosse síncrono, o SVL ficaria esperando o CorteCloud gerar os arquivos, e a lentidão ou queda de um sistema travaria os outros. Com o assíncrono, cada parte faz o seu trabalho sem depender do fim da outra.

### 2. Mensagem duplicada
Se a Digibee chamar duas vezes com o pagamento do pedido 1007, podem surgir registros duplicados, o serviço pode ir duas vezes para produção ou o SVL pode ser avisado duas vezes, e o sistema se perde sobre qual registro vale. Um endpoint idempotente é aquele que pode receber a mesma operação várias vezes sem mudar o resultado: a primeira vale e as seguintes não alteram nada. Para garantir, o endpoint verifica se aquele pedido (ou o id da mensagem) já foi processado antes de agir, e um índice único no banco impede que duas requisições simultâneas passem juntas.

### 3. Falha no meio do caminho
O pedido já foi salvo, mas o aviso ao SVL deu timeout, e o risco é o SVL nunca saber. A estratégia é gravar, antes de chamar o SVL, um registro de notificação com status `INICIAL`. Se a chamada der certo, o status vira `CONCLUIDO`. Se falhar, vira `PENDENTE`, e um worker tenta de novo periodicamente; depois de algumas tentativas sem sucesso vai para `FALHA`, sinalizando que alguém precisa olhar. Assim, mesmo que o servidor caia no meio, o registro mostra que havia um aviso a fazer (é uma versão simples do padrão outbox).

### 4. Códigos HTTP
- Pedido criado com sucesso: **201 Created**, pois um recurso novo foi criado (200 também seria aceitável).
- Corpo sem o campo obrigatório `serviceId`: **400 Bad Request**, pois o erro está na requisição do cliente.
- Token de autenticação ausente: **401 Unauthorized**, pois o cliente não se autenticou.
- Pedido informado não existe: **404 Not Found**, pois o recurso pedido não existe.
- Erro inesperado no banco: **500 Internal Server Error**, pois o erro é do servidor, não de quem chamou.

### 5. Intermediário (Digibee)
Vantagem: cada sistema só precisa conhecer a Digibee. Ela recebe de várias partes e redireciona para o lugar certo, então o fluxo fica organizado e, se um sistema mudar (como o CorteCloud), só a integração dentro dela muda. Desvantagem: ela vira um ponto único de falha. Se a Digibee falhar ou atrasar, as mensagens ficam paradas ou atrasadas, e é preciso ter retentativa e idempotência para reprocessar sem duplicar.

## Parte 3: Leitura de código

Linhas contadas a partir de public async Task ProcessarPagamentoAsync(...) como linha 1 (linhas em branco incluídas).

### 1. Problemas encontrados
| # | Linha | Problema | Impacto em produção | Gravidade |
|---|-------|----------|---------------------|-----------|
| 1 | 4 | SQL montado por concatenação com valor vindo da requisição (SQL injection) | Um valor malicioso pode vazar ou destruir dados do banco | Alta |
| 2 | 11 | Chamada à Digibee sem `await` | Erros se perdem e o método pode terminar antes do envio acabar | Média |
| 3 | 7 a 17 | A Digibee só é chamada quando o pedido é novo (no `else` não) | Pedido pago que já existia nunca vai para produção; além disso o pedido novo é criado sem `ServiceId` | Alta |
| 4 | 15 | `Status = "PAGO"` sobrescreve qualquer status | Um pedido `EM_PRODUCAO` ou `FINALIZADO` volta para `PAGO` | Média |
| 5 | 24 a 29 | `CONCLUIDO` é gravado antes de o SVL responder e o `catch` está vazio | O SVL pode nunca ser avisado, mas o banco diz que foi, e nenhum worker tenta de novo | Alta |
| 6 | 7 a 20 | Sem transação nem proteção contra duplicidade | Se o servidor cair entre os inserts, o banco fica inconsistente; a mesma notificação duas vezes pode criar pedido e controle em duplicidade | Alta |
| 7 | 9 e 19 | Status escritos como texto solto | Erro de digitação não é detectado pelo compilador | Baixa |

### 2. Método corrigido
```csharp
public async Task ProcessarPagamentoAsync(NotificacaoPagamento notif)
{
    // Parâmetro no lugar de concatenação: evita SQL injection
    var pedido = await _db.QueryFirstOrDefaultAsync<Pedido>(
        "SELECT * FROM Pedidos WHERE PedidoSVL = @PedidoSVL",
        new { notif.PedidoSVL });

    ControleNotificacao controle;

    // Pedido e controle gravados juntos: ou entra tudo, ou nada
    using (var transacao = _db.BeginTransaction())
    {
        if (pedido == null)
        {
            pedido = new Pedido { PedidoSVL = notif.PedidoSVL, Status = StatusPedido.Pago };
            await _db.InsertAsync(pedido);
        }
        else if (pedido.Status == StatusPedido.Criado)
        {
            // Só avança o status; não volta um pedido já em produção ou finalizado
            pedido.Status = StatusPedido.Pago;
            await _db.UpdateAsync(pedido);
        }

        // Reaproveita o controle existente (idempotência) ou cria um novo
        controle = await _db.QueryFirstOrDefaultAsync<ControleNotificacao>(
            "SELECT * FROM ControleNotificacao WHERE PedidoSVL = @PedidoSVL",
            new { pedido.PedidoSVL })
            ?? new ControleNotificacao { PedidoSVL = pedido.PedidoSVL, Status = StatusNotificacao.Inicial };

        if (controle.Id == 0)
            await _db.InsertAsync(controle);

        transacao.Commit();
    }

    // Em produção sempre, com await para não perder erros
    await _digibee.EnviaServicoParaProducaoAsync(pedido.ServiceId, pedido.PedidoSVL);

    // Notificação repetida: o SVL já foi avisado, não avisa de novo
    if (controle.Status == StatusNotificacao.Concluido)
        return;

    try
    {
        await _svl.NotificarPagamentoAsync(pedido.PedidoSVL);
        controle.Status = StatusNotificacao.Concluido; // só depois de o SVL responder
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Falha ao notificar o SVL do pedido {PedidoSVL}", pedido.PedidoSVL);
        controle.Status = StatusNotificacao.Pendente;  // o worker tenta de novo
        controle.Tentativas++;
    }

    await _db.UpdateAsync(controle);
}
```
Premissas: `BeginTransaction()` e `_logger` são suposições, pois o enunciado não mostra como o acesso a dados abre transação nem como se registra log; `StatusPedido` e `StatusNotificacao` são enums no lugar dos textos.

### 3. E se o ERP enviar a mesma notificação duas vezes?
No código original, o pedido ou o controle podem ser criados em duplicidade e o SVL pode ser avisado duas vezes. No código corrigido, o método busca o controle existente antes de criar outro e, se ele já está `CONCLUIDO`, não avisa o SVL de novo; além disso, só avança o status do pedido quando ele ainda está `CRIADO`. Isso resolve o reenvio em sequência, mas não duas requisições simultâneas, que podem passar pela busca ao mesmo tempo. Para isso, eu adicionaria um índice único em `PedidoSVL` no banco (nas tabelas de pedido e de controle), e a segunda gravação seria rejeitada.

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

Eu tornaria o PedidoSVL único:
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
