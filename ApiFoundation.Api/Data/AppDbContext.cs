using ApiFoundation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<LinkArquivo> Links => Set<LinkArquivo>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pedido>(e =>
        {
            // PedidoSVL é a chave primária e vem do SVL, por isso o banco não gera o valor

            e.HasKey(p => p.PedidoSVL);
            e.Property(p => p.PedidoSVL).ValueGeneratedNever();
            // Relação: um Pedido tem muitos Links; a chave estrangeira é LinkArquivo.PedidoSVL
            e.HasMany(p => p.Links).WithOne().HasForeignKey(l => l.PedidoSVL);
        });
    }
}