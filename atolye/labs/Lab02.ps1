function Lab02 {
    $d = Yol 'atolye/kum/lab02'
    New-Item -ItemType Directory -Force $d | Out-Null
    $repo = Test-Path (Join-Path $d '.git')
    Kontrol 'G1 atolye/kum/lab02 içinde kendi git deposu var (.git)' $repo 'cd atolye/kum/lab02 ; git init -b main   (dikkat: .git olmazsa git ÜST klasördeki depoyu kullanır!)'
    if (-not $repo) { return }
    function G([string[]]$a) { Calistir 'git' (@('-C', $d) + $a) $Kok 30 }

    $msjlar = @((G @('log', '--no-merges', '--format=%s')).Cikti -split "`n" | Where-Object { $_.Trim() })
    $kural = '^(feat|fix|docs|test|chore|ci|refactor)(\([^)]+\))?: .+'
    $bozuk = @($msjlar | Where-Object { $_ -notmatch $kural -and $_ -notmatch '^Revert "' })
    Kontrol ("G2 en az 3 commit ve hepsi Conventional Commits ({0} commit, {1} kuralsız)" -f $msjlar.Count, $bozuk.Count) `
        ($msjlar.Count -ge 3 -and $bozuk.Count -eq 0) 'git commit -m "feat: ilk dosya"  (feat/fix/docs/test/chore/ci/refactor + ": " + açıklama)'

    $merges = @((G @('log', '--merges', '--format=%H')).Cikti -split "`n" | Where-Object { $_.Trim() })
    $mergeMsj = (G @('log', '--merges', '--format=%s')).Cikti
    Kontrol ("G3 dal açıp --no-ff ile birleştirdin ({0} merge commit, dal adı ozellik/...)" -f $merges.Count) `
        ($merges.Count -ge 1 -and $mergeMsj -match 'ozellik/') 'git switch -c ozellik/selam ; (commit) ; git switch main ; git merge --no-ff ozellik/selam'

    $cakisma = $false
    foreach ($h in $merges) {
        $ebeveyn = @((G @('rev-list', '--parents', '-n', '1', $h)).Cikti.Trim() -split '\s+')
        if ($ebeveyn.Count -ge 3) {
            $taban = (G @('merge-base', $ebeveyn[1], $ebeveyn[2])).Cikti.Trim()
            if ($taban) {
                $a = @((G @('diff', '--name-only', $taban, $ebeveyn[1])).Cikti -split "`n" | Where-Object { $_.Trim() })
                $b = @((G @('diff', '--name-only', $taban, $ebeveyn[2])).Cikti -split "`n" | Where-Object { $_.Trim() })
                if (@($a | Where-Object { $b -contains $_ }).Count -gt 0) { $cakisma = $true }
            }
        }
    }
    $isaret = (G @('grep', '-n', '<<<<<<<', 'HEAD')).Kod -eq 0
    Kontrol 'G4 aynı dosyayı iki dalda değiştirip çakışmayı çözdün (conflict işareti kalmadı)' ($cakisma -and -not $isaret) `
        'iki dalda AYNI dosyanın AYNI satırını değiştir, birleştir, çakışma işaretlerini (<<<<<<<) elle temizle, git add + git commit'

    $revert = (G @('log', '--format=%s')).Cikti -match '(?m)^Revert "'
    Kontrol 'G5 git revert ile bir commit''i geri aldın' $revert 'git revert <commit-hash> --no-edit'

    $ignoreTakipte = (G @('ls-files', '.gitignore')).Cikti.Trim() -ne ''
    $ignoreIcerik = if (Test-Path (Join-Path $d '.gitignore')) { Get-Content (Join-Path $d '.gitignore') -Raw } else { '' }
    $envTakipte = (G @('ls-files', '.env')).Cikti.Trim() -ne ''
    Kontrol 'G6 .gitignore commit edildi, .env içeriyor ve .env takipte DEĞİL' ($ignoreTakipte -and $ignoreIcerik -match '(?m)^\.env\s*$' -and -not $envTakipte) `
        '".env" yazan bir .gitignore oluştur, commit et; .env dosyası varsa asla git add yapma'

    $tag = (G @('tag')).Cikti.Trim()
    Kontrol 'G7 sürüm etiketi (tag) var' ($tag -ne '') 'git tag -a v0.1 -m "ilk sürüm"'
}
