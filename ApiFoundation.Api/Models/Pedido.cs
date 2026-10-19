namespace ApiFoundation.Api.Models;

public class Pedido
{
    public int PedidoSVL { get; set; }
    public long ServiceId { get; set; }
    public TipoPedido Tipo { get; set; }

    public List<LinkArquivo> Links { get; set; } = new();
}
