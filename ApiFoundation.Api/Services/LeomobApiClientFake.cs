namespace ApiFoundation.Api.Services;

public class LeomobApiClientFake : ILeomobApiClient
{
    private readonly IConfiguration _config;
    private readonly ILogger<LeomobApiClientFake> _logger;

    public LeomobApiClientFake(IConfiguration config, ILogger<LeomobApiClientFake> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task NotificarAsync(int pedidoSVL, IEnumerable<string> links)
    {
        if (_config.GetValue<bool>("LeomobFake:FalharSempre"))
        {
            throw new InvalidOperationException("Falha simulada da LeomobAPI.");
        }

        _logger.LogInformation("Notificou a LeomobAPI sobre o pedido {PedidoSVL}", pedidoSVL);
        return Task.CompletedTask;
    }
}