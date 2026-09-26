# Backend Architecture

## Amaç ve kapsam

Bu belge, Smart Kitchen Assistant backend'i için Issue #7 kapsamında kabul edilen mimari sınırları tanımlar. Amaç; MVP'nin kiler, tarif uygunluğu ve onaylı stok tüketimi davranışlarını tek deploy edilen, anlaşılır ve büyütülebilir bir yapı içinde geliştirmektir.

Bu belge bir implementation değildir. EF Core kurulumu, SQL Server migration'ları, authentication, endpoint'ler ve fiziksel proje ayrımları sonraki issue'ların kapsamındadır.

## Mimari karar: modüler monolit

Backend modüler monolit olarak geliştirilecektir. Modüller iş yetenekleri ve veri sahipliği bakımından ayrılır; aynı uygulama içinde çalışır ve birlikte deploy edilir.

Bu yaklaşım şu an için uygundur:

- MVP'nin temel akışı Catalog, Recipes ve Pantry arasında tutarlı işlemler gerektirir.
- Ayrı servislerin ağ çağrısı, dağıtık transaction, mesajlaşma ve operasyon maliyetleri için doğrulanmış bir ihtiyaç yoktur.
- Tek deploy, başlangıç ortamını ve hata ayıklamayı sade tutar.
- Açık modül sınırları, ihtiyaç kanıtlanırsa ileride fiziksel ayrıştırmaya izin verir.

### Neden şimdilik tek deploy?

Ürün henüz başlangıç aşamasındadır ve kullanım hacmiyle bağımsız ölçekleme gereksinimleri bilinmemektedir. Tek deploy; tarif uygunluğu ile stok verisinin birlikte geliştirilmesini, test edilmesini ve sürümlenmesini kolaylaştırır. Bu karar, bütün kodun sınırsız biçimde birbirine erişebileceği anlamına gelmez; modül sözleşmeleri ve veri sahipliği kuralları korunur.

### Neden çok sayıda `.csproj` oluşturulmuyor?

API, Application, Domain ve Infrastructure kavramsal sınırları ilk aşamada ayrı assembly'ler olmak zorunda değildir. Yalnızca Clean Architecture görünümü oluşturmak için proje sayısını artırmak:

- build ve dependency yönetimini büyütür,
- küçük değişikliklerin izini zorlaştırır,
- henüz doğrulanmamış sınırları erkenden kalıcılaştırır.

İlk implementation; modül, namespace, klasör ve bağımlılık kurallarıyla başlayabilir. Ayrı assembly ihtiyacı bağımlılık ihlalleri, bağımsız test ihtiyacı veya modülün ayrı yaşam döngüsüyle kanıtlanırsa ayrıca kararlaştırılır.

## İş sınırları

### Catalog

Catalog, kanonik malzeme tanımlarını sahiplenir.

- `Ingredient` kayıtlarının sahibi Catalog'dur.
- Desteklenen Unit kümesi ile ölçüm boyutu ve dönüşüm kuralları Catalog domain bilgisidir.
- Unit, MVP'de yönetilebilir veri veya veritabanı tablosu değildir.
- Başka modüller ingredient tanımını değiştirmez; yalnız kimliği üzerinden referans verir.

### Recipes

Recipes, tarif tanımının ve yayın yaşam döngüsünün sahibidir.

- `Recipe`, `RecipeIngredient` ve `RecipeStep` bu sınıra aittir.
- Tarifin temel porsiyonu, zorunlu/opsiyonel malzemeleri, adımları ve yayın durumu burada yönetilir.
- Ingredient verisi kopyalanmaz; Catalog kimliğine referans verilir.
- Bir tarifin yayına hazır olup olmadığı Recipes domain/application davranışıdır.

### Pantry

Pantry, kullanıcıya ait stok verisinin ve stok mutasyonlarının sahibidir.

- `UserPantryItem` bu sınıra aittir.
- Her okuma ve yazma doğrulanmış kullanıcı kimliğiyle sınırlandırılır.
- Stok düşme davranışı, çağıran kullanım senaryosu Cooking olsa bile Pantry sözleşmesi üzerinden yürür.
- Recipes veya Recommendations, Pantry tablolarını doğrudan değiştiremez.

### Identity/Auth

Identity/Auth MVP için gerekli bir sistem sınırıdır; ancak Issue #7'de ayrı bir domain modeli veya authentication implementation'ı değildir.

- API, doğrulanmış kimliği bir current-user sözleşmesi üzerinden Application katmanına aktaracaktır.
- Route veya request body içindeki `UserId` güvenilir kimlik kaynağı değildir.
- Yerel kullanıcı tablosu, harici sağlayıcı ve session/token stratejisi henüz kararlaştırılmamıştır.
- Auth sağlayıcısına özgü ayrıntılar Domain katmanına taşınmamalıdır.

### Cooking

Cooking şu aşamada ayrı bir kalıcı backend modülü değildir.

- Aktif adım ve timer state'inin client-side, server-side veya hibrit tutulacağı henüz kesin değildir.
- Client-side state olası ve küçük bir MVP çözümüdür, fakat mimari karar olarak kabul edilmemiştir.
- Issue #7 kapsamında `CookingSession`, timer veya server-side state tablosu tanımlanmaz.
- Açık kullanıcı onayından sonraki stok tüketimi Pantry sınırında kalır.
- Cihazlar arası devam, kalıcı pişirme geçmişi veya sunucu tarafı oturum gereksinimi oluşursa Cooking sınırı yeniden değerlendirilir.

### Recommendations

Recommendations, MVP'de bağımsız bir domain veya ML servisi değildir.

- Recipes ve Pantry verilerini birleştiren bir Application sorgu yeteneğidir.
- Uygunluk doğruluğunun sahibi değildir; deterministic suitability kuralları Domain/Application katmanında korunur.
- Gelecekteki ML çalışması sıralama veya keşif davranışına katkı sağlayabilir, ancak güvenilir miktar karşılaştırmasının yerine geçmez.

## Katman sorumlulukları

### API

- HTTP endpoint'lerini ve taşıma modellerini tanımlar.
- Authentication/authorization entegrasyonunu gerçekleştirir.
- Doğrulanmış kullanıcı bağlamını Application katmanına iletir.
- Validation ve domain hatalarını güvenli HTTP yanıtlarına dönüştürür.
- Dahili exception, SQL veya gizli yapılandırma ayrıntılarını istemciye sızdırmaz.
- İş kuralı ve doğrudan veri erişim sorgusu içermez.

### Application

- Kullanım senaryolarını koordine eder.
- Kullanıcı sahipliği kapsamını uygular.
- Transaction sınırlarını ve modüller arası çağrıları yönetir.
- Persistence, current user ve zaman gibi dış yetenekler için portlar tanımlar.
- Domain davranışlarını çağırır; kuralları taşıma katmanında çoğaltmaz.

### Domain

- Ingredient, Recipe ve Pantry iş kurallarını ifade eder.
- Unit, ölçüm boyutu, miktar normalizasyonu ve uygunluk sınıflandırmasını taşır.
- Stok için negatif olmama, açık onay ve eşzamanlı değişikliğin sessizce ezilmemesi gibi iş invariants'larını tanımlar.
- ASP.NET Core, EF Core veya SQL Server'a bağımlı olmaz.

### Infrastructure

- EF Core mapping ve SQL Server erişimini uygular.
- Transaction, optimistic concurrency ve kalıcı idempotency mekanizmalarını sağlar.
- Authentication sağlayıcısı, saat ve diğer dış sistem adaptörlerini içerir.
- Application tarafından tanımlanan portları uygular.
- Domain davranışlarını altyapıya özel ikinci bir kural seti olarak yeniden yazmaz.

## Bağımlılık yönü

Kavramsal bağımlılık yönü şöyledir:

```text
API -> Application -> Domain
API -> Infrastructure (composition/configuration)
Infrastructure -> Application ports
Infrastructure -> Domain types
```

Domain dış katmanları bilmez. Application, Infrastructure implementation'larını değil kendi portlarını bilir. API composition root, somut Infrastructure implementation'larını kaydeder.

Modüller arası etkileşimlerde:

- başka modülün internal domain nesnesine veya tablosuna doğrudan yazılmaz,
- komut davranışları ilgili modülün Application sözleşmesi üzerinden çağrılır,
- read-only birleşik sorgular için açık query sözleşmeleri kullanılabilir,
- ortak alana yalnız gerçekten paylaşılan küçük primitive/value object'ler alınır; genel amaçlı bir “shared” katman büyütülmez.

## Veri sahipliği kuralları

- Catalog, Ingredient tanımının tek sahibidir.
- Recipes, tarif aggregate'lerini ve yayın durumunu sahiplenir.
- Pantry, kullanıcı stoğunun ve stok tüketimi iş davranışının tek sahibidir.
- Bir modül başka modülün verisini kimlik üzerinden referanslayabilir fakat o verinin yaşam döngüsünü üstlenemez.
- Kullanıcıya ait veri her sorguda doğrulanmış kullanıcı kimliğiyle sınırlandırılır.
- Fiziksel veritabanı schema'ları ve DbContext sayısı bu belgede kesinleştirilmemiş implementation kararlarıdır.

## MVP kapsamında yapılmayan ayrıştırmalar

- Mikroservislere bölme.
- Catalog, Recipes veya Pantry için ayrı deploy üretme.
- İhtiyacı kanıtlanmamış mesaj kuyruğu veya dağıtık event altyapısı.
- Ayrı Recommendations/ML servisi.
- Server-side Cooking session modeli.
- Stok partileri ve son tüketim tarihi alt modeli.
- Sırf katman adlarını fiziksel hale getirmek için çoklu `.csproj` yapısı.

## Karar sınıflandırması

### Kesin kararlar

- Backend modüler monolit ve başlangıçta tek deploy olacaktır.
- İlk gerçek iş sınırları Catalog, Recipes ve Pantry'dir.
- Deterministic suitability davranışı ML/Recommendations katmanına devredilmeyecektir.
- Stok mutasyonları Pantry sınırından geçecektir.
- Domain, ASP.NET Core, EF Core ve SQL Server'dan bağımsız tutulacaktır.

### Açık kararlar

- Authentication/session sağlayıcısı ve kullanıcı kimliği stratejisi.
- Cooking state'inin client-side, server-side veya hibrit tutulması.
- Gelecekteki ML öneri motorunun kesin sorumluluk sınırı.
- Stok partileri ve son tüketim tarihi modelinin hangi sürümde ekleneceği.

### Implementation'a bırakılan kararlar

- Modül ve katmanların kesin klasör/namespace düzeni.
- Ayrı `.csproj` veya assembly ihtiyacı.
- DbContext sayısı ve SQL schema düzeni.
- Modüller arası read model/query uygulamasının fiziksel biçimi.

## İlgili belgeler

- [MVP ürün brifi](product-brief.md)
- [İlk veri modeli](data-model.md)
- [ADR 0001: Modular monolith](adr/0001-use-modular-monolith.md)
- [ADR 0002: Normalized quantity](adr/0002-use-normalized-quantity-as-source-of-truth.md)
- [ADR 0003: Transactional ve idempotent stok tüketimi](adr/0003-require-transactional-idempotent-stock-consumption.md)
