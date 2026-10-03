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

## Doğrulama

```powershell
npm run typecheck
npm run lint
npm run format:check
npm test -- --runInBand
```
