using ApiFoundation.Api.Data;
using ApiFoundation.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Api.Controllers;

[ApiController]
[Route("api/pedidos")]
public class PedidosController : ControllerBase
{
    private readonly AppDbContext _db;

    public PedidosController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{pedidoSVL}")]
    public async Task<IActionResult> Get(int pedidoSVL)
    {
        var pedido = await _db.Pedidos.Include(p => p.Links).FirstOrDefaultAsync(p => p.PedidoSVL == pedidoSVL);
        
        if (pedido == null)
        {
            return NotFound();
        }

        var notificacao = await _db.Notificacoes.Where(n => n.PedidoSVL == pedidoSVL).OrderByDescending(n => n.DataCriacao).FirstOrDefaultAsync();
        var pedidoResponse = new PedidoResponse
        {
            PedidoSVL = pedido.PedidoSVL,
            ServiceId = pedido.ServiceId,
            Tipo = pedido.Tipo.ToString(),
            Links = pedido.Links.Select(l => new LinkResponse
            {
                Categoria = l.Categoria.ToString(),
                Url = l.Url
            }).ToList(),
            Notificacao = notificacao != null ? new NotificacaoResponse
            {
                Status = notificacao.Status.ToString(),
                Tentativas = notificacao.Tentativas
            } : null
        };

        return Ok(pedidoResponse);
    }
}