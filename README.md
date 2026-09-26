# Akıllı Mutfak Asistanı

Akıllı Mutfak Asistanı, kullanıcının evindeki malzemeleri ve miktarlarını takip ederek yapılabilecek tarifleri anlaşılır biçimde önermeyi amaçlayan bağımsız bir mobil uygulama projesidir.

MVP, küçük ve çalışan parçalar halinde geliştirilecektir. İlk hedef; kiler yönetimi, miktar farkındalıklı tarif uygunluğu ve standart pişirme akışıdır. KNN ve K-Means deneyleri daha sonraki aşamalarda Google Colab ortamında ele alınacak, Flex-Mode ise standart pişirme ekranı tamamlandıktan sonra uygulanacaktır.

## Planlanan teknoloji yığını

- Mobil uygulama: React Native ve TypeScript
- Backend: ASP.NET Core Web API
- Veri erişimi: Entity Framework Core
- Veritabanı: Microsoft SQL Server
- Mimari: Modüler monolit
- Algoritma deneyleri: Python ve Google Colab
- Kalite: Otomatik testler ve GitHub Actions

## Mevcut durum

Proje başlangıç ve planlama aşamasındadır. Minimal ASP.NET Core backend iskeleti ve health-check endpoint'i oluşturulmuştur; ürün özellikleri, mobil uygulama ve veritabanı henüz geliştirilmemiştir. Kurulum ve uygulama adımları ayrı GitHub issue'ları ve küçük pull request'ler halinde ilerletilecektir.

## Dokümantasyon ve mimari

- [MVP ürün brifi](docs/product-brief.md)
- [Backend mimarisi](docs/backend-architecture.md)
- [İlk domain ve veri modeli](docs/data-model.md)
- [ADR 0001: Modular monolith](docs/adr/0001-use-modular-monolith.md)
- [ADR 0002: Normalized quantity tek doğruluk kaynağı](docs/adr/0002-use-normalized-quantity-as-source-of-truth.md)
- [ADR 0003: Transactional ve idempotent stok tüketimi](docs/adr/0003-require-transactional-idempotent-stock-consumption.md)

## Backend geliştirme

### Gereksinim ve proje konumları

Backend, .NET 10 (`net10.0`) hedefler. Aşağıdaki komutlar .NET SDK `10.0.401` ile doğrulanmıştır.

- Solution: `backend/SmartKitchenAssistant.sln`
- API projesi: `backend/src/SmartKitchenAssistant.Api/SmartKitchenAssistant.Api.csproj`
- Test projesi: `backend/tests/SmartKitchenAssistant.Api.Tests/SmartKitchenAssistant.Api.Tests.csproj`

### Restore, build ve test

Aşağıdaki komutları depo kökünden çalıştırın:

```powershell
dotnet restore .\backend\SmartKitchenAssistant.sln
dotnet build .\backend\SmartKitchenAssistant.sln --no-restore
dotnet test .\backend\SmartKitchenAssistant.sln --no-build --no-restore
```

### API'yi çalıştırma

Başarılı bir build işleminden sonra, depo kökünden API klasörüne geçip uygulamayı çalıştırın:

```powershell
Set-Location .\backend\src\SmartKitchenAssistant.Api
dotnet run --no-build --urls http://127.0.0.1:5187
```

Bu HTTP adresi yalnızca yerel geliştirme ve doğrulama içindir. Üretim ortamında uygun HTTPS/TLS yapılandırması kullanılmalıdır.

### Health check

API çalışırken ayrı bir PowerShell terminalinden health-check endpoint'ine istek gönderin:

```powershell
Invoke-WebRequest -Uri 'http://127.0.0.1:5187/health' -Method Get
```

`GET /health`, sağlıklı durumda HTTP `200 OK` ve `Healthy` yanıtı döndürür. Uygulamayı durdurmak için API'nin çalıştığı terminalde `Ctrl+C` kullanın.
