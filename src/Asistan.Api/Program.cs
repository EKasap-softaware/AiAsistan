using Asistan.Application.Kullanicilar;
using Asistan.Application.Sohbet;
using Asistan.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1) Servisleri kaydet
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IKullaniciServisi, KullaniciServisi>();
builder.Services.AddScoped<ISohbetServisi, SohbetServisi>();

var app = builder.Build();

// 2) Middleware hattını kur (sıra önemli!)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // /scalar/v1
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
