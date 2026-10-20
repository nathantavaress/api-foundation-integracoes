namespace ApiFoundation.Api.Dtos;

public class LinksRequest
{
    public CorteRequest Corte { get; set; } = new();
    public List<string> Etiquetas { get; set; } = new();
    public List<string> Usinagem { get; set; } = new();
}