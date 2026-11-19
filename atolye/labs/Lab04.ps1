function Mongo([string]$js) {
    $e = EnvOku
    $r = Calistir 'docker' @('compose', 'exec', '-T', 'mongo', 'mongosh', '-u', $e['MONGO_USER'], '-p', $e['MONGO_PASSWORD'], '--authenticationDatabase', 'admin', '--quiet', '--eval', $js) $Kok 60
    return (SonSatir $r.Cikti)
}
function Lab04 {
    $e = EnvOku
    if (-not $e['MONGO_USER']) { Kontrol 'ön koşul: .env içinde MONGO_USER/MONGO_PASSWORD' $false '.env.example''ı kopyalayıp .env yap'; return }
    $t = Mongo "print(JSON.stringify({n:1}))"
    if ($t -notmatch '"n":1') { Kontrol 'ön koşul: MongoDB konteyneri çalışıyor' $false 'docker compose up -d mongo'; return }

    $o = Mongo "var c=db.getSiblingDB('atolye').siparisler; print(JSON.stringify({n:c.countDocuments({}), bad:c.countDocuments({`$or:[{musteri:{`$not:{`$type:'string'}}},{tutar:{`$not:{`$type:'number'}}},{durum:{`$not:{`$type:'string'}}}]})}))" | ConvertFrom-Json -ErrorAction SilentlyContinue
    Kontrol ("G1 atolye.siparisler: en az 5 belge, hepsinde musteri(string), tutar(sayı), durum(string)  [{0} belge, {1} eksik]" -f $o.n, $o.bad) ($o -and $o.n -ge 5 -and $o.bad -eq 0) `
        "db.getSiblingDB('atolye').siparisler.insertMany([{musteri:'Ali',tutar:120,durum:'bekliyor'}, ...])"

    $ix = Mongo "print(JSON.stringify(db.getSiblingDB('atolye').siparisler.getIndexes()))" | ConvertFrom-Json -ErrorAction SilentlyContinue
    Kontrol 'G2 musteri alanında normal index' (@($ix | Where-Object { $_.key.musteri -eq 1 -and -not $_.expireAfterSeconds }).Count -ge 1) "siparisler.createIndex({musteri:1})"
    Kontrol 'G3 bileşik index {durum:1, tutar:-1}' (@($ix | Where-Object { $_.key.durum -eq 1 -and $_.key.tutar -eq -1 }).Count -ge 1) "siparisler.createIndex({durum:1, tutar:-1})  (sıra önemli!)"
    Kontrol 'G4 TTL index: olusturulma alanı, 3600 sn sonra silinsin' (@($ix | Where-Object { $_.key.olusturulma -eq 1 -and $_.expireAfterSeconds -eq 3600 }).Count -ge 1) "siparisler.createIndex({olusturulma:1}, {expireAfterSeconds:3600})"

    $a = Mongo "print(JSON.stringify({n:db.getSiblingDB('atolye').musteri_toplam.countDocuments({toplam:{`$exists:true}})}))" | ConvertFrom-Json -ErrorAction SilentlyContinue
    Kontrol 'G5 aggregation: musteri_toplam koleksiyonu ($group + $out, alan adı "toplam")' ($a -and $a.n -ge 1) `
        "siparisler.aggregate([{`$group:{_id:'`$musteri', toplam:{`$sum:'`$tutar'}}}, {`$out:'musteri_toplam'}])"

    $u = Mongo "print(JSON.stringify({n:db.getSiblingDB('atolye').siparisler.countDocuments({durum:'tamamlandi', guncellenme:{`$exists:true}})}))" | ConvertFrom-Json -ErrorAction SilentlyContinue
    Kontrol 'G6 updateOne ile bir siparişi durum:"tamamlandi" yaptın ve guncellenme alanı ekledin' ($u -and $u.n -ge 1) `
        "siparisler.updateOne({musteri:'Ali'}, {`$set:{durum:'tamamlandi', guncellenme:new Date()}})"
}
