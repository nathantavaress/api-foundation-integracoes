using ApiFoundation.Api.Data;
using ApiFoundation.Api.Models;
using ApiFoundation.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers (MVC) e Swagger para documentar e testar os endpoints
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Banco SQLite em arquivo (app.db). Scoped por padrão: um DbContext por requisição
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

// Injeção de dependência: registramos a interface e a implementação falsa.
// Para usar um serviço real, basta trocar a classe aqui, sem mexer nas regras de negócio.
builder.Services.AddScoped<IArmazenamentoArquivos, ArmazenamentoArquivosFake>();
builder.Services.AddScoped<ILeomobApiClient, LeomobApiClientFake>();

// Classes com as regras de negócio
builder.Services.AddScoped<ProcessadorArquivosProducao>();
builder.Services.AddScoped<ReprocessadorNotificacoes>();

var app = builder.Build();

// Ao subir a aplicação: cria o banco (se não existir) e popula os pedidos iniciais (seed).
// Fora de uma requisição não há escopo, então criamos um manualmente para obter o DbContext.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Só popula se a tabela estiver vazia, para não duplicar a cada reinício
    if (!db.Pedidos.Any())
    {
        db.Pedidos.AddRange(
            new Pedido { PedidoSVL = 1007, ServiceId = 21763653, Tipo = TipoPedido.ZLPL },
            new Pedido { PedidoSVL = 1008, ServiceId = 21763654, Tipo = TipoPedido.ZCVS },
            new Pedido { PedidoSVL = 1012, ServiceId = 21763699, Tipo = TipoPedido.ZMOB }
        );
        db.SaveChanges();
    }
}

// Swagger só em desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Pipeline de middlewares: a ordem em que são registrados é a ordem em que rodam
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();