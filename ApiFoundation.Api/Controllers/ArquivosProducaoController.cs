using ApiFoundation.Api.Dtos;
using ApiFoundation.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiFoundation.Api.Controllers;

[ApiController]
[Route("api/arquivos-producao")]
public class ArquivosProducaoController : ControllerBase
{
    private readonly ProcessadorArquivosProducao _processador;

    public ArquivosProducaoController(ProcessadorArquivosProducao processador)
    {
        _processador = processador;
    }

    [HttpPost]
    public async Task<IActionResult> Post(ArquivosProducaoRequest request)
    {
        if (request.HasErrors)
            return UnprocessableEntity(new { mensagem = "O envio foi rejeitado porque hasErrors é true." });

        var resultados = await _processador.ProcessarAsync(request);
        return Ok(resultados);
    }
}