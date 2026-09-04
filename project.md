## Veri Kaynağı Notları

## Epic 1 — Proje İskeleti, Veri Modeli ve Erişim Anahtarları

- 1.1 Clean Architecture katmanları (Domain/Application/Infrastructure/Presentation)

- 1.2 MediatR + CQRS iskeleti

- 1.3 EF Core + `docker-compose.yml` (SQL Server)

- 1.4 Entity'ler: `Ioc`, `Provider`, `IocSource`, `FetchHistory`, `LogEntry`

- 1.5 abuse.ch Auth-Key + PhishTank app key kaydı, secrets kurulumu (`dotnet user-secrets` / `.env`)

## Epic 2 — Normalizasyon Kuralları ve Veri Kaynağı Entegrasyonları

- 2.1 Tip bazlı normalizasyon kuralları: IPv4 / Domain / URL için ayrı kurallar, DNS çözümlemesi **kullanılmayacak** (CDN nedeniyle IP üzerinden domain tekilleştirme yanlış sonuç verir)

- 2.2 PhishTank / ThreatFox / URLhaus fetch servisleri (`IThreatSourceProvider` ortak arayüzü, Refit client per provider)

- 2.3 Zamanlanmış görev — `BackgroundService` + `PeriodicTimer`, `Provider.FetchIntervalMinutes` konfigüre edilebilir

- 2.4 Force-fetch endpoint (provider bazlı)

- 2.5 Provider status endpoint (last-fetch time + status)

- 2.6 Rate-limit/backoff handling (PhishTank key'siz limiti, abuse.ch fair-use)


## Epic 3 — Tekilleştirme, Arama, API ve Hata Yönetimi

- 3.1 Upsert mantığı: `(IocType, NormalizedValue)` unique key üzerinden `Ioc` tablosuna yazma, ilgili `IocSource` satırının ekleme/güncellenmesi

- 3.2 Arama terimi normalize etme (2.1'deki fonksiyonun tekrar kullanımı)

- 3.3 DB → Provider fallback arama (MVP'de DB-only yeterli olabilir — asıl veri zaten scheduled job ile toplanıyor, live fallback ekstra karmaşıklık getirir)

- 3.4 `try-catch` + `FetchHistory` kaydı

- 3.5 Log listeleme endpoint (`LogEntry` tablosunu döner)


## Epic 4 — Frontend (React)

- 4.1 React + TS + Vite + Tailwind + RadixUI iskelet

- 4.2 Tanstack Query kurulumu

- 4.3 Routing (React Router) — arama/sonuç/detay sayfaları arası

- 4.4 Arama sayfası

- 4.5 Sonuç sayfası

- 4.6 Detay sayfası (`IocSource` listesi + provider bazlı first/last seen — REQ-04)


## Epic 5 — Test ve Dokümantasyon

- 5.1 Unit testler (dedup, normalizasyon kuralları) — kod yazılırken paralel yazılır, sona bırakılmaz

- 5.2 README (kurulum, veri kaynakları, teknik kararlar)

- 5.3 `docker-compose up` ile tek komutla çalıştırma

**Not:** Epic numarası sıralama içindir, uygulama sırası için değil — 5.1'deki testler özellikle Epic 2-3 yazılırken paralel yazılmalı; kural değiştiğinde regresyon aksi halde fark edilmez.
