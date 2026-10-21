namespace ApiFoundation.Api.Models;

public class Notificacao
{
	public long Id { get; set; }
	public StatusNotificacao Status { get; set; }
	public int PedidoSVL { get; set; }
	public int Tentativas { get; set; }
	public DateTime DataCriacao { get; set; }
}
