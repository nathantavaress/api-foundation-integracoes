namespace ApiFoundation.Api.Services
{
    public interface ILeomobApiClient
    {
        Task NotificarAsync(int pedidoSVL, IEnumerable<string> links);
    }
}
