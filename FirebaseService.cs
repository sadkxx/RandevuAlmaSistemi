/*using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Auth;
using Spectre.Console;

namespace RandevuSistemi
{
    public class FirebaseService
    {
        private readonly FirebaseClient firebase;
        private readonly FirebaseAuthProvider authProvider;
        
        // API Key'i environment variable olarak alın
        private static string FirebaseApiKey => Environment.GetEnvironmentVariable("FIREBASE_API_KEY") ?? 
            throw new Exception("Firebase API Key bulunamadı!");
        private static string FirebaseUrl => Environment.GetEnvironmentVariable("FIREBASE_URL") ?? 
            throw new Exception("Firebase URL bulunamadı!");

        public FirebaseService()
        {
            var apiKey = Environment.GetEnvironmentVariable("FIREBASE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("Firebase API Key bulunamadı! Environment variable'ı kontrol edin.");
            }

            firebase = new FirebaseClient(FirebaseUrl);
            authProvider = new FirebaseAuthProvider(new FirebaseConfig(FirebaseApiKey));
        }

        public async Task<bool> KullaniciKaydet(Kullanici kullanici)
        {
            try
            {
                // Önce Firebase Auth'a kaydet
                var auth = await authProvider.CreateUserWithEmailAndPasswordAsync(kullanici.Email, kullanici.Sifre);
                if (auth == null) return false;

                // Realtime Database'e kaydet (şifre hariç)
                var kullaniciData = new
                {
                    kullanici.TC,
                    kullanici.Isim,
                    kullanici.Soyisim,
                    kullanici.DogumTarihi,
                    kullanici.TelefonNo,
                    kullanici.Email,
                    kullanici.SonIptalTarihi
                };

                await firebase.Child("kullanicilar").Child(kullanici.TC).PutAsync(kullaniciData);
                return true;
            }
            catch (FirebaseAuthException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                switch (ex.Reason)
                {
                    case AuthErrorReason.EmailExists:
                        Console.WriteLine("Bu email adresi zaten kullanımda!");
                        break;
                    case AuthErrorReason.WeakPassword:
                        Console.WriteLine("Şifre çok zayıf. En az 6 karakter olmalı!");
                        break;
                    default:
                        Console.WriteLine($"Kayıt hatası: {ex.Message}");
                        break;
                }
                Console.ForegroundColor = ConsoleColor.White;
                return false;
            }
        }

        public async Task<bool> GirisYap(string email, string sifre)
        {
            try
            {
                var auth = await authProvider.SignInWithEmailAndPasswordAsync(email, sifre);
                return auth != null;
            }
            catch (FirebaseAuthException)
            {
                return false;
            }
        }

        // Kullanıcı işlemleri
        public async Task<Kullanici> KullaniciGetir(string tc)
        {
            try
            {
                var kullanici = await firebase
                    .Child("kullanicilar")
                    .Child(tc)
                    .OnceSingleAsync<Kullanici>();
                return kullanici;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> SendPasswordResetEmail(string email)
        {
            try
            {
                // Önce kullanıcının kayıtlı olup olmadığını kontrol edelim
                var kullanicilar = await firebase
                    .Child("kullanicilar")
                    .OnceAsync<Kullanici>();
                
                var kullanici = kullanicilar
                    .FirstOrDefault(k => k.Object.Email == email)?.Object;

                if (kullanici == null)
                {
                    AnsiConsole.MarkupLine("[red]Bu email adresi ile kayıtlı kullanıcı bulunamadı![/]");
                    return false;
                }

                // Firebase Auth üzerinden şifre sıfırlama maili gönder
                await authProvider.SendPasswordResetEmailAsync(email);

                return true;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Şifre sıfırlama hatası: {ex.Message}[/]");
                return false;
            }
        }

        // Randevu işlemleri
        public async Task<bool> RandevuKaydet(string tc, Randevu randevu)
        {
            try
            {
                randevu.Id = Guid.NewGuid().ToString();
                await firebase
                    .Child("randevular")
                    .Child(tc)
                    .PostAsync(randevu);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<Randevu>> RandevulariGetir(string tc)
        {
            try
            {
                var randevular = await firebase
                    .Child("randevular")
                    .Child(tc)
                    .OnceAsync<Randevu>();
                
                return randevular.Select(r => r.Object).ToList();
            }
            catch
            {
                return new List<Randevu>();
            }
        }

        public async Task<bool> RandevuIptal(string tc, string randevuId)
        {
            try
            {
                var randevular = await firebase
                    .Child("randevular")
                    .Child(tc)
                    .OnceAsync<Randevu>();

                var silinecekRandevu = randevular
                    .FirstOrDefault(r => r.Object.Id == randevuId);

                if (silinecekRandevu != null)
                {
                    await firebase
                        .Child("randevular")
                        .Child(tc)
                        .Child(silinecekRandevu.Key)
                        .DeleteAsync();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> TelefonNoKullaniliyor(string telefonNo)
        {
            try
            {
                var kullanicilar = await firebase
                    .Child("kullanicilar")
                    .OnceAsync<Kullanici>();
                
                return kullanicilar.Any(k => k.Object.TelefonNo == telefonNo);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EmailKullaniliyor(string email)
        {
            try
            {
                var kullanicilar = await firebase
                    .Child("kullanicilar")
                    .OnceAsync<Kullanici>();
                
                return kullanicilar.Any(k => k.Object.Email == email);
            }
            catch
            {
                return false;
            }
        }

        // Giriş yaparken hem Auth hem de Realtime Database kontrolü
        public async Task<Kullanici> GirisKontrol(string email, string sifre)
        {
            try
            {
                // Firebase Authentication ile giriş kontrolü
                var authResult = await authProvider.SignInWithEmailAndPasswordAsync(email, sifre);
                
                if (authResult?.User != null)
                {
                    // Email ile eşleşen kullanıcıyı Realtime Database'den al
                    var kullanicilar = await firebase
                        .Child("kullanicilar")
                        .OnceAsync<Kullanici>();
                    
                    var kullanici = kullanicilar
                        .FirstOrDefault(k => k.Object.Email == email)?.Object;

                    if (kullanici != null)
                    {
                        // Kullanıcı nesnesini güncelleyip döndür
                        kullanici.Sifre = sifre; // Yeni şifreyi güncelle
                        return kullanici;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Giriş hatası: {ex.Message}[/]");
                return null;
            }
        }

        public async Task<List<Randevu>> TumRandevulariGetir()
        {
            try
            {
                var randevular = await firebase
                    .Child("randevular")
                    .OnceAsync<Dictionary<string, Randevu>>();

                // Tüm TC'lerdeki tüm randevuları düz bir listeye çevir
                var tumRandevular = new List<Randevu>();
                foreach (var tcRandevular in randevular)
                {
                    tumRandevular.AddRange(tcRandevular.Object.Values);
                }

                return tumRandevular;
            }
            catch
            {
                return new List<Randevu>();
            }
        }

        public async Task<Kullanici> KullaniciGetirByEmail(string email)
        {
            try
            {
                var kullanicilar = await firebase
                    .Child("kullanicilar")
                    .OnceAsync<Kullanici>();
                
                return kullanicilar
                    .FirstOrDefault(k => k.Object.Email == email)?.Object;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Kullanici> TCileGirisYap(string tc, string sifre)
        {
            try
            {
                // TC ile kullanıcıyı bul
                var kullanici = await KullaniciGetir(tc);
                
                if (kullanici != null)
                {
                    // Kullanıcının email'i ile Firebase Auth'da doğrulama yap
                    var authResult = await authProvider.SignInWithEmailAndPasswordAsync(kullanici.Email, sifre);
                    
                    if (authResult?.User != null)
                    {
                        return kullanici;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
} */




using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Auth;
using Spectre.Console;

namespace RandevuSistemi
{
    public class FirebaseService
    {
        private readonly FirebaseClient firebase;
        private readonly FirebaseAuthProvider authProvider;

        // API Key'i environment variable olarak alın
        private static string FirebaseApiKey => Environment.GetEnvironmentVariable("FIREBASE_API_KEY") ??
            throw new Exception("Firebase API Key bulunamadı!");
        private static string FirebaseUrl => Environment.GetEnvironmentVariable("FIREBASE_URL") ??
            throw new Exception("Firebase URL bulunamadı!");

        public FirebaseService()
        {
            var apiKey = Environment.GetEnvironmentVariable("FIREBASE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("Firebase API Key bulunamadı! Environment variable'ı kontrol edin.");
            }

            firebase = new FirebaseClient(FirebaseUrl);
            authProvider = new FirebaseAuthProvider(new FirebaseConfig(FirebaseApiKey));
        }

        public async Task<bool> KullaniciKaydet(Kullanici kullanici)
        {
            try
            {
                // Önce Firebase Auth'a kaydet
                var auth = await authProvider.CreateUserWithEmailAndPasswordAsync(kullanici.Email, kullanici.Sifre);
                if (auth == null) return false;

                // Realtime Database'e kaydet (şifre hariç)
                var kullaniciData = new
                {
                    kullanici.TC,
                    kullanici.Isim,
                    kullanici.Soyisim,
                    kullanici.DogumTarihi,
                    kullanici.TelefonNo,
                    kullanici.Email,
                    kullanici.SonIptalTarihi,
                    YetkiSeviyesi = "User"  // Varsayılan olarak User yetkisi ver
                };

                await firebase.Child("kullanicilar").Child(kullanici.TC).PutAsync(kullaniciData);
                return true;
            }
            catch (FirebaseAuthException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                switch (ex.Reason)
                {
                    case AuthErrorReason.EmailExists:
                        Console.WriteLine("Bu email adresi zaten kullanımda!");
                        break;
                    case AuthErrorReason.WeakPassword:
                        Console.WriteLine("Şifre çok zayıf. En az 6 karakter olmalı!");
                        break;
                    default:
                        Console.WriteLine($"Kayıt hatası: {ex.Message}");
                        break;
                }
                Console.ForegroundColor = ConsoleColor.White;
                return false;
            }
        }

        public async Task<bool> GirisYap(string email, string sifre)
        {
            try
            {
                var auth = await authProvider.SignInWithEmailAndPasswordAsync(email, sifre);
                return auth != null;
            }
            catch (FirebaseAuthException)
            {
                return false;
            }
        }

        // Kullanıcı işlemleri
        public async Task<Kullanici> KullaniciGetir(string tc)
        {
            try
            {
                var kullanici = await firebase
                    .Child("kullanicilar")
                    .Child(tc)
                    .OnceSingleAsync<dynamic>();

                if (kullanici == null) return null;

                try
                {
                    var yeniKullanici = new Kullanici(
                        tc,
                        kullanici.Isim.ToString(),
                        kullanici.Soyisim.ToString(),
                        kullanici.DogumTarihi.ToString(),
                        "", // Şifre boş bırakılıyor
                        kullanici.TelefonNo.ToString()
                    )
                    {
                        Email = kullanici.Email.ToString(),
                        YetkiSeviyesi = kullanici.YetkiSeviyesi?.ToString() ?? "User"
                    };

                    return yeniKullanici;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> SendPasswordResetEmail(string email)
        {
            try
            {
                // Önce kullanıcının kayıtlı olup olmadığını kontrol edelim
                var kullanicilar = await firebase
                    .Child("kullanicilar")
                    .OnceAsync<Kullanici>();

                var kullanici = kullanicilar
                    .FirstOrDefault(k => k.Object.Email == email)?.Object;

                if (kullanici == null)
                {
                    AnsiConsole.MarkupLine("[red]Bu email adresi ile kayıtlı kullanıcı bulunamadı![/]");
                    return false;
                }

                // Firebase Auth üzerinden şifre sıfırlama maili gönder
                await authProvider.SendPasswordResetEmailAsync(email);

                return true;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Şifre sıfırlama hatası: {ex.Message}[/]");
                return false;
            }
        }

        // Randevu işlemleri
        public async Task<bool> RandevuKaydet(string tc, Randevu randevu)
        {
            try
            {
                randevu.Id = Guid.NewGuid().ToString();
                await firebase
                    .Child("randevular")
                    .Child(tc)
                    .PostAsync(randevu);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<Randevu>> RandevulariGetir(string tc)
        {
            try
            {
                var randevular = await firebase
                    .Child("randevular")
                    .Child(tc)
                    .OnceAsync<Randevu>();

                return randevular.Select(r => r.Object).ToList();
            }
            catch
            {
                return new List<Randevu>();
            }
        }

        public async Task<bool> RandevuIptal(string tc, string randevuId)
        {
            try
            {
                var randevular = await firebase
                    .Child("randevular")
                    .Child(tc)
                    .OnceAsync<Randevu>();

                var silinecekRandevu = randevular
                    .FirstOrDefault(r => r.Object.Id == randevuId);

                if (silinecekRandevu != null)
                {
                    await firebase
                        .Child("randevular")
                        .Child(tc)
                        .Child(silinecekRandevu.Key)
                        .DeleteAsync();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> TelefonNoKullaniliyor(string telefonNo)
        {
            try
            {
                var kullanicilar = await firebase
                    .Child("kullanicilar")
                    .OnceAsync<Kullanici>();

                return kullanicilar.Any(k => k.Object.TelefonNo == telefonNo);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EmailKullaniliyor(string email)
        {
            try
            {
                var kullanicilar = await firebase
                    .Child("kullanicilar")
                    .OnceAsync<Kullanici>();

                return kullanicilar.Any(k => k.Object.Email == email);
            }
            catch
            {
                return false;
            }
        }

        // Giriş yaparken hem Auth hem de Realtime Database kontrolü
        public async Task<Kullanici> GirisKontrol(string email, string sifre)
        {
            try
            {
                // Firebase Authentication ile giriş kontrolü
                var authResult = await authProvider.SignInWithEmailAndPasswordAsync(email, sifre);

                if (authResult?.User != null)
                {
                    // Email ile eşleşen kullanıcıyı Realtime Database'den al
                    var kullanicilar = await firebase
                        .Child("kullanicilar")
                        .OnceAsync<Kullanici>();

                    var kullanici = kullanicilar
                        .FirstOrDefault(k => k.Object.Email == email)?.Object;

                    if (kullanici != null)
                    {
                        // Kullanıcı nesnesini güncelleyip döndür
                        kullanici.Sifre = sifre; // Yeni şifreyi güncelle
                        return kullanici;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Giriş hatası: {ex.Message}[/]");
                return null;
            }
        }

        public async Task<List<Randevu>> TumRandevulariGetir()
        {
            try
            {
                var randevular = await firebase
                    .Child("randevular")
                    .OnceAsync<Dictionary<string, Randevu>>();

                // Tüm TC'lerdeki tüm randevuları düz bir listeye çevir
                var tumRandevular = new List<Randevu>();
                foreach (var tcRandevular in randevular)
                {
                    tumRandevular.AddRange(tcRandevular.Object.Values);
                }

                return tumRandevular;
            }
            catch
            {
                return new List<Randevu>();
            }
        }

        public async Task<Kullanici> KullaniciGetirByEmail(string email)
        {
            try
            {
                var kullanicilar = await firebase
                    .Child("kullanicilar")
                    .OnceAsync<Kullanici>();

                return kullanicilar
                    .FirstOrDefault(k => k.Object.Email == email)?.Object;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Kullanici> TCileGirisYap(string tc, string sifre)
        {
            try
            {
                // TC ile kullanıcıyı bul
                var kullanici = await KullaniciGetir(tc);

                if (kullanici != null)
                {
                    try
                    {
                        // Kullanıcının email'i ile Firebase Auth'da doğrulama yap
                        var authResult = await authProvider.SignInWithEmailAndPasswordAsync(kullanici.Email, sifre);

                        if (authResult?.User != null)
                        {
                            kullanici.Sifre = sifre; // Şifreyi set et
                            return kullanici;
                        }
                    }
                    catch (FirebaseAuthException)
                    {
                        // Auth hatası durumunda null dön
                        return null;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<Kullanici>> TumKullanicilariGetir()
        {
            try
            {
                var kullanicilar = await firebase
                    .Child("kullanicilar")
                    .OnceAsync<Kullanici>();

                return kullanicilar.Select(k => k.Object).ToList();
            }
            catch
            {
                return new List<Kullanici>();
            }
        }

        public async Task<bool> KullaniciSil(string tc)
        {
            try
            {
                // Önce kullanıcının randevularını sil
                await firebase
                    .Child("randevular")
                    .Child(tc)
                    .DeleteAsync();

                // Sonra kullanıcıyı sil
                await firebase
                    .Child("kullanicilar")
                    .Child(tc)
                    .DeleteAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> YetkiGuncelle(string tc, string yeniYetki)
        {
            try
            {
                var kullanici = await KullaniciGetir(tc);
                if (kullanici == null) return false;

                // Sadece yetki alanını güncelle
                await firebase
                    .Child("kullanicilar")
                    .Child(tc)
                    .Child("YetkiSeviyesi")
                    .PutAsync(yeniYetki);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}