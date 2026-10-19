namespace ApiFoundation.Api.Models;

public class LinkArquivo
{
	public long Id { get; set; }
	public int PedidoSVL { get; set; }
	public CategoriaLink Categoria { get; set; }
	public string Url { get; set; } = string.Empty;
}
