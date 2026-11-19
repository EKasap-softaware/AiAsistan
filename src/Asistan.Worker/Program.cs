using Asistan.Application;
using Asistan.Application.Kuyruk;
using Asistan.Infrastructure;
using Asistan.Infrastructure.Kuyruk;
using Asistan.Worker.Consumers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

// API ile aynı yedek yerel ayar dosyası (Git'e girmez). Bkz. Asistan.Api/Program.cs
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// API ile AYNI iş servisleri ve altyapı. Worker, API'nin yaptığı işin arka plandaki yarısını yapar.
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddKuyruk(
    builder.Configuration,
    tuketicileriKaydet: x =>
    {
        x.AddConsumer<AiIstegiConsumer>();
        x.AddConsumer<SohbetTamamlandiConsumer>();
        x.AddConsumer<AiIstegiHataConsumer>();
    },
    kuyruklariKur: (ctx, cfg) =>
    {
        // Geçici hata (LiteLLM çöktü, ağ koptu) olursa 5 kez dener: bekleme süreleri üstel artar (~1, 6, 11, 16, 21 sn).
        // ArgumentException "yeniden denemekle düzelmez" demektir (mesaj hatalı), onu hemen vazgeç.
        static void YenidenDene(IReceiveEndpointConfigurator e) => e.UseMessageRetry(r =>
        {
            r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
            r.Ignore<ArgumentException>();
        });

        // Kuyruk adlarını AÇIKÇA veriyoruz (Application/Kuyruk/KuyrukSozlesmeleri.cs): API aynı adlara gönderir.
        cfg.ReceiveEndpoint(KuyrukAdlari.AiIstegi, e =>
        {
            YenidenDene(e);
            e.ConcurrentMessageLimit = 4;   // Aynı anda en fazla 4 soru işlenir (LLM kotasını boğmamak için)
            e.ConfigureConsumer<AiIstegiConsumer>(ctx);
        });

        cfg.ReceiveEndpoint(KuyrukAdlari.SohbetTamamlandi, e =>
        {
            YenidenDene(e);
            e.ConfigureConsumer<SohbetTamamlandiConsumer>(ctx);
        });

        cfg.ReceiveEndpoint(KuyrukAdlari.AiIstegiHata, e =>
            e.ConfigureConsumer<AiIstegiHataConsumer>(ctx));
    });

builder.Build().Run();
