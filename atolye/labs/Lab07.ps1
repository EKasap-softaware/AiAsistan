function Lab07 {
    $kod = Icerik 'src/Asistan.Application/Bellek/KonusmaPenceresi.cs'
    Kontrol 'G1 Application/Bellek/KonusmaPenceresi.cs: static class + 3 metot (SonN, OzetGerekliMi, HafizaFiltrele)' `
        ($kod -match 'static\s+class\s+KonusmaPenceresi' -and $kod -match '\bSonN\b' -and $kod -match '\bOzetGerekliMi\b' -and $kod -match '\bHafizaFiltrele\b') `
        'namespace Asistan.Application.Bellek; public static class KonusmaPenceresi { SonN, OzetGerekliMi, HafizaFiltrele }  (imzalar lab dosyasında)'
    Kontrol 'KURAL Application, EF Core/Mongo kullanmıyor' (-not ($kod -match 'EntityFrameworkCore|MongoDB')) 'bu sınıf saf mantık olmalı: dış sistem yok'
    $tst = Icerik 'tests/Asistan.Tests/KonusmaPenceresiTests.cs'
    $say = ([regex]::Matches($tst, '\[(Fact|Theory)')).Count
    Kontrol ("G2 tests/Asistan.Tests/KonusmaPenceresiTests.cs: en az 5 test yazdın ({0} bulundu)" -f $say) ($say -ge 5) 'SonN (normal, n>sayı, n<=0), OzetGerekliMi (eşik), HafizaFiltrele (YOK, uzun, çok satır, tekrar)'
    if (-not ($kod -match 'KonusmaPenceresi')) { Kontrol 'G3 gizli doğrulama testleri' $false 'önce G1'; return }

    # gizli testler: kullanıcının kodunu bizim yazdığımız beklentilerle dener (geçici kopyalanır, sonra silinir)
    $gizli = Yol 'atolye/gizli/Lab07GizliTests.cs.txt'
    $hedef = Yol 'tests/Asistan.Tests/AtolyeLab07Gizli.cs'
    Copy-Item $gizli $hedef -Force
    try {
        Write-Host '  … gizli testler derleniyor/çalışıyor' -ForegroundColor DarkGray
        $r = Calistir 'dotnet' @('test', '--nologo', '--filter', 'FullyQualifiedName~AtolyeLab07Gizli') $Kok 400
        $gecen = if ($r.Cikti -match 'Passed:\s+(\d+)') { [int]$Matches[1] } else { 0 }
        $kalan = if ($r.Cikti -match 'Failed:\s+(\d+)') { [int]$Matches[1] } else { 0 }
        $hatalar = if ($r.Cikti -match 'being used by another process') { "[çalışan API/Worker DLL'leri kilitliyor: VS'te Stop / Ctrl+C ile durdur]" } else { ($r.Cikti -split "`n" | Where-Object { $_ -match 'error CS|Gizli.*\[FAIL\]' } | Select-Object -First 3) -join ' | ' }
        Kontrol ("G3 gizli doğrulama testleri geçiyor (geçen: {0}, kalan: {1}) {2}" -f $gecen, $kalan, $hatalar) ($gecen -ge 11 -and $kalan -eq 0) `
            'imzalar birebir uymalı. Başarısız testin adı davranışı söyler: SonN_..., HafizaFiltrele_...'
    } finally { Remove-Item $hedef -Force -ErrorAction SilentlyContinue }
}
