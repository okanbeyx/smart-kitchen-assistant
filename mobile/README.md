# Smart Kitchen Assistant Mobile

Bu klasör Expo SDK, React Native ve TypeScript tabanlı mobil uygulama
foundation'ını içerir. Uygulama Expo Router ve Continuous Native Generation
(CNG) kullanır; üretilen `android/` ve `ios/` klasörleri sürüm kontrolüne
alınmaz.

## Gereksinimler ve kurulum

Node sürümü `.nvmrc` içinde 22.18.0 olarak sabitlenmiştir; `package.json` Node 22
sınırını tanımlar. Dinamik config ortak `.ts` doğrulayıcısını Node ile yükler;
bu nedenle `.nvmrc` sürümünü kullanın. İlk kurulum için:

```powershell
Set-Location .\mobile
npm ci
Copy-Item .env.example .env
```

Yerel `.env` zaten varsa üzerine kopyalamayın. Örnek Auth0 değerlerini kendi
public yapılandırmanızla değiştirin; gerçek Client ID repoya yazılmaz.

Kurulmuş development client için Metro ve platform hedefleri:

```powershell
npm start
npm run android
npm run ios
```

`npm run ios` için macOS ve Xcode gerekir.

## API adresi

Mobil istemci API adresini yalnız `EXPO_PUBLIC_API_BASE_URL` üzerinden okur.
Backend'in default `dotnet run` launch profile'ı `http://localhost:5011`
adresini kullanır. Backend'i repository kökünden, root README'deki SQL Server ve
JWT doğrulama ayarlarını tamamladıktan sonra çalıştırabilirsiniz:

```powershell
dotnet run --project .\backend\src\SmartKitchenAssistant.Api
```

Platforma göre development URL örnekleri:

| Hedef                       | `EXPO_PUBLIC_API_BASE_URL` |
| --------------------------- | -------------------------- |
| Android emulator            | `http://10.0.2.2:5011`     |
| Fiziksel Android            | `http://<LAN-IP>:5011`     |
| Aynı Mac'teki iOS simulator | `http://127.0.0.1:5011`    |
| Production                  | HTTPS deployment URL       |

Fiziksel cihaz erişimi için backend'i kontrollü bir LAN interface'ine bind
etmek ve yerel firewall erişimini ayarlamak gerekir. Tablodaki HTTP adresleri
yalnız tokensız yerel geliştirme içindir. Gerçek Bearer token kullanılan
fiziksel cihaz doğrulamasında cihazın güvendiği HTTPS API adresi gerekir.

`EXPO_PUBLIC_*` değerleri uygulama bundle'ına gömülür ve secret değildir.
Mobil environment dosyalarına veritabanı credential'ı, connection string,
backend signing secret, private API key veya OAuth client secret koymayın.

## Auth0 oturumu — Issue #32, Faz 1–2

Mevcut ASP.NET Core API, harici bir OIDC/OAuth sağlayıcısının Bearer JWT access
token'ını doğrulayan resource server'dır. Login, register, refresh veya logout
endpoint'i üretmez. Sağlayıcı Auth0'dır; development tenant ve Native uygulama
haricen/provider tarafında yönetilir. Bu depo provider kaynaklarını oluşturmaz.

Gerekli public değişkenler:

- `EXPO_PUBLIC_API_BASE_URL`: mevcut API adresi.
- `EXPO_PUBLIC_AUTH0_DOMAIN`: protokol, port veya path içermeyen tenant hostname'i.
- `EXPO_PUBLIC_AUTH0_CLIENT_ID`: public Native uygulamanın Client ID'si.
- `EXPO_PUBLIC_AUTH0_AUDIENCE`: backend ile aynı custom API identifier.

Client Secret mobil uygulamada **asla** kullanılmaz. Tenant, Client ID veya
audience değiştiğinde Metro yeniden başlatılmalı; native callback domain'i
değiştiğinde development build yeniden üretilmelidir. `app.json` statik
ayarların kaynağıdır; `app.config.ts` yalnız environment'dan domain okuyarak
Auth0 plugin'ini ekler. Config değerlendirmesi domain ister; diğer Auth0
değerleri adapter oluşturulduğunda doğrulanır.

`auth0OidcClient.ts` sınıf tabanlı SDK adapter'ıdır. SDK, Authorization Code +
PKCE, state, nonce, callback doğrulaması ve code exchange işlemlerini yönetir.
Başarılı WebAuth sonucu, adapter başarı döndürmeden CredentialsManager'a
kaydedilir. CredentialsManager tek Auth0 kasasıdır; `getCredentials()` yenilemeyi
SDK'ya bırakır. Mevcut genel SecureStore yardımcısına Auth0 verisi yazılmaz.
Backend Bearer JWT beklediğinden SDK'da `useDPoP: false` açıkça seçilmiştir.
Token süreleri ve refresh rotation provider ayarlarıdır; uygulamada sabitlenmez.

React'ten bağımsız `SessionManager` oturum yaşam döngüsünü yönetir; AuthContext
ona abone olur ve yalnız durum, güvenli hata türü ve UI işlemlerini sunar.
Context token taşımaz. Arbitrary string ile giriş sağlayan eski
`completeAuthentication` kaldırılmıştır. Login ekranı Universal Login'i açar;
iptal, genel hata uyarısı göstermez. Giriş sürerken tekrar giriş engellenir.

Başlangıçta `getCredentials()` kullanılabilir, süresi dolmamış bir credential
döndürmeden korunan ekran açılmaz. Restore ve credential bekleme süresi 15
saniyeyle sınırlıdır; bu süre token ömrü değildir. Ağ/provider hatalarında kasa
silinmez, kullanıcı tekrar restore deneyebilir. SDK'nın açık `invalid_grant`,
invalid/no-session sinyalleri merkezi yerel çıkışa gider. Özellikle Android'de
yalnız `RENEW_FAILED` kodu iptal edilmiş refresh token ile geçici hatayı kesin
ayıramaz; belirsiz hata credential silmek için kanıt sayılmaz. Ham SDK hata
metinleri/payload'ları dışarı verilmez.

Her yeni giriş/restore ve yerel çıkış monoton oturum nesli kullanır. Aynı nesil
credential istekleri yalnız devam ederken tek Promise paylaşır; sonuç token
cache'ine alınmaz. Bir tüketicinin iptali diğerlerini iptal etmez. SDK kasasını
etkileyen işlemler sırayla yürür. Logout nesli hemen artırır ve korunan UI'ı
kapatır; devam eden SDK işlemi bittikten sonra kasayı temizler. Geç gelen sonuç
yeniden giriş yaptıramaz. SDK işlemi bekleme süresini aşsa da native iş bitmeden
temizlik tamamlandı sayılmaz ve yeni giriş başlatılmaz.

Home'daki “Bu cihazdan çıkış yap” yerel kasayı ve mevcut QueryClient cache'ini
temizler, aktif sorguları iptal eder. Şimdilik public/user cache ayrımı olmadığı
için tüm query/mutation cache temizlenir; aynı temizlik hesap değişiminde de
uygulanır. Kasa temizliği başarısızsa kullanıcı unauthenticated kalır, güvenli hata
ve temizlik tekrar deneme düğmesi gösterilir; login/restore temizliğe kadar
engellenir. Bu durum başarılı logout olarak sunulmaz.

Browser oturumu ve refresh-token revocation henüz yönetilmez. Bu nedenle yerel
çıkıştan sonraki girişte provider browser oturumu hesabı yeniden seçebilir.
`clearSession()` adapter'da bulunur ancak Faz 2 akışı çağırmaz. HTTP 401 replay,
API bağlantısı ve remote logout/revocation Faz 3 kapsamındadır.

## iOS credential temizliği yaması — Issue #32

`patches/react-native-auth0+5.11.1.patch`, sabitlenmiş Auth0 5.11.1 iOS bridge'ine
uygulanır. Bu sürümün Auth0.swift 2.25.0 / SimpleKeychain 1.3.0 zinciri, boş kayıt
ile gerçek Keychain silme hatasını aynı `false` sonucuna indirger. Bu belirsizlik
ilk açılışta ve logout sonrası tekrar girişte temizliğin kilitlenmesine yol açar.

Yama, CredentialsManager'ın aynı SimpleKeychain alanını kullanan storage
uyarlayıcısında yalnız başarılı silmeyi ve kesin `errSecItemNotFound` durumunu
başarı sayar. `credentialsManager.clear()` çalışmaya devam eder; yardımcı oturum
kayıtlarındaki gerçek silme hataları da bridge'den `STORE_FAILED` olarak çıkar.
`interactionNotAllowed`, `missingEntitlement`, `authFailed` ve bilinmeyen hatalar
başarıya çevrilmez. Uygulama bunları ham native payload olmadan `storage` hatası
olarak sunar. Beklenmeyen `false` sonucu da hata kalır. İkinci kasa veya token
kopyası oluşturulmaz; auth/token yenileme ve nesil sıralaması değiştirilmez.

`patch-package` 8.0.1 geliştirme bağımlılığıdır. Temiz checkout'ta `npm ci` ve
normal `npm install`, `postinstall` üzerinden önce `patch-package --error-on-fail`,
sonra salt okunur doğrulamayı çalıştırır. Yama dosyası kurulum sırasında mevcut
olmalı; build ortamında devDependencies kurulmalıdır. `node_modules` cache anahtarı
lockfile ile yama içeriğini de kapsamalıdır. `--ignore-scripts` kullanıldıysa native
derlemeden önce açıkça `npm run postinstall` çalıştırılmalıdır.

```powershell
npm run postinstall
node scripts/verify-auth0-patch.cjs
```

Doğrulayıcı SDK sürümünü, podspec'teki iki native sürümü, yama dosyasını ve yamalı
Swift kaynağının SHA-256 özetini kontrol eder; eksik/değişmiş yama hata verir.
Windows LF/CRLF farkı dışında kaynak değişiklikleri kabul edilmez. Yama değişirse
doğrulayıcıdaki beklenen özetler ancak diff incelendikten sonra güncellenmelidir.

Resmî Auth0 Expo plugin'i korunur. Prebuild, CocoaPods için yamalı bağımlılık
kaynağını kullanır; generated `ios/` ve `android/` klasörleri Git'e eklenmez.
Bu düzeltme için macOS/Xcode üzerinde **yeni iOS development build** gerekir;
Metro yeniden başlatmak veya OTA JavaScript güncellemesi yeterli değildir.

`auth0Patch` testleri temiz SDK kaynağına gerçek patch-package uygulamasını,
tekrar uygulamayı ve uyumsuz kaynakta başarısızlığı sınar. Adapter/session testleri
native sözleşmeyi durum tutan sahte kasa ile sınar; Swift/Keychain çalıştırmaz.
Native derleme ve cihaz doğrulaması henüz yapılmamıştır. Mac üzerinde boş kasa,
tekrarlanan logout ve gerçek/enjekte edilmiş Keychain hata durumları ayrıca
doğrulanmalıdır.

Yama ancak uyumlu kararlı bir upstream sürüm aynı ayrımı ve cleanup davranışını
sağladığında, native ve yaşam döngüsü regresyonları geçtikten sonra kaldırılabilir.
O zaman yama, doğrulayıcı, kurulum hook'u ve yalnız bu amaçlı bağımlılık birlikte
değerlendirilir; sürüm artışı tek başına kaldırma gerekçesi değildir.

## Expo Go, development build ve CNG

Auth0 native SDK için development build gerekir. Expo Go son OIDC doğrulaması
için geçerli değildir. `react-native-auth0` resmi config plugin'i ve
`expo-dev-client` kullanılır. Yerel Android SDK/JDK hazır olduğunda:

```powershell
npx expo run:android
npx expo start --dev-client
```

İlk komut CNG ile ignored `android/` dosyalarını üretip yerel native derleme
yapar. iOS eşdeğeri `npx expo run:ios` macOS/Xcode gerektirir. Native dosyaları
elle düzenlemeyin veya Git'e eklemeyin. Bu yerel akış için `eas.json`, EAS login,
cloud build veya signing credential oluşturma gerekmez.

Android package ve iOS bundle identifier:
`com.okanbeyx.smartkitchenassistant.dev`.
Genel deep link scheme'i `smartkitchenassistant` korunur. Auth0 plugin/SDK
varsayılan scheme'i `com.okanbeyx.smartkitchenassistant.dev.auth0` kullanır.
Provider callback ve logout allowlist'lerinde domain placeholder'ını değiştirin:

```text
com.okanbeyx.smartkitchenassistant.dev.auth0://<auth0-domain>/android/com.okanbeyx.smartkitchenassistant.dev/callback
com.okanbeyx.smartkitchenassistant.dev.auth0://<auth0-domain>/ios/com.okanbeyx.smartkitchenassistant.dev/callback
```

API/remote logout entegrasyonu sonrasında fiziksel Android doğrulama sırası: development build'i kur,
erişilebilir HTTPS API ve public Auth0 ayarlarını doğrula, sistem browser'ında
login/signup yap, callback ile uygulamaya dön, korunan API isteğini dene,
uygulamayı yeniden başlatıp restore'u kontrol et, logout'u ve provider tarafında
iptal edilen oturumun yenileme sırasındaki davranışını doğrula. Geçerli access
token ile tek istek, refresh revocation doğrulaması sayılmaz. En az bir fiziksel
platform gerekir; ikinci platform mevcut olduğunda ayrıca doğrulanır.

Faz 1–2 Jest testleri SDK/provider sınırlarını mock'lar; kontrollü Promise'lerle
restore/login/logout yarışları, tüketici iptali ve geç sonuçlar doğrulanır.
Bunlar gerçek native redirect, cihaz kasası ve provider davranışının kanıtı
değildir. Fiziksel cihaz testi veya native build yapılmamıştır.

Referans: [Auth0 React Native SDK ve Expo yapılandırması](https://github.com/auth0/react-native-auth0#expo).

## Görsel temel

Renk, spacing, radius, system typography, shadow, layout ve icon boyutları
`src/shared/theme/tokens.ts` içindedir. Ekranlar semantic tokenları ve shared
primitiveleri kullanır; yeni hex renk veya bağımsız font ölçeği eklemeyin.
Canlı `brand` kırmızısı dekoratiftir; beyaz buton metni için erişilebilir
`primary` tonu kullanılır. Yalnız light tema desteklenir.

İkonlar tek Ionicons ailesinden, `@react-native-vector-icons/ionicons` default
dynamic import ile yüklenir. Expo Go için `/static` import veya native font
plugin'i kullanılmaz. Metinler system font kullanır; icon yüklemesi auth veya
splash beklemesine bağlanmaz.

`motion.ts` kısa RN Animated opacity/transform davranışlarını merkezileştirir.
Reduce Motion tercihi okunana kadar, okuma başarısızsa veya tercih açıksa
dekoratif hareket kapalıdır. Tercih canlı takip edilir; açık hale gelince
animasyon durur ve içerik görünür kalır. Bootstrap yalnız auth bootstrap
durumunu gösterir; yapay gecikme veya animasyona bağlı navigation yoktur.

`AppScreen` safe area, scroll ve 520/720 dp maksimum content genişliğini yönetir.
Font scaling açık kalır; cihaz modeli veya sabit ekran ölçüsü varsayılmaz.
Login provider girişini başlatır; Home auth guard arkasında gerçek veri içermeyen
önizlemedir ve yerel çıkış sunar. Görsel kontrol için auth bypass veya preview
route eklemeyin.

## Tasarım ve hareket kararı — Issue #28

Mevcut uygulama yalnız kısa içerik/kart girişleri, buton basma geri bildirimi ve
bootstrap görselindeki dekoratif buhar hareketini içerir. React Native Animated,
bu opacity/transform animasyonlarını `useNativeDriver` ile çalıştırabildiği ve
ek animasyon bağımlılığı gerektirmediği için seçildi. Sistem fontları okunabilir
Türkçe metin sağlar; özel font yükleme ve dağıtım maliyeti bu kapsamda gerekli
görülmedi.

- **Uyumluluk ve performans:** Animated mevcut Expo SDK 57 / React Native
  kurulumunun parçasıdır; native driver mevcut hareketleri her karede JS
  çalıştırmadan yürütür, ancak layout özelliklerini animasyonla yönetmez.
  Reanimated da SDK 57 ile kullanılabilir; ek Reanimated/worklets bağımlılıkları
  karşılığında UI thread üzerinde daha gelişmiş etkileşim ve layout animasyonları
  sunar. Mevcut karmaşıklık için bu ek kapasite gerekli değildir.
- **Gesture ve pişirme etkileşimleri:** Basit durum geçişleri Animated ile
  sürdürülebilir. Gelecekte sürükleme, gesture ile yönetilen bottom-sheet veya
  etkileşimli pişirme adımları daha gelişmiş hareket gerektirirse Reanimated
  yeniden değerlendirilebilir. Bu etkileşimler henüz uygulanmış değildir.
- **Foldable/adaptive layout:** İki araç da responsive yerleşimin veya safe-area
  yönetiminin yerine geçmez. Önce esnek layout korunur; yalnız gerçek bir
  etkileşim veya animasyonlu layout geçişi ihtiyacı doğarsa araç seçimi yenilenir.
  Foldable-specific API bu issue kapsamında yoktur.
- **Bakım ve erişilebilirlik:** Animated mevcut React effect/cleanup ve test
  yaklaşımını korur. Reanimated ek API ve worklet bakımını gerektirir; reduced
  motion seçenekleri sunsa da erişilebilirlik politikası ayrıca doğrulanmalıdır.
  Mevcut Animated davranışı tercihi canlı izler; bilinmeyen/okunamayan tercihte
  hareket kapalıdır, tercih açılınca hareket durur ve içerik görünür kalır.

Aşağıdakiler gelecek feature çalışmaları için rehberdir; Issue #28'de
uygulanmış bileşen veya akış değildir:

- **Success/error:** Kısa, tek seferlik ve metin/ikonla desteklenen geri bildirim;
  yalnız renge veya harekete dayanan anlam, belirgin sarsılma ve konfeti yok.
- **Skeleton/loading:** Yalnız gerçek bekleme sırasında sade placeholder veya
  hafif shimmer; Reduce Motion altında statik karşılık. Döngüler veri geldiğinde
  veya unmount sırasında temizlenir; yapay bekleme süresi eklenmez.
- **Modal/bottom-sheet:** Kısa giriş/çıkış; odak, kapatma ve geri gezinme davranışı
  korunur. Gesture gereksinimi varsa Reanimated değerlendirilir; Reduce Motion
  altında hareket kaldırılır veya sadeleştirilir.
- **Liste ekleme/çıkarma:** Yalnız değişen öğede kısa, sınırlı geçiş; tüm listeyi
  yeniden oynatma, uzun stagger veya işlemi animasyon bitişine bağlama yok.

Hareket kısa, amaçlı ve etkileşimi engellemeyen bir destek olmalıdır; aşırı
sekme veya oyun benzeri davranış kullanılmaz. Dekoratif bootstrap döngüsü yalnız
gerçek auth bootstrap süresince çalışır; navigation animasyonu beklemez.

Teknik referanslar: [React Native Animated](https://reactnative.dev/docs/animations),
[Expo SDK 57 Reanimated](https://docs.expo.dev/versions/v57.0.0/sdk/reanimated/),
[Reanimated erişilebilirlik](https://docs.swmansion.com/react-native-reanimated/docs/guides/accessibility/).

## Doğrulama

```powershell
npm run typecheck
npm run lint
npm run format:check
npm test -- --runInBand
```
