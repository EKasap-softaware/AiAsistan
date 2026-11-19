using Asistan.Application.Bellek;
using Asistan.Application.Dokuman;
using Asistan.Application.Kullanicilar;
using Asistan.Application.Kuyruk;
using Asistan.Application.Sohbet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asistan.Application;

public static class DependencyInjection
{
    // API de Worker da aynı iş servislerini kullanır. Kayıtlar tek yerde dursun ki iki program
    // birbirinden ayrışmasın (biri yeni servis ister, diğerinde unutulmasın).
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IKullaniciServisi, KullaniciServisi>();
        services.AddScoped<ISohbetServisi, SohbetServisi>();
        services.AddScoped<IIsServisi, IsServisi>();
        services.AddScoped<IDokumanServisi, DokumanServisi>();

        // Hafıza ayarları appsettings.json > "Hafiza" bölümünden okunur (yoksa varsayılanlar).
        services.AddSingleton(config.GetSection("Hafiza").Get<HafizaAyarlari>() ?? new HafizaAyarlari());
        services.AddScoped<IHafizaServisi, HafizaServisi>();
        return services;
    }
}
