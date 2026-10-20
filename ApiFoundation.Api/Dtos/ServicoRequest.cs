namespace ApiFoundation.Api.Dtos;

public class ServicoRequest
{
    public long ServiceId { get; set; }
    public int PedidoSVL { get; set; }
    public LinksRequest Links { get; set; } = new();
}