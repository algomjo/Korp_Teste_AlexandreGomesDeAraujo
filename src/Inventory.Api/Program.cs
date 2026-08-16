using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<InventoryDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Database") ?? "Data Source=inventory.db"));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<InventoryDb>().Database.EnsureCreatedAsync();

var fail = false;
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapPost("/admin/simulate-failure", (FailureRequest request) => { fail = request.Enabled; return Results.Ok(new { enabled = fail }); });
app.MapGet("/products", async (InventoryDb db) => await db.Products.AsNoTracking().OrderBy(p => p.Description).ToListAsync());
app.MapPost("/products", async (CreateProduct request, InventoryDb db) =>
{
    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Description) || request.Balance < 0)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Código e descrição são obrigatórios; saldo não pode ser negativo."] });
    if (await db.Products.AnyAsync(p => p.Code == request.Code.Trim())) return Results.Conflict(new { message = "Código já cadastrado." });
    var product = new Product { Code = request.Code.Trim(), Description = request.Description.Trim(), Balance = request.Balance };
    db.Products.Add(product); await db.SaveChangesAsync();
    return Results.Created($"/products/{product.Id}", product);
});
app.MapPost("/stock/deduct", async (DeductStock request, InventoryDb db) =>
{
    if (fail) return Results.Problem("Falha simulada no serviço de estoque.", statusCode: 503, title: "Estoque indisponível");
    if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.Items.Count == 0) return Results.BadRequest(new { message = "Chave de idempotência e itens são obrigatórios." });
    if (await db.StockOperations.AnyAsync(x => x.IdempotencyKey == request.IdempotencyKey)) return Results.Ok(new { alreadyProcessed = true });
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var requested = request.Items.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
    if (requested.Values.Any(q => q <= 0)) return Results.BadRequest(new { message = "Quantidades devem ser positivas." });
    var ids = requested.Keys.ToList();
    var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
    if (products.Count != ids.Count) return Results.BadRequest(new { message = "Um ou mais produtos não existem." });
    var insufficient = products.Where(p => p.Balance < requested[p.Id]).Select(p => new { p.Code, Available = p.Balance, Requested = requested[p.Id] }).ToList();
    if (insufficient.Count > 0) return Results.Conflict(new { message = "Saldo insuficiente.", products = insufficient });
    foreach (var product in products) product.Balance -= requested[product.Id];
    db.StockOperations.Add(new StockOperation { IdempotencyKey = request.IdempotencyKey, CreatedAtUtc = DateTime.UtcNow });
    await db.SaveChangesAsync(); await tx.CommitAsync();
    return Results.Ok(new { alreadyProcessed = false });
});
app.Run();

public partial class Program { }
public sealed class InventoryDb(DbContextOptions<InventoryDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>(); public DbSet<StockOperation> StockOperations => Set<StockOperation>();
    protected override void OnModelCreating(ModelBuilder b) { b.Entity<Product>().HasIndex(x => x.Code).IsUnique(); b.Entity<StockOperation>().HasIndex(x => x.IdempotencyKey).IsUnique(); }
}
public sealed class Product { public int Id { get; set; } public required string Code { get; set; } public required string Description { get; set; } public int Balance { get; set; } }
public sealed class StockOperation { public int Id { get; set; } public required string IdempotencyKey { get; set; } public DateTime CreatedAtUtc { get; set; } }
public sealed record CreateProduct(string Code, string Description, int Balance);
public sealed record DeductStock(string IdempotencyKey, List<StockItem> Items);
public sealed record StockItem(int ProductId, int Quantity);
public sealed record FailureRequest(bool Enabled);
