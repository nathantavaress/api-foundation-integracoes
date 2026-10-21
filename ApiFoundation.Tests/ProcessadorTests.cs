using ApiFoundation.Api.Data;
using ApiFoundation.Api.Dtos;
using ApiFoundation.Api.Models;
using ApiFoundation.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Tests;

// Armazenamento falso que só anota quais URLs seriam copiadas
public class ArmazenamentoSpy : IArmazenamentoArquivos
{
    public List<string> Copiados { get; } = new();

    public Task CopiarAsync(string url)
    {
        Copiados.Add(url);
        return Task.CompletedTask;
    }
}

// LeomobAPI falsa: falha quando Falhar = true e conta quantas vezes foi chamada
public class LeomobFalsa : ILeomobApiClient
{
    public bool Falhar { get; set; }
    public int Chamadas { get; private set; }

    public Task NotificarAsync(int pedidoSVL, IEnumerable<string> links)
    {
        Chamadas++;
        if (Falhar) throw new InvalidOperationException("Falha simulada");
        return Task.CompletedTask;
    }
}

public class ProcessadorTests : IDisposable
{
    private readonly SqliteConnection _conexao;
    private readonly AppDbContext _db;
    private readonly ArmazenamentoSpy _armazenamento = new();
    private readonly LeomobFalsa _leomob = new();
    private readonly ProcessadorArquivosProducao _processador;

    public ProcessadorTests()
    {
        // Banco SQLite em memória: cada teste começa com um banco limpo
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        // Mesmos pedidos do seed
        _db.Pedidos.AddRange(
            new Pedido { PedidoSVL = 1007, ServiceId = 21763653, Tipo = TipoPedido.ZLPL },
            new Pedido { PedidoSVL = 1008, ServiceId = 21763654, Tipo = TipoPedido.ZCVS },
            new Pedido { PedidoSVL = 1012, ServiceId = 21763699, Tipo = TipoPedido.ZMOB });
        _db.SaveChanges();

        _processador = new ProcessadorArquivosProducao(_db, _armazenamento, _leomob);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexao.Dispose();
    }

    // Monta um envio com um serviço e as etiquetas informadas
    private static ArquivosProducaoRequest Envio(int pedidoSVL, long serviceId, params string[] etiquetas) => new()
    {
        DocumentType = "filesToProduction",
        HasErrors = false,
        Services = new List<ServicoRequest>
        {
            new()
            {
                PedidoSVL = pedidoSVL,
                ServiceId = serviceId,
                Links = new LinksRequest { Etiquetas = etiquetas.ToList() }
            }
        }
    };

    [Fact]
    public async Task PedidoZmob_NaoAcionaArmazenamento()
    {
        await _processador.ProcessarAsync(Envio(1012, 21763699, "https://x/a.png"));

        Assert.Empty(_armazenamento.Copiados);
    }

    [Fact]
    public async Task PedidoZlpl_AcionaArmazenamentoUmaVezPorArquivo()
    {
        await _processador.ProcessarAsync(Envio(1007, 21763653, "https://x/a.png", "https://x/b.png"));

        Assert.Equal(2, _armazenamento.Copiados.Count);
    }

    [Fact]
    public async Task EnvioRepetido_NaoDuplicaLinksNemNotificacoes()
    {
        var envio = Envio(1012, 21763699, "https://x/a.png");

        await _processador.ProcessarAsync(envio);
        await _processador.ProcessarAsync(envio);

        Assert.Equal(1, await _db.Links.CountAsync());
        Assert.Equal(1, await _db.Notificacoes.CountAsync());
    }

    [Fact]
    public async Task Notificacao_VaiParaFalhaAposTerceiraTentativa()
    {
        _leomob.Falhar = true;
        var reprocessador = new ReprocessadorNotificacoes(_db, _leomob);

        await _processador.ProcessarAsync(Envio(1012, 21763699, "https://x/a.png")); // falha 1
        await reprocessador.ReprocessarAsync();                                      // falha 2
        await reprocessador.ReprocessarAsync();                                      // falha 3

        var notificacao = await _db.Notificacoes.SingleAsync();
        Assert.Equal(1012, notificacao.PedidoSVL);
        Assert.Equal(StatusNotificacao.FALHA, notificacao.Status);
        Assert.Equal(3, notificacao.Tentativas);

        // Depois de FALHA, não tenta mais
        var chamadasAntes = _leomob.Chamadas;
        await reprocessador.ReprocessarAsync();
        Assert.Equal(chamadasAntes, _leomob.Chamadas);
    }

    [Fact]
    public async Task PedidoInexistente_NaoImpedeProcessamentoDosDemais()
    {
        var envio = Envio(1007, 21763653, "https://x/a.png");
        envio.Services.Add(new ServicoRequest
        {
            PedidoSVL = 9999,
            ServiceId = 1,
            Links = new LinksRequest { Etiquetas = new List<string> { "https://x/b.png" } }
        });

        var resultados = await _processador.ProcessarAsync(envio);

        Assert.Equal(StatusResultado.Processado, resultados[0].Status);
        Assert.Equal(StatusResultado.Falha, resultados[1].Status);
    }
}