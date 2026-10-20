using ApiFoundation.Api.Data;
using ApiFoundation.Api.Dtos;
using ApiFoundation.Api.Models;

namespace ApiFoundation.Api.Services;

public class ProcessadorArquivosProducao
{
    private readonly AppDbContext _db;
    private readonly IArmazenamentoArquivos _armazenamento;

    public ProcessadorArquivosProducao(AppDbContext db, IArmazenamentoArquivos armazenamento)
    {
        _db = db;
        _armazenamento = armazenamento;
    }

    public async Task<List<ResultadoServico>> ProcessarAsync(ArquivosProducaoRequest request)
    {
        var resultados = new List<ResultadoServico>();

        foreach (var servico in request.Services)
        {
            resultados.Add(await ProcessarServicoAsync(servico));
        }

        return resultados;
    }

    private async Task<ResultadoServico> ProcessarServicoAsync(ServicoRequest servico)
    {
        var pedido = await _db.Pedidos.FindAsync(servico.PedidoSVL);

        if (pedido == null)
        {
            return Resultado(servico, StatusResultado.Falha, "Pedido não encontrado.");
        }

        if (pedido.ServiceId != servico.ServiceId)
        {
            return Resultado(servico, StatusResultado.Falha, "ServiceId não corresponde ao pedido.");
        }

        return Resultado(servico, StatusResultado.Processado, "Serviço processado com sucesso.");
    }

    private static ResultadoServico Resultado(ServicoRequest s, StatusResultado status, string mensagem) =>
        new() { PedidoSVL = s.PedidoSVL, ServiceId = s.ServiceId, Status = status, Mensagem = mensagem };
}