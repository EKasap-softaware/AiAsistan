namespace Asistan.Application.Bellek;

// appsettings.json > "Hafiza" bölümünden okunur.
// Örnek akış (varsayılanlarla): Özetlenmemiş mesaj sayısı 20'yi aşınca, en yeni 10 mesaj
// olduğu gibi kalır, daha eskiler özete çevrilir.
public record HafizaAyarlari(
    int PencereBoyutu = 10,   // Özetleme sonrası prompt'ta ham olarak kalacak mesaj sayısı
    int OzetEsigi = 20,       // Özetlenmemiş mesaj sayısı bunu aşınca özetleme tetiklenir
    int MaksHafiza = 20,      // Prompt'a en fazla kaç uzun süreli hafıza girer
    string HizliModel = "hizli");  // Özetleme ve hafıza çıkarma için ucuz model
