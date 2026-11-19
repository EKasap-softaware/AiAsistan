using Asistan.Application.Bellek;
using Asistan.Application.Kullanicilar;
using Asistan.Application.Sohbet;
using Asistan.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Geliştirme için YEDEK yerel ayar dosyası (Git'e girmez, bkz. .gitignore). Öncelikli yol 'user-secrets';
// bazı ortamlarda (ör. Visual Studio'nun başlattığı süreç) kasa okunamazsa bu dosya devreye girer.
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 1) Servisleri kaydet
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IKullaniciServisi, KullaniciServisi>();
builder.Services.AddScoped<ISohbetServisi, SohbetServisi>();

// Hafıza ayarları appsettings.json > "Hafiza" bölümünden okunur (yoksa varsayılanlar).
builder.Services.AddSingleton(builder.Configuration.GetSection("Hafiza").Get<HafizaAyarlari>() ?? new HafizaAyarlari());
builder.Services.AddScoped<IHafizaServisi, HafizaServisi>();

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
