# Ders 8 · Git ve GitHub (ayrıntılı)

Bu derste iki gerçek veri kaynağı var: **bu projenin gerçek Git geçmişi** ve projeye hiç dokunmadan **geçici bir deneme deposunda** çalıştırdığım komutların gerçek çıktıları (branch, çakışma, rebase, revert, stash, reflog).

---

## 1. Git nedir, GitHub nedir?

- **Git**, kodunun her değişikliğini kaydeden **versiyon kontrol sistemidir**; bilgisayarında çalışır, internet gerekmez. "Zaman makinesi".
- **GitHub**, Git depolarını **bulutta saklayan** ve ekip çalışmasını sağlayan platformdur (Pull Request, issue, Actions, güvenlik). Git ≠ GitHub. (GitLab, Bitbucket da benzer platformlardır.)

## 2. Üç alan (en önemli kavram)

Bir dosya üç yerden geçer:
```
çalışma klasörü ──git add──► staging area ──git commit──► depo (kalıcı geçmiş) ──git push──► GitHub
(düzenlediğin)               (commit'e hazır)             (yerel)
```
Gerçek çıktı (deneme deposu):
```
$ echo "satir 4" >> not.txt ; git status --short
 M not.txt                      ← çalışma klasöründe değişti (boşluk + M)
$ git add not.txt ; git status --short
M  not.txt                      ← staging'e alındı (M, ilk sütunda)
$ git commit -m "feat: dorduncu satir" ; git status --short
(boş)                           ← temiz, her şey depoda
```
**Commit** = bir anlık görüntü + açıklama + yazar + zaman + üst commit'e bağ; kimliği bir **hash**'tir (`7b908ca` gibi). `git diff` değişikliğin satır satır farkını gösterir.

## 3. Bu projenin gerçek geçmişi

```
git log --oneline: 18 commit, 4 saat 19 dakikada (aynı gün, 11:44 → 16:03), rehberin aşama sırasıyla
```
| Tür (Conventional Commits) | Adet | Anlamı |
|---|---|---|
| `feat:` | 8 | Yeni özellik (docker, EF Core, LiteLLM, MongoDB, AI Memory, RabbitMQ, LangFlow, Docker/CD) |
| `docs:` | 4 | Dokümantasyon |
| `fix:` | 3 | Hata düzeltme (Scalar açılışı, açık ayar hatası, APPDATA yedeği) |
| `test:` `ci:` `chore:` | 1'er | Unit testler, CI workflow'u, iskelet |

**Conventional Commits** (`tür: kısa açıklama`) geçmişi **okunur** yapar: `git log --oneline` bir proje günlüğüne dönüşür ve otomatik sürüm notu üretmeyi sağlar. Türler: `feat`, `fix`, `docs`, `test`, `refactor`, `ci`, `chore`.

İlk commit'ten bugüne: **89 dosya, +7 879 / −65 satır.** Takip edilen dosya: 93.

**Git'e girmeyenler (`.gitignore`):** `bin/`, `obj/`, `.vs/`, `coverage/` (üretilen çıktılar) ve **gizliler: `.env` ve `appsettings.Local.json`**. `git status --ignored` ile bizzat gördük. `.env.example` ise Git'te (sahte değerlerle "hangi değişkenler lazım"). Commit öncesi alışkanlık: `.env`'deki gerçek değerlerin repo dosyalarında **geçmediğini tara** (yaptık: 94 dosya temiz).

## 4. Günlük komutlar

| Komut | Ne yapar |
|---|---|
| `git status` | Neyin değiştiğini gösterir |
| `git diff` / `git diff --staged` | Çalışma klasörü / staging farkı |
| `git add <dosya>` | Commit'e hazırlar (`git add .` hepsini, dikkatli) |
| `git commit -m "feat: ..."` | Kalıcı kayıt |
| `git log --oneline --graph --all` | Dallı geçmiş |
| `git pull` / `git push` | GitHub'dan çek / GitHub'a gönder |
| `git switch -c ozellik/x` | Yeni branch açar ve geçer |
| `git switch main` | Branch'e geçer |
| `git stash` / `git stash pop` | Yarım işi kenara koy / geri al |
| `git restore <dosya>` | Commit edilmemiş değişikliği geri alır |
| `git revert <commit>` | Bir commit'i geri alan **yeni** commit |
| `git reflog` | HEAD'in tüm hareketleri (kurtarma ağı) |
| `git remote -v` | Bağlı uzak depolar |

## 5. Branch ve merge: gerçek bir çakışma (conflict)

**Branch**, ana koddan ayrılan paralel çalışma dalıdır. İki kişi **aynı satırı** farklı şekilde değiştirdi:
```
$ git log --oneline --graph --all
* d052947 feat: mert satir 2'yi degistirdi          ← main
| * 7ebe246 feat: ayse satir 2'yi degistirdi        ← ozellik/ayse
|/
* ae477a4 feat: dorduncu satir                      ← ortak atası
```
Birleştirince Git **karar veremez** ve durur:
```
$ git merge ozellik/ayse
Auto-merging not.txt
CONFLICT (content): Merge conflict in not.txt
Automatic merge failed; fix conflicts and then commit the result.
```
Dosyanın içine **işaretler** koyar:
```
satir 1
<<<<<<< HEAD                          ← "bizim" (main) sürüm
satir 2 (MERT DEGISTIRDI)
=======
satir 2 (AYSE DEGISTIRDI)             ← "gelen" sürüm
>>>>>>> ozellik/ayse
```
**Çözüm:** işaretli bölgeyi (üç işaret satırı dahil) elle istediğin son hâle getir, `git add`, `git commit`:
```
*   8327204 merge: ayse ve mert degisiklikleri birlestirildi
|\
| * 7ebe246 feat: ayse ...
* | d052947 feat: mert ...
```
Gerçek hayatta çakışmayı **kişilerle konuşarak** çözersin: hangi sürüm doğru? Yanlış satırı silmek başkasının işini siler.

## 6. Merge mi, rebase mi?

- **Merge:** iki dalı birleştiren **yeni bir commit** üretir; geçmiş olduğu gibi kalır (yukarıdaki çatallı grafik).
- **Rebase:** senin commit'lerini hedef dalın **en ucuna taşır**; geçmiş **düz bir çizgi** olur.

Gerçek deneyde rebase de **çakıştı** (iki taraf dosyanın sonuna ekleme yaptı) ve depo "rebase ortasında" kaldı:
```
interactive rebase in progress; onto f115707
You are currently rebasing branch 'ozellik/yeni' on 'f115707'.
  (fix conflicts and then run "git rebase --continue")
```
İşaretleri düzeltip `git add` + `git rebase --continue` dedikten sonra:
```
* 3bbfd1b feat: besinci satir          ← düz çizgi, birleştirme commit'i YOK
* f115707 feat: altinci satir (main'de)
*   8327204 merge: ...
```
Ayrıca rebase ortasında `git switch` ve `git revert` **reddedildi** ("cannot switch branch while rebasing"): önce çakışmayı çöz ya da `git rebase --abort` ile vazgeç. **Altın kural:** sadece **kendi, henüz paylaşılmamış** branch'ini rebase et; başkalarının gördüğü geçmişi yeniden yazma.

## 7. Geri alma araçları (hangisi ne zaman?)

| İhtiyaç | Komut | Gerçek sonuç |
|---|---|---|
| Commit **edilmemiş** değişikliği at | `git restore not.txt` | "yarım is" satırı gitti |
| Yarım işi **geçici** kenara koy | `git stash` / `git stash pop` | Çalışma klasörü temizlendi, `pop` ile geri geldi (`stash@{0}: WIP on main`) |
| Commit edilmiş bir değişikliği **geri al** | `git revert <commit>` | **Yeni** bir `Revert "..."` commit'i: geçmiş **silinmez** |
| "Ben ne yaptım?" kurtarma | `git reflog` | HEAD'in tüm hareketleri (checkout, rebase, revert) listelenir |

**Revert ile `reset --hard` farkı:** `revert` güvenlidir (geçmişe ekler). `git reset --hard` ve `git push --force` geçmişi/çalışmayı **silebilir**: ne yaptığını bilmiyorsan kullanma. Silinen commit'ler de `reflog`'ta bir süre durur.

## 8. GitHub tarafı

**Uzak depo:** `origin` → `https://github.com/EKasap-softaware/Caricep_yeni.git` (adı yanıltıcı, içeriği AiAsistan). `git push` yerel commit'leri oraya gönderir.

**Repo private mı?** İlk push'ta repo'ya sormadan erişebildim ve "public olabilir" diye şüphelendim. Kimliksiz GitHub API'si **"Not Found"** döndürdü: private repo kimliksiz görünmez, yani **private**. Sormadan erişebilmemin sebebi, Git'in senin GitHub girişini (credential manager) hatırlamasıydı. Ders: bir şüpheyi tahminle değil **kontrolle** çöz.

**Önemli GitHub özellikleri:**

| Özellik | Ne işe yarar | Bizde |
|---|---|---|
| **Pull Request (PR)** | Branch'ini `main`'e birleştirme isteği; **inceleme (code review) ve CI burada olur** | Henüz kullanmadık (aşağıya bak) |
| **Actions** | CI/CD (`.github/workflows/`) | `ci.yml`, `cd.yml` |
| **Branch protection** | `main`'e PR'siz ve testler geçmeden birleştirmeyi engeller (Settings → Branches/Rules) | Elle açılmalı |
| **Secrets / Variables** | Actions için gizli/gizli olmayan değerler | `DEPLOY_ENABLED`, `DEPLOY_*` |
| **Issues** | Yapılacak işler ve hatalar | |
| `gh` CLI | Terminalden PR/issue (`gh pr create --fill`) | Bu makinede kurulu değil |

## 9. Çalışma şeklimiz: dürüst değerlendirme

Bu proje **tek kişilik bir öğrenme projesi** olduğu için **doğrudan `main`'e commit** attık: 18 commit, hiç branch/PR yok. Bu, burada makul ama **ekip çalışmasında yapılmaz.** Önerilen akış (**GitHub Flow**):
1. `main` her zaman çalışır durumdadır; doğrudan ona commit atma.
2. Her iş için branch aç: `git switch -c ozellik/mongo-hafiza`.
3. Küçük anlamlı commit'ler at, `git push -u origin ozellik/mongo-hafiza`.
4. GitHub'da **PR** aç: CI otomatik çalışır, biri kodu inceler.
5. Testler yeşil ve onay alınmışsa **merge**, branch silinir.
6. `main`'i **branch protection** ile koru.

## 10. Gizli bilgi sızarsa

Bir şifre/API anahtarı yanlışlıkla commit/push edilirse **dosyadan silmek yetmez**: Git geçmişinde kalır ve GitHub'da herkes (public ise) ya da erişimi olan herkes görür. **Hemen anahtarı iptal et ve yenisini üret.** Önlem: `.gitignore`, `.env.example` ile ayırma, commit öncesi tarama. (Bu projede Anthropic anahtarı bir kez **sohbete** yapıştırıldı, Git'e değil; yine de öğrenme sonunda iptal edilip yenilenmesi önerilir.)

## 11. Bilmen gerekenler

1. Git yerel, GitHub bulut platformudur. Commit = anlık görüntü; kimliği hash.
2. Üç alan: çalışma klasörü → `add` → staging → `commit` → depo → `push` → GitHub.
3. `.gitignore` üretilen çıktıları ve **gizli dosyaları** (`.env`) dışarıda tutar; `.env.example` şablon olarak girer.
4. Branch paralel çalışma dalıdır; merge birleştirir; **çakışmayı** `<<<<<<<`/`=======`/`>>>>>>>` işaretlerini düzelterek, `add` + `commit` ile çözersin.
5. Rebase geçmişi düz çizgi yapar; **sadece paylaşılmamış** branch'te kullan. Rebase da çakışabilir (`--continue` / `--abort`).
6. Geri alma: `restore` (edilmemişi at), `stash` (geçici kenara), `revert` (güvenli, yeni commit), `reflog` (kurtarma ağı). `reset --hard` ve `push --force` tehlikelidir.
7. Conventional Commits geçmişi okunur kılar.
8. Ekipte: branch → PR → CI → inceleme → merge; `main` korunur.
9. Sızan sır: sadece silmek yetmez, **iptal et ve yenile**.
10. Şüpheyi tahminle değil **kontrolle** çöz (repo private mı? → API ile baktık).
