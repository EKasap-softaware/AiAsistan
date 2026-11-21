"""Atölye sayfasındaki "▶ Çalıştır" düğmesinin arkasındaki güvenli çalıştırıcılar.

Her adım KUM KUTUSUNDA çalışır; gerçek verine dokunmaz:
  - MongoDB   : atolye_deneme adlı geçici veritabanı (her çalıştırmada sıfırlanır)
  - Git       : geçici klasörde, yalnızca izinli komutlar (push/clone/config yok)
  - Docker    : yalnızca 'deneme' konteyneri ve 'atolye-lab03' imajı
  - curl      : yalnızca localhost:15672 / 4000 / 7860, anahtarlar maskelenir
  - C# (Lab07): geçici konsol projesinde derlenip çalışır
  - JSON/YAML : sözdizimi kontrolü
"""
import json, os, re, shlex, shutil, subprocess, tempfile, time, urllib.request, urllib.error, urllib.parse, base64
from pathlib import Path

A = Path(__file__).resolve().parent
K = A.parent
KUM = A / "kum"
MONGO_KAP = "aiasistan-mongo-1"
SEVIYE = {"git": ["02-0", "02-1", "02-3", "02-4"],
          "mongo": ["04-0", "04-1", "04-2", "04-3", "04-4", "04-5"],
          "docker": ["03-0", "03-2", "03-3"],
          "curl": ["06-2", "08-1"],
          "rabbit": [],
          "cs": ["05-0", "05-1", "05-2", "05-3", "05-4", "05-5", "05-6", "07-0", "07-1", "07-2"],
          "sozdizimi": ["06-0", "06-1", "09-0", "09-1", "09-2", "09-3", "09-4", "10-3"]}
DESTEK = sorted({k for v in SEVIYE.values() for k in v})
# Sayfadaki "Örnek" ve "Başka örnekler" kutularının altına ▶ düğmesi konan adımlar (örnekler birebir çalışır)
ORNEK_OK = ["05-0", "05-1", "05-2", "05-3", "05-4", "05-5", "05-6"]


def _env():
    d = {}
    try:
        for l in (K / ".env").read_text("utf-8-sig").splitlines():
            if "=" in l and not l.lstrip().startswith("#"):
                a, b = l.split("=", 1)
                d[a.strip()] = b.strip()
    except Exception:
        pass
    return d


def _maske(metin, sirlar):
    for s in sirlar:
        if s and len(s) >= 4:
            metin = metin.replace(s, "***")
    return metin


def _calistir(args, cwd=None, girdi=None, sure=120, env=None):
    try:
        r = subprocess.run(args, cwd=cwd, input=girdi, capture_output=True, text=True, encoding="utf-8",
                           errors="replace", timeout=sure, env=env)
        return r.returncode, (r.stdout + r.stderr)
    except subprocess.TimeoutExpired:
        return 124, f"Zaman aşımı ({sure} sn)."
    except FileNotFoundError as e:
        return 127, f"Komut bulunamadı: {e.filename}"


# ---------------------------------------------------------------- MongoDB
_MONGO_YASAK = re.compile(r"dropDatabase|getMongo|connect\s*\(|\bload\s*\(|require\s*\(|process\.|adminCommand|createUser|dropUser|shutdown|runCommand|\buse\s+\w|\bexit\b|\.getSiblingDB\(\s*['\"](?!atolye['\"])", re.I)
_SEED = ("db.siparisler.insertMany([{musteri:'Ali',tutar:120,durum:'bekliyor'},{musteri:'Ayse',tutar:80,durum:'bekliyor'},"
         "{musteri:'Can',tutar:300,durum:'tamamlandi'}])")


def mongo(key, kod):
    if _MONGO_YASAK.search(kod):
        return False, "Güvenlik: bu atölye sadece 'atolye' veritabanında çalışır; bu komut engellendi."
    govde = re.sub(r"db\s*=\s*db\.getSiblingDB\(\s*['\"]atolye['\"]\s*\)", "", kod).strip()
    onek = "db = db.getSiblingDB('atolye_deneme')\ndb.dropDatabase()\ndb = db.getSiblingDB('atolye_deneme')\n"
    if key != "04-0":
        onek += _SEED + "\n"
    son = ""
    if "$out" in kod:
        son += "\ndb.musteri_toplam.find().sort({_id:1})"
    if "updateOne" in kod or "updateMany" in kod:
        son += "\ndb.siparisler.find().sort({tutar:1})"
    if "createIndex" in kod:
        son += "\ndb.siparisler.getIndexes().map(i => i.name + '  ' + JSON.stringify(i.key) + (i.expireAfterSeconds != null ? '  expireAfterSeconds=' + i.expireAfterSeconds : ''))"
    if key == "04-0":
        son += "\ndb.siparisler.countDocuments()"
    betik = onek + "'@@BASLA@@'\n" + govde + son + "\n'@@BITIS@@'\ndb.dropDatabase()\n"
    kod_, cikti = _calistir(["docker", "exec", "-i", MONGO_KAP, "sh", "-c",
                             'mongosh --quiet -u "$MONGO_INITDB_ROOT_USERNAME" -p "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin'],
                            girdi=betik, sure=60)
    if kod_ == 127:
        return False, "Docker bulunamadı ya da çalışmıyor."
    satirlar = []
    for s_ in cikti.replace("\r", "").split("\n"):
        satirlar.append(re.sub(r"^(?:\s*(?:[\w.-]+>|\.\.\.)\s?|\s*\|\s?)+", "", s_))
    metin = "\n".join(satirlar)
    if "@@BASLA@@" in metin:
        metin = metin.split("@@BASLA@@", 1)[1]
        metin = metin.split("@@BITIS@@")[0]
        metin = metin.lstrip("'").lstrip()
        metin = re.sub(r"'\s*$", "", metin.rstrip()).rstrip()
    metin = "\n".join(l for l in metin.split("\n") if l.strip() != "").strip()
    ok = not re.search(r"SyntaxError|MongoServerError|ReferenceError|TypeError|MongoInvalid", metin)
    return ok, metin or "(komut çıktı üretmedi)"
    return ok, metin or "(komut çıktı üretmedi)"


# ---------------------------------------------------------------- Git
_GIT_IZIN = {"init", "add", "commit", "switch", "checkout", "branch", "merge", "log", "status", "revert", "tag",
             "diff", "show", "restore", "reset", "stash", "rm", "mv"}


def git(key, kod):
    d = Path(tempfile.mkdtemp(prefix="atolye-git-"))
    env = dict(os.environ, GIT_AUTHOR_NAME="Ogrenci", GIT_AUTHOR_EMAIL="o@atolye.local", GIT_COMMITTER_NAME="Ogrenci",
               GIT_COMMITTER_EMAIL="o@atolye.local", GIT_EDITOR="true", GIT_TERMINAL_PROMPT="0",
               GIT_CONFIG_GLOBAL=os.devnull, GIT_CONFIG_NOSYSTEM="1", GIT_PAGER="cat")
    g = lambda *a: _calistir(["git", *a], cwd=str(d), env=env, sure=30)
    cikti = []
    try:
        if key in ("02-1", "02-3", "02-4"):   # hazır bir depo ver
            g("init", "-b", "main")
            (d / "notlar.txt").write_text("ilk not\n", "utf-8")
            g("add", "notlar.txt"); g("commit", "-m", "feat: notlar eklendi")
            if key == "02-3":
                (d / "a.txt").write_text("a\n", "utf-8"); g("add", "a.txt"); g("commit", "-m", "feat: a eklendi")
                hash_ = g("log", "--format=%h", "-1")[1].strip()
            if key == "02-4":
                (d / ".env").write_text("SIR=gizli-deger\n", "utf-8")
            cikti.append("(hazır depo: notlar.txt'li 1 commit" + (", .env dosyası mevcut" if key == "02-4" else "") + (", revert için ek commit" if key == "02-3" else "") + ")")
        for ham in kod.replace("\r", "").split("\n"):
            s = ham.strip()
            if not s or s.startswith("#"):
                continue
            cikti.append("$ " + s)
            m = re.fullmatch(r"echo\s+(\"[^\"]*\"|'[^']*'|\S+)\s*(>>?)\s*([\w./-]+)", s)
            if m:
                metin = m.group(1).strip("\"'")
                yol = (d / m.group(3)).resolve()
                if d.resolve() not in yol.parents:
                    cikti.append("Güvenlik: dosya sadece deneme klasörüne yazılabilir."); break
                with open(yol, "a" if m.group(2) == ">>" else "w", encoding="utf-8", newline="\n") as f:
                    f.write(metin + "\n")
                continue
            try:
                t = shlex.split(s)
            except ValueError as e:
                cikti.append(f"Komut okunamadı (tırnaklara bak): {e}"); break
            if t[0] == "cat" and len(t) == 2:
                try: cikti.append((d / t[1]).read_text("utf-8").rstrip())
                except Exception: cikti.append(f"cat: {t[1]}: dosya yok")
                continue
            if t[0] == "ls":
                cikti.append("  ".join(sorted(p.name for p in d.iterdir() if p.name != ".git"))); continue
            if t[0] != "git" or len(t) < 2 or t[1] not in _GIT_IZIN:
                cikti.append("Bu atölyede yalnızca şu komutlar çalışır: git (init/add/commit/switch/branch/merge/log/status/revert/tag...), echo > dosya, cat, ls."); break
            if "--hard" in t and key != "02-3":
                pass
            if key == "02-3":
                t = [hash_ if x == "a1b2c3d" else x for x in t]
            rc, out = g(*t[1:])
            cikti.append(out.rstrip())
        if key != "02-0" or True:
            cikti.append("\n— ek bilgi —\n$ git log --oneline --all")
            cikti.append(g("log", "--oneline", "--all", "--decorate")[1].rstrip() or "(commit yok)")
            cikti.append("$ git status --short")
            cikti.append(g("status", "--short")[1].rstrip() or "(temiz)")
    finally:
        shutil.rmtree(d, ignore_errors=True)
    metin = "\n".join(cikti)
    return "fatal:" not in metin and "error:" not in metin, metin


# ---------------------------------------------------------------- Docker
_APP = ('import os\nfrom http.server import BaseHTTPRequestHandler, HTTPServer\n'
        'class H(BaseHTTPRequestHandler):\n    def do_GET(self):\n        self.send_response(200); self.end_headers()\n'
        '        self.wfile.write(("Merhaba! MESAJ=" + os.environ.get("MESAJ", "yok")).encode())\n'
        'print("Sunucu 8000 portunda basladi", flush=True)\nHTTPServer(("0.0.0.0", 8000), H).serve_forever()\n')
_DOCKERFILE_COZUM = 'FROM python:3.12-slim\nWORKDIR /app\nCOPY app.py .\nRUN useradd -m uygulama\nUSER uygulama\nEXPOSE 8000\nCMD ["python", "app.py"]\n'


def _build_ozet(out):
    """docker build gürültüsünü at: sadece adımlar, hatalar ve imaj adı kalsın."""
    sonuc = []
    for s in out.splitlines():
        s = s.strip()
        if not s:
            continue
        if re.match(r"^#\d+ \[(\d+/\d+|internal)\]", s) and "load" not in s:
            sonuc.append(re.sub(r"^#\d+ ", "", s))
        elif re.match(r"^#\d+ naming to", s):
            sonuc.append("imaj adı: " + s.split("naming to ", 1)[1].replace(" done", "").replace("docker.io/library/", ""))
        elif re.search(r"ERROR|error|failed|not found", s) and not s.startswith("#") or re.match(r"^#\d+ ERROR", s):
            sonuc.append(s)
        elif re.match(r"^#\d+ \d+\.\d+ ", s):       # RUN adımlarının çıktısı
            sonuc.append("   " + re.sub(r"^#\d+ \d+\.\d+ ", "", s))
    g = []
    for x in sonuc:
        if x not in g:
            g.append(x)
    return g or ["(ayrıntılı çıktı yok)"]


def _dk_klasor(dockerfile=None):
    d = Path(tempfile.mkdtemp(prefix="atolye-dkr-"))
    (d / "app.py").write_text(_APP, "utf-8")
    (d / "Dockerfile").write_text(dockerfile if dockerfile is not None else _DOCKERFILE_COZUM, "utf-8", newline="\n")
    (d / ".dockerignore").write_text(".git\n__pycache__\n.env\n", "utf-8")
    return d


def docker(key, kod):
    if shutil.which("docker") is None:
        return False, "Docker bulunamadı ya da çalışmıyor."
    if key == "03-0":
        d = _dk_klasor(kod)
        try:
            rc, out = _calistir(["docker", "build", "-t", "atolye-deneme", "--progress=plain", "."], cwd=str(d), sure=300)
            satir = _build_ozet(out)
            ok = rc == 0
            son = "\n".join(satir[-14:])
            if ok:
                _, boyut = _calistir(["docker", "images", "atolye-deneme", "--format", "imaj: {{.Repository}}:{{.Tag}}  boyut: {{.Size}}"])
                son += "\n\n✔ Derleme başarılı.\n" + boyut.strip()
                _calistir(["docker", "rmi", "-f", "atolye-deneme"])
            else:
                son = "✘ Derleme başarısız:\n" + son
            return ok, son
        finally:
            shutil.rmtree(d, ignore_errors=True)
    if key == "03-3":
        d = _dk_klasor()
        try:
            (d / "docker-compose.yml").write_text(kod, "utf-8", newline="\n")
            if re.search(r"docker\.sock|privileged|\.\./|/etc/|C:\\|network_mode:\s*host", kod, re.I):
                return False, "Güvenlik: bu ayar atölyede çalıştırılmaz (host'a erişim)."
            rc, out = _calistir(["docker", "compose", "-p", "atolye-deneme", "config"], cwd=str(d), sure=60)
            return rc == 0, ("✔ Compose dosyası geçerli. Docker'ın yorumu:\n\n" if rc == 0 else "✘ Compose dosyası hatalı:\n\n") + out.strip()
        finally:
            shutil.rmtree(d, ignore_errors=True)
    # 03-2: komut dizisi
    d = _dk_klasor()
    cikti = []
    try:
        _calistir(["docker", "rm", "-f", "deneme"])
        for ham in kod.replace("\r", "").split("\n"):
            s = ham.strip()
            if not s or s.startswith("#"):
                continue
            cikti.append("$ " + s)
            try:
                t = shlex.split(s)
            except ValueError as e:
                cikti.append(f"Komut okunamadı: {e}"); break
            if len(t) < 2 or t[0] != "docker":
                cikti.append("Yalnızca docker komutları çalışır."); break
            alt, rest = t[1], t[2:]
            if alt == "build":
                if rest[:2] != ["-t", "atolye-lab03"] or rest[2:] != ["."]:
                    cikti.append("Bu görevde: docker build -t atolye-lab03 ."); break
                args = ["docker", "build", "-t", "atolye-lab03", "--progress=plain", "."]
            elif alt == "run":
                izin = True; i = 0; port = None; ad = None; imaj = None
                while i < len(rest):
                    x = rest[i]
                    if x == "-d": i += 1
                    elif x == "-p" and i + 1 < len(rest) and re.fullmatch(r"1\d{4}:\d{2,5}", rest[i + 1]): port = rest[i + 1]; i += 2
                    elif x == "--name" and i + 1 < len(rest): ad = rest[i + 1]; i += 2
                    elif x == "atolye-lab03" and i == len(rest) - 1: imaj = x; i += 1
                    else: izin = False; break
                if not izin or ad != "deneme" or imaj != "atolye-lab03":
                    cikti.append("Bu görevde: docker run -d -p 18099:8000 --name deneme atolye-lab03 (başka bayrak/imaj çalışmaz)."); break
                args = ["docker", "run"] + rest
            elif alt in ("logs", "rm", "stop", "exec", "ps"):
                if alt == "ps":
                    args = ["docker", "ps", "--filter", "name=deneme"]
                else:
                    hedefler = [x for x in rest if not x.startswith("-")]
                    if not hedefler or hedefler[0] != "deneme":
                        cikti.append("Bu görevde yalnızca 'deneme' konteyneri kullanılır."); break
                    args = ["docker", alt] + rest
            else:
                cikti.append(f"'docker {alt}' bu atölyede çalıştırılmaz."); break
            rc, out = _calistir(args, cwd=str(d), sure=300)
            if alt == "build":
                out = "\n".join(_build_ozet(out)[-8:])
            cikti.append(out.rstrip())
            if alt == "run" and rc == 0 and port:
                time.sleep(1.5)
                try:
                    with urllib.request.urlopen(f"http://localhost:{port.split(':')[0]}", timeout=5) as r:
                        cikti.append(f"(otomatik: http://localhost:{port.split(':')[0]} → {r.read().decode()})")
                except Exception as e:
                    cikti.append(f"(otomatik istek başarısız: {e})")
    finally:
        _calistir(["docker", "rm", "-f", "deneme"])
        shutil.rmtree(d, ignore_errors=True)
    metin = "\n".join(cikti)
    red = re.search(r"Bu görevde|çalıştırılmaz|Yalnızca docker|okunamadı", metin)
    return (not red) and "error" not in metin.lower().split("(otomatik")[0], metin


# ---------------------------------------------------------------- curl (urllib ile; kabuk yok)
_URL = re.compile(r"^http://(localhost|127\.0\.0\.1):(15672|4000|7860)/")


def _http(yontem, url, basliklar, govde=None):
    r = urllib.request.Request(url, data=govde, method=yontem, headers=basliklar)
    try:
        with urllib.request.urlopen(r, timeout=40) as x:
            return x.status, x.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")
    except Exception as e:
        return 0, str(e)


def _rab(env, yontem, yol, govde=None):
    b = "Basic " + base64.b64encode(f"{env.get('RABBITMQ_USER','')}:{env.get('RABBITMQ_PASSWORD','')}".encode()).decode()
    return _http(yontem, "http://localhost:15672/api/" + yol, {"Authorization": b, "Content-Type": "application/json"},
                 json.dumps(govde).encode() if govde is not None else None)


def curl(key, kod):
    env = _env()
    sirlar = [env.get(k, "") for k in ("RABBITMQ_PASSWORD", "LITELLM_MASTER_KEY", "LANGFLOW_API_KEY", "ANTHROPIC_API_KEY")]
    cikti = []
    # curl komutu
    duz = re.sub(r"\\\r?\n", " ", kod)
    duz = "\n".join(l for l in duz.splitlines() if l.strip() and not l.strip().startswith("#"))
    try:
        t = shlex.split(duz)
    except ValueError as e:
        return False, f"✘ Komut okunamadı (tırnakları kontrol et): {e}"
    if not t or t[0] != "curl":
        return False, "✘ Komut `curl` ile başlamalı."
    yontem, url, basliklar, govde, kul = None, None, {}, None, None
    i = 1
    while i < len(t):
        x = t[i]
        if x in ("-X", "--request"): yontem = t[i + 1]; i += 2
        elif x in ("-H", "--header"):
            a, _, b = t[i + 1].partition(":"); basliklar[a.strip()] = b.strip(); i += 2
        elif x in ("-d", "--data", "--data-raw"): govde = t[i + 1]; i += 2
        elif x in ("-u", "--user"): kul = t[i + 1]; i += 2
        elif x in ("-s", "-i", "-k", "-S"): i += 1
        elif x.startswith("http"): url = x; i += 1
        else: return False, f"✘ Bu atölyede desteklenmeyen curl seçeneği: {x}"
    if not url or not _URL.match(url):
        return False, "✘ Adres http://localhost:15672, :4000 veya :7860 ile başlamalı."
    sub = {"$LITELLM_MASTER_KEY": env.get("LITELLM_MASTER_KEY", ""), "${LITELLM_MASTER_KEY}": env.get("LITELLM_MASTER_KEY", ""),
           "$LANGFLOW_API_KEY": env.get("LANGFLOW_API_KEY", ""), "${LANGFLOW_API_KEY}": env.get("LANGFLOW_API_KEY", "")}
    for k_, v_ in sub.items():
        basliklar = {a: b.replace(k_, v_) for a, b in basliklar.items()}
    if kul is not None:
        basliklar["Authorization"] = "Basic " + base64.b64encode(f"{env.get('RABBITMQ_USER','')}:{env.get('RABBITMQ_PASSWORD','')}".encode()).decode()
    notlar = []
    if govde and key == "06-2" and '"atolye"' in govde:
        govde = govde.replace('"atolye"', '"hizli"'); notlar.append("Not: bu makinedeki LiteLLM'de 'atolye' takma adı yok; 'hizli' ile çalıştırıldı. Config'e eklenince 'atolye' çalışır.")
    if key == "06-2" and govde:
        try:
            j = json.loads(govde); j.setdefault("max_tokens", 300); govde = json.dumps(j)
        except Exception:
            pass
    if key == "08-1":
        notlar.append("Not: ABC123 örnek bir akış kimliğidir; LangFlow'da bu id'li akış olmadığı için hata/404 dönmesi NORMALDİR. Gerçek akışta cevap gelir.")
    cikti.append(f"{yontem or ('POST' if govde else 'GET')} {url}")
    s, o = _http(yontem or ("POST" if govde else "GET"), url, basliklar, govde.encode() if govde else None)
    try:
        j_ = json.loads(o)
        if isinstance(j_, dict) and "choices" in j_:
            m_ = (j_["choices"][0].get("message") or {}).get("content")
            u_ = j_.get("usage", {})
            o_ = ("cevap  : " + str(m_) + "\nmodel  : " + str(j_.get("model")) + "\nusage  : " + str(u_.get("prompt_tokens")) +
                  " giriş + " + str(u_.get("completion_tokens")) + " çıkış token  (fatura buradan hesaplanır)")
            if m_ in (None, ""):
                o_ += "\n⚠ cevap boş: max_tokens çok küçük olabilir, model cevabı bitiremeden kesildi."
        else:
            o_ = json.dumps(j_, ensure_ascii=False, indent=2)
    except Exception:
        o_ = o
    cikti.append(f"HTTP {s}\n{o_[:1800]}")
    cikti += notlar
    ok = 200 <= s < 300
    return ok, _maske("\n".join(cikti), sirlar)


# ---------------------------------------------------------------- RabbitMQ (Lab 05): gerçek rabbitmqadmin
RAB_KAP = "aiasistan-rabbitmq-1"
_RAB_KOMUT = {("declare", "queue"), ("declare", "exchange"), ("declare", "binding"), ("publish", "message"),
              ("get", "messages"), ("purge", "queue"), ("delete", "queue"), ("delete", "exchange"),
              ("list", "queues"), ("list", "exchanges"), ("list", "bindings")}
_RAB_BAYRAK = {"--name", "--type", "--durable", "--auto-delete", "--arguments", "--source", "--destination-type",
               "--destination", "--routing-key", "-k", "--exchange", "-e", "--payload", "-m", "--properties", "-p",
               "--queue", "-q", "--count", "-c", "--ack-mode", "-a"}
_RAB_AD_BAYRAK = {"--name", "--source", "--destination", "--queue", "-q"}


def _rab_temizle(env):
    """atolye.* ile başlayan tüm deneme kuyruk/exchange'lerini sil (gerçek kuyruklara dokunmaz)."""
    s, o = _rab(env, "GET", "queues/%2F")
    try:
        for q in json.loads(o):
            if q["name"].startswith("atolye."):
                _rab(env, "DELETE", "queues/%2F/" + urllib.parse.quote(q["name"], safe=""))
    except Exception:
        pass
    s, o = _rab(env, "GET", "exchanges/%2F")
    try:
        for x in json.loads(o):
            if x["name"].startswith("atolye."):
                _rab(env, "DELETE", "exchanges/%2F/" + urllib.parse.quote(x["name"], safe=""))
    except Exception:
        pass


def _rab_durum(env):
    """Çalışma sonunda kuyrukların/bağlantıların gerçek durumu."""
    sat = []
    rc, out = _calistir(["docker", "exec", RAB_KAP, "rabbitmqctl", "list_queues", "-q", "--formatter", "json",
                         "name", "messages_ready", "messages_unacknowledged", "arguments"], sure=30)
    try:
        qs = sorted((q for q in json.loads(out[out.index("["):]) if q["name"].startswith("atolye.")), key=lambda q: q["name"])
    except Exception:
        qs = []
    s, o = _rab(env, "GET", "exchanges/%2F")
    try:
        exs = [(x["name"], x["type"]) for x in json.loads(o) if x["name"].startswith("atolye.")]
    except Exception:
        exs = []
    s, o = _rab(env, "GET", "bindings/%2F")
    try:
        bs = [(b["source"], b["destination"], b["destination_type"], b["routing_key"]) for b in json.loads(o)
              if b["source"].startswith("atolye.")]
    except Exception:
        bs = []
    sat.append("── Sonuç: RabbitMQ'da şu an ne var? ──")
    if not (qs or exs):
        sat.append("(hiçbir şey yok — bu çalıştırmada kuyruk/exchange açılmadı)")
    for n, t in exs:
        sat.append(f"exchange  {n:<26} tip: {t}")
    for q in qs:
        a = ", ".join(f"{k}={v}" for k, _, v in q["arguments"] if k != "x-queue-type")
        sat.append(f"kuyruk    {q['name']:<26} bekleyen: {q['messages_ready']}  işlenmekte(ack bekliyor): {q['messages_unacknowledged']}" + (f"   [{a}]" if a else ""))
    for src, dst, tip, rk in sorted(bs):
        sat.append(f"bağ       {src}  --[{rk}]-->  {dst}" + ("  (exchange)" if tip == "exchange" else ""))
    return "\n".join(sat)


def rabbit(key, kod):
    env = _env()
    sirlar = [env.get(k, "") for k in ("RABBITMQ_PASSWORD",)]
    komutlar = []
    for ham in kod.splitlines():
        l = ham.strip()
        if not l or l.startswith("#"):
            continue
        try:
            t = shlex.split(l)
        except ValueError as e:
            return False, f"✘ Tırnaklar kapanmamış ({e}):\n{l}"
        komutlar.append((l, t))
    if not komutlar:
        return False, "Önce editöre komutları yaz."
    if len(komutlar) > 25:
        return False, "En fazla 25 komut."
    # ---- doğrulama (hiçbir şey çalışmadan önce)
    for l, t in komutlar:
        if t[0] == "sleep":
            if len(t) != 2 or not t[1].isdigit() or int(t[1]) > 5:
                return False, f"✘ `sleep` en fazla 5 saniye: {l}"
            continue
        if t[0] != "rabbitmqadmin" or len(t) < 3 or (t[1], t[2]) not in _RAB_KOMUT:
            return False, (f"✘ Anlamadığım komut: {l}\nDesteklenenler: rabbitmqadmin declare queue|exchange|binding, publish message, "
                           "get messages, purge queue, delete queue|exchange, list queues|exchanges|bindings (ve `sleep 2`).")
        a = t[3:]
        i = 0
        while i < len(a):
            if a[i] not in _RAB_BAYRAK:
                return False, f"✘ Bu seçeneği tanımıyorum: {a[i]}   (komut: {l})"
            if i + 1 >= len(a):
                return False, f"✘ {a[i]} seçeneğinin değeri eksik. (komut: {l})"
            bay, val = a[i], a[i + 1]
            if bay in _RAB_AD_BAYRAK and not val.startswith("atolye."):
                return False, f"✘ Güvenlik: bu atölyede adlar `atolye.` ile başlamalı ({bay} {val}). Böylece asıl kuyruklarına dokunulmaz."
            if bay in ("--exchange", "-e") and val not in ("", "amq.default") and not val.startswith("atolye."):
                return False, f"✘ Güvenlik: exchange adı `atolye.` ile başlamalı ({val})."
            if bay == "--arguments":
                try:
                    j = json.loads(val)
                except Exception as e:
                    return False, f"✘ --arguments geçerli JSON olmalı ({e}):\n{val}\nİpucu: JSON'u tek tırnak içine al: --arguments '{{\"x-message-ttl\":2000}}'"
                d = j.get("x-dead-letter-exchange")
                if d is not None and not str(d).startswith("atolye."):
                    return False, "✘ Güvenlik: x-dead-letter-exchange `atolye.` ile başlamalı."
            if bay in ("--payload", "-m") and len(val) > 500:
                return False, "✘ Mesaj en fazla 500 karakter."
            i += 2
    # ---- çalıştır
    _rab_temizle(env)
    cikti, ok = [], True
    toplam_bekle = 0
    try:
        for l, t in komutlar:
            cikti.append("$ " + l)
            if t[0] == "sleep":
                toplam_bekle += int(t[1])
                if toplam_bekle > 8:
                    cikti.append("  ✘ toplam bekleme 8 sn'yi geçemez"); ok = False; break
                time.sleep(int(t[1]))
                cikti.append(f"  … {t[1]} sn beklendi")
                continue
            rc, out = _calistir(["docker", "exec", RAB_KAP, "sh", "-c",
                                 'exec rabbitmqadmin --non-interactive --host localhost --port 15672 '
                                 '--username "$RABBITMQ_DEFAULT_USER" --password "$RABBITMQ_DEFAULT_PASS" "$@"', "sh"] + t[1:], sure=40)
            out = out.strip()
            if rc == 127:
                return False, "Docker bulunamadı ya da RabbitMQ konteyneri çalışmıyor."
            if rc != 0:
                m = re.search(r"deprecated|not permitted", out)
                cikti.append("  ✘ " + out[:600].replace("\n", "\n    ") + ("\n  İpucu: kuyruk kalıcı olmalı: --durable true" if m else ""))
                ok = False
                break
            alt = (t[1], t[2])
            if alt == ("publish", "message"):
                if "NOT routed" in out:
                    cikti.append("  ⚠ yayınlandı AMA hiçbir kuyruğa ulaşmadı → mesaj KAYBOLDU (routing key yanlış ya da bağ/binding yok)")
                else:
                    cikti.append("  ✔ yayınlandı ve en az bir kuyruğa ulaştı (routed)")
            elif alt == ("get", "messages"):
                if not out:
                    cikti.append("  (kuyrukta mesaj yok)")
                else:
                    cikti.append("  no  yeniden_teslim  exchange  routing_key  kalan  [özellik/başlık]  içerik  kodlama")
                    for r_ in out.splitlines():
                        r_ = r_.strip()
                        sebep = re.search(r'"reason":"(\w+)"', r_)
                        cikti.append("  " + (re.sub(r'headers: \{.*?\}\}?,\s', 'headers: {x-death…}, ', r_) if sebep else r_))
                        if re.match(r"^\d+\s+true\b", r_):
                            cikti.append("  ↑ yeniden_teslim=true: bu mesaj daha önce verilmiş (ack gelmediği için ya da ölü mektup olarak geri döndü)")
                        if sebep:
                            cikti.append(f"  ↑ ölü mektup! RabbitMQ başlığa neden yazdı: reason={sebep.group(1)}  (rejected = reddedildi, expired = süresi doldu, maxlen = kuyruk doldu)")
            elif alt[0] == "list":
                sat = [r_ for r_ in out.splitlines() if "atolye." in r_]
                cikti.append("  " + ("\n  ".join(re.sub(r"\s{2,}", "  ", r_.strip()) for r_ in sat) if sat else "(atolye.* adlı bir şey yok)"))
            else:
                cikti.append("  ✔ tamam" + (": " + out[:200] if out else ""))
        cikti.append("")
        cikti.append(_rab_durum(env))
    finally:
        _rab_temizle(env)
    cikti.append("(deneme nesneleri temizlendi; her çalıştırma sıfırdan başlar)")
    return ok, _maske("\n".join(cikti), sirlar)


# ---------------------------------------------------------------- C# (Lab 07)
_SURUCU = {
    "07-0": ('var m = new[] { "a", "b", "c", "d" };\n'
             'foreach (var n in new[] { 2, 9, 0, -3 }) Console.WriteLine($"SonN([a,b,c,d], {n}) -> [{string.Join(\", \", KonusmaPenceresi.SonN(m, n))}]");\n'),
    "07-1": ('foreach (var (s, e) in new[] { (19, 20), (20, 20), (21, 20) }) Console.WriteLine($"OzetGerekliMi({s}, {e}) -> {K.OzetGerekliMi(s, e)}");\n'),
    "07-2": ('var a = new[] { "Kahve sever", "  kahve SEVER ", "YOK", "yok", "", "   ", "Ad: Emre\\nYas: 30", new string(\'x\', 201), "Vegan" };\n'
             'Console.WriteLine("HafizaFiltrele(9 aday) -> [" + string.Join(", ", K.HafizaFiltrele(a).Select(x => "\\"" + x + "\\"")) + "]");\n'),
}


_RAB_CS_BASLIK = """using RabbitMQ.Client; using RabbitMQ.Client.Events; using System.Text;
Console.OutputEncoding = Encoding.UTF8;
const string KULLANICI = "@@U@@"; const string SIFRE = "@@P@@";
"""
_RAB_CS_BAGLANTI = """var fabrika = new ConnectionFactory { HostName = "localhost", UserName = KULLANICI, Password = SIFRE };
await using var baglanti = await fabrika.CreateConnectionAsync();
await using var kanal = await baglanti.CreateChannelAsync();
"""
_RAB_CS_IS = """const string kuyruk = "atolye.is";
await kanal.QueueDeclareAsync(kuyruk, durable: true, exclusive: false, autoDelete: false);
foreach (var m in new[] { "siparis-1", "siparis-2", "siparis-3" })
    await kanal.BasicPublishAsync("", kuyruk, Encoding.UTF8.GetBytes(m));
Console.WriteLine("[hazır] atolye.is kuyruğunda 3 mesaj bekliyor.");
"""
_RAB_CS_IS_SON = """
await Task.Delay(1500);
await kanal.CloseAsync();   // ack verilmeyen mesajlar kuyruğa GERİ DÖNER
await using var kanal2 = await baglanti.CreateChannelAsync();
var durum = await kanal2.QueueDeclarePassiveAsync(kuyruk);
Console.WriteLine(durum.MessageCount == 0 ? "[sonuç] Kuyrukta bekleyen mesaj: 0  -> hiç mesaj kalmadı (hepsi ack'lendi ya da reddedildi) ✔"
    : $"[sonuç] Kuyrukta bekleyen mesaj: {durum.MessageCount}  -> ack vermediğin mesajlar kuyruğa geri döndü (at-least-once!)");
"""
_RAB_CS_YASAK = re.compile(r"System\.IO|Process\.|File\.|Directory\.|HttpClient|Environment\.|Registry|DllImport|Assembly\.|Socket|"
                           r"Delete\w*Async|Purge\w*Async|\.Uri\b|AmqpTcpEndpoint|Endpoint|ai-istegi|sohbet-tamamlandi|MassTransit|"
                           r"HostName\s*=\s*\"(?!localhost\")")


def _cs_rabbit(key, kod):
    if _RAB_CS_YASAK.search(kod):
        return False, ("Güvenlik: bu atölyede yalnızca localhost'a bağlanabilirsin; dosya/süreç/ağ kodu, silme/boşaltma ve "
                       "gerçek kuyruk adları (ai-istegi...) engelli. Kuyruk/exchange adların `atolye.` ile başlasın.")
    env = _env()
    d = KUM / "cs-rabbit"
    d.mkdir(parents=True, exist_ok=True)
    (d / "cs.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                                 '<TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
                                 '<Nullable>enable</Nullable><TreatWarningsAsErrors>false</TreatWarningsAsErrors><WarningsAsErrors></WarningsAsErrors><NoWarn>CS8602;CS8604;CS8600;CS0219;CS0168;CS1998</NoWarn></PropertyGroup>'
                                 '<ItemGroup><PackageReference Include="RabbitMQ.Client" Version="7.2.2" /></ItemGroup></Project>', "utf-8")
    on = _RAB_CS_BASLIK.replace("@@U@@", env.get("RABBITMQ_USER", "")).replace("@@P@@", env.get("RABBITMQ_PASSWORD", ""))
    son = ""
    if key != "05-0":
        on += _RAB_CS_BAGLANTI
    if key == "05-2":
        on += _RAB_CS_IS
        son = _RAB_CS_IS_SON
    on += "// ======== SENİN KODUN BAŞLIYOR ========\n"
    (d / "Program.cs").write_text(on + kod + "\n" + son, "utf-8")
    ofset = on.count("\n")
    _rab_temizle(env)
    out = ""
    try:
        rc, out = _calistir(["dotnet", "run", "--nologo", "-v", "q"], cwd=str(d), sure=180)
        durum = _rab_durum(env) if rc == 0 else ""
    finally:
        _rab_temizle(env)
    out = _maske(out, [env.get("RABBITMQ_PASSWORD", "")])
    if rc == 0:
        return True, (out.strip() or "(çıktı yok)") + ("\n\n" + durum if durum else "") + "\n(deneme nesneleri temizlendi; her çalıştırma sıfırdan başlar)"
    hata = [x.strip() for x in out.splitlines() if "error" in x and "Program.cs(" in x]
    def _sat(m):
        return f"satır {max(1, int(m.group(1)) - ofset)}: "
    hata = sorted(set(re.sub(r"^.*?Program\.cs\((\d+),\d+\):\s*", _sat, h).split(" [")[0] for h in hata))[:6]
    kes = out.strip().splitlines()
    return False, "✘ Derleme/çalışma hatası:\n" + "\n".join(hata or kes[:6] + kes[-6:])


def cs(key, kod):
    if key.startswith("05-"):
        return _cs_rabbit(key, kod)
    if re.search(r"System\.IO|Process\.|File\.|Directory\.|HttpClient|Environment\.|Registry|DllImport|Assembly\.", kod):
        return False, "Güvenlik: bu atölyede dosya/ağ/süreç erişimi olan C# çalıştırılmaz."
    d = KUM / "cs-calistir"
    d.mkdir(parents=True, exist_ok=True)
    (d / "cs.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                                 '<TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
                                 '<Nullable>enable</Nullable></PropertyGroup></Project>', "utf-8")
    sinif = kod if key == "07-0" else "public static class K\n{\n" + kod + "\n}\n"
    (d / "Program.cs").write_text("Console.OutputEncoding = System.Text.Encoding.UTF8;\n" + _SURUCU[key] + "\n" + sinif, "utf-8")
    rc, out = _calistir(["dotnet", "run", "--nologo", "-v", "q"], cwd=str(d), sure=180)
    if rc == 0:
        return True, out.strip() or "(çıktı yok)"
    hata = [s.strip() for s in out.splitlines() if "error" in s]
    hata = sorted(set(re.sub(r"^.*?Program\.cs\((\d+),\d+\):\s*", r"satır \1: ", h).split(" [")[0] for h in hata))[:6]
    return False, "✘ Derleme/çalışma hatası:\n" + "\n".join(hata or [out[-600:]])


# ---------------------------------------------------------------- sözdizimi
def sozdizimi(key, kod):
    if key == "10-3":
        try:
            j = json.loads(kod)
            return True, "✔ Geçerli JSON.\n" + ("mcpServers: " + ", ".join(j.get("mcpServers", {}).keys()) if isinstance(j, dict) else "")
        except Exception as e:
            return False, f"✘ JSON hatası: {e}"
    try:
        import yaml
    except ImportError:
        return False, "PyYAML yok: pip install pyyaml"
    try:
        v = yaml.safe_load(kod)
    except yaml.YAMLError as e:
        return False, f"✘ YAML hatası (girintiye bak, TAB kullanma):\n{e}"
    def _duzelt(x):
        if isinstance(x, dict):
            return {("on" if k is True else str(k)): _duzelt(v_) for k, v_ in x.items()}
        if isinstance(x, list):
            return [_duzelt(i) for i in x]
        return x
    return True, "✔ Geçerli YAML. Anladığım yapı:\n" + json.dumps(_duzelt(v), ensure_ascii=False, indent=2, default=str)[:1800]


def calistir(key, kod):
    """(ok, çıktı) döndürür. Desteklenmiyorsa (None, mesaj)."""
    if len(kod) > 20000:
        return False, "Kod çok uzun."
    for ad, anahtarlar in SEVIYE.items():
        if key in anahtarlar:
            return {"git": git, "mongo": mongo, "docker": docker, "curl": curl, "rabbit": rabbit, "cs": cs, "sozdizimi": sozdizimi}[ad](key, kod)
    return None, "Bu adım sayfada çalıştırılamaz (metin cevabı ya da dış sistem gerektirir). 'Kontrol et' ile doğrula."
