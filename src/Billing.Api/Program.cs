using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<BillingDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Database") ?? "Data Source=billing.db"));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHttpClient("inventory", c => c.BaseAddress = new Uri(builder.Configuration["InventoryUrl"] ?? "http://localhost:5101"))
    .AddStandardResilienceHandler(o => { o.Retry.MaxRetryAttempts = 3; o.Retry.Delay = TimeSpan.FromMilliseconds(250); o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3); o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12); });
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build(); app.UseCors();
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<BillingDb>().Database.EnsureCreatedAsync();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/invoices", async (BillingDb db) => await db.Invoices.AsNoTracking().Include(i => i.Items).OrderByDescending(i => i.Number).ToListAsync());
app.MapPost("/invoices", async (CreateInvoice request, BillingDb db) =>
{
    if (request.Items.Count == 0 || request.Items.Any(x => x.ProductId <= 0 || x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.ProductDescription)))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["items"] = ["Inclua ao menos um produto com quantidade positiva."] });
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var nextNumber = (await db.Invoices.MaxAsync(i => (int?)i.Number) ?? 0) + 1;
    var invoice = new Invoice { Number = nextNumber, Status = InvoiceStatus.Open, CreatedAtUtc = DateTime.UtcNow,
        Items = request.Items.GroupBy(x => x.ProductId).Select(g => new InvoiceItem { ProductId = g.Key, ProductDescription = g.First().ProductDescription.Trim(), Quantity = g.Sum(x => x.Quantity) }).ToList() };
    db.Invoices.Add(invoice); await db.SaveChangesAsync(); await tx.CommitAsync();
    return Results.Created($"/invoices/{invoice.Id}", invoice);
});
app.MapPost("/invoices/{id:int}/print", async (int id, BillingDb db, IHttpClientFactory factory, ILogger<Program> log) =>
{
    var invoice = await db.Invoices.Include(i => i.Items).SingleOrDefaultAsync(i => i.Id == id);
    if (invoice is null) return Results.NotFound(new { message = "Nota não encontrada." });
    if (invoice.Status != InvoiceStatus.Open) return Results.Conflict(new { message = "Somente notas abertas podem ser impressas." });
    var request = new { idempotencyKey = $"invoice-{invoice.Id}", items = invoice.Items.Select(i => new { i.ProductId, i.Quantity }).ToList() };
    try
    {
        var response = await factory.CreateClient("inventory").PostAsJsonAsync("/stock/deduct", request);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            log.LogWarning("Estoque recusou fechamento da nota {InvoiceId}: {Status} {Detail}", id, response.StatusCode, detail);
            return Results.Problem(response.StatusCode == HttpStatusCode.Conflict ? "Saldo insuficiente para fechar a nota." : "O estoque está indisponível. A nota continua aberta e pode ser tentada novamente.", statusCode: (int)response.StatusCode, title: "Não foi possível imprimir");
        }
    }
    catch (HttpRequestException ex) { log.LogError(ex, "Estoque indisponível ao fechar nota {InvoiceId}", id); return Results.Problem("O estoque está temporariamente indisponível. A nota continua aberta.", statusCode: 503, title: "Falha de comunicação"); }
    invoice.Status = InvoiceStatus.Closed; invoice.ClosedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync();
    return Results.Ok(invoice);
});
app.Run();

public partial class Program { }
public sealed class BillingDb(DbContextOptions<BillingDb> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>(); public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    protected override void OnModelCreating(ModelBuilder b) { b.Entity<Invoice>().HasIndex(x => x.Number).IsUnique(); b.Entity<Invoice>().Property(x => x.Status).HasConversion<string>(); }
}
public enum InvoiceStatus { Open, Closed }
public sealed class Invoice { public int Id { get; set; } public int Number { get; set; } public InvoiceStatus Status { get; set; } public DateTime CreatedAtUtc { get; set; } public DateTime? ClosedAtUtc { get; set; } public List<InvoiceItem> Items { get; set; } = []; }
public sealed class InvoiceItem { public int Id { get; set; } public int InvoiceId { get; set; } public int ProductId { get; set; } public required string ProductDescription { get; set; } public int Quantity { get; set; } }
public sealed record CreateInvoice(List<CreateInvoiceItem> Items);
public sealed record CreateInvoiceItem(int ProductId, string ProductDescription, int Quantity);
