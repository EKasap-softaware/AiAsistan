using Asistan.Application;
using Asistan.Infrastructure;
using Asistan.Infrastructure.Kuyruk;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Geliştirme için YEDEK yerel ayar dosyası (Git'e girmez, bkz. .gitignore). Öncelikli yol 'user-secrets';
// bazı ortamlarda (ör. Visual Studio'nun başlattığı süreç) kasa okunamazsa bu dosya devreye girer.
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 1) Servisleri kaydet
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication(builder.Configuration);     // iş servisleri (API ve Worker aynısını kullanır)
builder.Services.AddInfrastructure(builder.Configuration);  // PostgreSQL, MongoDB, LiteLLM
builder.Services.AddKuyruk(builder.Configuration);          // RabbitMQ: API sadece GÖNDERİR, tüketici (consumer) eklemez

var app = builder.Build();

// 2) Middleware hattını kur (sıra önemli!)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // /scalar/v1

    // Ana sayfa yok; "/" açılınca 404 yerine API deneme ekranına yönlendir.
    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
