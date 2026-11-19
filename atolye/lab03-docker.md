# Lab 03: Docker

**Hedef:** Küçük bir Python web uygulamasını konteynerleştir: Dockerfile, root olmayan kullanıcı, `.dockerignore`, compose.
**Çalışma yeri:** `atolye/kum/lab03` (içinde hazır `app.py` var: 8000 portunda `MESAJ` ortam değişkenini döndürür).

Zihinsel model: **imaj** = tarif/şablon (değişmez), **konteyner** = o imajdan çalışan canlı süreç.
`Dockerfile` imajı yapar, `docker-compose.yml` birden çok konteyneri tek dosyayla yönetir.
Docker Desktop çalışıyor olmalı.

## Görevler

**G1. `Dockerfile` yaz.** FROM, WORKDIR, COPY, EXPOSE 8000, CMD.
> Örnek (Node uygulaması için, sen Python yazacaksın):
> ```dockerfile
> FROM node:22-slim
> WORKDIR /app
> COPY package*.json ./
> RUN npm ci
> COPY . .
> EXPOSE 3000
> CMD ["node", "server.js"]
> ```
> Neden bağımlılık dosyası ayrı COPY? **Katman önbelleği:** kod değişince `npm ci` tekrar çalışmaz. (Python'da hazır bağımlılık yok, bu uygulama sadece standart kütüphane kullanır.)
> İmaj: `python:3.12-slim`, `CMD ["python", "app.py"]`.

**G2. Root olmayan kullanıcı.** Konteynerde root çalışmak güvenlik riski.
> Örnek (Debian tabanlı imajda): `RUN useradd -m uygulama` sonra `USER uygulama` (CMD'den önce).

**G3. `.dockerignore`.** `.git` ve `__pycache__` imaja girmesin (imaj küçülür, sır sızmaz).
> Örnek: dosyaya satır satır `.git`, `node_modules`, `.env`, `__pycache__` yaz.

**G4. Derle.** `docker build -t atolye-lab03 atolye/kum/lab03`
> `-t` ad verir. İkinci derlemede "CACHED" yazan adımlar önbellekten gelir: farkı gör.

**G5-G6. Çalıştır.** `docker run -d -p 18099:8000 --name deneme atolye-lab03`, sonra `curl http://localhost:18099`
> `-p DIŞ:İÇ` port yönlendirir. `docker logs deneme`, `docker exec deneme id -u` (0 olmamalı).
> Temizlik: `docker rm -f deneme`. (Doğrulayıcı kendi konteynerini kurar ve siler.)

**G7-G8. `docker-compose.yml`.** Servis adı `web`; `build: .`, `ports`, `environment: MESAJ`, `volumes`.
> Örnek:
> ```yaml
> services:
>   web:
>     build: .
>     ports: ["18098:8000"]
>     environment:
>       MESAJ: "compose ile calisiyorum"
>     volumes:
>       - veri:/veri
> volumes:
>   veri:
> ```
> `docker compose up -d --build` · `docker compose logs web` · `docker compose down` (`-v` volume'ü de siler: VERİ GİDER).

## Sık hatalar
- Konteyner içinde `localhost` konteynerin KENDİSİDİR; başka servise adıyla ulaşılır (`mongo`, `rabbitmq`).
- Port zaten kullanımda: başka bir yere ver (`18097:8000`).
- Değişiklik görünmüyor: `--build` unutuldu.

## Mülakat soruları
1. İmaj ile konteyner farkı? Volume neden var (konteyner silinince veri ne olur)?
2. Multi-stage build nedir, neden .NET imajlarında kullanılır? (proje: `src/Asistan.Api/Dockerfile`)
3. `CMD` ile `ENTRYPOINT` farkı? `COPY` ile `ADD` farkı?
4. Katman önbelleği nasıl çalışır, Dockerfile satır sırası neden önemli?
5. `docker compose down` ile `down -v` farkı?
