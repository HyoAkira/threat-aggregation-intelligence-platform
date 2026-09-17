## Why

IOC detay ekranı kaynaklara atfedilmiş kanıtı gösteriyor, fakat analistin bu kanıttan tutarlı ve izlenebilir bir değerlendirme üretmesi için akış sunmuyor. Yönetici tarafından seçilen OpenAI uyumlu bir modelle, mevcut kanıta dayalı Türkçe rapor ve rapora bağlı soru-cevap akışı eklenmeli.

## What Changes

- Yöneticinin AI sunucusu Base URL ve model kimliğini ayrı bir ayar alanında yönetmesini; API anahtarını mevcut şifreli, yalnızca yazılabilir credential uç noktasıyla kaydetmesini sağla.
- Varsayılan sürümlü rapor yönergelerini `system.md` benzeri bir dosyayla dağıt; Admin panelinden metni görüntüleme, değiştirme ve yeni sürüm olarak yayımlama imkânı sağla. Değişmez güvenlik/veri erişim sınırları bu metinden ayrı tutulur.
- IOC detayında kimliği doğrulanmış kullanıcı için isteğe bağlı rapor oluşturma düğmesi ekle. Backend, IOC grubunun kaynaklara atfedilmiş ve sınırlandırılmış anlık görüntüsünü alıp yayımlanmış rapor kurallarıyla modele gönderir; yanıtı akışla iletir ve yalnızca tamamlanmış raporu saklar.
- Raporu gözlem, analist çıkarımı, belirsizlik ve önerilen aksiyon olarak ayır; desteklenen iddialarda kaynak kimlikleri göster ve eksik kanıtı açıkça belirt. IOC adreslerine kendiliğinden ağ isteği, harici zenginleştirme, otomatik bloklama veya kural yürütme yapılmaz.
- Tamamlanmış rapora bağlı, yalnızca o raporun IOC kanıtı ve rapor içeriğiyle beslenen sınırlı sohbet ekle. Sohbet yalnızca oturum sahibi tarafından kullanılır; mesajlar ve model çıktısı boyut ve hız sınırlarına tabidir.
- AI üretim uç noktalarını kimlik doğrulama, kullanıcı başına hız/eşzamanlılık sınırları, güvenli hedef doğrulaması, gizli veri redaksiyonu ve akış hatası sözleşmesiyle koru.

## Capabilities

### New Capabilities

- `ai-provider-configuration`: Admin'e özel OpenAI uyumlu sunucu ayarları, bağlantı hedefi politikası ve credential durumu.
- `ai-report-policy-management`: Admin tarafından düzenlenebilen ve sürümlenen rapor yönergeleri; değişmez uygulama güvenliği kurallarından ayrım.
- `ioc-ai-reporting`: IOC kanıtından istek üzerine, kaynak gösteren ve akışla iletilen Türkçe rapor üretimi, saklama ve görüntüleme.
- `ioc-ai-chat`: Tamamlanmış rapora ve tek IOC kanıtına bağlı, yetkilendirilmiş sınırlı sohbet.

### Modified Capabilities

- `admin-frontend-runtime-settings`: `/admin/settings` sayfası ayrı bir AI yapılandırma bölümü gösterir; tam provider/log ayarları PUT sözleşmesine AI anahtarı veya yapılandırması eklemez.

## Impact

.NET API/Application/Infrastructure katmanlarında yeni AI ayar, rapor yönergesi, rapor, sohbet ve OpenAI uyumlu istemci sözleşmeleri; SQL Server şema değişiklikleri; `/api/admin/ai/*` ve `/api/iocs/{id}/ai/*` uç noktaları; React IOC detay ve Admin ayarlar arayüzleri etkilenir. Mevcut `OpenAICompatible.ServerApiKey` secret kimliği, `ISecretStore` ve JWT kimlik doğrulaması kullanılır. Devam eden `migrate-provider-api-keys-to-encrypted-storage` değişikliği secret depolama bağımlılığıdır; bu değişiklik onun şifreleme uygulamasını yeniden tanımlamaz.
