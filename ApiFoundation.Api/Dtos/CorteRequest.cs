namespace ApiFoundation.Api.Dtos;

public class CorteRequest
{
    public List<string> PdfSumario { get; set; } = new();
    public List<string> PdfDetalhado { get; set; } = new();
}