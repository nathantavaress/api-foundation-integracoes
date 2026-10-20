namespace ApiFoundation.Api.Services;

public interface IArmazenamentoArquivos
{
    Task CopiarAsync(string url);
}