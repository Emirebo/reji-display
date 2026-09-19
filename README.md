# RejiDisplay — NovaStar VX2000 Pro LED Çıktı & Simultane Çeviri Yöneticisi (v0.4.0)

RejiDisplay; canlı etkinliklerde, kongrelerde ve sahnelerde Sol (LEFT) ve Sağ (RIGHT) LED ekranlara bağımsız olarak **Görsel (Image)**, **Video (MP4/MOV)** ve **Canlı Web Siteleri (Website — Simultane Çeviri / Canlı Altyazı Akışları)** aktarmak üzere tasarlanmış, C# / .NET 8 WPF mimarisinde yüksek güvenirlikli bir Windows uygulamasıdır.

---

## 1. RejiDisplay Nedir ve Ne İşe Yarar?

- **Bağımsız Çift LED Çıktısı**: **LEFT LED** ve **RIGHT LED** ekranlarına tamamen bağımsız görseller, videolar veya canlı web sayfaları gönderilebilir.
- **Rezerve CENTER Ekran Koruması**: **CENTER** ekranı (Orta ekran) Windows Extended Desktop olarak rezerve edilir. PowerPoint sunumları, ana sahneler ve tarayıcı pencereleri için serbest bırakılır; RejiDisplay asla bu ekranı ele geçirmez.
- **Operatör Ekran Koruması**: RejiDisplay operatörün birincil ekranını (Primary Monitor) algılar ve tam ekran çıktılarının yanlışlıkla reji ekranına açılmasını donanımsal düzeyde engeller.
- **Draft vs. Live Güvenlik Mimarisi**: Operatör canlı yayını kesintiye uğratmadan taslak (Draft) görsel, video ve web adresi düzenlemeleri yapabilir. Canlı senkronizasyon (`Live Sync`) kapalıyken değişiklikler sadece yayına uygulama (`APPLY TO LIVE`) butonuna basıldığında canlı ekrana aktarılır.
- **Simultane Çeviri Web Desteği**: Microsoft WebView2 tarayıcı motoru entegrasyonu sayesinde canlı altyazı, simultane çeviri web panelleri ve dinamik web uygulamaları doğrudan LED panellere aktarılır.
- **Anlık Canlı Güvenlik Kontrolleri**: `⚫ SİYAH (BLACK)` ile yayın anında karartılabilir, `🟡 GERİ YÜKLE (RESTORE)` ile sayfa yeniden yüklenmeden yayın anında geri getirilebilir, `⏹ DURDUR (STOP)` ile çıktı güvenle kapatılabilir.
- **Fiziksel Sahat Uyarısı**: Uygulama içi kalibrasyon ve test desenleri tam uyumlu olmakla birlikte, nihai renk, sinyal haritalama ve VX2000 katman konfigürasyonu sahadaki LED panellerde fiziksel test gerektirir.

---

## 2. Donanım, Ekran Düzeni ve Kablolama Mimarisi

### Quadro P2000 Çıkış Topolojisi (4 GPU Çıkışı)

Hedef sistem tek bir Windows bilgisayar ve 4 fiziksel GPU çıkışına sahip NVIDIA Quadro P2000 (veya muadili çoklu çıkışlı GPU) ekran kartıdır:

1. **GPU Çıkış 1**: Operatör Monitörü (RejiDisplay Kontrol Arayüzü)
2. **GPU Çıkış 2**: LEFT LED Video Sinyali (NovaStar VX2000 Pro Girdisi)
3. **GPU Çıkış 3**: CENTER Rezerve Sunum Masaüstü (PowerPoint / Normal Windows Ekranı)
4. **GPU Çıkış 4**: RIGHT LED Video Sinyali (NovaStar VX2000 Pro Girdisi)

> **⚠️ ÖNEMLİ (VX2000 Ön Ayar / Preset Uyarısı):**
> Mevcut salon preset haritasına göre beklenen sinyal girişi: **CENTER = HDMI 1**, **RIGHT = HDMI 2**, **LEFT = HDMI 3** şeklindedir. Bu harita varsayılan beklentidir; sahadaki DisplayPort-HDMI dönüştürücüler (adapters) ve VX2000 fiziksel giriş portları mutlaka fiziksel olarak kontrol edilmelidir.

### Mantıksal LED Çözünürlükleri vs. GPU Sinyalleri

Sistemde iki farklı çözünürlük kavramı ayrıştırılmıştır:

- **Mantıksal LED Çözünürlüğü (Logical LED Canvas)**:
  - **LEFT LED**: `860 × 1720` piksel
  - **CENTER LED**: `2581 × 1376` piksel
  - **RIGHT LED**: `860 × 1720` piksel
- **Windows GPU Sinyal Çözünürlüğü**: Genellikle `1920 × 1080` (Full HD) veya `3840 × 2160` (4K UHD) olarak ayarlanır.

`860 × 1720` boyutundaki mantıksal LED içeriği, GPU tarafından verilen `1920 × 1080` veya `3840 × 2160` sinyali içerisindeki kalibre edilmiş görüş alanına (Viewport Offset & Scaling) oturtulur. VX2000 katman kırpma (Layer Cropping) ve sinyal haritalama ayarları ile LED panele aktarılır.

---

## 3. Gereksinimler ve Resmi İndirme Bağlantıları

Uygulamanın çalışması ve derlenmesi için gerekli bileşenler:

### Sistem Gereksinimleri
- **İşletim Sistemi**: Windows 10 x64 (Versiyon 1809 ve üzeri) veya Windows 11 x64
- **Grafik Kartı**: NVIDIA Quadro P2000 veya 4 çıkış destekli NVIDIA GPU

### Çalıştırma Gereksinimleri (Hazır Paket İçin - Release Package)
Eğer derlenmiş yayın paketini (`.zip`) kullanacaksanız bilgisayarda bulunması gerekenler:

1. **.NET 8 Desktop Runtime (x64)**:
   - Uygulamanın Windows üzerinde çalışması için gereklidir.
   - [Resmi İndirme Bağlantısı (.NET 8 Desktop Runtime x64)](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Microsoft Edge WebView2 Runtime (x64)**:
   - Web sitesi ve altyazı akışlarının işlenmesi için gereklidir.
   - [Resmi İndirme Bağlantısı (WebView2 Evergreen Standalone / Bootstrapper x64)](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
3. **NVIDIA Quadro P2000 GPU Sürücüsü**:
   - 4 ekran desteği ve ekran yönetimi için güncel sürücü.
   - [Resmi İndirme Bağlantısı (NVIDIA Sürücü İndirme)](https://www.nvidia.com/Download/index.aspx)

### Kaynak Koddan Derleme Gereksinimleri (Build from Source)
Projeyi sıfırdan klonlayıp derleyecek geliştiriciler için:

1. **.NET 8 SDK (x64)**: Projeyi derlemek ve testleri çalıştırmak için.
   - [Resmi İndirme Bağlantısı (.NET 8 SDK)](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Git for Windows**: Depoyu klonlamak için.
   - [Resmi İndirme Bağlantısı (Git for Windows)](https://git-scm.com/download/win)

### Video Oynatma Motoru & Kodek Bilgisi
RejiDisplay v0.4, **WPF Native `MediaElement`** (Direct3D yüzey kompozisyonu ve Windows Media Foundation) motorunu kullanır.
- **Doğrudan Desteklenen Formatlar**: `MP4` (H.264 / AAC), `MOV`, `WMV`, `AVI`.
- **Ek Kodek Gerektirebilecek Formatlar**: `MKV`, `WebM` (Sistemde Windows Media Foundation / DirectShow kodekleri varsa oynatılır).
- *(Not: Projedeki eski LibVLCSharp paketleri, Win32 HWND airspace çakışmalarını ve bağımlılık çakışmalarını önlemek amacıyla temizlenmiştir).*

---

## 4. Kurulum Yöntemleri

### Yöntem 1 — Önerilen: Hazır Yayın Paketini Kullanma (Release ZIP)

Etkinlik bilgisayarında projenin derlenmiş sürümünü çalıştırmak için:

1. GitHub Release sayfasından veya `artifacts/` klasöründen **`RejiDisplay-v0.4.0-win-x64.zip`** arşivini indirin.
2. ZIP arşivini Hedef Bilgisayarda bir klasöre çıkarın (Örn: `C:\RejiDisplay`).
3. Bilgisayarda **.NET 8 Desktop Runtime (x64)** ve **WebView2 Runtime** kurulu olduğundan emin olun.
4. `RejiDisplay.exe` dosyasını çalıştırın.

---

### Yöntem 2 — Kaynak Koddan Klonlama ve Derleme

Geliştirme bilgisayarında veya test sisteminde kaynak koddan derlemek için aşağıdaki PowerShell komutlarını sırasıyla çalıştırın:

```powershell
# 1. Depoyu klonlayın
git clone https://github.com/Emirebo/reji-display.git

# 2. Proje klasörüne girin
cd reji-display

# 3. Yayınlanan v0.4.0 etiketine veya web-output dalına geçin
git checkout v0.4.0

# 4. Bağımlılıkları geri yükleyin
dotnet restore

# 5. Projeyi derleyin
dotnet build

# 6. Tüm birim testlerini çalıştırın
dotnet test

# 7. Uygulamayı başlatın
dotnet run --project src/RejiDisplay/RejiDisplay.csproj
```

---

## 5. Windows Çoklu Monitör Yapılandırması

Saha bilgisayarında sorunsuz yayın için Windows masaüstü ayarları:

1. **Masaüstünü Genişletin**:
   - `Win + P` kısayoluna basıp **Genişlet (Extend)** seçeneğini işaretleyin. Ekranları Asla Yineleme (Duplicate) modunda bırakmayın.
2. **Ekranları Numaralandırın ve Tanımlayın**:
   - Masaüstüne sağ tıklayıp **Görüntü Ayarları (Display Settings)** menüsünü açın.
   - **Tanımla (Identify)** butonuna basarak Windows'un ekranlara atadığı numaraları öğrenin.
3. **Operatör Ekranını Birincil Yapın**:
   - Operatörün kullandığı monitörü seçin ve **"Bunu ana ekranım yap" (Make this my main display)** kutucuğunu işaretleyin.
4. **Çözünürlük ve Ölçeklendirme**:
   - Tüm ekranların ölçeklendirmesini **%100** (Recommended) olarak ayarlayın.
   - Ekran çözünürlüklerini 60Hz olarak sabitleyin.
5. **Kablo Söküp Takma Durumu**:
   - Kablo çıkarıldığında veya bilgisayar yeniden başlatıldığında Windows ekran sıralamasını değiştirebilir. RejiDisplay menüsündeki `🔍 EKRANLARI NUMARALANDIR (IDENTIFY)` butonunu kullanarak ekran atamalarını kontrol edin.

---

## 6. İlk Çalıştırma ve Adım Adım Güvenli Test Prosedürü

Canlı etkinlik öncesinde izlenmesi gereken güvenli test adımları:

### Faz A: Ofis / Hazırlık Testi (Standart Monitörler İle)
1. Uygulamayı başlatın. Operatör arayüzünün birincil ekranda açıldığını doğrulayın.
2. `🖥️ REZERVE CENTER` menüsünden sunum için ayrılan ekranı seçin.
3. **LEFT LED** ve **RIGHT LED** kartlarından hedef ekranları atayın.
4. **Live Sync (Canlı Güncelle)** seçeneğinin **KAPALI** olduğunu kontrol edin.
5. **🎯 TEST PATTERN** butonuna basarak her iki karta da test deseni yükleyin.
6. `▶ UYGULA (APPLY TO LIVE)` butonuna basarak deseni yayına alın. Dört köşe etiketlerinin (TOP, BOTTOM, LEFT, RIGHT) doğru göründüğünü, kırpılma olmadığını doğrulayın.
7. Dikey (Portrait) bir görsel yükleyin. `Fit` modunda orijinal oran korunarak kenarlarda siyah bant oluştuğunu, görselin kırpılmadığını doğrulayın.
8. Bir MP4 video yükleyin. Videonun varsayılan olarak **Sessiz (Muted)** başladığını görün.
9. Web sitesi seçeneğine girin (`https://subtitles.live.com`). `🌐 YÜKLE` ve `▶ UYGULA` butonları ile simultane çeviri web yayınını test edin.
10. `⚫ SİYAH (BLACK)` butonuna basın; yayının anında karardığını doğrulayın. `🟡 GERİ YÜKLE (RESTORE)` butonuna basın; yayının kesintisiz geri geldiğini görün.

### Faz B: Saha Testi (NovaStar VX2000 Pro & LED Paneller İle)
1. Quadro P2000 çıkışlarını VX2000 Pro girişlerine bağlayın.
2. VX2000 ön ayarını (Preset) yükleyin.
3. RejiDisplay üzerinden Test Desenlerini yayına vererek LED panellerdeki sınır çizgilerini ve modül dizilimini doğrulayın.
4. Canlı Simultane Çeviri web sitesini yayına alarak altyazı akışını test edin.

---

## 7. Sorun Giderme (Troubleshooting)

| Sorun | Olası Neden | Çözüm |
| :--- | :--- | :--- |
| **Uygulama Açılmıyor / Çöküyor** | Eksik .NET 8 Desktop Runtime | .NET 8 Desktop Runtime (x64) paketini yükleyin. |
| **Web Sitesi Siyah / Boş Görünüyor** | Eksik WebView2 Runtime veya İnternet Yok | Microsoft Edge WebView2 Runtime yükleyin. İnternet bağlantısını ve URL formatını (`https://`) kontrol edin. |
| **Yayın Yanlış Ekrana Gidiyor** | Windows ekran numaraları değişmiş | `🔍 EKRANLARI NUMARALANDIR` butonuna basarak ekran atamalarını güncelleyin. |
| **Video İlerleme Çubuğu Hareket Etmiyor** | Video oynatma durdurulmuş | `▶ OYNAT` butonuna basın veya video dosya yolunu kontrol edin. |
| **Web Sitesi Ses Çıkarıyor** | Web Sessiz (Mute) kutusu işaretli değil | Web Kontrol panelindeki `🔇 Sessiz` seçeneğinin işaretli olduğunu kontrol edin. |
| **Görsel Kenarlardan Kırpılıyor** | Ölçekleme modu `Fill` veya `Custom` kalmış | Ölçek modunu `Fit` olarak değiştirin ve `↺ Konumu Sıfırla` butonuna basın. |
| **İkinci Bilgisayarda Ayarlar Yüklenmiyor** | Ekran GUID/Isimleri cihaz özelindedir | Yeni bilgisayarda hedef ekranları dropdown menüden bir kez yeniden seçip kaydedin. |

---

## 8. Konfigürasyon, Dosya Konumları ve Ayarlar

RejiDisplay çalışma verilerini aşağıdaki dizinlerde saklar:

- **Kullanıcı Ayarları Dosyası**:
  `%APPDATA%\RejiDisplay\settings.json`
  *(Tam Yol: `C:\Users\<KullanıcıAdı>\AppData\Roaming\RejiDisplay\settings.json`)*
  *(İçerik: Son kullanılan görsel/video/web yolları, kalibrasyon offsetleri, mekan ön ayarları).*
- **Web Tarayıcı Profil & Önbellek Verileri**:
  `%LOCALAPPDATA%\RejiDisplay\EBWebView`
  *(Tam Yol: `C:\Users\<KullanıcıAdı>\AppData\Local\RejiDisplay\EBWebView`)*
- **Geçici Test Deseni Görselleri**:
  `%TEMP%\TestPattern_*.png`
