// Alıştırma motoru: metin dosyasını ayrıştırır ve kullanıcının cevabını kontrol eder.
// Tarayıcıda da Node'da (doğrulama testi) aynı kod çalışır.
//
// Dosya biçimi (satır başı işaretleri):
//   §LAB 01 | Başlık | Kısa özet
//   §ADIM Başlık | tur           (tur: cs, sh, yaml, js, json, txt, md)
//   §ANLAT / §ORNEK / §GOREV / §BASLANGIC / §COZUM   (ardından gelen satırlar içeriktir)
//   §K Kontrol adı ⟫ /regex/bayrak ⟫ ipucu     (başına ! koyarsan "EŞLEŞMEMELİ"; /regex yerine @json = geçerli JSON olmalı)
//   §SORU Soru metni   (ardından gelen satırlar cevaptır)
function egzParse(metin) {
  const labs = []; let lab = null, adim = null, soru = null, bolum = null, m;
  for (const ham of metin.split(/\r?\n/)) {
    if ((m = ham.match(/^§LAB\s+(\d\d)\s*\|\s*(.+?)\s*\|\s*(.+)$/))) { lab = { id: m[1], ad: m[2], ozet: m[3], adimlar: [], sorular: [] }; labs.push(lab); adim = soru = bolum = null; continue; }
    if ((m = ham.match(/^§ADIM\s+(.+?)\s*\|\s*(\w+)\s*$/))) { adim = { b: m[1], tur: m[2], anlat: '', ornek: '', gorev: '', baslangic: '', cozum: '', k: [] }; lab.adimlar.push(adim); soru = bolum = null; continue; }
    if ((m = ham.match(/^§K\s+(.+?)\s*⟫\s*(@json|!?\/.*\/[a-z]*)\s*⟫\s*(.*)$/))) {
      const ad = m[1], ip = m[3]; let r = m[2];
      if (r === '@json') adim.k.push({ ad, ipucu: ip, test: s => { try { JSON.parse(s); return true; } catch (e) { return false; } } });
      else { const yok = r[0] === '!'; if (yok) r = r.slice(1); const mm = r.match(/^\/(.*)\/([a-z]*)$/); const re = new RegExp(mm[1], mm[2]); adim.k.push({ ad, ipucu: ip, test: s => re.test(s) !== yok }); }
      continue;
    }
    if ((m = ham.match(/^§(ANLAT|ORNEK|GOREV|BASLANGIC|COZUM)\s*$/))) { bolum = m[1].toLowerCase(); continue; }
    if ((m = ham.match(/^§SORU\s+(.+)$/))) { soru = { s: m[1], c: '' }; lab.sorular.push(soru); adim = null; bolum = 'cevap'; continue; }
    if (bolum === 'cevap' && soru) soru.c += ham + '\n';
    else if (adim && bolum) adim[bolum] += ham + '\n';
  }
  for (const l of labs) {
    for (const a of l.adimlar) for (const k of ['anlat', 'ornek', 'gorev', 'baslangic', 'cozum']) a[k] = a[k].replace(/^\n+|\s+$/g, '');
    for (const s of l.sorular) s.c = s.c.trim();
  }
  return labs;
}
// Yorum satırları sayılmaz (başlangıç şablonundaki açıklamalar kontrolü yanıltmasın)
function egzTemizle(tur, s) {
  if (tur === 'cs' || tur === 'js') return s.replace(/\/\/.*$/gm, '');
  if (tur === 'sh' || tur === 'yaml') return s.split(/\r?\n/).filter(l => !/^\s*#/.test(l)).join('\n');
  return s;
}
function egzKontrol(adim, cevap) {
  const s = egzTemizle(adim.tur, cevap || '');
  const sonuc = adim.k.map(k => ({ ad: k.ad, ok: !!k.test(s), ipucu: k.ipucu }));
  return { gecti: sonuc.length > 0 && sonuc.every(x => x.ok), sonuc };
}
if (typeof module !== 'undefined') module.exports = { egzParse, egzKontrol };
