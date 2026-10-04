# Smart Kitchen Assistant Mobile

Bu klasör Expo SDK, React Native ve TypeScript tabanlı mobil uygulama
foundation'ını içerir. Uygulama Expo Router ve Continuous Native Generation
(CNG) kullanır; üretilen `android/` ve `ios/` klasörleri sürüm kontrolüne
alınmaz.

## Gereksinimler ve kurulum

Node sürümü `.nvmrc` ve `package.json` içinde sabitlenmiştir. İlk kurulum için:

```powershell
Set-Location .\mobile
npm ci
Copy-Item .env.example .env
```

Geliştirme sunucusu ve platform hedefleri:

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
etmek ve yerel firewall erişimini ayarlamak gerekir. Yerel HTTP yalnız
development içindir; production API HTTPS kullanmalıdır.

`EXPO_PUBLIC_*` değerleri uygulama bundle'ına gömülür ve secret değildir.
Mobil environment dosyalarına veritabanı credential'ı, connection string,
backend signing secret, private API key veya OAuth client secret koymayın.

## Authentication sınırı

Mevcut ASP.NET Core API, harici bir OIDC/OAuth sağlayıcısının Bearer JWT access
token'ını doğrulayan resource server'dır. Login, register, refresh veya logout
endpoint'i üretmez. Bu foundation:

- bootstrapping, unauthenticated ve authenticated session durumlarını,
- memory access-token extension point'ini,
- provider-agnostic SecureStore adapter'ını,
- protected request'ler için bearer-token callback'ini,
- local logout ve query-cache temizleme yolunu

sağlar. Gerçek provider, PKCE, token exchange, refresh ve revocation ayrı auth
issue'sunda eklenecektir. Bu nedenle normal başlangıç login placeholder'ına
yönlenir; sahte development login'i yoktur.

## Expo Go, development build ve CNG

Mevcut foundation Expo Go ile çalışabilir. Gelecekte özel native module,
provider SDK veya platform-specific yapılandırma gerektiğinde development build
ve local Expo module/config plugin kullanılacaktır. CNG tarafından üretilen
native dosyalara kalıcı elle değişiklik yapılmamalıdır.

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
Login bilgilendiricidir; Home mevcut auth guard arkasında gerçek veri içermeyen
önizlemedir. Görsel kontrol için auth bypass veya preview route eklemeyin.

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
