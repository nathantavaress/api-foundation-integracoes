namespace ApiFoundation.Api.Dtos;

public class ArquivosProducaoRequest
{
    public string DocumentType { get; set; } = string.Empty;
    public bool HasErrors { get; set; }
    public List<ServicoRequest> Services { get; set; } = new();
}

