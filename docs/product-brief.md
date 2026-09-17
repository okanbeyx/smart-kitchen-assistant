# Akıllı Mutfak Asistanı - MVP Ürün Brifi

## 1. Dokümanın amacı

Bu doküman, Akıllı Mutfak Asistanı'nın ilk çalışan sürümü için ürün amacını, kapsam sınırlarını, temel kullanıcı akışlarını ve başarı koşullarını tanımlar. Buradaki amaç, geliştirme başlamadan önce herkesin aynı MVP tanımıyla çalışmasını sağlamak ve sonraki fazlara ait özelliklerin ilk sürümü büyütmesini önlemektir.

Bu proje, iptal edilen BoluPass projesinden bağımsızdır. BoluPass'a ait kod, dosya, ilerleme veya teknik kararlar bu projenin parçası kabul edilmez.

## 2. Projenin amacı

Akıllı Mutfak Asistanı; kullanıcının evindeki gıda malzemelerini miktarlarıyla kaydetmesine, bu stoktan hangi tariflerin yapılabileceğini anlamasına ve seçtiği tarifi adım adım pişirmesine yardımcı olan bir mobil uygulamadır.

Ürünün temel değeri yalnızca tarif listelemek değildir. Kilerdeki gerçek miktarları tarif gereksinimleriyle karşılaştırmak, sonucu açıklanabilir bir uygunluk sınıfıyla sunmak ve kullanıcı onayı olmadan stok miktarını değiştirmemek ürünün ayırt edici davranışlarıdır.

## 3. Hedef kullanıcılar

Birincil hedef kullanıcılar:

- Evde yemek yapan ve mevcut malzemelerini daha verimli kullanmak isteyen kişiler.

- Ne pişireceğine karar verirken elindeki malzemeleri ve miktarlarını dikkate almak isteyen kişiler.

- Eksik veya yetersiz malzemeleri tarife başlamadan önce görmek isteyen kişiler.

İkincil hedef kullanıcılar:

- Ortak mutfak veya kalabalık ev için stok takibi yapmak isteyen kişiler.

- Sınırlı bütçeyle elindeki malzemeleri değerlendirmek isteyen öğrenciler.

- Pişirirken kolay okunabilen, adım adım bir rehbere ihtiyaç duyan kullanıcılar.

Bu hedef gruplar ve ihtiyaçlar başlangıç hipotezleridir. Kullanıcı görüşmeleri ve prototip testleri yapılmadan doğrulanmış pazar bilgisi olarak kabul edilmemelidir.

### Doğrulanması gereken temel varsayımlar

- Kullanıcılar kilerlerindeki malzemeleri ve miktarları girmeye yeterli değer atfeder.

- "Yapılabilir", "miktar yetersiz" ve "eksik malzeme" ayrımı, yalnızca genel bir benzerlik puanından daha anlaşılır ve yararlıdır.

- Kullanıcılar önerinin nedenini gördüklerinde sonuca daha fazla güvenir.

- Pişirme sonunda stok tüketimini onaylama adımı, stok doğruluğunu korurken kullanıcıya gereksiz yük oluşturmaz.

## 4. Çözülen problem

Tarif katalogları genellikle kullanıcının bir malzemeye sahip olup olmadığına odaklanır; sahip olunan miktarın tarif için yeterli olup olmadığını her zaman açıkça göstermez. Bu durum, kullanıcının uygun görünen bir tarife başladıktan sonra malzemenin eksik veya yetersiz olduğunu fark etmesine yol açabilir.

Akıllı Mutfak Asistanı bu problemi şu sırayla ele alır:

1. Kullanıcının kilerindeki malzemeleri, miktarları ve birimleri kaydeder.

2. Tarif miktarlarını hedef porsiyona göre hesaplar.

3. Karşılaştırılabilir birimleri aynı temel birime dönüştürür.

4. Zorunlu tarif malzemelerini kullanıcının stok miktarlarıyla karşılaştırır.

5. Sonucu açık bir uygunluk sınıfı veya "değerlendirilemedi" teknik sonucu ve insan tarafından anlaşılabilir nedenlerle sunar.

Uygulama, gıda güvenliği veya bir malzemenin tüketime uygunluğu konusunda garanti vermez. Son tüketim tarihi ve alerjen gibi bilgiler ayrıca doğrulanması gereken veri ve politika kurallarına tabidir.

## 5. MVP kapsamı

MVP, kullanıcının kilerden tarife uzanan temel yolculuğu tamamlayabildiği en küçük çalışan ürün olacaktır.

### Hesap ve erişim

- E-posta ve parola ile hesap oluşturma ve oturum açma.

- Güvenli oturum sonlandırma.

- Misafir kullanıcı için yayımlanmış tarifleri salt okunur inceleme.

- Kişisel kiler ve stok işlemlerinde kullanıcı hesabı zorunluluğu.

### Kiler

- Malzeme arama ve katalogdan malzeme seçme.

- Pozitif ondalık miktar ve desteklenen birimle kiler kaydı ekleme.

- Kullanıcının kendi kiler kayıtlarını listeleme, düzeltme ve silme.

- Kütle için gram/kilogram, hacim için mililitre/litre ve sayılabilir malzemeler için adet desteği.

- Yalnızca aynı boyuttaki kesin birim dönüşümleri. Doğrulanmış malzeme dönüşümü olmadan kütle-hacim tahmini yapılmaması.

- İlk MVP kiler kaydı malzeme, miktar ve desteklenen birim bilgilerini içerir. Stok partileri ve son tüketim tarihi yönetimi, kapsam kararı verilene kadar ilk MVP uygulamasına dahil edilmez.

### Tarif kataloğu ve uygunluk

- Kaynağı ve kullanım hakkı doğrulanmış, sınırlı bir başlangıç tarif kataloğu.

- Tarif başlığı, porsiyon, süre, zorunlu/opsiyonel malzemeler ve pişirme adımları.

- Tarif miktarlarını hedef porsiyona göre ölçekleme.

- Kural tabanlı ve açıklanabilir uygunluk hesaplama.

- Tarifleri "yapılabilir", "miktar yetersiz" ve "eksik malzeme" olarak sınıflandırma.
- Zorunlu veri veya güvenilir birim dönüşümü bulunmadığında sonucu "değerlendirilemedi" olarak işaretleme ve tarifi kesinlikle yapılabilir göstermeme.
- Eksik veya yetersiz malzemeleri ve gerekli miktarları kullanıcıya gösterme.

### Pişirme

- Standart telefon ekranında tarif adımları arasında ileri ve geri hareket etme.

- Tarif adımına bağlı temel zamanlayıcı desteği.

- Aktif adımın uygulama yaşam döngüsü içinde korunması.

- Pişirmenin sonunda kullanıcıdan açık tüketim onayı alma.

- Yalnızca onaydan sonra stok miktarını azaltma.

- Yinelenen isteklerin stoğu iki kez azaltmaması için idempotent tüketim davranışı.

### Temel kalite

- Kullanıcının yalnızca kendi kiler verilerine erişebilmesi.

- Kritik miktar, birim dönüşümü ve tarif uygunluğu kurallarının otomatik testlerle doğrulanması.

- Hataların kullanıcıya teknik ayrıntı sızdırmadan anlaşılır biçimde sunulması.

- Temel ekran okuyucu etiketleri, yeterli dokunma alanları ve okunabilir metin.

## 6. MVP dışı kapsam

Aşağıdaki özellikler MVP tamamlanmadan zorunlu kabul edilmez:

- KNN tabanlı tarif sıralama. Sonraki fazda Google Colab deneyleriyle değerlendirilecektir.

- K-Means ile tarif kümeleme. Veri seti ve baseline sonuçları oluştuktan sonra Google Colab'da deneysel olarak incelenecektir.

- Katlanabilir cihazlara özel Flex-Mode ve native platform köprüsü. Standart pişirme ekranı tamamlandıktan sonra ele alınacaktır.

- Kullanıcı tarif gönderimi, sosyal akış, yorumlar, mesajlaşma ve kullanıcı puanları.

- Yönetici paneli, şikâyet kuyruğu ve topluluk moderasyonu.

- Abonelik, uygulama içi satın alma, premium paketler ve reklam.

- Sosyal hesaplarla giriş.

- Barkod tarama ve fotoğraftan otomatik malzeme tanıma.

- Gelişmiş diyet paketleri veya tıbbi/gıda güvenliği iddiaları.

- Çevrimdışı stok ekleme, düzenleme, silme veya tüketme. Çevrimdışı stok güncellemesi MVP kapsamında olmayacaktır.
- Çevrimdışı tarif okuma. Kapsamı ve önbellek davranışı karara bağlanana kadar ilk MVP uygulamasına dahil edilmez.

- Stok partileri ve son tüketim tarihi yönetimi. Bu özellikler açık karar olarak tutulur ve kapsam kararı verilene kadar ilk MVP uygulamasına dahil edilmez.
- Mikroservis mimarisi ve ihtiyacı kanıtlanmamış mesaj kuyruğu ya da önbellek bileşenleri.

Bu özelliklerin sonraki fazlarda yer alması kesin uygulama taahhüdü değildir. Her biri kullanıcı ihtiyacı, teknik fizibilite ve ölçüm sonuçlarıyla yeniden değerlendirilmelidir.

## 7. Kullanıcı türleri ve yetkiler

### Misafir kullanıcı

Misafir kullanıcı:

- Yayımlanmış tarifleri listeleyebilir.

- Tarif ayrıntısını, malzemelerini ve adımlarını okuyabilir.

- Uygulamanın temel değerini hesap oluşturmadan inceleyebilir.

Misafir kullanıcı:

- Kişisel kiler oluşturamaz veya sunucuda saklayamaz.

- Kiler miktarlarına dayalı kişiselleştirilmiş tarif uygunluğu alamaz.

- Stok tüketim işlemi yapamaz.

- Kişisel tercih veya pişirme geçmişi saklayamaz.

### Kayıtlı kullanıcı

Kayıtlı kullanıcı:

- Kendi kiler kayıtlarını oluşturabilir, listeleyebilir, güncelleyebilir ve silebilir.

- Kilerindeki miktarlara ve seçtiği porsiyona göre tarif uygunluğu alabilir.

- Eksik ve miktarı yetersiz malzemeleri görebilir.

- Pişirme akışını kullanabilir.

- Pişirme sonunda stok tüketimini onaylayabilir veya iptal edebilir.

Kayıtlı kullanıcı yalnızca kendi kişisel kiler ve pişirme verilerine erişebilir. Bir kaydın kimliğini değiştirerek başka kullanıcının verisine erişim sunucu tarafında engellenmelidir.

## 8. Kiler yönetimi

Kiler, tarif uygunluğu hesaplamasının temel veri kaynağıdır. Her kayıt en azından malzeme, pozitif miktar ve desteklenen birim bilgisini içerir.

Temel iş kuralları:

- Sıfır veya negatif miktar kabul edilmez.

- Miktarlar kayan noktalı sayı yerine ondalık hassasiyetle işlenir.

- Gram ve kilogram kütle; mililitre ve litre hacim; adet ise sayı boyutundadır.

- Yalnızca aynı boyuttaki birimler doğrudan dönüştürülür.

- Malzemeye özel doğrulanmış bir dönüşüm yoksa "bir bardak" gibi mutfak ölçüleri gram veya mililitreye tahminle çevrilmez.

- Tarif görüntülemek veya pişirmeye başlamak stoğu otomatik olarak azaltmaz.

- Stok ancak kullanıcının açık onayıyla azaltılır.

- Aynı malzemenin farklı stok partileri ile son tüketim tarihi yönetimi açık karar olarak tutulur ve karar verilene kadar ilk MVP uygulama kapsamına alınmaz.

## 9. Tarif uygunluk sınıfları

Uygunluk hesabında tarifin hedef porsiyona ölçeklenmiş zorunlu malzemeleri kullanılır. Opsiyonel malzemeler, zorunlu eksik hesabına katılmaz. "Yapılabilir", "miktar yetersiz" ve "eksik malzeme" kullanıcıya sunulan üç uygunluk sınıfıdır. Hesabın güvenilir biçimde tamamlanamadığı durumlar için bunlardan ayrı bir teknik sonuç olan "değerlendirilemedi" kullanılır.

### 9.1 Yapılabilir

Bir tarif, bütün zorunlu malzemeleri kullanıcının kilerinde bulunuyor ve karşılaştırılabilir temel birimlerdeki toplam miktarlar tarif gereksinimini karşılıyorsa "yapılabilir" olarak sınıflandırılır.

Koşullar:

- Her zorunlu malzeme eşleştirilmiştir.

- Her zorunlu malzeme için mevcut miktar, gereken miktara eşit veya daha fazladır.

- Dönüştürülemeyen veya güvenilirliği belirsiz bir zorunlu miktar yoktur.

### 9.2 Miktar yetersiz

Bir tarifin bütün zorunlu malzemeleri kilerde bulunuyor, ancak en az bir malzemenin mevcut miktarı gereken miktardan azsa tarif "miktar yetersiz" olarak sınıflandırılır.

Sonuçta en az şu bilgiler gösterilmelidir:

- Yetersiz malzemenin adı.

- Kilerdeki kullanılabilir miktar.

- Tarif için gereken miktar.

- Aradaki miktar farkı.

### 9.3 Eksik malzeme

Tarifin en az bir zorunlu malzemesi kullanıcının kilerinde bulunmuyorsa tarif "eksik malzeme" olarak sınıflandırılır.

Bir tarifte hem tamamen eksik hem miktarı yetersiz malzemeler bulunuyorsa ana sınıf "eksik malzeme" olur; ayrıntıda iki sorun türü de listelenir. Bu öncelik kuralının kullanıcı testleriyle anlaşılabilirliği doğrulanmalıdır.

### 9.4 Değerlendirilemedi

"Değerlendirilemedi", bir tarif uygunluk sınıfına güvenilir biçimde yerleştirilemediğinde kullanılan teknik sonuçtur. Aşağıdaki durumlardan biri buna neden olabilir:

- Zorunlu bir tarif malzemesinin miktar veya birim bilgisinin eksik olması.
- Tarif birimi ile kiler birimi arasında doğrulanmış bir dönüşüm bulunmaması.
- Zorunlu bir malzemenin katalog kaydıyla güvenilir biçimde eşleştirilememesi.

Bu sonuçtaki tarif kesinlikle "yapılabilir" olarak gösterilmez. Kullanıcıya değerlendirmenin neden tamamlanamadığı açıklanır. Eksik veya dönüştürülemeyen veri, malzemenin kullanıcının kilerinde bulunmadığı şeklinde yorumlanmaz.

## 10. Temel kullanıcı akışları

### 10.1 Kiler oluşturma ve güncelleme

**Başlangıç koşulu:** Kullanıcı oturum açmıştır.

1. Kullanıcı kiler ekranını açar.

2. Malzeme adıyla arama yapar ve katalogdaki doğru malzemeyi seçer.

3. Pozitif miktarı ve desteklenen birimi girer.
4. Uygulama girdiyi doğrular ve sunucu miktarı temel birime normalize eder.
5. Kayıt kullanıcının kilerinde okunabilir miktar ve birimle gösterilir.
6. Kullanıcı daha sonra miktarı güncelleyebilir ya da kaydı silebilir.

**Başarılı sonuç:** Geçerli malzeme ve miktar kullanıcının kilerinde saklanır ve tarif uygunluğu hesaplarında kullanılabilir.

**Başarısız/alternatif durumlar:**

- Miktar sıfır, negatif veya sayısal değilse kayıt yapılmaz.

- Birim malzemenin boyutuyla uyumsuzsa kullanıcıdan düzeltme istenir.

- Malzeme bulunamazsa gelişigüzel yeni bir katalog kaydı oluşturmak yerine kontrollü bir geri bildirim yolu sunulur.

- Eşzamanlı bir güncelleme algılanırsa kullanıcının daha yeni verisinin sessizce ezilmesine izin verilmez.

### 10.2 Kilerden açıklanabilir tarif önerisi alma

**Başlangıç koşulu:** Kullanıcı oturum açmış ve kilerinde en az bir geçerli kayıt oluşturmuştur.

1. Kullanıcı "Ne pişirebilirim?" eylemini başlatır.

2. Hedef porsiyonu ve desteklenen temel filtreleri seçer.

3. Sistem yalnızca aktif ve yayımlanmış tarifleri aday olarak alır.

4. Tarif malzemeleri hedef porsiyona göre ölçeklenir.

5. Tarif gereksinimleri ile kiler miktarları karşılaştırılabilir temel birimlere dönüştürülür.

6. Yeterli ve dönüştürülebilir verisi bulunan her tarif "yapılabilir", "miktar yetersiz" veya "eksik malzeme" olarak sınıflandırılır. Hesap güvenilir biçimde tamamlanamıyorsa "değerlendirilemedi" teknik sonucu verilir.

7. Yapılabilir tarifler öncelikli olmak üzere sonuçlar sıralanır.

8. Her sonuçta sınıfın nedeni, eksik/yetersiz malzemeler ve ilgili miktarlar gösterilir.

9. Kullanıcı bir tarifi seçerek ayrıntı, porsiyon, malzeme ve adımları inceler.

**Başarılı sonuç:** Kullanıcı, bir tarifin neden uygun veya uygun olmadığını anlayarak karar verebilir.

**Başarısız/alternatif durumlar:**

- Kiler boşsa kullanıcıya malzeme eklemesini sağlayan yönlendirici boş ekran gösterilir.

- Hiçbir tarif tamamen yapılamıyorsa sistem boş sonuç yerine eksik veya yetersiz malzemeli en yakın seçenekleri açıkça etiketleyerek gösterebilir.

- Birim dönüştürülemiyorsa veya zorunlu tarif verisi eksikse "değerlendirilemedi" sonucu verilir; tarif kesinlikle yapılabilir sayılmaz ve sorunun nedeni açıklanır.
- Tarif verisi henüz doğrulanmamışsa kullanıcıya açık bir belirsizlik uyarısı gösterilir.

- Sunucuya ulaşılamıyorsa stok verisi güncelmiş gibi gösterilmez ve tekrar deneme seçeneği sunulur.

### 10.3 Tarif pişirme ve kullanıcı onayıyla stok tüketme

**Başlangıç koşulu:** Kayıtlı kullanıcı bir tarifin ayrıntısını açmış ve pişirmeye başlamayı seçmiştir.

1. Kullanıcı tarifin porsiyonunu, malzemelerini ve uygunluk durumunu son kez kontrol eder.

2. Pişirme ekranı ilk adımı büyük ve okunabilir kontrollerle gösterir.

3. Kullanıcı ileri ve geri hareket ederek adımları takip eder.

4. Zamanlayıcı bulunan adımlarda kullanıcı zamanlayıcıyı başlatabilir.

5. Ekran yönü veya uygulama durumu değişse aktif adım ve zaman bilgisi korunur.

6. Son adımdan sonra uygulama, tarif malzemelerinin kilerden düşülmesini açıkça sorar.

7. Kullanıcı onaylamazsa stokta hiçbir değişiklik yapılmaz.

8. Kullanıcı onaylarsa sunucu, tüketim işlemini tek bir tutarlı işlem olarak uygular.

9. Aynı onay isteği ağ veya tekrar dokunma nedeniyle yeniden gelirse stok ikinci kez azaltılmaz.

10. Başarılı işlemden sonra güncel kiler miktarları kullanıcıya gösterilir.

**Başarılı sonuç:** Kullanıcı pişirme adımlarını tamamlar ve yalnızca açık onayından sonra stok miktarları güvenli biçimde güncellenir.

**Başarısız/alternatif durumlar:**

- Pişirme sırasında stok başka bir işlemle değişmişse sessizce negatif miktar oluşturulmaz; kullanıcıya uyuşmazlık bildirilir.

- Tüketim isteği başarısızsa stok azaltılmış gibi gösterilmez.

- Çevrimdışı tarif okuma henüz kesinleşmiş bir MVP davranışı değildir. Daha sonra kapsama alınırsa yalnızca önceden önbelleğe alınmış tarif içeriği salt okunur sunulur. Çevrimdışı stok güncellemesi veya tüketim yapılmaz.

## 11. Genel hata ve boş ekran ilkeleri

- Boş kiler ekranı, kullanıcıya ilk malzemeyi nasıl ekleyeceğini gösteren tek ve açık bir eylem sunar.

- Boş tarif sonucu, sistem hatası gibi gösterilmez; filtreleri değiştirme veya kilere malzeme ekleme yolları sunulur.

- Yetkisiz erişimde kişisel veri gösterilmez; kullanıcı uygun oturum ekranına yönlendirilir.

- Doğrulama hataları alanla ilişkili, düzeltilebilir ve teknik olmayan mesajlarla gösterilir.

- Ağ hatalarında kullanıcının girdisi mümkün olduğunca korunur ve tekrar deneme eylemi sunulur.

- Sunucu hatalarında dahili exception, SQL veya gizli sistem bilgileri istemciye gönderilmez.

- Yükleme sırasında aynı işlemin tekrar başlatılması ve çift stok tüketimi engellenir.

- Verinin güvenilirliği bilinmiyorsa sistem kesin güvenlik veya yapılabilirlik iddiasında bulunmaz.

## 12. MVP başarı kriterleri

MVP teknik olarak tamamlanmış sayılmadan önce en az şu koşullar sağlanmalıdır:

- Yeni bir kullanıcı hesap oluşturup kiler kaydı ekleyebilir.

- Miktarlar desteklenen birimlerden temel birimlere doğru dönüştürülür.

- Tarifler hedef porsiyona göre ölçeklenir.

- Etiketli başlangıç senaryolarında yanlış "yapılabilir" sonucu oluşmaz.
- Zorunlu verisi eksik veya birimi güvenilir biçimde dönüştürülemeyen tarifler "değerlendirilemedi" sonucuyla sunulur.

- Kullanıcı sonucun nedenini ve eksik/yetersiz miktarları anlayabilir.

- Kullanıcı kilerden tarif seçimine, pişirme adımlarına ve onaylı stok tüketimine kadar temel akışı tamamlayabilir.

- Kullanıcı onayı olmadan stok azaltılmaz.

- Tekrarlanan tüketim isteği stoğu iki kez azaltmaz.

- Bir kullanıcı başka bir kullanıcının kiler verisini okuyamaz veya değiştiremez.

- Kritik birim, miktar, uygunluk, yetki ve tüketim senaryoları otomatik testlerden geçer.

### Ürün doğrulama göstergeleri

Aşağıdaki göstergeler beta sırasında ölçülebilir; hedef değerler kullanıcı araştırması yapılmadan kesinleştirilmez:

- Kiler oluşturmayı tamamlayan kullanıcı oranı.

- Öneri ekranını açan kullanıcı oranı.

- Tarif başlatma ve tamamlama oranı.

- Uygunluk sonucundaki yanlış pozitiflerin sayısı.

- Hiç sonuç bulunamayan sorguların oranı.

- Kullanıcıların uygunluk sınıflarını ve nedenlerini doğru anlayıp anlamadığı.

- Belirli bir dönem içindeki tekrar kullanım davranışı.

## 13. Açık kararlar

Geliştirme sırası gelmeden aşağıdaki konular ilgili issue veya mimari karar kaydında netleştirilmelidir:

- Stok partileri ve opsiyonel son tüketim tarihi yönetiminin hangi sürümde ve hangi iş kurallarıyla uygulanacağı. Karar verilene kadar bu özellikler ilk MVP uygulamasının dışındadır.

- Kullanıcının girdiği orijinal birim ile normalize temel miktarın birlikte nasıl saklanacağı.

- Birden fazla stok partisi varsa tüketim sırasının nasıl belirleneceği.

- Misafir kullanıcının tarif ayrıntısı ve pişirme ekranına hangi sınıra kadar erişebileceği.

- Alerjen ve diyet filtrelerinin MVP'ye girip girmeyeceği; girerse veri kaynağı, bilinmeyen içerik ve uyarı politikası.

- "Varsayılan ev malzemeleri" kullanılıp kullanılmayacağı. Açık kullanıcı tercihi olmadan tuz, su veya yağ var kabul edilmemelidir.

- Başlangıç tariflerinin sayısı, kaynağı, lisansı ve kalite inceleme süreci.

- Pişirme ekranında tek veya birden fazla eşzamanlı zamanlayıcı desteklenip desteklenmeyeceği.

- Çevrimdışı tarif okumanın sonraki bir MVP revizyonuna veya daha ileri bir sürüme alınıp alınmayacağı; alınırsa önbellek süresi, veri güncelliği ve geçersizleştirme davranışı.

- Hesap silme, yedek saklama ve kişisel verilerin silinme takvimi.

- Beta başlangıcında kullanılacak kesin ürün ve performans hedefleri.

## 14. Teknik ve ürün riskleri

| Risk | Etki | Başlangıç önlemi |

| --- | --- | --- |

| Yanlış "yapılabilir" sonucu | Kullanıcı tarife başladıktan sonra malzemenin yetmediğini fark eder ve sisteme güven azalır. | Zorunlu miktar kuralları, altın test senaryoları ve belirsiz veride kesin sonuç vermeme. |

| Birim belirsizliği | Yanlış miktar karşılaştırması ve stok kaybı oluşabilir. | Yalnızca aynı boyutta kesin dönüşüm; doğrulanmamış kütle-hacim tahmini yapmama. |

| Eşzamanlı veya yinelenen tüketim | Stok iki kez azalabilir ya da negatif olabilir. | Transaction, optimistic concurrency ve idempotency anahtarı. |

| Eksik/yanlış tarif verisi | Uygunluk hesabı ve kullanıcı güvenliği etkilenir. | Yalnızca incelenmiş katalog; veri durumu ve sürümünü kaydetme. |

| Tarif ve görsel lisansı | İçerik kaldırma veya hukuki sorun oluşabilir. | Kaynağı ve kullanım hakkı belirsiz içeriği kataloğa almama. |

| Alerjen bilgisinin yanlış yorumlanması | Sağlık riski ve yanıltıcı güvenlik iddiası oluşabilir. | Doğrulanmış veri olmadan "güvenli" dememe; belirsizliği açıkça gösterme. |

| Kiler girişinin zahmetli bulunması | Kullanıcı ana değere ulaşmadan uygulamayı terk edebilir. | Kısa giriş akışı, kullanılabilirlik testi ve ileride kanıta dayalı kolaylaştırma. |

| Kişisel veriye yetkisiz erişim | Gizlilik ve güvenlik ihlali oluşur. | Her kayıtta sunucu tarafı sahiplik kontrolü ve negatif yetki testleri. |

| Çevrimdışı durumun yanlış sunulması | Kullanıcı eski veriyi güncel sanabilir. | Salt okunur önbelleği etiketleme; sunucuyla uzlaşmayan stok değişikliğini kesinleşmiş göstermeme. |

| Kapsamın erken büyümesi | Çalışan temel akış gecikir. | MVP dışı listesini koruma; KNN, K-Means, Flex-Mode, topluluk ve ödemeyi ayrı fazlara bölme. |

## 15. Sonraki adım

Bu brif onaylandıktan sonra teknik uygulamaya geçmeden önce açık kararlar önceliklendirilmelidir. Bir sonraki depo görevi; katkı ve Git akışını netleştirmek, ardından modüler monolit ve ilk geliştirme ortamı için ayrı, küçük issue'lar oluşturmak olmalıdır.
