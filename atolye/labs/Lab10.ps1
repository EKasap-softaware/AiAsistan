function Lab10 {
    $skillDir = Get-ChildItem (Yol '.claude/skills') -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'yeni-entity' } | Select-Object -First 1
    $sk = if ($skillDir -and (Test-Path "$($skillDir.FullName)/SKILL.md")) { Get-Content "$($skillDir.FullName)/SKILL.md" -Raw -Encoding UTF8 } else { '' }
    $fm = if ($sk -match '(?s)^---\s*\r?\n(.*?)\r?\n---') { $Matches[1] } else { '' }
    $ad = if ($fm -match '(?m)^name:\s*(\S+)') { $Matches[1] } else { '' }
    $ac = if ($fm -match '(?m)^description:\s*(.+)$') { $Matches[1].Trim() } else { '' }
    Kontrol 'G1 .claude/skills/<senin-skill''in>/SKILL.md var (yeni-entity dışında yeni bir skill)' ($sk.Length -gt 0) 'mkdir .claude/skills/test-yaz ; içine SKILL.md  (yeni-entity/SKILL.md örnek)'
    Kontrol 'G2 frontmatter: name klasör adıyla aynı, description en az 60 karakter ve NE ZAMAN kullanılacağını söylüyor' `
        ($skillDir -and $ad -eq $skillDir.Name -and $ac.Length -ge 60 -and $ac -match '(?i)kullan|zaman|iste|ekle|yaz') `
        '--- / name: test-yaz / description: "Yeni servis için unit test yazarken kullan: ... " / ---'
    $govde = ($sk -replace '(?s)^---.*?---', '')
    Kontrol 'G3 skill gövdesinde numaralı en az 3 adım' (([regex]::Matches($govde, '(?m)^\s*\d+\.\s+\S')).Count -ge 3) 'gövdeye 1. 2. 3. şeklinde adım adım talimat yaz'

    $cmd = Get-ChildItem (Yol '.claude/commands') -Filter *.md -ErrorAction SilentlyContinue | Select-Object -First 1
    $cmdIc = if ($cmd) { Get-Content $cmd.FullName -Raw -Encoding UTF8 } else { '' }
    Kontrol 'G4 .claude/commands/<ad>.md: $ARGUMENTS kullanan kendi slash komutun' ($cmdIc -match '\$ARGUMENTS') 'mkdir .claude/commands ; ozet.md içine: "Şu dosyayı özetle: $ARGUMENTS"  -> /ozet src/...'

    $cl = Icerik 'CLAUDE.md'
    Kontrol 'G5 CLAUDE.md''ye "## Test" başlıklı en az 3 maddelik bölüm ekledin' ($cl -match '(?s)##\s+Test[^\n]*\n(\s*-\s+[^\n]+\n){3,}') '## Test kuralları / - her servis için unit test / - Moq ile dış sistemi sahtele / - Times.Never ile kaydedilmediğini doğrula'

    $mcpYol = Yol '.mcp.json'
    $mj = try { Get-Content $mcpYol -Raw -Encoding UTF8 | ConvertFrom-Json } catch { $null }
    Kontrol 'G6 .mcp.json geçerli JSON ve eski sunucular (mongodb, github) duruyor' ($mj -and $mj.mcpServers.mongodb -and $mj.mcpServers.github) 'dosyayı bozma; yalnızca yeni bir anahtar ekle'
    $yeni = if ($mj) { @($mj.mcpServers.PSObject.Properties | Where-Object { $_.Name -notin @('mongodb', 'github') }) } else { @() }
    Kontrol 'G7 .mcp.json''a kendi MCP sunucunu ekledin (command + args)' ($yeni.Count -ge 1 -and $yeni[0].Value.command -and $yeni[0].Value.args) '"dosya-okuyucu": { "command": "npx", "args": ["-y","@modelcontextprotocol/server-filesystem","./docs"] }'
    $ham = if (Test-Path $mcpYol) { Get-Content $mcpYol -Raw } else { '' }
    $e = EnvOku; $sizinti = $false
    foreach ($k in $e.Keys) { if ($e[$k].Length -ge 8 -and $ham.Contains($e[$k])) { $sizinti = $true } }
    Kontrol 'G8 .mcp.json''da düz yazı sır yok (anahtarlar ${ORTAM_DEGISKENI} ile okunur)' (-not $sizinti -and -not ($ham -match 'sk-[A-Za-z0-9_-]{20,}|ghp_[A-Za-z0-9]{20,}')) 'env: { "TOKEN": "${GITHUB_TOKEN}" } biçimini kullan, değeri dosyaya yazma'
}
