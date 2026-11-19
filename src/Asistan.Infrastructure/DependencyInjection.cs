using Asistan.Application.Bellek;
using Asistan.Application.Kullanicilar;
using Asistan.Application.Sohbet;
using Asistan.Infrastructure.Llm;
using Asistan.Infrastructure.Mongo;
using Asistan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Asistan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // --- PostgreSQL (EF Core) ---
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("Sql")));
        services.AddScoped<IKullaniciDeposu, KullaniciDeposu>();

        // --- MongoDB ---
        // MongoClient thread-safe'tir ve içinde bağlantı havuzu tutar: Singleton.
        MongoSinifHaritalari.Kaydet();
        services.AddSingleton<IMongoClient>(_ => new MongoClient(config["Mongo:Url"]));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(config["Mongo:Database"] ?? "asistan"));
        services.AddScoped<IMesajDeposu, MongoMesajDeposu>();
        services.AddScoped<IHafizaDeposu, MongoHafizaDeposu>();
        services.AddScoped<IOturumOzetDeposu, MongoOturumOzetDeposu>();
        services.AddHostedService<MongoIndexServisi>();

        // --- LiteLLM ---
        // Typed HttpClient: adres ve anahtar tek yerde, LLM çağrıları 120 sn'ye kadar bekler.
        services.AddHttpClient<ILlmIstemcisi, LiteLlmIstemcisi>(c =>
        {
            c.BaseAddress = new Uri(config["LiteLlm:BaseUrl"]!);
            c.DefaultRequestHeaders.Authorization = new("Bearer", config["LiteLlm:Key"]);
            c.Timeout = TimeSpan.FromSeconds(120);
        });
        return services;
    }
}
