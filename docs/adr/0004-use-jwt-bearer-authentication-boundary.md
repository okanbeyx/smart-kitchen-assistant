# ADR 0004: Use a JWT Bearer Authentication Boundary

## Status

Accepted

## Context

Smart Kitchen Assistant'ın kullanıcıya ait Pantry verisini güvenilir biçimde sınırlandırabilmesi için API'nin doğrulanmış bir kullanıcı kimliğine ihtiyacı vardır. React Native istemci hedeflenmektedir; ancak identity provider, Google/Apple login akışı ve yerel kullanıcı profili henüz seçilmemiştir.

Route veya request body içindeki `UserId` güvenilir değildir. Domain ve Application katmanları ASP.NET Core, JWT ya da provider-specific claim tiplerini bilmemelidir. API'nin kendi password, refresh token veya token issuance sistemini kurması MVP için gereksiz güvenlik ve operasyon sorumluluğu oluşturur.

## Decision

API, JWT bearer access token doğrulayan bir resource server olacaktır. Token üretme ve kullanıcı login akışı API'nin sorumluluğu değildir; ileride seçilecek OIDC/OAuth identity provider bu sorumluluğu üstlenecektir.

JWT doğrulaması aşağıdaki garantileri sağlayacaktır:

- Tek bir HTTPS Authority ve API Audience zorunludur.
- Signature, issuer, audience ve token lifetime doğrulanır.
- Inbound claim mapping kapatılır; raw `sub` claim'i kullanılır.
- API tokenı saklamaz ve doğrulama hata ayrıntılarını response'a eklemez.
- Korumalı endpoint'ler authenticated ve geçerli user-id sözleşmesini sağlayan principal ister.
- Geçerli user-id; tam olarak bir adet, boş olmayan ve en fazla 256 karakterlik `sub` değeridir.
- `sub` trim edilmez, case değiştirilmez ve normalize edilmez.
- Application yalnız `ICurrentUser.UserId` sözleşmesini görür.
- `/health` açıkça anonymous kalır; diğer endpoint'ler fallback policy ile varsayılan olarak korunur.

Bu karar başlangıçta tek güvenilir issuer varsayar. `sub` değeri issuer kapsamında kararlı ve benzersiz opaque kullanıcı kimliği olarak saklanır.

## Consequences

Olumlu sonuçlar:

- React Native istemci standart bearer access token kullanabilir.
- API stateless kalabilir; auth session tablosu gerekmez.
- Password ve token issuance sorumluluğu backend'e taşınmaz.
- Application ve Domain provider/JWT tiplerinden bağımsız kalır.
- Yeni endpoint'ler varsayılan olarak authentication ister.
- Test authentication production JWT doğrulamasından ayrı tutulabilir.

Bedeller:

- Gerçek authenticated kullanım için bir identity provider, Authority ve Audience yapılandırması gerekir.
- Mobile login ve token yenileme akışı ayrı implementation gerektirir.
- Birden fazla issuer desteklenirse raw `sub` tek başına global olarak benzersiz olmayabilir.
- Token revocation ve provider outage davranışı seçilen sağlayıcının özelliklerine bağlıdır.

## Alternatives considered

### Cookie/session authentication

Browser tabanlı uygulamalar için uygundur; ancak React Native-first API'de cookie saklama, CSRF ve session yaşam döngüsü ek karmaşıklık getirir. Bu nedenle seçilmedi.

### API'nin kendi JWT tokenlarını üretmesi

Password saklama, login, refresh token, recovery, signing-key rotation ve token revocation sorumluluklarını API'ye yükler. MVP için YAGNI nedeniyle reddedildi.

### API key veya development header

Son kullanıcı kimliği, expiration ve güvenilir issuer doğrulaması sağlamaz. Yalnız test assembly'sindeki kontrollü authentication handler test amacıyla kullanılabilir; production alternatifi değildir.

### Provider-specific SDK veya claim'leri Application'a taşımak

Vendor lock-in oluşturur ve katman sınırlarını bozar. Provider adaptation Infrastructure authentication sınırında kalmalıdır.

## Open / deferred points

- OIDC/OAuth identity provider seçimi.
- React Native authorization-code + PKCE login akışı.
- Google/Apple hesaplarının provider üzerinden bağlanması.
- Token yenileme ve logout davranışı.
- Birden fazla issuer gerekirse canonical kullanıcı kimliği.
- Yerel kullanıcı profiline gerçekten ihtiyaç olup olmadığı.
