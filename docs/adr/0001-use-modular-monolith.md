# ADR 0001: Use a Modular Monolith

## Status

Accepted

## Context

Smart Kitchen Assistant MVP; ingredient kataloğu, tarifler, kullanıcı kileri, deterministic tarif uygunluğu ve açık onaylı stok tüketimi arasında yakın koordinasyon gerektirir. Ürünün trafik hacmi, bağımsız ölçekleme gereksinimi ve operasyonel sınırları henüz doğrulanmamıştır.

Mikroservisler veya erken fiziksel proje ayrımları; dağıtık transaction, ağ hataları, ayrı deployment ve ek dependency yönetimi getirir. Buna karşılık sınırsız tek parça kod tabanı da veri sahipliğini ve iş sınırlarını belirsizleştirir.

## Decision

Backend modüler monolit olarak ve başlangıçta tek deploy halinde geliştirilecektir.

İlk gerçek iş sınırları:

- Catalog
- Recipes
- Pantry

Identity/Auth bir entegrasyon sınırıdır; sağlayıcısı henüz seçilmemiştir. Cooking kalıcı backend modülü değildir ve state persistence konumu ertelenmiştir. Recommendations, deterministic suitability doğruluğunun sahibi olmayan bir Application sorgu yeteneğidir.

API, Application, Domain ve Infrastructure kavramsal katmanları korunacaktır. Başlangıçta bu katmanları temsil etmek için zorunlu olarak ayrı `.csproj` oluşturulmayacaktır. Modül veri sahipliği ve bağımlılık yönü namespace, klasör, sözleşme ve code review kurallarıyla korunacaktır.

## Consequences

Olumlu sonuçlar:

- MVP tek süreçte geliştirilir, test edilir ve deploy edilir.
- Tarif uygunluğu ile stok işlemleri dağıtık transaction gerektirmez.
- Modül sahipliği gelecekteki ayrıştırma için açık kalır.
- Gereksiz operasyon ve proje yönetimi maliyeti oluşmaz.

Bedeller:

- Modül sınırlarını korumak için disiplin ve otomatik mimari testler gerekebilir.
- Tek deploy içindeki bir hata bütün backend sürümünü etkileyebilir.
- Bağımsız ölçekleme ihtiyacı çıkarsa sonraki bir ayrıştırma çalışması gerekir.

## Alternatives considered

### Mikroservisler

Bağımsız ölçekleme ve deploy sağlayabilir; ancak MVP için doğrulanmış ihtiyaç yoktur ve dağıtık sistem maliyeti erkendir.

### Katmansız tek proje

Başlangıçta basittir; fakat veri sahipliği ve bağımlılık sınırları hızla belirsizleşebilir.

### Her katman ve modül için ayrı `.csproj`

Compile-time sınırlar sağlayabilir; ancak mevcut kod hacminde gereksiz proje ve dependency yönetimi oluşturur. İhtiyaç kanıtlanırsa daha sonra uygulanabilir.

## Open / deferred points

- Authentication/session sağlayıcısı.
- Cooking state'inin client-side, server-side veya hibrit tutulması.
- Modül ve katmanların ileride ayrı assembly'lere ayrılması.
- DbContext sayısı ve SQL schema düzeni.
- Bağımsız deploy veya mikroservise geçişi gerektirecek ölçütler.
