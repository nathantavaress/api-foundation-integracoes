using ApiFoundation.Api.Data;
using ApiFoundation.Api.Dtos;
using ApiFoundation.Api.Models;
using Microsoft.EntityFrameworkCore;

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

        var recebidos = ExtrairLinks(servico.Links);
        var existentes = await _db.Links.Where(l => l.PedidoSVL == pedido.PedidoSVL).ToListAsync();
        var novos = recebidos.Where(r => !existentes.Any(e => e.Categoria == r.Categoria && e.Url == r.Url)).ToList();

        if (novos.Count == 0)
        {
            return Resultado(servico, StatusResultado.JaProcessado, "Serviço já processado com os mesmos links.");
        }

        foreach (var (categoria, url) in novos)
        {
            var link = new LinkArquivo
            {
                PedidoSVL = pedido.PedidoSVL,
                Categoria = categoria,
                Url = url
            };
            _db.Links.Add(link);

            if (pedido.Tipo != TipoPedido.ZMOB)
            {
                await _armazenamento.CopiarAsync(url);
            }
        }

        await _db.SaveChangesAsync();

        return Resultado(servico, StatusResultado.Processado, "Serviço processado com sucesso.");
    }

    private static List<(CategoriaLink Categoria, string Url)> ExtrairLinks(LinksRequest links)
    {
        var lista = new List<(CategoriaLink Categoria, string Url)>();
        lista.AddRange(links.Corte.PdfSumario.Select(u => (CategoriaLink.CorteSumario, u)));
        lista.AddRange(links.Corte.PdfDetalhado.Select(u => (CategoriaLink.CorteDetalhado, u)));
        lista.AddRange(links.Etiquetas.Select(u => (CategoriaLink.Etiqueta, u)));
        lista.AddRange(links.Usinagem.Select(u => (CategoriaLink.Usinagem, u)));
        return lista;
    }

    private static ResultadoServico Resultado(ServicoRequest s, StatusResultado status, string mensagem) =>
        new() { PedidoSVL = s.PedidoSVL, ServiceId = s.ServiceId, Status = status, Mensagem = mensagem };
}