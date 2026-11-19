# Lab 02: Git / GitHub

**Hedef:** Boş bir klasörde dal, merge, çakışma çözme, revert, .gitignore ve tag'i kendi elinle yap.
**Çalışma yeri:** `atolye/kum/lab02` (kendi küçük deponu burada açarsın; ana projeye dokunmaz).
**Başla:** `cd atolye/kum/lab02` · **Doğrula:** projenin kökünden `./atolye/atolye.ps1`

Git'in 3 alanı: **çalışma klasörü → (`git add`) → staging → (`git commit`) → depo**. Her komut bu hattın bir yerinde.
Önemli tuzak: klasörde kendi `.git` yoksa git üst klasördeki (ana proje) deponun içinde çalışır. G1 bunu kontrol eder.

## Görevler (hepsinin yanında benzer bir ÖRNEK var)

**G1. Depo aç.**
`git init -b main`
> Örnek: `mkdir deneme ; cd deneme ; git init -b main ; git status`

**G2. En az 3 commit, hepsi Conventional Commits.** Format: `tip: açıklama` (tip: feat, fix, docs, test, chore, ci, refactor).
> Örnek:
> ```
> echo "merhaba" > a.txt
> git add a.txt
> git commit -m "feat: a.txt eklendi"
> echo "dunya" >> a.txt
> git commit -am "fix: selamlama tamamlandi"
> ```
> `git log --oneline` ile bak. Neden Conventional Commits? Değişiklik günlüğü ve sürüm numarası otomatik üretilebilir.

**G3. Dal aç, çalış, `--no-ff` ile birleştir.** Dal adı `ozellik/...` ile başlasın.
> Örnek:
> ```
> git switch -c ozellik/selam      # dal aç ve geç
> echo "selam" > selam.txt ; git add . ; git commit -m "feat: selam dosyasi"
> git switch main
> git merge --no-ff ozellik/selam  # --no-ff: "bu özellik şuradan geldi" diye merge commit bırakır
> git log --oneline --graph
> ```

**G4. Çakışma (conflict) yarat ve çöz.** İki dalda AYNI dosyanın AYNI satırını değiştir, birleştir.
> Örnek:
> ```
> git switch -c ozellik/a ; (a.txt'in ilk satırını "AAA" yap) ; git commit -am "feat: A"
> git switch main ; git switch -c ozellik/b ; (aynı satırı "BBB" yap) ; git commit -am "feat: B"
> git switch main ; git merge --no-ff ozellik/a      # sorunsuz
> git merge --no-ff ozellik/b                        # CONFLICT!
> ```
> Dosyayı aç: `<<<<<<< HEAD ... ======= ... >>>>>>> ozellik/b`. Hangisini istediğine karar ver,
> **işaretleri sil**, sonra `git add a.txt ; git commit` (mesajı hazır gelir). İşaret kalırsa G4 kırmızı kalır.

**G5. `git revert`.** Bir commit'i silmeden, tersini yapan yeni commit ekler (paylaşılmış geçmişi bozmaz).
> Örnek: `git log --oneline` > hash kopyala > `git revert <hash> --no-edit`

**G6. `.gitignore`.** İçinde `.env` olsun, commit et; `.env` dosyan varsa `git status`'te untracked kalsın (asla `git add .env`).
> Örnek: `echo ".env" > .gitignore ; echo "SIR=1" > .env ; git add .gitignore ; git commit -m "chore: gitignore"`
> Kontrol: `git status` .env'yi göstermemeli.

**G7. Tag.** `git tag -a v0.1 -m "ilk surum"`

## Sonra GitHub (bu lab otomatik doğrulamaz, ama mülakatta sorulur)
`git remote add origin <url>` · `git push -u origin main` · Pull Request = dalını ana dala birleştirme TEKLİFİ + inceleme.

## Mülakat soruları
1. `merge` ile `rebase` farkı? Paylaşılan dalda neden rebase yapılmaz?
2. `reset --hard` ile `revert` farkı? Hangisi geçmişi yeniden yazar?
3. `git stash` ne işe yarar? `git reflog` ne zaman hayat kurtarır?
4. Yanlışlıkla `.env` commit ettin; ne yaparsın? (cevap: anahtarı DEĞİŞTİR, sonra geçmişten temizle)
5. Fast-forward merge nedir, `--no-ff` neden kullanılır?
