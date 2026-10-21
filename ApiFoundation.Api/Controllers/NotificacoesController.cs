using ApiFoundation.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiFoundation.Api.Controllers;

[ApiController]
[Route("api/notificacoes")]
public class NotificacoesController : ControllerBase
{
    private readonly ReprocessadorNotificacoes _reprocessador;

    public NotificacoesController(ReprocessadorNotificacoes reprocessador)
    {
        _reprocessador = reprocessador;
    }

    [HttpPost("reprocessar")]
    public async Task<IActionResult> Reprocessar()
    {
        var quantidade = await _reprocessador.ReprocessarAsync();
        return Ok(new { reprocessadas = quantidade });
    }
}