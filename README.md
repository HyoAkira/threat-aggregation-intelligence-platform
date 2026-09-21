# Threat Aggregation Intelligence Platform

Threat intelligence sağlayıcılarından IOC verilerini toplayan, normalize eden ve arama, inceleme, yönetim arayüzü sunan bir platform. React/Vite SPA, Nginx üzerinden sunulur; `/api/*` istekleri ASP.NET Core API’ye iletilir. API işleri RabbitMQ’ya gönderir, Worker verileri işler ve SQL Server’a kaydeder.

## Önkoşullar

- Git, Docker Engine veya Docker Desktop ve `docker compose` (`--wait` destekleyen Compose v2 veya sonrası). Docker daemon çalışmalı ve kullanıcınız erişebilmelidir.
- Bash uyumlu terminal, OpenSSL ve curl. Windows’ta WSL2 kullanılabilir.
- Tam Docker kurulumu için host üzerinde Node.js veya .NET gerekmez. Yerel geliştirme/test için .NET SDK **10**, Node.js **24** ve npm gerekir.
- Varsayılan portlar: frontend `3000`, API `8080`, Worker health `8081`, SQL `1433`, RabbitMQ `5672`, yönetim `15672`; test SQL `1432`.

Depoyu klonlayıp kök dizine geçin. Aşağıdaki komutlar aksi belirtilmedikçe bu dizinde çalıştırılır.

```bash
docker compose version
docker info
cp .env.example .env
chmod 600 .env
```

Mevcut kurulumda `.env` veya anahtarları üzerine yazmayın.

## Ortam değişkenleri ve secret hazırlığı

`.env` içindeki tüm `CHANGE_ME_*` değerlerini değiştirin. Her parola için ayrı bir değer üretin; örneğin `openssl rand -hex 24` çıktısına `Aa1!` ekleyin. Böylece SQL Server’ın büyük/küçük harf, rakam ve özel karakter koşulları karşılanır ve Compose interpolasyonu/connection string için sorun yaratabilecek `$` ve `;` karakterlerinden kaçınılır.

| Değişken | Açıklama |
| --- | --- |
| `SA_PASSWORD` | Compose SQL Server başlangıç parolası. |
| `DATABASE__CONNECTIONSTRING` | Yerel .NET için `localhost,1433`; içindeki parola `SA_PASSWORD` ile aynı olmalı. Compose bunu `mssql,1433` ile geçersiz kılar. |
| `TEST_SQLSERVER_PASSWORD` | İzole test SQL parolası; uygulama parolasından farklı tutun. |
| `RABBITMQ__HOST`, `RABBITMQ__PORT` | Yerelde `localhost`, `5672`; Compose host’u `rabbitmq` yapar. |
| `RABBITMQ__USER`, `RABBITMQ__PASSWORD`, `RABBITMQ__EXCHANGE` | Broker ve uygulama ortak ayarları; `guest` yerine örnekteki özel kullanıcıyı kullanın. |
| `ADMIN__USERNAME`, `ADMIN__PASSWORD` | İlk yönetici hesabı. Mevcut hesabın parolası `.env` değiştirilince sıfırlanmaz. |
| `JWT__PRIVATEKEYFILE` | Yerelde `secrets/jwt-private-key.pem`; Compose `/run/secrets/...` yolunu kullanır. |
| `JWT__ISSUER`, `JWT__AUDIENCE` | Token üreten/doğrulayan servislerde aynı kalmalı. |
| `JWT__EXPIRYMINUTES` | Uygulama gereği `480` olmalı. |
| `AUTH__PASSWORDWORKFACTOR` | Varsayılan bcrypt maliyeti `12`. |
| `ENCRYPTION__ROOTKEYFILE`, `ENCRYPTION__KEYVERSION` | Yerelde `secrets/encryption-root-key`, başlangıç sürümü `1`. |
| `RAWSTORAGE__BASEPATH` | Worker ham veri arşivi; varsayılan `data/providers`. |
| `LOGGING__MINIMUMLEVEL` | Varsayılan `Information`. |
| `FRONTEND_PORT` | Compose Nginx host portu; varsayılan `3000`. |

Anahtarları **yalnızca ilk kurulumda** oluşturun:

```bash
umask 077
mkdir -p secrets
# Var olan dosyaları yanlışlıkla ezmeyi engeller.
(set -o noclobber; openssl rand -base64 32 > secrets/encryption-root-key)
(set -o noclobber; openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 > secrets/jwt-private-key.pem)
chmod 700 secrets
chmod 600 secrets/*
openssl pkey -in secrets/jwt-private-key.pem -check -noout
test "$(openssl base64 -d -A < secrets/encryption-root-key | wc -c)" -eq 32
```

Root key tam 32 rastgele byte’ın Base64 karşılığıdır. JWT anahtarı parolasız RSA PEM olmalıdır; dosya erişimi host izinleri ile korunur. Anahtarları güvenli bir yerde yedekleyin. Şifreleme anahtarını kaybetmek veritabanındaki sağlayıcı secret’larını okunamaz yapar; yeniden üretmek bir kurtarma yöntemi değildir. JWT anahtarını değiştirmek mevcut oturumları geçersiz kılar.

### Compose secret bağlantıları

`docker-compose.yml` içindeki üst seviye `secrets` tanımları host dosyalarını seçer. Yalnızca `api` ve `worker` bu secret’ları salt okunur olarak alır:

| Host dosyası | Container yolu | Uygulama ayarı |
| --- | --- | --- |
| `./secrets/encryption-root-key` | `/run/secrets/threatintel-encryption-root-key` | `ENCRYPTION__ROOTKEYFILE` |
| `./secrets/jwt-private-key.pem` | `/run/secrets/threatintel-jwt-private-key` | `JWT__PRIVATEKEYFILE` |

Worker da ortak yapılandırma doğrulamasında JWT dosyasını okuduğu için iki mount’a ihtiyaç duyar. Frontend’e `.env` veya secret verilmez. `.gitignore` secret’ları Git’ten; kök ve frontend `.dockerignore` dosyaları build context’inden dışlar. Secret’ları Dockerfile `COPY`, build arg veya `VITE_*` değişkeni ile taşımayın: tarayıcıya gönderilen değerler gizli değildir.

Yerel Compose dosya secret’ları host dosyasına dayanır; bir şifreli secret kasası değildir. Host dosyalarını ve Docker daemon erişimini koruyun. Mevcut uyumluluk nedeniyle SQL/RabbitMQ/admin parolaları `.env` üzerinden environment olarak aktarılır; Docker erişimi olan kişiler bunları inceleyebilir. Uygulama bu parolalar için genel bir `_FILE` sözleşmesi desteklemez. Production’da secret yöneticinizle korunan `.env` ve anahtar dosyalarını deployment sırasında hazırlayın; `docker compose config` çıktısını paylaşmayın (parolalar içerebilir), doğrulamada `config --quiet` kullanın.

Sağlayıcı API anahtarlarını giriş yaptıktan sonra Admin → Settings üzerinden girin. Bunlar AES-256-GCM ile şifrelenerek SQL’e yazılır. Geçiş ve rotasyon: [Credential runbook](ProjectDocs/Provider-Credential-Migration-Runbook.md). Opsiyonel OpenAI-compatible servis URL/model/anahtarı da Admin ayarlarından yönetilir; container içindeki `localhost` host makineyi göstermez.

## Tam uygulamayı Docker ile çalıştırma

```bash
docker compose config --quiet
docker compose build
docker compose up -d --wait --wait-timeout 300
docker compose ps
```

İlk başlangıçta API migration’ları uygular ve ilk admin’i oluşturur. Nginx, API sağlıklı olunca başlar. İlk image indirmesi/build internet bağlantısı gerektirir ve birkaç dakika sürebilir.

- Arayüz: [http://localhost:3000](http://localhost:3000); `.env` admin bilgileriyle giriş yapın.
- API: [http://localhost:8080](http://localhost:8080).
- RabbitMQ yönetimi: [http://localhost:15672](http://localhost:15672); `.env` broker bilgilerini kullanın.
- Worker bir kullanıcı arayüzü sunmaz, `8081` yalnızca health erişimi içindir.

Frontend `dist/` çıktısı Nginx image’ına alınır. `/api/*` aynı origin’den API’ye gider, SSE için buffering kapalıdır. Sayfa yenilemelerinde SPA fallback uygulanır; bulunamayan dosyalar 404 döner. HTML yeniden doğrulanır, hash’li `/assets/` dosyaları uzun süre cache’lenir. Kod güncellemesinden sonra `docker compose up -d --build --wait` çalıştırın.

```bash
# Veriyi koruyarak durdurur:
docker compose down
# Loglar:
docker compose logs --tail=100 api worker frontend
```

SQL, RabbitMQ, Worker verileri ve loglar named volume’larda saklanır. `docker compose down -v` bu verileri siler; normal durdurma veya sorun giderme komutu olarak kullanmayın.

## Geliştirme seçenekleri

### Bağımlılıklar Docker’da, uygulama yerelde

Tam stack çalışıyorsa port/iş tüketicisi çakışmasını önleyin:

```bash
docker compose stop frontend api worker
docker compose up -d --wait mssql rabbitmq
dotnet restore
# Terminal 1 (API varsayılan http profili: 5092)
dotnet run --project src/ThreatIntelligence.API
```

API migration’ları tamamlandıktan sonra ikinci terminalde:

```bash
ASPNETCORE_URLS=http://localhost:5093 dotnet run --project src/ThreatIntelligence.Worker
```

Üçüncü terminalde:

```bash
cd frontend
npm ci
npm run dev
```

Arayüz `3000` portundadır; Vite `/api` isteklerini `5092` portuna yollar. Backend `.env` dosyasını üst dizinlerde bulup yükler; göreli secret yolları bu dosyanın dizinine göre çözülür. Shell’de önceden tanımlanmış değişkenler `.env` değerlerinden önceliklidir.

Yerel PDF üretimi için Pandoc ve XeLaTeX/font paketleri de gereklidir; bunlar API Docker image’ında hazırdır. Geliştirme Scalar dokümanı: [http://localhost:5092/scalar/v1](http://localhost:5092/scalar/v1), OpenAPI: `/openapi/v1.json`.

### Backend Docker’da, frontend HMR ile

```bash
docker compose up -d --build --wait api worker
docker compose stop frontend
cd frontend
npm ci
VITE_API_TARGET=http://localhost:8080 npm run dev
```

`VITE_API_TARGET` yalnızca Vite development proxy’sini değiştirir. Production image’ındaki Nginx hedefi Compose ağı içindeki `api:8080` adresidir.

## Production çalıştırma

Tam Docker akışı Release backend ve statik frontend build’i üretir; varsayılan ASP.NET ortamı Production’dır. Scalar/OpenAPI yalnızca Development/Testing ortamlarında açılır. Production için ayrıca:

1. Güvenilir bir sunucuda secret dosyalarını provision edin, `.env` ve volume/anahtar yedeklerini koruyun; anahtarları image’a eklemeyin.
2. Nginx frontend önünde HTTPS sonlandıran reverse proxy/load balancer kullanın. Bu Compose dosyası HTTP sunar, sertifika sağlamaz.
3. Mevcut port uyumluluğu korunmuştur ve portlar tüm host arayüzlerine yayınlanır. İnternete açmadan önce SQL, RabbitMQ, yönetim, API ve Worker portlarını kaldırın veya loopback’e (`127.0.0.1:8080:8080` gibi) bağlayın. Yalnızca HTTPS girişini dışarı açın. Compose override listeleri port ekleyebildiği için son birleşik yapılandırmayı kontrol edin.
4. Mevcut SQL ayarı `sa`, `TrustServerCertificate=True;Encrypt=False` kullanır. Gerçek deployment için yetkileri sınırlı DB hesabı ve doğrulanan TLS bağlantısını deployment yapılandırmasında ayarlayın; Compose’un connection string override’ını da güncelleyin.
5. Image sürümlerini/digest’lerini deployment sürecinizde sabitleyin; migration öncesi DB yedeği alın. İmajı build ettikten sonra `docker compose up -d --wait` ve aşağıdaki smoke kontrollerini çalıştırın.

## Doğrulama ve testler

```bash
docker compose config --quiet
docker compose ps
docker compose exec frontend nginx -t
curl -fsS http://localhost:3000/healthz
curl -fsS http://localhost:8080/health
curl -fsS http://localhost:8081/health
curl -fsS http://localhost:3000/browse > /dev/null
# Beklenen: 404 (HTML fallback değil)
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:3000/assets/missing.js
# Beklenen: 401 (Nginx üzerinden gerçek API yetkilendirmesi)
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:3000/api/admin/summary
# Secret içeriklerini yazdırmadan mount kontrolü:
docker compose exec api sh -c 'test -r "$JWT__PRIVATEKEYFILE" && test -r "$ENCRYPTION__ROOTKEYFILE"'
docker compose exec worker sh -c 'test -r "$JWT__PRIVATEKEYFILE" && test -r "$ENCRYPTION__ROOTKEYFILE"'
```

Tarayıcıda giriş yapın, `/browse` sayfasını doğrudan açıp yenileyin, arama yapın ve Admin ayarlarını kontrol edin. `/health` bağımlılık sağlığını, `/health/live` host canlılığını kontrol eder; frontend `/healthz` yalnızca Nginx’in çalıştığını gösterir.

```bash
dotnet test tests/ThreatIntelligence.UnitTests
# Entegrasyon testleri ayrı veritabanları oluşturup siler; production DB kullanmayın.
docker compose --profile test up -d --wait testsqlserver
dotnet test tests/ThreatIntelligence.IntegrationTests
cd frontend
npm ci
npm test -- --runInBand
npm run lint
npm run build
# İlk kullanımda browser bağımlılıklarını kurun:
npx playwright install --with-deps chromium
npm run test:e2e
```

Test SQL varsayılanı `localhost:1432`, parola `.env` içindeki `TEST_SQLSERVER_PASSWORD` değeridir. Testleri production bağlantısıyla çalıştırmayın.

## Sık karşılaşılan sorunlar

| Belirti | Kontrol / çözüm |
| --- | --- |
| Docker socket permission denied | Daemon ve Docker kullanıcı izinlerini kontrol edin; kurumunuzun Docker erişim politikasını izleyin. |
| JWT dosyası bulunamadı | İki secret dosyasını oluşturun; `docker compose exec ... test -r` kontrollerini çalıştırın. Container içinde host yolu yerine `/run/secrets/...` kullanılır. |
| Encryption unavailable / credential okunamıyor | Base64 anahtarın 32 byte olduğunu, sürümünü ve mevcut veriyi şifreleyen orijinal anahtarı kullandığınızı kontrol edin. Rastgele yeni anahtar üretmeyin. |
| SQL unhealthy / login failed | SQL loglarını, güçlü parola koşullarını ve RAM’i kontrol edin. Kalıcı volume’daki parola `.env` değişince otomatik değişmez; mevcut DB parolasını kullanın veya SQL üzerinden yönetin. |
| RabbitMQ ACCESS_REFUSED | Kalıcı broker’daki kullanıcı/parolayı kontrol edin; başlangıç env değerleri mevcut kullanıcıyı güncellemez. |
| Port already allocated | Çakışan süreci durdurun veya host portunu değiştirin. Frontend için `FRONTEND_PORT` kullanın; HMR’a geçerken Compose frontend’i durdurun. |
| Nginx 502 / API unhealthy | `docker compose logs --tail=100 api frontend` ve API `/health` kontrolü; API başlangıcı, DB migration ve broker bağlantısını inceleyin. |
| HMR arayüzünde API bağlantısı yok | Docker API için `VITE_API_TARGET=http://localhost:8080` ile Vite’ı yeniden başlatın. |
| Docker’da Scalar 404 | Production’da beklenen davranış; yerel Development API’sini kullanın. |
| Admin parolası değişmedi | Seed sadece ilk hesap içindir; mevcut hesabı uygulamanın parola yönetimiyle güncelleyin. |
| PDF üretimi başarısız | Yerelde Pandoc/XeLaTeX kurulumu; Docker’da API logları ve AI runtime ayarlarını kontrol edin. |
| Build sırasında npm/NuGet/image indirme hatası | Registry/ağ/proxy erişimini düzeltip build’i tekrar çalıştırın. |

## Dizinler ve diğer belgeler

- `frontend/`: React, TanStack Router, Vite SPA; Dockerfile ve Nginx yapılandırması.
- `src/ThreatIntelligence.API`, `Worker`: HTTP ve işleyici host’ları.
- `src/ThreatIntelligence.Application`, `Domain`, `Infrastructure`: use case, domain, persistence/adaptörler.
- `tests/`: .NET unit ve entegrasyon testleri.
- `openspec/`, `ProjectDocs/`: gereksinimler ve tasarım/runbook belgeleri.

API kullanım örnekleri ve işlevler için [USAGE.md](USAGE.md), frontend ayrıntıları için [frontend/README.md](frontend/README.md) dosyasına bakın. Kurulum ve Compose için bu README esas alınmalıdır.
