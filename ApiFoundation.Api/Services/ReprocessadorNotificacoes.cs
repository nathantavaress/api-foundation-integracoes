using ApiFoundation.Api.Data;
using ApiFoundation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Api.Services;

public class ReprocessadorNotificacoes
{
    private readonly AppDbContext _db;
    private readonly ILeomobApiClient _leomob;

    public ReprocessadorNotificacoes(AppDbContext db, ILeomobApiClient leomob)
    {
        _db = db;
        _leomob = leomob;
    }

    public async Task<int> ReprocessarAsync()
    {
        // Só as notificações que ainda precisam de nova tentativa (FALHA e CONCLUIDO ficam de fora)
        var pendentes = await _db.Notificacoes
            .Where(n => n.Status == StatusNotificacao.PENDENTE)
            .ToListAsync();

        foreach (var notificacao in pendentes)
        {
            // URLs dos links do pedido, para reenviar à LeomobAPI
            var urls = await _db.Links
                .Where(l => l.PedidoSVL == notificacao.PedidoSVL)
                .Select(l => l.Url)
                .ToListAsync();

            try
            {
                await _leomob.NotificarAsync(notificacao.PedidoSVL, urls);
                notificacao.Status = StatusNotificacao.CONCLUIDO;
            }
            catch (Exception)
            {
                // Tentativas conta as que falharam: ao chegar em 3, desistimos (FALHA)
                // e o worker não tenta mais
                notificacao.Tentativas++;
                if (notificacao.Tentativas >= 3)
                {
                    notificacao.Status = StatusNotificacao.FALHA;
                }
            }
        }

        await _db.SaveChangesAsync();
        return pendentes.Count;
    }
}