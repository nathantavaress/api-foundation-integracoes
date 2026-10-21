# API Foundation (simulação): integração de arquivos de produção

API em .NET 8 que simula a API Foundation recebendo, via Digibee, os links dos arquivos de produção do CorteCloud.

## Como rodar
Pré-requisito: SDK do .NET 8 (o `global.json` fixa a versão).

```bash
dotnet run --project ApiFoundation.Api
```

O Swagger fica em `/swagger` (use a URL que o terminal mostrar). O banco SQLite (`app.db`) é criado e populado com os pedidos 1007, 1008 e 1012 ao subir. Para recriar o banco, apague `app.db*` com a API parada.

## Como rodar os testes
```bash
dotnet test
```

## Endpoints
- `POST /api/arquivos-producao`: recebe os links de um ou mais serviços.
- `GET /api/pedidos/{pedidoSVL}`: pedido, links e status da notificação.
- `POST /api/notificacoes/reprocessar`: simula o worker que tenta de novo as notificações `PENDENTE`.

## Como simular falha da LeomobAPI
Em `appsettings.json`, defina `"LeomobFake": { "FalharSempre": true }` e reinicie a API.

## Premissas
- `PedidoSVL` é a chave primária do pedido (vem do SVL, não é gerado pelo banco).
- `hasErrors = true` rejeita o envio inteiro com **422**.
- Pedido inexistente ou `serviceId` divergente falham só aquele serviço; a resposta é uma lista com o resultado de cada um (`Processado`, `JaProcessado` ou `Falha`).
- Idempotência: só links ainda não salvos (mesma categoria e URL) são gravados e copiados; se não houver nenhum novo, o resultado é `JaProcessado` e nada é duplicado, nem a notificação.
- Se o mesmo serviço chegar com links diferentes, os novos são adicionados.
- `Tentativas` conta as tentativas que falharam; ao chegar em 3, a notificação vai para `FALHA`.
- Banco SQLite criado com `EnsureCreated()`, sem migrations.
- Armazenamento de arquivos e LeomobAPI são implementações falsas atrás de interfaces.

## O que melhoraria com mais tempo
- Gravar os links e a notificação `INICIAL` na mesma transação (hoje, se o processo cair entre os dois passos, a notificação se perde).
- Migrations e índice único em `LinkArquivo (PedidoSVL, Categoria, Url)` para proteger contra envios simultâneos.
- Reprocessamento automático com `BackgroundService` e logs estruturados com o `pedidoSVL`.
- Implementações reais de armazenamento e LeomobAPI, com timeout e política de retentativa.