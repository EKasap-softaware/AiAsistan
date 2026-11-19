function Lab01 {
    Write-Host "`nLab 01: .NET + EF Core + Unit Test (Etiket)" -ForegroundColor Cyan

    $ent = Icerik 'src/Asistan.Domain/Entities/Etiket.cs'
    Kontrol 'G1 Domain: Etiket entity (Id, Ad, Olusturulma)' `
        ($ent -match 'class\s+Etiket' -and $ent -match '\bAd\b' -and $ent -match 'Olusturulma') `
        'src/Asistan.Domain/Entities/Etiket.cs: public class Etiket { Id, Ad, Olusturulma } (Kullanici.cs gibi)'

    $app = (TumKod 'src/Asistan.Application' | ForEach-Object { Get-Content $_.FullName -Raw -Encoding UTF8 }) -join "`n"
    Kontrol 'G2 Application: IEtiketDeposu + IEtiketServisi + EtiketServisi' `
        ($app -match 'interface\s+IEtiketDeposu' -and $app -match 'interface\s+IEtiketServisi' -and $app -match 'class\s+EtiketServisi') `
        'src/Asistan.Application/Etiketler/ altında arayüzleri ve servisi yaz; servis depoyu constructor ile alsın'
    Kontrol 'G2 Application: 50 karakter kuralı + ArgumentException' `
        ($app -match 'class\s+EtiketServisi' -and $app -match '50' -and $app -match 'ArgumentException') `
        'ad boş/boşluk ise ve Trim() sonrası 50''yi aşarsa ArgumentException fırlat'
    Kontrol 'G2 Application: IEtiketServisi DI kaydı (AddApplication)' `
        ((Icerik 'src/Asistan.Application/DependencyInjection.cs') -match 'IEtiketServisi\s*,\s*EtiketServisi') `
        'AddApplication içine: services.AddScoped<IEtiketServisi, EtiketServisi>();'
    Kontrol 'KURAL Application, EF Core kullanmıyor' `
        (-not ($app -match 'EntityFrameworkCore')) `
        'Application katmanında "using Microsoft.EntityFrameworkCore" olmamalı; EF sadece Infrastructure''da'

    $ctx = Icerik 'src/Asistan.Infrastructure/Persistence/AppDbContext.cs'
    Kontrol 'G3 DbContext: DbSet<Etiket> + HasMaxLength(50)' `
        ($ctx -match 'DbSet<Etiket>' -and $ctx -match 'Etiket' -and $ctx -match 'HasMaxLength\(50\)') `
        'DbSet<Etiket> Etiketler ekle; OnModelCreating içinde Ad için HasMaxLength(50) (şu an Abonelik.Plan için var)'
    $depo = Icerik 'src/Asistan.Infrastructure/Persistence/EtiketDeposu.cs'
    Kontrol 'G3 Infrastructure: EtiketDeposu : IEtiketDeposu' `
        ($depo -match 'class\s+EtiketDeposu' -and $depo -match 'IEtiketDeposu' -and $depo -match 'SaveChangesAsync') `
        'KullaniciDeposu.cs gibi; EkleAsync: Add + SaveChangesAsync, ListeleAsync: AsNoTracking'
    Kontrol 'G3 Infrastructure: IEtiketDeposu DI kaydı' `
        ((Icerik 'src/Asistan.Infrastructure/DependencyInjection.cs') -match 'IEtiketDeposu\s*,\s*EtiketDeposu') `
        'Infrastructure/DependencyInjection.cs içinde IKullaniciDeposu satırının yanına ekle'

    $mig = Get-ChildItem (Yol 'src/Asistan.Infrastructure/Persistence/Migrations') -Filter *.cs -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notmatch 'Designer|Snapshot' -and (Get-Content $_.FullName -Raw) -match 'Etiket' }
    Kontrol 'G4 Migration: Etiket tablosunu oluşturan migration var' `
        ($null -ne $mig) `
        'dotnet ef migrations add EtiketEkle -p src/Asistan.Infrastructure -s src/Asistan.Api -o Persistence/Migrations'

    $testDosya = Icerik 'tests/Asistan.Tests/EtiketServisiTests.cs'
    $say = ([regex]::Matches($testDosya, '\[(Fact|Theory)')).Count
    Kontrol "G5 Test: EtiketServisiTests.cs en az 3 test ($say bulundu)" ($say -ge 3) `
        'geçerli ad (Times.Once), boş ad (Times.Never), 51 karakter; KullaniciServisiTests.cs''yi örnek al'
    Kontrol 'G5 Test: hata durumunda "kaydedilmedi" doğrulanıyor (Times.Never)' `
        ($testDosya -match 'Times\.Never') 'depo.Verify(..., Times.Never) ile kaydın yapılmadığını da doğrula'

    Write-Host "  … derleniyor (dotnet build)" -ForegroundColor DarkGray
    Push-Location $Kok
    try {
        $b = & dotnet build --nologo -v q 2>&1 | Out-String
        $buildOk = ($LASTEXITCODE -eq 0)
        Kontrol 'G6 dotnet build temiz (uyarı = hata)' $buildOk `
            $(if ($b -match 'being used by another process') { "çalışan API/Worker DLL'leri kilitliyor: Visual Studio'da Stop / terminalde Ctrl+C ile durdur, tekrar dene" } else { "derleme hatası: " + (($b -split "`n" | Where-Object { $_ -match 'error' } | Select-Object -First 2) -join ' | ') })
        if ($buildOk) {
            $t = & dotnet test --no-build --nologo --filter 'FullyQualifiedName~EtiketServisiTests' 2>&1 | Out-String
            $gecen = if ($t -match 'Passed:\s+(\d+)') { [int]$Matches[1] } else { 0 }
            $kalan = if ($t -match 'Failed:\s+(\d+)') { [int]$Matches[1] } else { 0 }
            Kontrol "G6 EtiketServisiTests yeşil (geçen: $gecen, kalan: $kalan)" ($gecen -ge 3 -and $kalan -eq 0) `
                'dotnet test --filter EtiketServisiTests ile hatayı oku; servis kuralı mı yoksa test beklentisi mi yanlış?'
        }
    } finally { Pop-Location }
}
