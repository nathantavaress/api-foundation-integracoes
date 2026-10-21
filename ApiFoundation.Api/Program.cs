using ApiFoundation.Api.Data;          
using ApiFoundation.Api.Models;
using ApiFoundation.Api.Services;
using Microsoft.EntityFrameworkCore;   

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

builder.Services.AddScoped<IArmazenamentoArquivos, ArmazenamentoArquivosFake>();
builder.Services.AddScoped<ProcessadorArquivosProducao>();
builder.Services.AddScoped<ILeomobApiClient, LeomobApiClientFake>();
builder.Services.AddScoped<ReprocessadorNotificacoes>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();