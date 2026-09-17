# Proje Çalışma Kuralları

Bu depo, iptal edilen BoluPass projesinden tamamen bağımsızdır. BoluPass kodu, dosyaları, ilerleme durumu veya kararları bu projeye taşınmamalıdır.

## Öğrenme ve iletişim

- Her görevden önce ne yapılacağını, neden gerekli olduğunu ve beklenen etkisini Türkçe açıkla.
- Kod farklarını ve önemli teknik kavramları, kullanıcının projeyi geliştirirken öğrenmesini destekleyecek şekilde Türkçe anlat.
- Varsayımları, açık kararları ve doğrulanmamış noktaları açıkça belirt.

## Değişiklik kapsamı

- Küçük, bağımsız ve kolayca incelenebilir değişiklikler yap.
- Yalnızca mevcut görev için gerekli dosyaları değiştir; ilgisiz dosyalara dokunma.
- Mevcut dosyaları gerekçesiz yere yeniden biçimlendirme, taşıma veya yeniden yazma.
- Kullanıcının mevcut değişikliklerini koru ve üzerine yazma.

## Test ve doğrulama

- Değişiklikle ilgili testleri ve uygun statik kontrolleri çalıştır.
- Çalıştırılan testleri ve sonuçlarını işlem sonunda bildir.
- Bir test çalıştırılamadıysa veya henüz mevcut değilse bunu ve nedenini açıkça belirt.
- Yapılmamış kurulumu, geçmemiş testi veya tamamlanmamış özelliği tamamlanmış gibi gösterme.

## Git ve inceleme akışı

- Her işi açık kapsamlı bir GitHub issue ile ilişkilendir.
- Değişiklikleri amaca uygun, küçük bir branch üzerinde geliştir; `main` dalına doğrudan geliştirme yapma.
- Commit mesajlarında Conventional Commits biçimini kullan.
- Pull request açıklamasında neyin, neden ve nasıl değiştiğini; test kanıtını ve ilgili issue'yu belirt.
- Kullanıcı açıkça istemedikçe commit, push, merge veya uzak depo değişikliği yapma.

## Güvenlik

- Parola, token, API anahtarı, sertifika, bağlantı dizesi veya başka bir gizli bilgiyi repoya ekleme.
- Gerçek sırlar yerine takip edilebilir ve güvenli yer tutucular içeren `.env.example` gibi örnek dosyalar kullan.
- Log, test verisi ve ekran görüntülerinde gizli veya kişisel bilgi bulunmadığını kontrol et.
