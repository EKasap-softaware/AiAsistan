"""Atölye öğretim sayfası için küçük yerel sunucu (yalnızca bu bilgisayardan erişilir).

Çalıştır:  python atolye/sunucu.py   →   http://localhost:5300
Sayfa görevleri gösterir; "Kontrol et" düğmesi doğrulayıcıyı (atolye.ps1) çalıştırır.
Neden ayrı sunucu? Lab 01/07 `dotnet build` yapar; çalışan API'nin DLL'lerini kilitlerdi.
"""
import json, mimetypes, re, shutil, subprocess, sys, threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
import calistir as CAL   # "▶ Çalıştır" düğmesinin güvenli çalıştırıcıları
CALIS = threading.Lock()
A = Path(__file__).resolve().parent
K = A.parent
WEB = A / "web"
PORT = 5300
LABLER = [("01", ".NET + EF Core + Solution + Unit Test"), ("02", "Git / GitHub"), ("03", "Docker"), ("04", "MongoDB"), ("05", "RabbitMQ"),
          ("06", "LiteLLM"), ("07", "AI Memory"), ("08", "LangFlow"), ("09", "CI/CD"), ("10", "Claude CLI / Skills / MCP / Markdown")]
IDLER = [i for i, _ in LABLER]
MESGUL = threading.Lock()   # aynı anda tek doğrulama (docker/dotnet çakışmasın)


def tamamlananlar():
    try:
        d = json.loads((A / ".ilerleme.json").read_text("utf-8-sig")).get("tamam", [])
        return [str(x) for x in ([d] if isinstance(d, str) else d)]
    except Exception:
        return []


def guncel(t):
    return next((i for i in IDLER if i not in t), None)


def kilitli(i, t):
    for j in IDLER:
        if j == i:
            return False
        if j not in t:
            return True
    return True


def hazirla(i):   # başlangıç dosyalarını kum klasörüne kopyala (yoksa)
    kaynak, hedef = A / "baslangic" / f"lab{i}", A / "kum" / f"lab{i}"
    if kaynak.is_dir() and not hedef.exists():
        shutil.copytree(kaynak, hedef)


def dogrula(i):
    r = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(A / "atolye.ps1"), "dogrula", i],
                       cwd=str(K), capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=900)
    cikti = r.stdout + r.stderr
    kontroller = []
    for satir in cikti.splitlines():
        m = re.match(r"^\s*([✔✘])\s+(.*)$", satir)
        if m:
            kontroller.append({"ok": m.group(1) == "✔", "ad": m.group(2).strip(), "ipucu": ""})
            continue
        m = re.match(r"^\s*ipucu:\s*(.*)$", satir)
        if m and kontroller:
            kontroller[-1]["ipucu"] = m.group(1).strip()
        m = re.match(r"^\s*•\s*(.*)$", satir)
        if m:
            kontroller.append({"ok": None, "ad": m.group(1).strip(), "ipucu": ""})
    return {"kod": r.returncode, "gecti": r.returncode == 0, "kontroller": kontroller,
            "seviyeAtladi": "LEVEL" in cikti and "TAMAM" in cikti, "ham": "" if kontroller else cikti[-1500:]}


class Isleyici(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def _json(self, veri, kod=200):
        b = json.dumps(veri, ensure_ascii=False).encode("utf-8")
        self.send_response(kod)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(b)

    def _guvenli(self, yazma=False):
        host = self.headers.get("Host", "").split(":")[0]
        if host not in ("localhost", "127.0.0.1"):       # DNS rebinding'e karşı
            self._json({"hata": "yasak"}, 403); return False
        if self.path.startswith("/api/") and self.headers.get("X-Atolye") != "1":   # başka siteler bu başlığı gönderemez
            self._json({"hata": "yasak"}, 403); return False
        return True

    def do_GET(self):
        if not self._guvenli():
            return
        p = self.path.split("?")[0]
        if p == "/api/durum":
            t = tamamlananlar()
            return self._json({"tamam": t, "guncel": guncel(t), "labler": [{"id": i, "ad": a} for i, a in LABLER]})
        if p == "/api/calistir":
            return self._json({"destek": CAL.DESTEK, "ornek": CAL.ORNEK_OK})
        if p == "/api/kayit":     # web sayfasının ilerlemesi (tarayıcı verisi silinse de kaybolmasın)
            try:
                return self._json(json.loads((A / ".web-ilerleme.json").read_text("utf-8")))
            except Exception:
                return self._json({})
        m = re.fullmatch(r"/api/lab/(\d\d)", p)
        if m:
            i = m.group(1)
            if i not in IDLER:
                return self._json({"hata": "lab yok"}, 404)
            t = tamamlananlar()
            ad = dict(LABLER)[i]
            if kilitli(i, t):
                return self._json({"id": i, "ad": ad, "kilitli": True, "tamam": False})
            hazirla(i)
            md = next(A.glob(f"lab{i}-*.md")).read_text("utf-8-sig")
            return self._json({"id": i, "ad": ad, "kilitli": False, "tamam": i in t, "md": md,
                               "gorsel": f"/img/lab{i}.svg" if (WEB / "img" / f"lab{i}.svg").exists() else None})
        # statik dosyalar
        yol = "index.html" if p == "/" else p.lstrip("/")
        f = (WEB / yol).resolve()
        if WEB.resolve() not in f.parents or not f.is_file():
            self.send_response(404); self.end_headers(); return
        b = f.read_bytes()
        self.send_response(200)
        self.send_header("Content-Type", (mimetypes.guess_type(str(f))[0] or "application/octet-stream") + ("; charset=utf-8" if f.suffix in (".html", ".svg") else ""))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(b)

    def do_POST(self):
        if not self._guvenli(True):
            return
        if self.path.split("?")[0] == "/api/kayit":
            n = int(self.headers.get("Content-Length") or 0)
            if n > 2_000_000:
                return self._json({"hata": "çok büyük"}, 413)
            try:
                veri = json.loads(self.rfile.read(n).decode("utf-8"))
                if not isinstance(veri, dict):
                    raise ValueError
            except Exception:
                return self._json({"hata": "geçersiz"}, 400)
            (A / ".web-ilerleme.json").write_text(json.dumps(veri, ensure_ascii=False), "utf-8")
            return self._json({"ok": True})
        mc = re.fullmatch(r"/api/calistir/(\d\d-\d)", self.path.split("?")[0])
        if mc:
            n = int(self.headers.get("Content-Length") or 0)
            if n > 100_000:
                return self._json({"hata": "çok büyük"}, 413)
            try:
                kod = json.loads(self.rfile.read(n).decode("utf-8")).get("kod", "")
            except Exception:
                return self._json({"hata": "geçersiz"}, 400)
            if not CALIS.acquire(blocking=False):
                return self._json({"ok": False, "cikti": "Başka bir çalıştırma sürüyor, birkaç saniye bekle."}, 409)
            try:
                ok, cikti = CAL.calistir(mc.group(1), kod)
                return self._json({"ok": ok, "cikti": cikti})
            except Exception as e:
                return self._json({"ok": False, "cikti": f"Çalıştırıcı hatası: {e}"})
            finally:
                CALIS.release()
        m = re.fullmatch(r"/api/dogrula/(\d\d)", self.path.split("?")[0])
        if not m or m.group(1) not in IDLER:
            return self._json({"hata": "bulunamadı"}, 404)
        i = m.group(1)
        if kilitli(i, tamamlananlar()):
            return self._json({"hata": "kilitli"}, 403)
        if not MESGUL.acquire(blocking=False):
            return self._json({"hata": "Başka bir doğrulama sürüyor, bekle."}, 409)
        try:
            self._json(dogrula(i))
        except subprocess.TimeoutExpired:
            self._json({"hata": "Doğrulama zaman aşımına uğradı (15 dk)."}, 504)
        finally:
            MESGUL.release()


if __name__ == "__main__":
    srv = ThreadingHTTPServer(("127.0.0.1", PORT), Isleyici)
    print(f"Atölye hazır: http://localhost:{PORT}   (durdurmak için Ctrl+C)")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
