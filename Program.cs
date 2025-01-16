/*using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Spectre.Console;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Auth;

namespace RandevuSistemi
{
    public class Kullanici
    {
        public string TC { get; set; }
        public string Isim { get; set; }
        public string Soyisim { get; set; }
        public string DogumTarihi { get; set; }
        public string Sifre { get; set; }
        public string TelefonNo { get; set; }
        public string Email { get; set; }
        public DateTime? SonIptalTarihi { get; set; } // ? kullanılmasının sebebi nullable olması

        public Kullanici(string tc, string isim, string soyisim, string dogumTarihi, string sifre, string telefonNo) 
        {
            TC = tc; 
            Isim = isim;
            Soyisim = soyisim;
            DogumTarihi = dogumTarihi;
            Sifre = sifre;
            TelefonNo = telefonNo;
            SonIptalTarihi = null;
        }
    }

    // Kalıtım alınan sınıf
    public class AdminKullanici : Kullanici
    {
        public string YetkiSeviyesi { get; set; }

        // AdminKullanici sınıfının constructor'ı (yapıcı metot)
        public AdminKullanici(string tc, string isim, string soyisim, string dogumTarihi, string sifre, string yetkiSeviyesi)
            : base(tc, isim, soyisim, dogumTarihi, sifre, "")
        {
            YetkiSeviyesi = yetkiSeviyesi;
        }

        // Yetki verme fonksiyonu
        public void YetkiVer()
        {
            Console.WriteLine("Yetki verildi.");
        }

        //Henüz yapılmadı ancak yetkili birimler oluşturulduğunda buraya yönlendirilecek.
    }

    public class KullaniciYonetimi : KullaniciBase, IRandevuIslemleri
    {
        //Kapsülleme için private kullanıldı.
        private List<Kullanici> KullaniciListesi = new List<Kullanici>();
        private Kullanici aktifKullanici;
        private readonly FirebaseService _firebaseService;

        public KullaniciYonetimi()
        {
            _firebaseService = new FirebaseService();
        }

        //TC Doğrulama fonksiyonu
        private bool ValidateTC(string tc)
        {
            if (tc.Length != 11 || !long.TryParse(tc, out _))
            {
                ClearLastLines(1);
                Console.WriteLine("TC Kimlik Numarası 11 haneli rakamlardan oluşmalıdır! Tekrar deneyiniz.\n");
                return false;
            }
            return true;
        }

        //TC doğru girme fonksiyonu
        private string ReadTC()
        {
            string tc = "";
            ConsoleKeyInfo key;

            while (true)
            {
                key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (key.Key == ConsoleKey.Backspace && tc.Length > 0)
                {
                    tc = tc.Remove(tc.Length - 1);
                    Console.Write("\b \b");
                }
                else if (char.IsDigit(key.KeyChar) && tc.Length < 11)
                {
                    tc += key.KeyChar;
                    Console.Write(key.KeyChar);
                }
            }
            return tc;
        }

        private bool ValidateIsimSoyisim(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !Regex.IsMatch(text, @"^[a-zA-ZğüşıöçĞÜŞİÖÇ\s]+$"))
            {
                ClearLastLines(1);
                Console.WriteLine("İsim/Soyisim sadece harflerden oluşmalıdır!");
                return false;
            }
            return true;
        }

        private bool ValidateDogumTarihi(string tarih)
        {
            if (!Regex.IsMatch(tarih, @"^\d{2}/\d{2}/\d{4}$"))
            {
                ClearLastLines(1);
                Console.WriteLine("Doğum tarihi GG/AA/YYYY formatında olmalıdır!");
                return false;
            }

            try
            {
                string[] parts = tarih.Split('/');
                int gun = int.Parse(parts[0]);
                int ay = int.Parse(parts[1]);
                int yil = int.Parse(parts[2]);

                if (0 >= gun || gun > 31)
                {
                    if (0 >= ay || ay > 12)
                    {
                        if (yil < 1900 || yil > DateTime.Now.Year)
                        {
                            ClearLastLines(2);
                            Console.WriteLine("Gün, ay ve yıl yanlış girildi! Tekrar Deneyiniz.");
                            return false;
                        }
                        ClearLastLines(2);
                        Console.WriteLine("Gün ve ay yanlış girildi! Tekrar deneyiniz.");
                        return false;
                    }
                    ClearLastLines(2);
                    Console.WriteLine("Gün yanlış girildi! Tekrar Deneyiniz.");
                    return false;
                }

                if (0 >= ay || ay > 12)
                {
                    if (yil < 1900 || yil > DateTime.Now.Year)
                    {
                        ClearLastLines(2);
                        Console.WriteLine("Ay ve yıl yanlış girildi! Tekrar Deneyiniz.");
                        return false;
                    }
                    ClearLastLines(2);
                    Console.WriteLine("Hatalı ay girişi! Tekrar deneyiniz.");
                    return false;
                }

                if (yil < 1900 || yil > DateTime.Now.Year)
                {
                    ClearLastLines(2);
                    Console.WriteLine("Hatalı yıl girişi! Tekrar deneyiniz.");
                    return false;
                }

                DateTime dt = new DateTime(yil, ay, gun);
                if (dt > DateTime.Now)
                {
                    ClearLastLines(2);
                    Console.WriteLine("Geçersiz doğum tarihi!");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Geçersiz tarih! Hata : ", ex.Message);
                return false;
            }
        }

        private bool ValidateSifre(string sifre)
        {
            if (sifre.Length < 6)
            {
                Console.WriteLine("Şifre en az 6 karakterden oluşmalıdır!");
                return false;
            }
            // İzin verilen karakterler dışında herhangi bir karakter bulunup bulunmadığının kontrolü
            if (!Regex.IsMatch(sifre, @"^[A-Za-z0-9ğüşıöçĞÜŞİÖÇ.]+$"))
            {
                AnsiConsole.MarkupLine("[red]Şifre sadece büyük/küçük harfler, sayılar ve nokta (.) içerebilir![/]");
                return false;
            }
            return true;
        }

        private string ReadPassword()
        {
            string sifre = "";
            ConsoleKeyInfo key;

            while (true)
            {
                key = Console.ReadKey(true);



                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write("Şifre: " + new string('*', sifre.Length));
                    Console.WriteLine();
                    break;
                }
                else if (key.Key == ConsoleKey.Backspace && sifre.Length > 0)
                {
                    sifre = sifre.Remove(sifre.Length - 1);
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    sifre += key.KeyChar;
                    Console.Write(key.KeyChar);
                }
            }
            return sifre;
        }

        private bool ValidateTelefon(string telefon)
        {
            if (!Regex.IsMatch(telefon, @"^5[0-9]{9}$"))
            {
                ClearLastLines(1);
                Console.WriteLine("Telefon numarası 5XX XXX XX XX formatında olmalıdır!");
                return false;
            }
            return true;
        }

        private bool ValidateEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                if (!email.EndsWith(".com") && !email.Contains("@"))
                {
                    AnsiConsole.MarkupLine("[red]Geçersiz email formatı![/]");
                    return false;
                }
                return addr.Address == email;
            }
            catch
            {
                AnsiConsole.MarkupLine("[red]Geçersiz email formatı![/]");
                return false;
            }
        }

        public async Task KayitOl()
        {
            string tc, isim, soyisim, dogumTarihi, sifre;

            Console.Clear();
            AnsiConsole.Write(new Rule("[cyan]Kayıt Ol[/]").RuleStyle("grey").Centered());
            AnsiConsole.MarkupLine("\n[yellow]Ana menüye dönmek için TC alanına 0 giriniz.[/]\n");

            bool tcKayitli;
            do
            {
                do
                {
                    Console.Write("TC Kimlik Numarası: ");
                    tc = ReadTC();

                    if (tc == "0")
                    {
                        Console.Clear();
                        return;
                    }

                } while (!ValidateTC(tc));

                // TC'nin veritabanında varlığının kontrolü
                var mevcutKullanici = await _firebaseService.KullaniciGetir(tc);
                tcKayitli = mevcutKullanici != null;
                
                if (tcKayitli)
                {
                    AnsiConsole.MarkupLine("[red]Bu TC kimlik numarası zaten kayıtlı! Lütfen başka bir TC giriniz.[/]");
                    Thread.Sleep(2000);
                }
            } while (tcKayitli);

            do
            {
                Console.Write("\nİsim: ");
                isim = Console.ReadLine();
            } while (!ValidateIsimSoyisim(isim));

            do
            {
                Console.Write("\nSoyisim: ");
                soyisim = Console.ReadLine();
            } while (!ValidateIsimSoyisim(soyisim));

            do
            {
                Console.Write("\nDoğum Tarihi (GG/AA/YYYY): ");
                dogumTarihi = Console.ReadLine();
            } while (!ValidateDogumTarihi(dogumTarihi));

            do
            {
                Console.Write("\nŞifre (en az 6 karakter): ");
                sifre = ReadPassword();
            } while (!ValidateSifre(sifre));

            // Telefon numarasının kayıtlı olup olmadığının kontrolü
            string telefonNo;
            bool telefonKayitli;
            do
            {
                do
                {
                    Console.Write("\nTelefon Numarası (5XX XXX XX XX): +90");
                    telefonNo = Console.ReadLine();
                } while (!ValidateTelefon(telefonNo));

                telefonNo = "+90" + telefonNo;
                telefonKayitli = await _firebaseService.TelefonNoKullaniliyor(telefonNo);
                
                if (telefonKayitli)
                {
                    AnsiConsole.MarkupLine("[red]Bu telefon numarası zaten kayıtlı! Lütfen başka bir numara giriniz.[/]");
                    telefonNo = telefonNo.Substring(3); // +90'ı kaldır
                }
            } while (telefonKayitli);

            // Email'in veritabanında varlığının kontrolü
            string email;
            bool emailKayitli;
            do
            {
                do
                {
                    Console.Write("\nEmail Adresi: ");
                    email = Console.ReadLine();
                } while (!ValidateEmail(email));

                emailKayitli = await _firebaseService.EmailKullaniliyor(email);
                
                if (emailKayitli)
                {
                    AnsiConsole.MarkupLine("[red]Bu email adresi zaten kayıtlı! Lütfen başka bir email giriniz.[/]");
                }
            } while (emailKayitli);

            Kullanici yeniKullanici = new Kullanici(tc, isim, soyisim, dogumTarihi, sifre, telefonNo)
            {
                Email = email
            };

            var kayitBasarili = await _firebaseService.KullaniciKaydet(yeniKullanici);
            if (!kayitBasarili)
            {
                Console.WriteLine("Kayıt sırasında bir hata oluştu!");
                return;
            }

            aktifKullanici = yeniKullanici;
            Console.Clear();
            Console.Write("Kayıt başarılı! 3 saniye içinde sisteme giriş yapılıyor ");
            char[] loadingChars = { '|', '/', '-', '\\' };
            int charIndex = 0;

            for (int i = 0; i < 20; i++) 
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(loadingChars[charIndex]);
                Thread.Sleep(100);
                Console.Write("\b"); // Bir önceki karakteri sil
                charIndex = (charIndex + 1) % loadingChars.Length; // Sonraki karakteri seç
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.WriteLine("\n");
            // Console.Write("Kayıt başarılı! 3 saniye içinde sisteme giriş yapılıyor");
            // for (int i = 0; i < 3; i++)
            // {
            //     Thread.Sleep(300);
            //     Console.ForegroundColor = ConsoleColor.Red;
            //     Console.Write(".");
            // }
            // Console.Write("\b\b\b");
            // for (int i = 0; i < 3; i++)
            // {
            //     Thread.Sleep(300);
            //     Console.ForegroundColor = ConsoleColor.Green;
            //     Console.Write(".");
            // }
            // Console.Write("\b\b\b");
            // for (int i = 0; i < 3; i++)
            // {
            //     Thread.Sleep(300);
            //     Console.ForegroundColor = ConsoleColor.Cyan;
            //     Console.Write(".");
            // }
            // Console.ForegroundColor = ConsoleColor.White;
            // Console.WriteLine("\n");
        }


        public abstract class KullaniciBase
        {
            public abstract Task GirisYap();
            public abstract void CikisYap();
            public abstract void ProfiliGoruntule();
        }

        public async Task<Kullanici> GirisYap()
        {
            while (true)
            {
                Console.Clear();
                AnsiConsole.Write(new Rule("[cyan]Giriş Yap[/]").RuleStyle("grey").Centered());
                AnsiConsole.MarkupLine("\n[yellow]Ana menüye dönmek için TC alanına 0 giriniz.[/]\n");

                Console.Write("TC Kimlik Numarası: ");
                string tc = ReadTC();

                if (tc == "0")
                {
                    Console.Clear();
                    return null;
                }

                if (!ValidateTC(tc))
                {
                    AnsiConsole.MarkupLine("[red]Geçersiz TC Kimlik Numarası! Tekrar deneyiniz.[/]");
                    Thread.Sleep(2000);
                    continue;
                }

                Console.Write("Şifre: ");
                string sifre = ReadPassword();

                var kullanici = await _firebaseService.TCileGirisYap(tc, sifre);

                if (kullanici != null)
                {
                    Console.WriteLine($"Giriş başarılı! Hoş geldiniz, {kullanici.Isim} {kullanici.Soyisim}");
                    aktifKullanici = kullanici;
                    Thread.Sleep(1000);
                    return kullanici;
                }

                AnsiConsole.MarkupLine("[red]TC Kimlik No veya şifre hatalı! Tekrar deneyiniz.[/]");
                Thread.Sleep(2000);
            }
        }

        public void CikisYap()
        {
            aktifKullanici = null;
            Console.WriteLine("Çıkış yapıldı.");
        }

        public Kullanici GetAktifKullanici()
        {
            return aktifKullanici;
        }

        public void ProfiliGoruntule()
        {
            if (aktifKullanici == null)
            {
                Console.WriteLine("Profilinizi görüntülemek için giriş yapmanız gerekiyor.");
                return;
            }

            Console.WriteLine("\nProfil Bilgileriniz:");
            Console.WriteLine($"TC: {aktifKullanici.TC}");
            Console.WriteLine($"İsim: {aktifKullanici.Isim}");
            Console.WriteLine($"Soyisim: {aktifKullanici.Soyisim}");
            Console.WriteLine($"Doğum Tarihi: {aktifKullanici.DogumTarihi}");
            Console.WriteLine("\nAna menüye dönmek için bir tuşa basınız...");
            Console.ReadKey();
            Console.Clear();
        }

        public async Task SifremiUnuttum()
        {
            Console.Write("Email Adresiniz: ");
            string email = Console.ReadLine();

            if (!ValidateEmail(email))
            {
                return;
            }

            var resetGonderildi = await _firebaseService.SendPasswordResetEmail(email);
            if (resetGonderildi)
            {
                AnsiConsole.MarkupLine($"""
                    [lightgreen]Şifre sıfırlama bağlantısı email adresinize gönderildi.[/]
                    [yellow]Lütfen mail kutunuzu kontrol ediniz.[/]
                    [red]Not:[/] Spam klasörünü kontrol etmeyi unutmayınız.
                    """);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Şifre sıfırlama maili gönderilirken bir hata oluştu![/]");
            }
        }


        // Son eklenen satırları temizlemek için kullanılan fonksiyon
        static void ClearLastLines(int lineCount)
        {
            int currentCursorTop = Console.CursorTop;

            for (int i = 0; i < lineCount; i++)
            {
                Console.SetCursorPosition(0, currentCursorTop - 1);
                Console.Write(new string(' ', Console.WindowWidth));
                currentCursorTop--;
            }

            Console.SetCursorPosition(0, currentCursorTop);
        }
    }

    // Normal randevu için temel sınıf
    public class Randevu
    {
        public string Id { get; set; }
        public string Bolum { get; set; }
        public string Hastane { get; set; }
        public string Doktor { get; set; }
        public string Saat { get; set; }
        public DateTime RandevuTarihi { get; set; }

        // Kurucu metot
        public Randevu(string bolum, string hastane, string doktor)
        {
            Bolum = bolum;
            Hastane = hastane;
            Doktor = doktor;
        }
    }

    // Acil randevular için özel sınıf
    public class AcilRandevu : Randevu
    {
        public string AciliyetDurumu { get; set; }
        public bool OncelikliHasta { get; set; }

        public AcilRandevu(string bolum, string hastane, string doktor, string aciliyetDurumu)
            : base(bolum, hastane, doktor)
        {
            AciliyetDurumu = aciliyetDurumu;
            OncelikliHasta = true;
        }
    }


    public class RandevuSistemi
    {
        //İç içe geçmiş dictionaryler ile hastaneler ve bölümler arasında ilişki
        private Dictionary<string, Dictionary<string, List<string>>> HastaneBolumleri = new Dictionary<string, Dictionary<string, List<string>>>
        {
            { "Beykoz Devlet Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Dahiliye", new List<string> { "Dr. Ahmet Aksöz", "Dr. Ayşe Özcan" } },
                    { "Ortopedi", new List<string> { "Dr. Ozan Öztürk", "Dr. Can Yıldız" } },
                    { "Kardiyoloji", new List<string> { "Dr. Mehmet Gündüz", "Dr. Elif Eda Yeşir" } }
                }
            },
            { "Medeniyet Üniversitesi Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Dahiliye", new List<string> { "Dr. Emir Yağız Çermik" } },
                    { "Ortopedi", new List<string> { "Dr. Onur Gür", "Dr. Nusret Yavuz" } },
                    { "Göz Hastalıkları", new List<string> { "Dr. Eda Taşlı" } }
                }
            },
            { "Üsküdar Devlet Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Genel Cerrahi", new List<string> { "Dr. Harun Yılmaz", "Dr. Taner Yiğit" } },
                    { "Göz Hastalıkları", new List<string> { "Dr. Nazlı Sözen" } },
                    { "Dermatoloji", new List<string> { "Dr. Simge Yalçın", "Dr. Demir Ayaz" } }
                }
            },
            { "Pendik Devlet Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Üroloji", new List<string> { "Dr. Ela Altındağ", "Dr. Levent Atahanlı" } },
                    { "Dermatoloji", new List<string> { "Dr. Zenan Parlar", "Dr. Suat Birtan" } },
                    { "Nöroloji", new List<string> { "Dr. Zeynep Su Meri", "Dr. Ali Kızıltaş" } }
                }
            },
            { "Ümraniye Eğitim ve Araştırma Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Üroloji", new List<string> { "Dr. Erdem Akbaş" } },
                    { "Dermatoloji", new List<string> { "Dr. Hasan Oğuz Kaya", "Dr. Rüya Deniz Vural" } }
                }
            },
            { "Acıbadem Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Dahiliye", new List<string> { "Dr. Kaan Pala" } },
                    { "Kardiyoloji", new List<string> { "Dr. Mert Durmaz", "Dr. Seda Altun" } },
                    { "Genel Cerrahi", new List<string> { "Dr. Ahmet Aksöz", "Dr. Ayşe Özcan" } },
                    { "Dermatoloji", new List<string> { "Dr. Ozan Öztürk", "Dr. Can Yıldız" } },
                    { "Nöroloji", new List<string> { "Dr. Mehmet Gündüz", "Dr. Elif Eda Yeşir" } }
                }
            },
            { "Florence Nightingale Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Dahiliye", new List<string> { "Dr. Emir Yağız Çermik" } },
                    { "Ortopedi", new List<string> { "Dr. Onur Gür", "Dr. Nusret Yavuz" } },
                    { "Kardiyoloji", new List<string> { "Dr. Eda Taşlı" } },
                    { "Göz Hastalıkları", new List<string> { "Dr. Harun Yılmaz", "Dr. Taner Yiğit" } },
                    { "Dermatoloji", new List<string> { "Dr. Nazlı Sözen" } }
                }
            },
            { "Medipol Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Ortopedi", new List<string> { "Dr. Simge Yalçın", "Dr. Demir Ayaz" } },
                    { "Üroloji", new List<string> { "Dr. Ela Altındağ", "Dr. Levent Atahanlı" } },
                    { "Nöroloji", new List<string> { "Dr. Zenan Parlar", "Dr. Suat Birtan" } }
                }
            },
            { "Kartal Dr. Lütfi Kırdar Şehir Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Dahiliye", new List<string> { "Dr. Zeynep Su Meri", "Dr. Ali Kızıltaş" } },
                    { "Kardiyoloji", new List<string> { "Dr. Erdem Akbaş" } },
                    { "Genel Cerrahi", new List<string> { "Dr. Hasan Oğuz Kaya", "Dr. Rüya Deniz Vural" } }
                }
            },
            { "Hisar Hospital Intercontinental", new Dictionary<string, List<string>>
                {
                    { "Beslenme ve Diyet", new List<string> { "Dyt. Beyza Topçu" } },
                    { "Dermatoloji", new List<string> { "Dr. Cemile Dilek Uysal", "Funda Ataman" } },
                    { "Genel Cerrahi", new List<string> { "Dr. Kıvanç Derya Peker", "Dr. Merve Karlı" } }
                }
            },
        };

        private List<string> RandevuSaatleri = new List<string>
        {
            "09:00-09:30", "09:30-10:00", "10:00-10:30", "10:30-11:00", "11:00-11.30", "11:30-12:00", "13:00-13:30", "13:30-14:00"
        };

        private readonly FirebaseService _firebaseService;

        public RandevuSistemi()
        {
            _firebaseService = new FirebaseService();
        }

        private List<string> GetTarihler()
        {
            var tarihler = new List<string>();
            var bugun = DateTime.Now.Date;

            // Bugünden itibaren 30 günlük randevu tarihleri
            for (int i = 1; i <= 30; i++)
            {
                var tarih = bugun.AddDays(i);
                if (tarih.DayOfWeek != DayOfWeek.Saturday && tarih.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Tarih ve gün adını birleştirerek listeye ekle
                    string tarihVeGun = $"{tarih:dd/MM/yyyy} ({tarih.ToString("ddd", new System.Globalization.CultureInfo("tr-TR"))})";
                    tarihler.Add(tarihVeGun);
                }
            }
            return tarihler;
        }

        public interface IRandevuIslemleri
        {
            Task RandevuAl(Kullanici kullanici);
            Task RandevulariGoruntule(Kullanici kullanici);
            Task RandevuIptal(Kullanici kullanici);
        }

        public async Task RandevuAl(Kullanici kullanici)
        {
            if (kullanici == null)
            {
                AnsiConsole.MarkupLine("[red]Randevu alabilmek için giriş yapmanız gerekiyor.[/]");
                return;
            }

            var bolumler = new[] {
                "Geri",
                "Dahiliye",
                "Ortopedi",
                "Kardiyoloji",
                "Genel Cerrahi",
                "Göz Hastalıkları",
                "Üroloji",
                "Dermatoloji",
                "Nöroloji"
            };

            while (true)
            {
                Console.Clear();
                AnsiConsole.Write(new Rule("[cyan]Randevu Alma İşlemi[/]").RuleStyle("grey").Centered());

                var bolumSecimi = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("\n[green]Lütfen Bölüm Seçiniz[/]")
                        .PageSize(10)
                        .HighlightStyle(new Style().Foreground(Color.Cyan1))
                        .AddChoices(bolumler));

                if (bolumSecimi == "Geri")
                    return;

                // Bölüme göre hastaneleri filtrele
                var uygunHastaneler = HastaneBolumleri
                    .Where(h => h.Value.ContainsKey(bolumSecimi))
                    .Select(h => h.Key)
                    .ToList();

                if (!uygunHastaneler.Any())
                {
                    AnsiConsole.MarkupLine($"[red]{bolumSecimi} bölümü hiçbir hastanede bulunmamaktadır. Lütfen başka bir bölüm seçiniz.[/]");
                    Thread.Sleep(2000);
                    continue;
                }

                // Hastane seçimi
                var secilenHastane = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"[cyan]Hastaneler[/]")
                        .PageSize(10)
                        .HighlightStyle(new Style(foreground: Color.Cyan1)
                            .Decoration(Decoration.Bold))
                        .AddChoices(uygunHastaneler.Prepend("Geri"))
                        .UseConverter(hastane =>
                        {
                            return hastane == "Geri"
                                ? "[red]< Geri[/]"
                                : $"[white]{hastane}[/]";
                        }));

                if (secilenHastane == "Geri")
                    continue;

                Console.Clear();

                // Doktor seçimi (bölüme özgü)
                var doktorlar = HastaneBolumleri[secilenHastane][bolumSecimi];
                var secilenDoktor = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"\n[green]{secilenHastane} - {bolumSecimi} Bölümü - Doktor Seçimi[/]")
                        .PageSize(5)
                        .HighlightStyle(new Style().Foreground(Color.Cyan1))
                        .AddChoices(doktorlar.Prepend("Geri")));

                if (secilenDoktor == "Geri")
                    continue;

                // Tarih seçimi
                var tarihler = GetTarihler();
                var secilenTarih = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("\n[green]Lütfen Randevu Tarihi Seçiniz[/]")
                        .PageSize(10)
                        .HighlightStyle(new Style().Foreground(Color.Cyan1))
                        .AddChoices(tarihler.Prepend("Geri")));

                if (secilenTarih == "Geri")
                    continue;

                // Randevu saati seçimi
                var tumRandevular = await _firebaseService.TumRandevulariGetir();
                var saatler = new Table()
                    .Border(TableBorder.Rounded)
                    .Title("[cyan]Randevu Saatleri[/]")
                    .AddColumn("Saat")
                    .AddColumn("Durum");

                // Seçilen tarihi DateTime objesine dönüştür
                string tarihString = secilenTarih.Split(' ')[0];
                DateTime secilenTarihObj = DateTime.ParseExact(tarihString, "dd/MM/yyyy", null);

                foreach (var saat in RandevuSaatleri)
                {
                    string randevuKey = $"{bolumSecimi}-{secilenHastane}-{secilenDoktor}-{secilenTarihObj:dd/MM/yyyy}-{saat}";
                    string durum = tumRandevular.Any(r => 
                        $"{r.Bolum}-{r.Hastane}-{r.Doktor}-{r.RandevuTarihi:dd/MM/yyyy}-{r.Saat}" == randevuKey) 
                        ? "[red]Dolu[/]" : "[green]Müsait[/]";
                    saatler.AddRow(saat, durum);
                }

                AnsiConsole.Write(saatler);

                var secilenSaat = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("\n[green]Lütfen Randevu Saati Seçiniz[/]")
                        .PageSize(5)
                        .HighlightStyle(new Style().Foreground(Color.Cyan1))
                        .AddChoices(RandevuSaatleri.Where(s =>
                            !tumRandevular.Any(r => 
                                $"{r.Bolum}-{r.Hastane}-{r.Doktor}-{r.RandevuTarihi:dd/MM/yyyy}-{r.Saat}" == 
                                $"{bolumSecimi}-{secilenHastane}-{secilenDoktor}-{secilenTarihObj:dd/MM/yyyy}-{s}"))
                            .Prepend("Geri")));

                if (secilenSaat == "Geri")
                    continue;

                // Randevu oluşturma işlemleri...
                string randevuKeyFinal = $"{bolumSecimi}-{secilenHastane}-{secilenDoktor}-{secilenTarihObj:dd/MM/yyyy}-{secilenSaat}";

                if (tumRandevular.Any(r => 
                    $"{r.Bolum}-{r.Hastane}-{r.Doktor}-{r.RandevuTarihi:dd/MM/yyyy}-{r.Saat}" == randevuKeyFinal))
                {
                    AnsiConsole.MarkupLine("[red]Bu randevu saati dolu![/]");
                    continue;
                }

                var yeniRandevu = new Randevu
                {
                    Bolum = bolumSecimi,
                    Hastane = secilenHastane,
                    Doktor = secilenDoktor,
                    Saat = secilenSaat,
                    RandevuTarihi = secilenTarihObj
                };

                var kayitBasarili = await _firebaseService.RandevuKaydet(kullanici.TC, yeniRandevu);
                if (!kayitBasarili)
                {
                    AnsiConsole.MarkupLine("[red]Randevu kaydedilirken bir hata oluştu![/]");
                    return;
                }

                // Randevu onay ekranı
                var onayPanel = new Panel(
                    Align.Center(
                        new Markup($"""
                            [green]Randevunuz Başarıyla Oluşturuldu![/]

                            [cyan]Bölüm:[/] {bolumSecimi}
                            [cyan]Hastane:[/] {secilenHastane}
                            [cyan]Doktor:[/] {secilenDoktor}
                            [cyan]Tarih:[/] {secilenTarihObj:dd/MM/yyyy}
                            [cyan]Saat:[/] {secilenSaat}

                            [yellow]SMS Bilgilendirmesi:[/] {kullanici.TelefonNo} numaralı telefona
                            randevu detaylarınız gönderilmiştir.

                            [red]NOT:[/] Randevunuza gelmemeniz durumunda 15 gün
                            boyunca yeni randevu alamazsınız!
                            """)
                    ))
                {
                    Border = BoxBorder.Double,
                    Padding = new Padding(2)
                };

                Console.Clear();
                AnsiConsole.Write(onayPanel);
                AnsiConsole.MarkupLine("\n[cyan]Ana menüye dönmek için bir tuşa basınız...[/]");
                Console.ReadKey();
                break;
            }
        }

        public async Task RandevulariGoruntule(Kullanici kullanici)
        {
            if (kullanici == null)
            {
                AnsiConsole.MarkupLine("[red]Randevularınızı görüntülemek için giriş yapmanız gerekiyor.[/]");
                return;
            }

            var randevular = await _firebaseService.RandevulariGetir(kullanici.TC);
            
            if (!randevular.Any())
            {
                AnsiConsole.MarkupLine("[yellow]\nHenüz randevunuz bulunmamaktadır.[/]");
                Console.WriteLine("\nAna menüye dönmek için bir tuşa basınız...");
                Console.ReadKey();
                Console.Clear();
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title($"[cyan]{kullanici.Isim} {kullanici.Soyisim}[/] Randevuları")
                .AddColumn(new TableColumn("[green]Bölüm[/]").Centered())
                .AddColumn(new TableColumn("[green]Hastane[/]").Centered())
                .AddColumn(new TableColumn("[green]Doktor[/]").Centered())
                .AddColumn(new TableColumn("[green]Saat[/]").Centered())
                .AddColumn(new TableColumn("[green]Tarih[/]").Centered());

            foreach (var randevu in randevular)
            {
                table.AddRow(
                    randevu.Bolum,
                    randevu.Hastane,
                    randevu.Doktor,
                    randevu.Saat,
                    randevu.RandevuTarihi.ToString("dd/MM/yyyy")
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("\n[red]NOT:[/] Randevunuza gelmemeniz durumunda 15 gün boyunca aynı bölümden yeni randevu alamazsınız!");
            AnsiConsole.MarkupLine("\n[cyan]Ana menüye dönmek için bir tuşa basınız...[/]");
            Console.ReadKey();
            Console.Clear();
        }

        public async Task RandevuIptal(Kullanici kullanici)
        {
            if (kullanici == null)
            {
                AnsiConsole.MarkupLine("[red]Randevu iptal etmek için giriş yapmanız gerekiyor.[/]");
                return;
            }

            var randevular = await _firebaseService.RandevulariGetir(kullanici.TC);
            
            if (!randevular.Any())
            {
                AnsiConsole.MarkupLine("[yellow]\nİptal edilecek randevunuz bulunmamaktadır.[/]");
                AnsiConsole.MarkupLine("\n[cyan]Ana menüye dönmek için bir tuşa basınız...[/]");
                Console.ReadKey();
                Console.Clear();
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[cyan]Randevularınız[/]")
                .AddColumn(new TableColumn("[green]No[/]").Centered())
                .AddColumn(new TableColumn("[green]Bölüm[/]").Centered())
                .AddColumn(new TableColumn("[green]Hastane[/]").Centered())
                .AddColumn(new TableColumn("[green]Doktor[/]").Centered())
                .AddColumn(new TableColumn("[green]Saat[/]").Centered());

            for (int i = 0; i < randevular.Count; i++)
            {
                var randevu = randevular[i];
                table.AddRow(
                    (i + 1).ToString(),
                    randevu.Bolum,
                    randevu.Hastane,
                    randevu.Doktor,
                    randevu.Saat
                );
            }

            AnsiConsole.Write(table);

            var secim = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]İptal etmek istediğiniz randevunun numarasını girin (Geri için 0):[/]")
                    .Validate(num =>
                        num >= 0 && num <= randevular.Count
                            ? ValidationResult.Success()
                            : ValidationResult.Error("[red]Geçersiz seçim![/]")));

            if (secim == 0) return;

            var iptalEdilecekRandevu = randevular[secim - 1];
            var iptalBasarili = await _firebaseService.RandevuIptal(kullanici.TC, iptalEdilecekRandevu.Id);
            if (!iptalBasarili)
            {
                AnsiConsole.MarkupLine("[red]Randevu iptal edilirken bir hata oluştu![/]");
                return;
            }

            kullanici.SonIptalTarihi = DateTime.Now;

            Console.WriteLine("\nRandevunuz iptal edildi.");
            Console.WriteLine($"SMS Bilgilendirmesi: {kullanici.TelefonNo} numaralı telefona randevu iptal bilgileriniz gönderilmiştir.");
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            AnsiConsole.Write(new FigletText("Hastane Randevu").Centered().Color(Color.Cyan1));
            AnsiConsole.WriteLine();

            KullaniciYonetimi yonetici = new KullaniciYonetimi();
            RandevuSistemi randevuSistemi = new RandevuSistemi();

            while (true)
            {
                Kullanici aktifKullanici = yonetici.GetAktifKullanici();

                if (aktifKullanici == null)
                {
                    AnsiConsole.Write(new Rule("[cyan]Randevu Alma Sistemi[/]").RuleStyle("grey").Centered());
                    var menu = new SelectionPrompt<string>()
                        .Title("[cyan]Randevu Alma Sistemi[/]")
                        .PageSize(4)
                        .AddChoices(new[] {
                            "1. Kayıt Ol",
                            "2. Giriş Yap",
                            "3. Şifremi Unuttum",
                            "4. Çıkış"
                        });

                    string secim = AnsiConsole.Prompt(menu);

                    switch (secim.Substring(0, 1))
                    {
                        case "1":
                            Console.Clear();
                            await yonetici.KayitOl();
                            break;
                        case "2":
                            Console.Clear();
                            await yonetici.GirisYap();
                            break;
                        case "3":
                            Console.Clear();
                            await yonetici.SifremiUnuttum();
                            break;
                        case "4":
                            AnsiConsole.MarkupLine("[yellow]Çıkış yapılıyor...[/]");
                            return;
                    }
                }
                else
                {
                    var menu = new SelectionPrompt<string>()
                        .Title($"[green]Hoş geldiniz, {aktifKullanici.Isim} {aktifKullanici.Soyisim}[/]")
                        .PageSize(6)
                        .AddChoices(new[] {
                            "1. Randevu Al",
                            "2. Randevularımı Görüntüle",
                            "3. Profili Görüntüle",
                            "4. Randevu İptal",
                            "5. Çıkış Yap"
                        });

                    string secim = AnsiConsole.Prompt(menu);

                    switch (secim.Substring(0, 1))
                    {
                        case "1":
                            Console.Clear();
                            await randevuSistemi.RandevuAl(aktifKullanici);
                            break;
                        case "2":
                            Console.Clear();
                            await randevuSistemi.RandevulariGoruntule(aktifKullanici);
                            break;
                        case "3":
                            Console.Clear();
                            yonetici.ProfiliGoruntule();
                            break;
                        case "4":
                            Console.Clear();
                            await randevuSistemi.RandevuIptal(aktifKullanici);
                            break;
                        case "5":
                            yonetici.CikisYap();
                            break;
                    }
                }
            }
        }
    }
}*/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Spectre.Console;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Auth;

namespace RandevuSistemi
{
    public class Kullanici
    {
        public string TC { get; set; }
        public string Isim { get; set; }
        public string Soyisim { get; set; }
        public string DogumTarihi { get; set; }
        public string Sifre { get; set; }
        public string TelefonNo { get; set; }
        public string Email { get; set; }
        public string YetkiSeviyesi { get; set; }
        public DateTime? SonIptalTarihi { get; set; } // ? kullanılmasının sebebi nullable olması

        public Kullanici(string tc, string isim, string soyisim, string dogumTarihi, string sifre, string telefonNo)
        {
            TC = tc;
            Isim = isim;
            Soyisim = soyisim;
            DogumTarihi = dogumTarihi;
            Sifre = sifre;
            TelefonNo = telefonNo;
            SonIptalTarihi = null;
        }
    }

    // Kalıtım alınan sınıf
    public class AdminKullanici : Kullanici
    {
        private readonly FirebaseService _firebaseService;

        public AdminKullanici(string tc, string isim, string soyisim, string dogumTarihi, string sifre, string yetkiSeviyesi)
            : base(tc, isim, soyisim, dogumTarihi, sifre, "")
        {
            YetkiSeviyesi = yetkiSeviyesi;
            _firebaseService = new FirebaseService();
        }

        // Yetki kontrol metodu
        public bool YetkiKontrol()
        {
            return YetkiSeviyesi == "Admin";
        }

        // Tüm kullanıcıları listeleme
        public async Task KullanicilariListele()
        {
            if (!YetkiKontrol())
            {
                AnsiConsole.MarkupLine("[red]Bu işlem için yetkiniz bulunmamaktadır![/]");
                return;
            }

            var kullanicilar = await _firebaseService.TumKullanicilariGetir();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[cyan]Sistem Kullanıcıları[/]")
                .AddColumn(new TableColumn("[green]TC[/]").Centered())
                .AddColumn(new TableColumn("[green]İsim[/]").Centered())
                .AddColumn(new TableColumn("[green]Soyisim[/]").Centered())
                .AddColumn(new TableColumn("[green]Email[/]").Centered())
                .AddColumn(new TableColumn("[green]Telefon[/]").Centered());

            foreach (var kullanici in kullanicilar)
            {
                table.AddRow(
                    kullanici.TC,
                    kullanici.Isim,
                    kullanici.Soyisim,
                    kullanici.Email,
                    kullanici.TelefonNo
                );
            }

            AnsiConsole.Write(table);
            Console.WriteLine("\nDevam etmek için bir tuşa basın...");
            Console.ReadKey();
        }

        // Kullanıcı silme
        public async Task KullaniciSil(string tc)
        {
            if (!YetkiKontrol())
            {
                AnsiConsole.MarkupLine("[red]Bu işlem için yetkiniz bulunmamaktadır![/]");
                return;
            }

            if (await _firebaseService.KullaniciSil(tc))
            {
                AnsiConsole.MarkupLine($"[green]{tc} TC numaralı kullanıcı başarıyla silindi.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Kullanıcı silinirken bir hata oluştu![/]");
            }
        }

        // Kullanıcı yetkilerini güncelleme
        public async Task YetkiGuncelle(string tc, string yeniYetki)
        {
            if (!YetkiKontrol())
            {
                AnsiConsole.MarkupLine("[red]Bu işlem için yetkiniz bulunmamaktadır![/]");
                return;
            }

            if (await _firebaseService.YetkiGuncelle(tc, yeniYetki))
            {
                AnsiConsole.MarkupLine($"[green]{tc} TC numaralı kullanıcının yetkisi '{yeniYetki}' olarak güncellendi.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Yetki güncellenirken bir hata oluştu![/]");
            }
        }

        // Admin paneli
        public async Task AdminPaneliGoster()
        {
            if (!YetkiKontrol())
            {
                AnsiConsole.MarkupLine("[red]Admin paneline erişim yetkiniz bulunmamaktadır![/]");
                return;
            }

            while (true)
            {
                Console.Clear();
                AnsiConsole.Write(new Rule("[cyan]Admin Paneli[/]").RuleStyle("grey").Centered());

                var secim = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("\n[green]Lütfen yapmak istediğiniz işlemi seçin:[/]")
                        .PageSize(6)
                        .AddChoices(new[]
                        {
                            "1. Kullanıcıları Listele",
                            "2. Kullanıcı Sil",
                            "3. Kullanıcı Yetkisi Güncelle",
                            "4. Tüm Randevuları Görüntüle",
                            "5. Ana Menüye Dön"
                        }));

                switch (secim.Substring(0, 1))
                {
                    case "1":
                        Console.Clear();
                        await KullanicilariListele();
                        break;

                    case "2":
                        Console.Clear();
                        Console.Write("Silinecek kullanıcının TC numarası: ");
                        string silinecekTc = Console.ReadLine();
                        await KullaniciSil(silinecekTc);
                        Thread.Sleep(2000);
                        break;

                    case "3":
                        Console.Clear();
                        Console.Write("Yetkisi güncellenecek kullanıcının TC numarası: ");
                        string guncellenecekTc = Console.ReadLine();
                        Console.Write("Yeni yetki seviyesi (Admin/User): ");
                        string yeniYetki = Console.ReadLine();
                        await YetkiGuncelle(guncellenecekTc, yeniYetki);
                        Thread.Sleep(2000);
                        break;

                    case "4":
                        Console.Clear();
                        await TumRandevulariGoruntule();
                        break;

                    case "5":
                        return;
                }
            }
        }

        // Tüm randevuları görüntüleme
        private async Task TumRandevulariGoruntule()
        {
            if (!YetkiKontrol())
            {
                AnsiConsole.MarkupLine("[red]Bu işlem için yetkiniz bulunmamaktadır![/]");
                return;
            }

            var tumRandevular = await _firebaseService.TumRandevulariGetir();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[cyan]Tüm Randevular[/]")
                .AddColumn(new TableColumn("[green]TC[/]").Centered())
                .AddColumn(new TableColumn("[green]Hastane[/]").Centered())
                .AddColumn(new TableColumn("[green]Bölüm[/]").Centered())
                .AddColumn(new TableColumn("[green]Doktor[/]").Centered())
                .AddColumn(new TableColumn("[green]Tarih[/]").Centered())
                .AddColumn(new TableColumn("[green]Saat[/]").Centered());

            foreach (var randevu in tumRandevular)
            {
                table.AddRow(
                    randevu.TC ?? "N/A",
                    randevu.Hastane,
                    randevu.Bolum,
                    randevu.Doktor,
                    randevu.RandevuTarihi.ToString("dd/MM/yyyy"),
                    randevu.Saat
                );
            }

            AnsiConsole.Write(table);
            Console.WriteLine("\nDevam etmek için bir tuşa basın...");
            Console.ReadKey();
        }
    }

    public abstract class KullaniciBase
    {
        public abstract Task GirisYap();
        public abstract void CikisYap();
        public abstract void ProfiliGoruntule();
    }

    public interface IRandevuIslemleri
    {
        Task RandevuAl(Kullanici kullanici);
        Task RandevulariGoruntule(Kullanici kullanici);
        Task RandevuIptal(Kullanici kullanici);
    }

    public class KullaniciYonetimi : KullaniciBase, IRandevuIslemleri
    {
        //Kapsülleme için private kullanıldı.
        private List<Kullanici> KullaniciListesi = new List<Kullanici>();
        private Kullanici aktifKullanici;
        private readonly FirebaseService _firebaseService;
        private readonly RandevuSistemi randevuSistemi;

        public KullaniciYonetimi()
        {
            _firebaseService = new FirebaseService();
            randevuSistemi = new RandevuSistemi();
        }

        //TC Doğrulama fonksiyonu
        private bool ValidateTC(string tc)
        {
            if (tc.Length != 11 || !long.TryParse(tc, out _))
            {
                ClearLastLines(1);
                Console.WriteLine("TC Kimlik Numarası 11 haneli rakamlardan oluşmalıdır! Tekrar deneyiniz.\n");
                return false;
            }
            return true;
        }

        //TC doğru girme fonksiyonu
        private string ReadTC()
        {
            string tc = "";
            ConsoleKeyInfo key;

            while (true)
            {
                key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (key.Key == ConsoleKey.Backspace && tc.Length > 0)
                {
                    tc = tc.Remove(tc.Length - 1);
                    Console.Write("\b \b");
                }
                else if (char.IsDigit(key.KeyChar) && tc.Length < 11)
                {
                    tc += key.KeyChar;
                    Console.Write(key.KeyChar);
                }
            }
            return tc;
        }

        private bool ValidateIsimSoyisim(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !Regex.IsMatch(text, @"^[a-zA-ZğüşıöçĞÜŞİÖÇ\s]+$"))
            {
                ClearLastLines(1);
                Console.WriteLine("İsim/Soyisim sadece harflerden oluşmalıdır!");
                return false;
            }
            return true;
        }

        private bool ValidateDogumTarihi(string tarih)
        {
            if (!Regex.IsMatch(tarih, @"^\d{2}/\d{2}/\d{4}$"))
            {
                ClearLastLines(1);
                Console.WriteLine("Doğum tarihi GG/AA/YYYY formatında olmalıdır!");
                return false;
            }

            try
            {
                string[] parts = tarih.Split('/');
                int gun = int.Parse(parts[0]);
                int ay = int.Parse(parts[1]);
                int yil = int.Parse(parts[2]);

                if (0 >= gun || gun > 31)
                {
                    if (0 >= ay || ay > 12)
                    {
                        if (yil < 1900 || yil > DateTime.Now.Year)
                        {
                            ClearLastLines(2);
                            Console.WriteLine("Gün, ay ve yıl yanlış girildi! Tekrar Deneyiniz.");
                            return false;
                        }
                        ClearLastLines(2);
                        Console.WriteLine("Gün ve ay yanlış girildi! Tekrar deneyiniz.");
                        return false;
                    }
                    ClearLastLines(2);
                    Console.WriteLine("Gün yanlış girildi! Tekrar Deneyiniz.");
                    return false;
                }

                if (0 >= ay || ay > 12)
                {
                    if (yil < 1900 || yil > DateTime.Now.Year)
                    {
                        ClearLastLines(2);
                        Console.WriteLine("Ay ve yıl yanlış girildi! Tekrar Deneyiniz.");
                        return false;
                    }
                    ClearLastLines(2);
                    Console.WriteLine("Hatalı ay girişi! Tekrar deneyiniz.");
                    return false;
                }

                if (yil < 1900 || yil > DateTime.Now.Year)
                {
                    ClearLastLines(2);
                    Console.WriteLine("Hatalı yıl girişi! Tekrar deneyiniz.");
                    return false;
                }

                DateTime dt = new DateTime(yil, ay, gun);
                if (dt > DateTime.Now)
                {
                    ClearLastLines(2);
                    Console.WriteLine("Geçersiz doğum tarihi!");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Geçersiz tarih! Hata : ", ex.Message);
                return false;
            }
        }

        private bool ValidateSifre(string sifre)
        {
            if (sifre.Length < 6)
            {
                Console.WriteLine("Şifre en az 6 karakterden oluşmalıdır!");
                return false;
            }
            // İzin verilen karakterler dışında herhangi bir karakter bulunup bulunmadığının kontrolü
            if (!Regex.IsMatch(sifre, @"^[A-Za-z0-9ğüşıöçĞÜŞİÖÇ.]+$"))
            {
                AnsiConsole.MarkupLine("[red]Şifre sadece büyük/küçük harfler, sayılar ve nokta (.) içerebilir![/]");
                return false;
            }
            return true;
        }


        private string ReadPassword()
        {
            string sifre = "";
            ConsoleKeyInfo key;
            bool sifreGizli = true; // Şifrenin gizli olup olmadığını kontrol eden flag

            // Başlangıçta kullanım bilgisini göster
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("(Şifreyi göster/gizle için F2'ye basın)");
            Console.ResetColor();
            Console.Write("Şifre: ");

            while (true)
            {
                key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.F2) // F2'ye basıldığında gizle/göster
                {
                    sifreGizli = !sifreGizli; // Durumu tersine çevir

                    // Mevcut satırı temizle ve şifreyi tekrar yazdır
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write("Şifre: ");

                    // Şifreyi göster veya gizle
                    if (sifreGizli)
                        Console.Write(new string('*', sifre.Length));
                    else
                        Console.Write(sifre);

                    // +- ikonunu yazdır
                    Console.Write(" ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(sifreGizli ? "+" : "-");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (key.Key == ConsoleKey.Backspace && sifre.Length > 0)
                {
                    sifre = sifre.Remove(sifre.Length - 1);

                    // Mevcut satırı temizle ve şifreyi tekrar yazdır
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write("Şifre: ");

                    // Şifreyi göster veya gizle
                    if (sifreGizli)
                        Console.Write(new string('*', sifre.Length));
                    else
                        Console.Write(sifre);

                    // +- ikonunu yazdır
                    Console.Write(" ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(sifreGizli ? "+" : "-");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    sifre += key.KeyChar;

                    // Mevcut satırı temizle ve şifreyi tekrar yazdır
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write("Şifre: ");

                    // Şifreyi göster veya gizle
                    if (sifreGizli)
                        Console.Write(new string('*', sifre.Length));
                    else
                        Console.Write(sifre);

                    // +- ikonunu yazdır
                    Console.Write(" ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(sifreGizli ? "+" : "-");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            return sifre;
        }

        private bool ValidateTelefon(string telefon)
        {
            if (!Regex.IsMatch(telefon, @"^5[0-9]{9}$"))
            {
                ClearLastLines(1);
                Console.WriteLine("Telefon numarası 5XX XXX XX XX formatında olmalıdır!");
                return false;
            }
            return true;
        }

        private bool ValidateEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                if (!email.EndsWith(".com") && !email.Contains("@"))
                {
                    AnsiConsole.MarkupLine("[red]Geçersiz email formatı![/]");
                    return false;
                }
                return addr.Address == email;
            }
            catch
            {
                AnsiConsole.MarkupLine("[red]Geçersiz email formatı![/]");
                return false;
            }
        }

        public async Task KayitOl()
        {
            string tc, isim, soyisim, dogumTarihi, sifre;

            Console.Clear();
            AnsiConsole.Write(new Rule("[cyan]Kayıt Ol[/]").RuleStyle("grey").Centered());
            AnsiConsole.MarkupLine("\n[yellow]Ana menüye dönmek için TC alanına 0 giriniz.[/]\n");

            bool tcKayitli;
            do
            {
                do
                {
                    Console.Write("TC Kimlik Numarası: ");
                    tc = ReadTC();

                    if (tc == "0")
                    {
                        Console.Clear();
                        return;
                    }

                } while (!ValidateTC(tc));

                // TC'nin veritabanında varlığının kontrolü
                var mevcutKullanici = await _firebaseService.KullaniciGetir(tc);
                tcKayitli = mevcutKullanici != null;

                if (tcKayitli)
                {
                    AnsiConsole.MarkupLine("[red]Bu TC kimlik numarası zaten kayıtlı! Lütfen başka bir TC giriniz.[/]");
                    Thread.Sleep(2000);
                }
            } while (tcKayitli);

            do
            {
                Console.Write("\nİsim: ");
                isim = Console.ReadLine();
            } while (!ValidateIsimSoyisim(isim));

            do
            {
                Console.Write("\nSoyisim: ");
                soyisim = Console.ReadLine();
            } while (!ValidateIsimSoyisim(soyisim));

            do
            {
                Console.Write("\nDoğum Tarihi (GG/AA/YYYY): ");
                dogumTarihi = Console.ReadLine();
            } while (!ValidateDogumTarihi(dogumTarihi));

            do
            {
                Console.Write("\nŞifre (en az 6 karakter): ");
                sifre = ReadPassword();
            } while (!ValidateSifre(sifre));

            // Telefon numarasının kayıtlı olup olmadığının kontrolü
            string telefonNo;
            bool telefonKayitli;
            do
            {
                do
                {
                    Console.Write("\nTelefon Numarası (5XX XXX XX XX): +90");
                    telefonNo = Console.ReadLine();
                } while (!ValidateTelefon(telefonNo));

                telefonNo = "+90" + telefonNo;
                telefonKayitli = await _firebaseService.TelefonNoKullaniliyor(telefonNo);

                if (telefonKayitli)
                {
                    AnsiConsole.MarkupLine("[red]Bu telefon numarası zaten kayıtlı! Lütfen başka bir numara giriniz.[/]");
                    telefonNo = telefonNo.Substring(3); // +90'ı kaldır
                }
            } while (telefonKayitli);

            // Email'in veritabanında varlığının kontrolü
            string email;
            bool emailKayitli;
            do
            {
                do
                {
                    Console.Write("\nEmail Adresi: ");
                    email = Console.ReadLine();
                } while (!ValidateEmail(email));

                emailKayitli = await _firebaseService.EmailKullaniliyor(email);

                if (emailKayitli)
                {
                    AnsiConsole.MarkupLine("[red]Bu email adresi zaten kayıtlı! Lütfen başka bir email giriniz.[/]");
                }
            } while (emailKayitli);

            Kullanici yeniKullanici = new Kullanici(tc, isim, soyisim, dogumTarihi, sifre, telefonNo)
            {
                Email = email
            };

            var kayitBasarili = await _firebaseService.KullaniciKaydet(yeniKullanici);
            if (!kayitBasarili)
            {
                Console.WriteLine("Kayıt sırasında bir hata oluştu!");
                return;
            }

            aktifKullanici = yeniKullanici;
            Console.Clear();
            Console.Write("Kayıt başarılı! 3 saniye içinde sisteme giriş yapılıyor ");
            char[] loadingChars = { '|', '/', '-', '\\' };
            int charIndex = 0;

            for (int i = 0; i < 20; i++)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(loadingChars[charIndex]);
                Thread.Sleep(100);
                Console.Write("\b"); // Bir önceki karakteri sil
                charIndex = (charIndex + 1) % loadingChars.Length; // Sonraki karakteri seç
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.WriteLine("\n");
            // Console.Write("Kayıt başarılı! 3 saniye içinde sisteme giriş yapılıyor");
            // for (int i = 0; i < 3; i++)
            // {
            //     Thread.Sleep(300);
            //     Console.ForegroundColor = ConsoleColor.Red;
            //     Console.Write(".");
            // }
            // Console.Write("\b\b\b");
            // for (int i = 0; i < 3; i++)
            // {
            //     Thread.Sleep(300);
            //     Console.ForegroundColor = ConsoleColor.Green;
            //     Console.Write(".");
            // }
            // Console.Write("\b\b\b");
            // for (int i = 0; i < 3; i++)
            // {
            //     Thread.Sleep(300);
            //     Console.ForegroundColor = ConsoleColor.Cyan;
            //     Console.Write(".");
            // }
            // Console.ForegroundColor = ConsoleColor.White;
            // Console.WriteLine("\n");
        }


        public override async Task GirisYap()
        {
            aktifKullanici = await GirisYapKontrol();
        }

        private async Task<Kullanici> GirisYapKontrol()
        {
            while (true)
            {
                Console.Clear();
                AnsiConsole.Write(new Rule("[cyan]Giriş Yap[/]").RuleStyle("grey").Centered());
                AnsiConsole.MarkupLine("\n[yellow]Ana menüye dönmek için TC alanına 0 giriniz.[/]\n");

                Console.Write("TC Kimlik Numarası: ");
                string tc = ReadTC();

                if (tc == "0")
                {
                    Console.Clear();
                    return null;
                }

                if (!ValidateTC(tc))
                {
                    AnsiConsole.MarkupLine("[red]Geçersiz TC Kimlik Numarası! Tekrar deneyiniz.[/]");
                    Thread.Sleep(2000);
                    continue;
                }

                Console.Write("Şifre: ");
                string sifre = ReadPassword();

                var kullanici = await _firebaseService.TCileGirisYap(tc, sifre);

                if (kullanici != null)
                {
                    Console.WriteLine($"Giriş başarılı! Hoş geldiniz, {kullanici.Isim} {kullanici.Soyisim}");
                    aktifKullanici = kullanici;
                    Thread.Sleep(1000);
                    return kullanici;
                }

                AnsiConsole.MarkupLine("[red]TC Kimlik No veya şifre hatalı! Tekrar deneyiniz.[/]");
                Thread.Sleep(2000);
            }
        }

        public override void CikisYap()
        {
            aktifKullanici = null;
            Console.WriteLine("Çıkış yapıldı.");
        }

        public Kullanici GetAktifKullanici()
        {
            return aktifKullanici;
        }

        public override void ProfiliGoruntule()
        {
            if (aktifKullanici == null)
            {
                Console.WriteLine("Profilinizi görüntülemek için giriş yapmanız gerekiyor.");
                return;
            }

            Console.WriteLine("\nProfil Bilgileriniz:");
            Console.WriteLine($"TC: {aktifKullanici.TC}");
            Console.WriteLine($"İsim: {aktifKullanici.Isim}");
            Console.WriteLine($"Soyisim: {aktifKullanici.Soyisim}");
            Console.WriteLine($"Doğum Tarihi: {aktifKullanici.DogumTarihi}");
            Console.WriteLine("\nAna menüye dönmek için bir tuşa basınız...");
            Console.ReadKey();
            Console.Clear();
        }

        public async Task SifremiUnuttum()
        {
            Console.Write("Email Adresiniz: ");
            string email = Console.ReadLine();

            if (!ValidateEmail(email))
            {
                return;
            }

            var resetGonderildi = await _firebaseService.SendPasswordResetEmail(email);
            if (resetGonderildi)
            {
                AnsiConsole.MarkupLine($"""
                    [lightgreen]Şifre sıfırlama bağlantısı email adresinize gönderildi.[/]
                    [yellow]Lütfen mail kutunuzu kontrol ediniz.[/]
                    [red]Not:[/] Spam klasörünü kontrol etmeyi unutmayınız.
                    """);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Şifre sıfırlama maili gönderilirken bir hata oluştu![/]");
            }
        }


        // Son eklenen satırları temizlemek için kullanılan fonksiyon
        static void ClearLastLines(int lineCount)
        {
            int currentCursorTop = Console.CursorTop;

            for (int i = 0; i < lineCount; i++)
            {
                Console.SetCursorPosition(0, currentCursorTop - 1);
                Console.Write(new string(' ', Console.WindowWidth));
                currentCursorTop--;
            }

            Console.SetCursorPosition(0, currentCursorTop);
        }

        public async Task RandevuAl(Kullanici kullanici)
        {
            await randevuSistemi.RandevuAl(kullanici);
        }

        public async Task RandevulariGoruntule(Kullanici kullanici)
        {
            await randevuSistemi.RandevulariGoruntule(kullanici);
        }

        public async Task RandevuIptal(Kullanici kullanici)
        {
            await randevuSistemi.RandevuIptal(kullanici);
        }
    }

    // Normal randevu için temel sınıf
    public class Randevu
    {
        public string Id { get; set; }
        public string TC { get; set; }
        public string Bolum { get; set; }
        public string Hastane { get; set; }
        public string Doktor { get; set; }
        public string Saat { get; set; }
        public DateTime RandevuTarihi { get; set; }

        // Add parameterless constructor
        public Randevu() { }

        // Keep existing constructor
        public Randevu(string bolum, string hastane, string doktor)
        {
            Bolum = bolum;
            Hastane = hastane;
            Doktor = doktor;
        }
    }

    // Acil randevular için özel sınıf
    public class AcilRandevu : Randevu
    {
        public string AciliyetDurumu { get; set; }
        public bool OncelikliHasta { get; set; }

        public AcilRandevu(string bolum, string hastane, string doktor, string aciliyetDurumu)
            : base(bolum, hastane, doktor)
        {
            AciliyetDurumu = aciliyetDurumu;
            OncelikliHasta = true;
        }
    }


    public class RandevuSistemi
    {
        //İç içe geçmiş dictionaryler ile hastaneler ve bölümler arasında ilişki
        private Dictionary<string, Dictionary<string, List<string>>> HastaneBolumleri = new Dictionary<string, Dictionary<string, List<string>>>
        {
            { "Beykoz Devlet Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Dahiliye", new List<string> { "Dr. Ahmet Aksöz", "Dr. Ayşe Özcan" } },
                    { "Ortopedi", new List<string> { "Dr. Ozan Öztürk", "Dr. Can Yıldız" } },
                    { "Kardiyoloji", new List<string> { "Dr. Mehmet Gündüz", "Dr. Elif Eda Yeşir" } }
                }
            },
            { "Medeniyet Üniversitesi Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Dahiliye", new List<string> { "Dr. Emir Yağız Çermik" } },
                    { "Ortopedi", new List<string> { "Dr. Onur Gür", "Dr. Nusret Yavuz" } },
                    { "Göz Hastalıkları", new List<string> { "Dr. Eda Taşlı" } }
                }
            },
            { "Üsküdar Devlet Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Genel Cerrahi", new List<string> { "Dr. Harun Yılmaz", "Dr. Taner Yiğit" } },
                    { "Göz Hastalıkları", new List<string> { "Dr. Nazlı Sözen" } },
                    { "Dermatoloji", new List<string> { "Dr. Simge Yalçın", "Dr. Demir Ayaz" } }
                }
            },
            { "Pendik Devlet Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Üroloji", new List<string> { "Dr. Ela Altındağ", "Dr. Levent Atahanlı" } },
                    { "Dermatoloji", new List<string> { "Dr. Zenan Parlar", "Dr. Suat Birtan" } },
                    { "Nöroloji", new List<string> { "Dr. Zeynep Su Meri", "Dr. Ali Kızıltaş" } }
                }
            },
            { "Ümraniye Eğitim ve Araştırma Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Üroloji", new List<string> { "Dr. Erdem Akbaş" } },
                    { "Dermatoloji", new List<string> { "Dr. Hasan Oğuz Kaya", "Dr. Rüya Deniz Vural" } }
                }
            },
            { "Acıbadem Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Dahiliye", new List<string> { "Dr. Kaan Pala" } },
                    { "Kardiyoloji", new List<string> { "Dr. Mert Durmaz", "Dr. Seda Altun" } },
                    { "Genel Cerrahi", new List<string> { "Dr. Ahmet Aksöz", "Dr. Ayşe Özcan" } },
                    { "Dermatoloji", new List<string> { "Dr. Ozan Öztürk", "Dr. Can Yıldız" } },
                    { "Nöroloji", new List<string> { "Dr. Mehmet Gündüz", "Dr. Elif Eda Yeşir" } }
                }
            },
            { "Florence Nightingale Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Dahiliye", new List<string> { "Dr. Emir Yağız Çermik" } },
                    { "Ortopedi", new List<string> { "Dr. Onur Gür", "Dr. Nusret Yavuz" } },
                    { "Kardiyoloji", new List<string> { "Dr. Eda Taşlı" } },
                    { "Göz Hastalıkları", new List<string> { "Dr. Harun Yılmaz", "Dr. Taner Yiğit" } },
                    { "Dermatoloji", new List<string> { "Dr. Nazlı Sözen" } }
                }
            },
            { "Medipol Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Ortopedi", new List<string> { "Dr. Simge Yalçın", "Dr. Demir Ayaz" } },
                    { "Üroloji", new List<string> { "Dr. Ela Altındağ", "Dr. Levent Atahanlı" } },
                    { "Nöroloji", new List<string> { "Dr. Zenan Parlar", "Dr. Suat Birtan" } }
                }
            },
            { "Kartal Dr. Lütfi Kırdar Şehir Hastanesi", new Dictionary<string, List<string>>
                {
                    { "Dahiliye", new List<string> { "Dr. Zeynep Su Meri", "Dr. Ali Kızıltaş" } },
                    { "Kardiyoloji", new List<string> { "Dr. Erdem Akbaş" } },
                    { "Genel Cerrahi", new List<string> { "Dr. Hasan Oğuz Kaya", "Dr. Rüya Deniz Vural" } }
                }
            },
            { "Hisar Hospital Intercontinental", new Dictionary<string, List<string>>
                {
                    { "Beslenme ve Diyet", new List<string> { "Dyt. Beyza Topçu" } },
                    { "Dermatoloji", new List<string> { "Dr. Cemile Dilek Uysal", "Funda Ataman" } },
                    { "Genel Cerrahi", new List<string> { "Dr. Kıvanç Derya Peker", "Dr. Merve Karlı" } }
                }
            },
        };

        private List<string> RandevuSaatleri = new List<string>
        {
            "09:00-09:30", "09:30-10:00", "10:00-10:30", "10:30-11:00", "11:00-11.30", "11:30-12:00", "13:00-13:30", "13:30-14:00"
        };

        private readonly FirebaseService _firebaseService;

        public RandevuSistemi()
        {
            _firebaseService = new FirebaseService();
        }

        private List<string> GetTarihler()
        {
            var tarihler = new List<string>();
            var bugun = DateTime.Now.Date;

            // Bugünden itibaren 30 günlük randevu tarihleri
            for (int i = 1; i <= 30; i++)
            {
                var tarih = bugun.AddDays(i);
                if (tarih.DayOfWeek != DayOfWeek.Saturday && tarih.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Tarih ve gün adını birleştirerek listeye ekle
                    string tarihVeGun = $"{tarih:dd/MM/yyyy} ({tarih.ToString("ddd", new System.Globalization.CultureInfo("tr-TR"))})";
                    tarihler.Add(tarihVeGun);
                }
            }
            return tarihler;
        }

        public interface IRandevuIslemleri
        {
            Task RandevuAl(Kullanici kullanici);
            Task RandevulariGoruntule(Kullanici kullanici);
            Task RandevuIptal(Kullanici kullanici);
        }

        public async Task RandevuAl(Kullanici kullanici)
        {
            if (kullanici == null)
            {
                AnsiConsole.MarkupLine("[red]Randevu alabilmek için giriş yapmanız gerekiyor.[/]");
                return;
            }

            var bolumler = new[] {
                "Geri",
                "Dahiliye",
                "Ortopedi",
                "Kardiyoloji",
                "Genel Cerrahi",
                "Göz Hastalıkları",
                "Üroloji",
                "Dermatoloji",
                "Nöroloji"
            };

            while (true)
            {
                Console.Clear();
                AnsiConsole.Write(new Rule("[cyan]Randevu Alma İşlemi[/]").RuleStyle("grey").Centered());

                var bolumSecimi = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("\n[green]Lütfen Bölüm Seçiniz[/]")
                        .PageSize(10)
                        .HighlightStyle(new Style().Foreground(Color.Cyan1))
                        .AddChoices(bolumler));

                if (bolumSecimi == "Geri")
                    return;

                // Bölüme göre hastaneleri filtrele
                var uygunHastaneler = HastaneBolumleri
                    .Where(h => h.Value.ContainsKey(bolumSecimi))
                    .Select(h => h.Key)
                    .ToList();

                if (!uygunHastaneler.Any())
                {
                    AnsiConsole.MarkupLine($"[red]{bolumSecimi} bölümü hiçbir hastanede bulunmamaktadır. Lütfen başka bir bölüm seçiniz.[/]");
                    Thread.Sleep(2000);
                    continue;
                }

                // Hastane seçimi
                var secilenHastane = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"[cyan]Hastaneler[/]")
                        .PageSize(10)
                        .HighlightStyle(new Style(foreground: Color.Cyan1)
                            .Decoration(Decoration.Bold))
                        .AddChoices(uygunHastaneler.Prepend("Geri"))
                        .UseConverter(hastane =>
                        {
                            return hastane == "Geri"
                                ? "[red]< Geri[/]"
                                : $"[white]{hastane}[/]";
                        }));

                if (secilenHastane == "Geri")
                    continue;

                Console.Clear();

                // Doktor seçimi (bölüme özgü)
                var doktorlar = HastaneBolumleri[secilenHastane][bolumSecimi];
                var secilenDoktor = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"\n[green]{secilenHastane} - {bolumSecimi} Bölümü - Doktor Seçimi[/]")
                        .PageSize(5)
                        .HighlightStyle(new Style().Foreground(Color.Cyan1))
                        .AddChoices(doktorlar.Prepend("Geri")));

                if (secilenDoktor == "Geri")
                    continue;

                // Tarih seçimi
                var tarihler = GetTarihler();
                var secilenTarih = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("\n[green]Lütfen Randevu Tarihi Seçiniz[/]")
                        .PageSize(10)
                        .HighlightStyle(new Style().Foreground(Color.Cyan1))
                        .AddChoices(tarihler.Prepend("Geri")));

                if (secilenTarih == "Geri")
                    continue;

                // Randevu saati seçimi
                var tumRandevular = await _firebaseService.TumRandevulariGetir();
                var saatler = new Table()
                    .Border(TableBorder.Rounded)
                    .Title("[cyan]Randevu Saatleri[/]")
                    .AddColumn("Saat")
                    .AddColumn("Durum");

                // Seçilen tarihi DateTime objesine dönüştür
                string tarihString = secilenTarih.Split(' ')[0];
                DateTime secilenTarihObj = DateTime.ParseExact(tarihString, "dd/MM/yyyy", null);

                foreach (var saat in RandevuSaatleri)
                {
                    string randevuKey = $"{bolumSecimi}-{secilenHastane}-{secilenDoktor}-{secilenTarihObj:dd/MM/yyyy}-{saat}";
                    string durum = tumRandevular.Any(r =>
                        $"{r.Bolum}-{r.Hastane}-{r.Doktor}-{r.RandevuTarihi:dd/MM/yyyy}-{r.Saat}" == randevuKey)
                        ? "[red]Dolu[/]" : "[green]Müsait[/]";
                    saatler.AddRow(saat, durum);
                }

                AnsiConsole.Write(saatler);

                var secilenSaat = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("\n[green]Lütfen Randevu Saati Seçiniz[/]")
                        .PageSize(5)
                        .HighlightStyle(new Style().Foreground(Color.Cyan1))
                        .AddChoices(RandevuSaatleri.Where(s =>
                            !tumRandevular.Any(r =>
                                $"{r.Bolum}-{r.Hastane}-{r.Doktor}-{r.RandevuTarihi:dd/MM/yyyy}-{r.Saat}" ==
                                $"{bolumSecimi}-{secilenHastane}-{secilenDoktor}-{secilenTarihObj:dd/MM/yyyy}-{s}"))
                            .Prepend("Geri")));

                if (secilenSaat == "Geri")
                    continue;

                // Randevu oluşturma işlemleri...
                string randevuKeyFinal = $"{bolumSecimi}-{secilenHastane}-{secilenDoktor}-{secilenTarihObj:dd/MM/yyyy}-{secilenSaat}";

                if (tumRandevular.Any(r =>
                    $"{r.Bolum}-{r.Hastane}-{r.Doktor}-{r.RandevuTarihi:dd/MM/yyyy}-{r.Saat}" == randevuKeyFinal))
                {
                    AnsiConsole.MarkupLine("[red]Bu randevu saati dolu![/]");
                    continue;
                }

                var yeniRandevu = new Randevu
                {
                    Bolum = bolumSecimi,
                    Hastane = secilenHastane,
                    Doktor = secilenDoktor,
                    Saat = secilenSaat,
                    RandevuTarihi = secilenTarihObj
                };

                var kayitBasarili = await _firebaseService.RandevuKaydet(kullanici.TC, yeniRandevu);
                if (!kayitBasarili)
                {
                    AnsiConsole.MarkupLine("[red]Randevu kaydedilirken bir hata oluştu![/]");
                    return;
                }

                // Randevu onay ekranı
                var onayPanel = new Panel(
                    Align.Center(
                        new Markup($"""
                            [green]Randevunuz Başarıyla Oluşturuldu![/]

                            [cyan]Bölüm:[/] {bolumSecimi}
                            [cyan]Hastane:[/] {secilenHastane}
                            [cyan]Doktor:[/] {secilenDoktor}
                            [cyan]Tarih:[/] {secilenTarihObj:dd/MM/yyyy}
                            [cyan]Saat:[/] {secilenSaat}

                            [yellow]SMS Bilgilendirmesi:[/] {kullanici.TelefonNo} numaralı telefona
                            randevu detaylarınız gönderilmiştir.

                            [red]NOT:[/] Randevunuza gelmemeniz durumunda 15 gün
                            boyunca yeni randevu alamazsınız!
                            """)
                    ))
                {
                    Border = BoxBorder.Double,
                    Padding = new Padding(2)
                };

                Console.Clear();
                AnsiConsole.Write(onayPanel);
                AnsiConsole.MarkupLine("\n[cyan]Ana menüye dönmek için bir tuşa basınız...[/]");
                Console.ReadKey();
                break;
            }
        }

        public async Task RandevulariGoruntule(Kullanici kullanici)
        {
            if (kullanici == null)
            {
                AnsiConsole.MarkupLine("[red]Randevularınızı görüntülemek için giriş yapmanız gerekiyor.[/]");
                return;
            }

            var randevular = await _firebaseService.RandevulariGetir(kullanici.TC);

            if (!randevular.Any())
            {
                AnsiConsole.MarkupLine("[yellow]\nHenüz randevunuz bulunmamaktadır.[/]");
                Console.WriteLine("\nAna menüye dönmek için bir tuşa basınız...");
                Console.ReadKey();
                Console.Clear();
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title($"[cyan]{kullanici.Isim} {kullanici.Soyisim}[/] Randevuları")
                .AddColumn(new TableColumn("[green]Bölüm[/]").Centered())
                .AddColumn(new TableColumn("[green]Hastane[/]").Centered())
                .AddColumn(new TableColumn("[green]Doktor[/]").Centered())
                .AddColumn(new TableColumn("[green]Saat[/]").Centered())
                .AddColumn(new TableColumn("[green]Tarih[/]").Centered());

            foreach (var randevu in randevular)
            {
                table.AddRow(
                    randevu.Bolum,
                    randevu.Hastane,
                    randevu.Doktor,
                    randevu.Saat,
                    randevu.RandevuTarihi.ToString("dd/MM/yyyy")
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("\n[red]NOT:[/] Randevunuza gelmemeniz durumunda 15 gün boyunca aynı bölümden yeni randevu alamazsınız!");
            AnsiConsole.MarkupLine("\n[cyan]Ana menüye dönmek için bir tuşa basınız...[/]");
            Console.ReadKey();
            Console.Clear();
        }

        public async Task RandevuIptal(Kullanici kullanici)
        {
            if (kullanici == null)
            {
                AnsiConsole.MarkupLine("[red]Randevu iptal etmek için giriş yapmanız gerekiyor.[/]");
                return;
            }

            var randevular = await _firebaseService.RandevulariGetir(kullanici.TC);

            if (!randevular.Any())
            {
                AnsiConsole.MarkupLine("[yellow]\nİptal edilecek randevunuz bulunmamaktadır.[/]");
                AnsiConsole.MarkupLine("\n[cyan]Ana menüye dönmek için bir tuşa basınız...[/]");
                Console.ReadKey();
                Console.Clear();
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[cyan]Randevularınız[/]")
                .AddColumn(new TableColumn("[green]No[/]").Centered())
                .AddColumn(new TableColumn("[green]Bölüm[/]").Centered())
                .AddColumn(new TableColumn("[green]Hastane[/]").Centered())
                .AddColumn(new TableColumn("[green]Doktor[/]").Centered())
                .AddColumn(new TableColumn("[green]Saat[/]").Centered());

            for (int i = 0; i < randevular.Count; i++)
            {
                var randevu = randevular[i];
                table.AddRow(
                    (i + 1).ToString(),
                    randevu.Bolum,
                    randevu.Hastane,
                    randevu.Doktor,
                    randevu.Saat
                );
            }

            AnsiConsole.Write(table);

            var secim = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]İptal etmek istediğiniz randevunun numarasını girin (Geri için 0):[/]")
                    .Validate(num =>
                        num >= 0 && num <= randevular.Count
                            ? ValidationResult.Success()
                            : ValidationResult.Error("[red]Geçersiz seçim![/]")));

            if (secim == 0) return;

            var iptalEdilecekRandevu = randevular[secim - 1];
            var iptalBasarili = await _firebaseService.RandevuIptal(kullanici.TC, iptalEdilecekRandevu.Id);
            if (!iptalBasarili)
            {
                AnsiConsole.MarkupLine("[red]Randevu iptal edilirken bir hata oluştu![/]");
                return;
            }

            kullanici.SonIptalTarihi = DateTime.Now;

            Console.WriteLine("\nRandevunuz iptal edildi.");
            Console.WriteLine($"SMS Bilgilendirmesi: {kullanici.TelefonNo} numaralı telefona randevu iptal bilgileriniz gönderilmiştir.");
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            AnsiConsole.Write(new FigletText("Hastane Randevu").Centered().Color(Color.Cyan1));
            AnsiConsole.WriteLine();

            KullaniciYonetimi kullaniciYonetimi = new KullaniciYonetimi();
            FirebaseService firebaseService = new FirebaseService();

            while (true)
            {
                if (kullaniciYonetimi.GetAktifKullanici() == null)
                {
                    Console.Clear();
                    AnsiConsole.Write(new Rule("[cyan]Randevu Sistemi[/]").RuleStyle("grey").Centered());

                    var secim = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("\n[green]Lütfen yapmak istediğiniz işlemi seçin:[/]")
                            .PageSize(4)
                            .AddChoices(new[] {
                                "1. Giriş Yap",
                                "2. Kayıt Ol",
                                "3. Şifremi Unuttum",
                                "4. Çıkış"
                            }));

                    switch (secim.Substring(0, 1))
                    {
                        case "1":
                            await kullaniciYonetimi.GirisYap();
                            break;
                        case "2":
                            await kullaniciYonetimi.KayitOl();
                            break;
                        case "3":
                            await kullaniciYonetimi.SifremiUnuttum();
                            Console.WriteLine("\nAna menüye dönmek için bir tuşa basın...");
                            Console.ReadKey();
                            break;
                        case "4":
                            Environment.Exit(0);
                            break;
                    }
                }
                else
                {
                    var aktifKullanici = kullaniciYonetimi.GetAktifKullanici();

                    // Kullanıcının admin olup olmadığını kontrol et
                    var adminKullanici = await firebaseService.KullaniciGetir(aktifKullanici.TC);
                    bool isAdmin = adminKullanici != null &&
                                 adminKullanici.YetkiSeviyesi == "Admin";

                    Console.Clear();
                    AnsiConsole.Write(new Rule("[cyan]Randevu Sistemi[/]").RuleStyle("grey").Centered());

                    var menuSecenekleri = new List<string>();
                    if (isAdmin)
                    {
                        menuSecenekleri.AddRange(new[] {
                            "1. Admin Paneli",
                            "2. Randevu Al",
                            "3. Randevularımı Görüntüle",
                            "4. Profili Görüntüle",
                            "5. Randevu İptal",
                            "6. Çıkış Yap"
                        });
                    }
                    else
                    {
                        menuSecenekleri.AddRange(new[] {
                            "1. Randevu Al",
                            "2. Randevularımı Görüntüle",
                            "3. Profili Görüntüle",
                            "4. Randevu İptal",
                            "5. Çıkış Yap"
                        });
                    }

                    var secim = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title($"\n[green]Hoş geldiniz, {aktifKullanici.Isim} {aktifKullanici.Soyisim}[/]")
                            .PageSize(6)
                            .AddChoices(menuSecenekleri));

                    int secimNo = int.Parse(secim.Substring(0, 1));

                    if (isAdmin)
                    {
                        switch (secimNo)
                        {
                            case 1: // Admin Paneli
                                var adminKullaniciObj = new AdminKullanici(
                                    aktifKullanici.TC,
                                    aktifKullanici.Isim,
                                    aktifKullanici.Soyisim,
                                    aktifKullanici.DogumTarihi,
                                    aktifKullanici.Sifre,
                                    "Admin"
                                );
                                await adminKullaniciObj.AdminPaneliGoster();
                                break;
                            case 2:
                                await kullaniciYonetimi.RandevuAl(aktifKullanici);
                                break;
                            case 3:
                                await kullaniciYonetimi.RandevulariGoruntule(aktifKullanici);
                                break;
                            case 4:
                                kullaniciYonetimi.ProfiliGoruntule();
                                break;
                            case 5:
                                await kullaniciYonetimi.RandevuIptal(aktifKullanici);
                                break;
                            case 6:
                                kullaniciYonetimi.CikisYap();
                                break;
                        }
                    }
                    else
                    {
                        switch (secimNo)
                        {
                            case 1:
                                await kullaniciYonetimi.RandevuAl(aktifKullanici);
                                break;
                            case 2:
                                await kullaniciYonetimi.RandevulariGoruntule(aktifKullanici);
                                break;
                            case 3:
                                kullaniciYonetimi.ProfiliGoruntule();
                                break;
                            case 4:
                                await kullaniciYonetimi.RandevuIptal(aktifKullanici);
                                break;
                            case 5:
                                kullaniciYonetimi.CikisYap();
                                break;
                        }
                    }
                }
            }
        }
    }
}