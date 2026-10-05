# Development SQL Server ve deterministic seed — Issue #30

Bu akış yalnız yerel geliştirme içindir. Docker Compose kalıcı SQL Server çalıştırır;
`SmartKitchenAssistant.DevDb` console aracı migration, seed ve reset işlemlerini
açık komutlarla yürütür. API başlangıcı migration veya seed çalıştırmaz.
Testcontainers kendi geçici SQL database'lerini kullanmaya devam eder.

## Gereksinimler

- .NET SDK 10; repo-local EF tool sürümü `10.0.12`.
- PowerShell 7+ ve Docker Compose v2 (`up --wait` desteği).
- Docker Desktop'ın Linux container backend'i ve x86-64 ortamı; SQL Server için
  yeterli RAM/disk. ARM emülasyonu bu akışın destek varsayımı değildir.
- `127.0.0.1:14330` portu boş olmalı.

Compose, SQL Server 2022 CU27 Developer image'ını Microsoft registry digest'iyle
sabitler. SQL hostname `ska-development-sql`, database adı
`SmartKitchenAssistantDevelopment` ve bağlantı hedefi `tcp:127.0.0.1,14330` sabittir.
`ACCEPT_EULA=Y` ile Microsoft SQL Server lisans koşulları kabul edilir; Developer
edition yalnız geliştirme/test içindir. Named volume `/var/opt/mssql` verisini saklar.

## Build ve yalnız Docker gerektirmeyen kontroller

Komutları repository kökünden çalıştırın. Her komut başarılı olmadan sonrakine
geçmeyin. Native komutların sıfır dışı exit code'u hata demektir.

```powershell
Set-Location C:\Projects\smart-kitchen-assistant
dotnet tool restore
dotnet restore .\backend\SmartKitchenAssistant.sln
dotnet build .\backend\SmartKitchenAssistant.sln --no-restore
dotnet format .\backend\SmartKitchenAssistant.sln --no-restore --verify-no-changes
dotnet test .\backend\tests\SmartKitchenAssistant.Api.Tests --no-build --no-restore --filter "FullyQualifiedName~DevelopmentDatabaseGuardTests"
```

SQL integration testlerini çalıştırmak Docker container'ları ve geçici database'ler
oluşturur. Bunlar yukarıdaki guard testinden ayrı bir doğrulama aşamasıdır.

## Yerel configuration ve secrets

Her development terminalinde ortamı açıkça ayarlayın:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
```

İlk kurulumda:

```powershell
.\scripts\dev-database.ps1 -Action Configure
```

Configure gizli parola istemi açar. 16–128 karakter, büyük/küçük harf, rakam ve
sembol gerekir. Desteklenen semboller: `!@#%^&*()_+=.,:;?/-`. Tırnak, `$`, boşluk ve
backslash kabul edilmez; bu kısıt `.env` quoting/interpolation belirsizliğini önler.
Parola terminale veya process argümanlarına yazılmaz.

Komut iki yerel hedefi hazırlar:

- Ignore edilen `infra/development/sqlserver/.env`: `MSSQL_SA_PASSWORD`.
- API'nin mevcut User Secrets store'u: `ConnectionStrings:SmartKitchen`.

Connection string, `sa`, `Encrypt=True`, `TrustServerCertificate=True` ve
`Persist Security Info=False` kullanır. Self-signed certificate güveni ve yüksek
yetkili SA hesabı yalnız bu izole development instance içindir.

`.env.example` sadece örnektir; gerçek `.env` dosyasını Configure oluşturur.
Mevcut `.env` veya SmartKitchen secret'ı varsa Configure üzerine yazmayı reddeder.
Bu komut parola rotation'ı değildir: persistent volume'daki SA parolası yalnız
environment değiştirerek güncellenmez. Yarım kalan Configure işleminde secret veya
`.env` tek başına oluşmuş olabilir; yeniden çalıştırmadan önce yerel ayarları
inceleyin. Araç bunları otomatik silmez veya düzeltmez.

User Secrets şifreli vault değildir. Gerçek `.env`, connection string, parola,
dump ve secret içerebilecek Docker config/inspect çıktıları commit edilmemelidir.
Script User Secrets JSON'unu stdin ile aktarır; diğer secret anahtarlarını korur.

API normal configuration sırasını korur; environment değişkenleri User Secrets'ı
override edebilir. Development araçları bu belirsizliği reddeder:
`ConnectionStrings__SmartKitchen`, `ConnectionStrings:SmartKitchen`,
`SQLCONNSTR_SmartKitchen`, `SQLAZURECONNSTR_SmartKitchen` ve
`CUSTOMCONNSTR_SmartKitchen` bu shell'de bulunmamalı. Script ayrıca ambient
`MSSQL_SA_PASSWORD` override'ını reddeder. API'yi aynı temiz shell ayarlarıyla başlatın.

Eksik configuration durumunda araç ve mevcut API fail-fast davranır; başka bir
database'e fallback yapılmaz. Console aracı yalnız API User Secrets'ını okur.

## SQL runtime, migration ve seed

Docker Desktop'ın Linux backend'i hazır olduktan sonra:

```powershell
.\scripts\dev-database.ps1 -Action Up
.\scripts\dev-database.ps1 -Action Status
.\scripts\dev-database.ps1 -Action Migrate
.\scripts\dev-database.ps1 -Action Seed
.\scripts\dev-database.ps1 -Action Seed
.\scripts\dev-database.ps1 -Action Verify
```

Up yalnız container'ı başlatır ve healthcheck bekler; database migration/seed yapmaz.
Status SQL'e bağlanır, target/marker ve pending migration durumunu okur; ilk
Migrate öncesinde database'in henüz olmadığını bildirmesi normaldir.

Migrate yalnız sabit instance'ta olmayan development database'ini oluşturur,
development marker'ı ekler ve mevcut üç EF migration'ını `MigrateAsync` ile uygular.
`EnsureCreated` kullanılmaz. Model/snapshot drift'i veya tanınmayan migration
history varsa durur; historical migration'lar değiştirilmez ve otomatik yeni
migration üretilmez. Seed için bütün migration'ların uygulanmış olması gerekir.

İlk Seed canonical graph'ı oluşturur; ikinci Seed doğru graph için no-op olmalıdır.
Verify, hedef/marker/schema ve canonical seed içeriğini doğrular; ayrıca gerçek
Draft fixture ID'sini bildirir. Status tek başına seed doğruluğu kanıtı değildir.

## Canonical veriler ve sahiplik

Seed, altı aktif Ingredient (Domates, Yumurta, Mercimek, Süt, Zeytinyağı, Tuz), bir
pasif Ingredient, iki Published Recipe ve bir Draft Recipe oluşturur. İki Published
tarifte sıralı ingredient ve step verileri bulunur. Tarif metinleri bu proje için
yazılmış development fixture'larıdır; kullanıcı verisi veya production katalog
ithalatı değildir. Pantry, kullanıcı ve consumption kaydı eklenmez.

`[SKA-DEV-SEED]` namespace'i development fixture'larına ayrılmıştır. İsimleri UI'da
bu önekle görmek beklenen davranıştır. Önekin büyük/küçük harf varyantları da
çakışma sayılır; canonical alanlar ordinal karşılaştırılır.

Sahiplik isimlerden çıkarılmaz. Database-level
`SmartKitchenAssistant.DevelopmentSeed` extended property içinde sürümlü JSON
manifest, logical fixture key'lerini o database'deki gerçek identity ID'lerine
bağlar. Ingredient, Recipe, RecipeIngredient ve RecipeStep kimlikleri kaydedilir.
Sabit ID varsayımı yoktur. Manifest 7.500 byte sınırına sığmalıdır; boyut aşımında
yazma yapılmaz. Yeni tablo, kolon veya EF migration eklenmez.

- Manifest yok ve reserved namespace boşsa ilk seed yapılabilir; unrelated kayıtlar
  bu işlemi engellemez.
- Manifest yokken reserved kayıt varsa otomatik adoption yapılmaz.
- Manifest ve bütün canonical alan/ilişkiler doğruysa Seed hiçbir yazma yapmaz.
- Silinmiş, yeniden adlandırılmış, duplicate veya değiştirilmiş seed kaydı;
  bozuk/uyumsuz manifest veya seed Recipe'ye eklenmiş adım drift hatası üretir.
- Normal `Domates` gibi isimlerde unrelated kayıtlar ve duplicate normal isimler
  serbesttir. Unrelated Recipe'ler seed Ingredient'larına referans verebilir.
- Seed unrelated veriyi güncellemez/silmez. Reserved namespace dışındaki farklı
  adlı bir kopyanın geçmişini tespit etmeye çalışmaz.

Dataset güncellemesi otomatik upgrade/repair yapmaz. Mevcut seed drift'inde önce
veriyi inceleyin; tüm development verisini kaybetmeyi kabul etmeden Reset kullanmayın.

## Transaction ve Published fixture sınırı

Seed mevcut domain constructor'larını ve UnitConversions'ı kullanır. Miktarlar
`decimal(18,6)` ve temel g/ml/adet semantiğini korur: `0.25 kg -> 250 g`,
`0.5 l -> 500 ml`, `2 adet -> 2`. Display quantity ayrıca persist edilmez.

Fixture graph'ı önce Draft olarak oluşturulur. Yalnız aynı seed transaction'ında
yeni oluşturulan Published-fixture ID'leri, `Status == Draft` koşuluyla dar bir
`ExecuteUpdateAsync` işleminde Published yapılır. Etkilenen satır sayısı doğrulanır,
son kontrol fresh/no-tracking sorguyla yapılır. Production Recipe domain'i veya
publishing API'si eklenmez; reflection/private setter müdahalesi yoktur.

Graph, durum güncellemesi ve manifest aynı kısa Serializable transaction içindedir.
Eşzamanlı komutlar conflict/deadlock alabilir; başarısız işlem rollback olur. Otomatik
retry veya application-lock altyapısı yoktur. Conflict sonrasında Verify/Seed
yeniden açıkça çalıştırılabilir. Migration'lar seed transaction'ına sarılmaz.

## Reset, kalıcılık ve güvenlik sınırı

Reset **bütün development database'ini**, unrelated developer verileri dahil siler.
Önce API terminalinde `Ctrl+C` kullanın, sonra:

```powershell
.\scripts\dev-database.ps1 -Action Reset -ConfirmDatabase SmartKitchenAssistantDevelopment
.\scripts\dev-database.ps1 -Action Verify
```

Reset tam ad confirmation'ı, sabit loopback hedefi, SQL hostname ve
`SmartKitchenAssistant.DevelopmentDatabase = issue-30:v1` database marker'ını
zorunlu tutar. Mevcut markerless/unknown database hiçbir komutta benimsenmez veya
silinmez. Marker yalnız başarılı ilk CREATE sonrasında yazılır; marker yazımı
başarısız kaldıysa sonraki komut otomatik adoption yapmaz.

Bağlantılar zorla kapatılmaz. Database kullanımda ise DROP başarısız olur; API ve
SQL istemcilerini kapatıp durumu inceleyin. Reset -> create -> migrate -> seed
tek atomik işlem değildir; bir aşama başarısız olursa sonraki aşamaya geçilmez.
Database silindikten sonra tekrar oluşturma başarısız olduysa önce Status okuyun;
database yoksa Migrate/Seed adımlarıyla açıkça devam edin.

Normal kapatma/açma veriyi korur:

```powershell
.\scripts\dev-database.ps1 -Action Down
.\scripts\dev-database.ps1 -Action Up
.\scripts\dev-database.ps1 -Action Verify
```

Up/Down yalnız local named-pipe/Unix-socket Docker context kabul eder; `DOCKER_HOST`
override'ı reddedilir. Compose project ve dosyası sabittir. `down -v`, prune,
forced disconnect, arbitrary target veya guard bypass seçeneği yoktur. Marker ve
hostname kazara yanlış hedeflemeyi azaltır; SQL/Docker admin'inin bilinçli müdahalesine
karşı güvenlik sınırı değildir.

## API smoke

Mevcut HTTPS JWT Authority/Audience zorunluluğu korunur. Gerçek development issuer
ayarlarınız varsa onları User Secrets'ta kullanın. Sağlayıcı henüz yoksa yalnız
anonim smoke için açıkça işlevsiz configuration kullanılabilir:

```powershell
dotnet user-secrets set "Authentication:Jwt:Authority" "https://issuer.example.invalid" --project .\backend\src\SmartKitchenAssistant.Api
dotnet user-secrets set "Authentication:Jwt:Audience" "smart-kitchen-assistant-development" --project .\backend\src\SmartKitchenAssistant.Api
dotnet run --project .\backend\src\SmartKitchenAssistant.Api --no-build --no-launch-profile --urls http://127.0.0.1:5011
```

Bu configuration token üretmez, JWT doğrulamasını bypass etmez ve gerçek OIDC testi
sayılmaz. Gerçek ayarları bu örnekle üzerine yazmayın. TestAuthenticationHandler
development runtime'ına bağlanmaz.

İkinci Development terminalinde Verify'ın bildirdiği gerçek Draft ID'yi kullanın:

```powershell
.\scripts\dev-database.ps1 -Action Verify
$draftFixtureId = [long](Read-Host 'Verify çıktısındaki Draft fixture ID')
.\scripts\smoke-development-api.ps1 -DraftRecipeId $draftFixtureId
```

Smoke; `/health`, ingredient list/search, Published recipe list/detail,
quantity/unit dönüşümleri, Draft 404 ve tokensız Pantry/suitability 401 kontrollerini
yapar. Canonical kayıtları mevcut catalog içinde arar; unrelated kayıt sayısını
kısıtlamaz. `/health` yalnız liveness'tır; SQL erişiminin kanıtı gerçek catalog
sorguları ve Verify'dır. Protected endpoint'lerin başarılı gerçek-token testi
gelecekteki OIDC/OAuth çalışmasına kalır.

## SQL-backed testler ve son inceleme

Docker hazır olduğunda, ayrı Testcontainers database'lerinde:

```powershell
dotnet test .\backend\tests\SmartKitchenAssistant.Api.Tests --no-build --no-restore --filter "FullyQualifiedName~DevelopmentDatabase"
dotnet test .\backend\SmartKitchenAssistant.sln --no-build --no-restore
git diff --check
git status --short
git ls-files --others --exclude-standard
git check-ignore infra/development/sqlserver/.env
git ls-files infra/development/sqlserver/.env
```

Son `git ls-files` komutu çıktı vermemelidir. SQL testleri seed/metadata rollback,
idempotency, drift, unrelated veri koruması ve farklı identity değerleriyle recreate
akışını doğrular. Gerçek development CLI Reset/aktif bağlantı davranışı ayrıca
yukarıdaki manual runtime akışında doğrulanmalıdır; testlere arbitrary CLI target
veya guard bypass eklenmemiştir.

Production/cloud provisioning, auth implementasyonu, mobile feature'lar, admin UI,
Cooking flow, ML, deployment/monitoring/backups ve şema redesign bu issue dışında.
