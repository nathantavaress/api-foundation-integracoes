namespace ApiFoundation.Api.Services;

public class ArmazenamentoArquivosFake : IArmazenamentoArquivos
{
    private readonly ILogger<ArmazenamentoArquivosFake> _logger;

    public ArmazenamentoArquivosFake(ILogger<ArmazenamentoArquivosFake> logger)
    {
        _logger = logger;
    }

    public Task CopiarAsync(string url)
    {
        _logger.LogInformation("Copiaria o arquivo {Url} para o armazenamento interno", url);
        return Task.CompletedTask;
    }
}