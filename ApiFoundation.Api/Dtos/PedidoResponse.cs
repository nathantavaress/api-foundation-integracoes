namespace ApiFoundation.Api.Dtos
{
    public class PedidoResponse
    {
        public int PedidoSVL { get; set; }
        public long ServiceId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public List<LinkResponse> Links { get; set; } = new();
        public NotificacaoResponse? Notificacao { get; set; }
    }
}
