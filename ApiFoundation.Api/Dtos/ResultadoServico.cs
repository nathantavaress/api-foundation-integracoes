namespace ApiFoundation.Api.Dtos;

public class ResultadoServico
{
    public int PedidoSVL { get; set; }
    public long ServiceId { get; set; }
    public StatusResultado Status { get; set; }
    public string? Mensagem { get; set; }
}