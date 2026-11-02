# Parkology

Smartlogy için Android ve iOS hedefli, dikey Unity 6 (6000.6.1f1) bulmaca oyunu. Bundle ID: `com.smartlogy.parkology`. Ana dil İngilizce, Türkçe desteği var.

## Oyun

Oynanış, referans oyunla (Parking Loop) birebir aynı kurallara sahip; karakterler araba yerine yüzlü Lego bloklar.

- Otopark oyuncu öğrendikçe büyür: 1–2. bölüm 3x3 (2 şerit), 3–4. bölüm 4x4, 5. bölümden itibaren 5 sütun x 5 yuva. Sütunlar kuyruk gibi çalışır: bloklar arkadan (üstten) girer, önden (alttan) çıkar.
- Alttaki 3 şeritten birine dokununca en öndeki blok döngüye girer.
- Döngüdeki blok, en arkadaki bloğu kendi renginde olan bir sütuna üstten girer. Sütun doluysa öndeki blok döngüye itilir ve döngüdeki sayı değişmez. Sütunda boş yer varsa itme olmaz ve sayı bir azalır.
- Blok boş bir sütuna yalnızca kendi rengi hiçbir sütunun arkasında yoksa girer. Böylece bir renk iki boş sütuna bölünmez.
- Tamamen aynı renkle dolan sütun temizlenir. Her şey temizlenince bölüm biter. Gönderilecek blok kalmadığında kalanlar en fazla 2 sütunluksa döngü hızlanır ve son bloklar hızla yerine gider.
- Döngüde en fazla 5 blok olabilir. Döngü dolu ve hiçbir blok bir sütuna giremiyorsa bölüm kaybedilir.
- Gizemli (?) bloklar sütunun bir ucuna gelince rengini gösterir. 8. bölümden itibaren şeritlerde de gizemli bloklar var; öne gelince açılır.
- Jokerler:
  - **+1** (3. bölüm): döngü kapasitesini bir artırır.
  - **Çekiç** (5. bölüm): seçilen sütunun arkasına vurur, öndeki blok döngüye itilir. Hiçbir blok yok olmaz, çünkü her renk 5'in katı olmak zorunda.
  - **El** (7. bölüm): şeritlerdeki herhangi bir bloğu döngüye gönderir.

  Her joker açılınca 3 adet verilir, sonra 150 altın.

İlk açılışta ana menü atlanır: oyuncu doğrudan 1. bölümle başlar ve ilk 5 bölümü arka arkaya oynar, 5. bölümden sonra ana menüye geçer (`Save.Onboarded`). Bu bölümlerde alttan kayan maskotlu bir balon, dokunan el ve tahtadaki parlamalarla kurallar oyunda oldukları anda anlatılır (`App.Tutorial.cs`). 1–2. bölümlerde joker çubuğu gizlidir.

Bölümler oyunun hamleleri sondan başa uygulanarak üretilir, bu yüzden her bölümün en az bir çözümü vardır. Zorluk, rastgele oyunlarla ölçülen kazanma oranına göre ayarlanır.

## Proje yapısı

Sahnede yalnızca `App` bileşenli bir obje vardır; kamera, ışık, arayüz ve tüm 3D içerik koddan üretilir.

- `Assets/Parkology/Scripts/Game`: `LevelGenerator` (tohumlu, ters kurulumla çözülebilir), `GameBoard` (kuyruk sütunlar, döngü trafiği, kazanma/kaybetme, jokerler), `RingPath`.
- `Assets/Parkology/Scripts/Visual`: prosedürel tekerlekli blok mesh'leri (`MeshKit`), SDF ile çizilen yüz ifadeleri (`FaceAtlas`), `Buddy` (göz kırpma, ifade, squash), sahne, efektler.
- `Assets/Parkology/Scripts/UI`: splash (tren + yükleme çubuğu), ana sayfa (arkada otomatik oynayan demo), HUD, popup'lar, blok perdesi geçişi.
- `Assets/Parkology/Scripts/Core`: dil (`Assets/Resources/Localization/en.json`, `tr.json`), ses, haptik, kayıt.
- `Assets/Plugins/iOS`: Core Haptics desenleri (`PKHaptics.mm`), marka açılış ekranı devri (`PKLaunchScene.mm`).
- `Assets/Resources/Shaders/Plastic.shader`: oyuncak plastik görünümü.

## Varlıkları yeniden üretme

```bash
swift Tools/generate-art.swift      # logo, ikon, arayüz sprite'ları, splash treni
python3 Tools/generate-audio.py     # tüm efektler ve müzik (sentez, örnek kullanılmaz)
```

Font: Rammetto One (SIL OFL, `Assets/Resources/Fonts/OFL.txt`), tüm Türkçe karakterleri içerir.

## Unity splash ve marka

"Made with Unity" kapalıdır (`PlayerSettings.SplashScreen.show = false`, Android manifest'te de kapalı). iOS'ta açılışta gökyüzü + logo storyboard'u görünür ve oyun içi splash ilk kareyi çizene kadar ekranda kalır.

## Kontrol ve build

```bash
Unity -batchmode -projectPath . -executeMethod Parkology.Editor.SmokeCheck.Run -logFile -
```

1–150. bölümlerin üretildiğini doğrular, ardından 1–8. bölümleri gerçek oyun tahtasında otomatik oynar (kazanma ve kayıplar raporlanır). Oyuncu ilerlemesi yedeklenip geri yüklenir.

- iOS: `Parkology.Editor.MobileBuild.ExportIOS` (cihaz) / `ExportIOSSimulator`. App Store Connect yüklemesi için build numarası her seferinde artırılmalı: `-buildNumber N`. App Store adı "Parkology: Block Loop" (uygulama no 6829916996); "Parkology" adı başka bir uygulamaya ait, ikon altındaki ad yine Parkology.
- Android: `Parkology.Editor.MobileBuild.BuildAndroidAPK`
- Ayarlar ve sahne: `Parkology > Prepare Project` menüsü

## Açık işler

- Gizlilik/koşullar/destek bağlantıları `https://www.smartlogy.com.tr/Parkology/...` adreslerine gider; bu sayfalar henüz oluşturulmalı.
- Gerçek satın alma ve reklam yok; altın ve jokerler cihazda `PlayerPrefs` içinde.
- Bölüm zorluğu ve ekonomi gerçek oyuncu testleriyle ayarlanmalı.
