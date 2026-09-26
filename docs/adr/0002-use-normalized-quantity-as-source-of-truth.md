# ADR 0002: Use Normalized Quantity as the Single Source of Truth

## Status

Accepted

## Context

Kullanıcı miktarı `g`, `kg`, `ml`, `l` veya `adet` birimiyle girebilir. Ekranda tercih edilen birim korunurken tarif uygunluğu ve stok tüketimi karşılaştırılabilir değerler üzerinden yapılmalıdır.

Hem girilen/display miktarını hem normalize miktarı persist etmek, iki sayısal alanın güncelleme veya tüketim sırasında birbirinden kopmasına neden olabilir. Desteklenen dönüşümler aynı boyut içinde exact `x1000` veya `x1` olduğundan ikinci bir persist edilmiş miktara ihtiyaç yoktur.

## Decision

`NormalizedQuantity`, persist edilen tek otoritatif miktar olacaktır. Kullanıcı gösterim tercihi `DisplayUnit` olarak persist edilecektir. Gösterilecek sayısal değer normalize miktar ve DisplayUnit üzerinden türetilecektir.

Örnek:

```text
NormalizedQuantity = 1000 g
DisplayUnit = kg
UI = 1 kg
```

`DisplayQuantity` persist edilmeyecektir. Geçmişte girilen değişmez bir değeri çağrıştıran ikinci bir miktar alanı da tanımlanmayacaktır.

Normalized temel birim `Ingredient.QuantityDimension` üzerinden türetileceği için ayrı `NormalizedUnit` kolonu tutulmayacaktır:

- `Mass -> g`
- `Volume -> ml`
- `Count -> adet`

Unit, MVP'de tablo/entity değil; şu kapalı değerlerden oluşan domain value object/enum'dur:

- `g`: Mass, base `g`, factor 1
- `kg`: Mass, base `g`, factor 1000
- `ml`: Volume, base `ml`, factor 1
- `l`: Volume, base `ml`, factor 1000
- `adet`: Count, base `adet`, factor 1

Boyutlar arasında tahmini dönüşüm yapılmayacaktır. Miktar alanları için mevcut veri modeli kararı SQL Server `decimal(18,6)` ve C# `decimal` kullanımıdır.

## Consequences

Olumlu sonuçlar:

- İki miktar kolonu arasında drift oluşmaz.
- Karşılaştırma ve stok tüketimi tek değer üzerinden yapılır.
- Kullanıcının birim tercihi korunur.
- Exact Unit dönüşümleri merkezi ve test edilebilir olur.

Bedeller:

- UI miktarı her okumada dönüştürür.
- Kullanıcının `1.000` gibi lexical yazım biçimi aynen korunmaz.
- Ingredient boyutu ile DisplayUnit uyumu Domain/Application seviyesinde doğrulanmalıdır.
- Yeni ve bağlama bağlı ölçüler bu kapalı modele doğrudan eklenemez.

## Alternatives considered

### DisplayQuantity ve NormalizedQuantity değerlerini birlikte persist etmek

Okumayı kolaylaştırabilir; ancak iki otoritatif sayısal değer üretir ve stok değişikliklerinde drift riski yaratır. Bu nedenle reddedildi.

### Yalnız kullanıcının girdiği miktarı saklamak

Her uygunluk ve stok sorgusunda normalizasyon gerektirir; güvenilir karşılaştırmayı ve indekslenebilir sorguları zorlaştırır.

### Unit tablosu kullanmak

Kapalı beş birimin bağımsız CRUD veya yaşam döngüsü yoktur. Yeni Unit eklemek conversion davranışını da değiştirdiği için salt veri değişikliği değildir. Bu nedenle MVP için reddedildi.

## Open / deferred points

- Hesaplanan değer persistence scale'ini aştığında kullanılacak kesin rounding modu.
- Fractional serving desteği ve porsiyon alanının fiziksel tipi.
- Gelecekte ingredient'a bağlı “bardak/kaşık” gibi ölçülerin desteklenip desteklenmeyeceği.
- Implementation ve test kanıtı farklı hassasiyet gerektirirse `decimal(18,6)` kararının ADR süreciyle revizyonu.
