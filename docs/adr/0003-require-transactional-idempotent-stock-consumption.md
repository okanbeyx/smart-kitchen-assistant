# ADR 0003: Require Transactional and Idempotent Stock Consumption

## Status

Accepted

## Context

Pişirme tamamlandıktan sonra kullanıcı stok tüketimini açıkça onaylar. Ağ tekrarları, çift dokunma veya eşzamanlı stok güncellemeleri aynı tüketimin iki kez uygulanmasına, kısmi güncellemeye ya da negatif stoğa neden olabilir.

Bu davranış ürün güvenilirliğinin parçasıdır. Bununla birlikte SQL Server `rowversion` gibi mekanizmalar ve kalıcı consumption tablo şeması domain kavramları değil, implementation seçenekleridir.

## Decision

Stok tüketimi business seviyesinde aşağıdaki garantileri sağlayacaktır:

- Açık kullanıcı onayı olmadan stok azaltılmaz.
- Bir tüketimdeki bütün stok değişiklikleri atomiktir.
- Aynı mantıksal onayın stok üzerindeki etkisi en fazla bir kez gerçekleşir.
- Stok negatif olamaz.
- Yalnız stok sahibi tüketim yapabilir.
- Eşzamanlı değişiklik daha yeni veriyi sessizce ezemez.
- Aynı isteğin idempotent retry'ı ikinci bir stok etkisi oluşturmaz.

Tüketim davranışı Pantry sınırına aittir. Cooking veya başka bir kullanım senaryosu bu davranışı Pantry Application sözleşmesi üzerinden çağırır.

## Consequences

Olumlu sonuçlar:

- Ağ tekrarları çift stok düşümüne yol açmaz.
- Birden fazla ingredient içeren tüketim kısmi uygulanmaz.
- Eşzamanlı değişiklikler kullanıcı verisini sessizce bozmaz.
- Domain garantileri belirli bir veritabanı teknolojisinden bağımsız kalır.

Bedeller:

- İstemci ve sunucu idempotency anahtarı yaşam döngüsünü yönetmelidir.
- Concurrency conflict kullanıcıya anlaşılır biçimde iletilmelidir.
- Tamamlanmış sonucu tekrar üretmek için kalıcı işlem kaydı gerekebilir.
- Transaction ve retry davranışı dikkatli entegrasyon testleri gerektirir.

## Alternatives considered

### Yalnız istemcide çift tıklamayı engellemek

Ağ retry'larını ve birden fazla istemciyi kapsamaz; business garantisi sağlamaz.

### Yalnız transaction kullanmak

Kısmi güncellemeyi önler fakat commit sonrasında tekrarlanan aynı isteğin ikinci kez uygulanmasını tek başına önlemez.

### Yalnız idempotency kaydı kullanmak

Tekrarı engelleyebilir fakat eşzamanlı farklı stok işlemlerinin birbirini sessizce ezmesini veya kısmi güncellemeyi tek başına çözmez.

## Open / deferred points

Aşağıdakiler SQL Server/EF Core implementation adaylarıdır; kesin domain kararı değildir:

- Tek SQL transaction ve kesin transaction isolation seviyesi.
- `(UserId, IdempotencyKey)` unique constraint.
- Aynı anahtarın farklı payload ile kullanımını algılayan request hash.
- SQL Server `rowversion` ile optimistic concurrency.
- EF Core concurrency exception handling ve sınırlı retry politikası.
- Kalıcı `StockConsumption` ve `StockConsumptionItem` tabloları.
- Tamamlanan yanıtın ne kadar süre saklanacağı.
- Tam tükenen UserPantryItem satırının silinmesi veya başka biçimde ele alınması.

Bu candidate extension ilk EF Core migration'ın otomatik kapsamı değildir. Kesin şema ve operasyonel politika sonraki implementation issue'sunda onaylanacaktır.
