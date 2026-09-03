# Amard.Android — اپلیکیشن پرداخت قبض (MAUI + دستگاه POS)

اپلیکیشن .NET MAUI (فقط اندروید) برای: جستجوی قبض از سرور، پرداخت با دستگاه POS اسان‌پرداخت (AsanPardakht) از طریق AIDL، اعلام پرداخت به سرور و چاپ رسید.

## فلوی کار
1. کاربر نوع جستجو را انتخاب می‌کند (نام مالک / کد ملی / کد نوسازی) و دکمه «جستجو» را می‌زند.
2. اپ به سرور درخواست می‌زند و مبلغ قبض را دریافت می‌کند. (`Services/BillApiService.cs`)
3. با زدن «پرداخت با دستگاه POS»، اپ از طریق AIDL به سرویس POS متصل شده و تراکنش را شروع می‌کند. (`Services/PosService.cs`)
4. کاربر پرداخت را روی دستگاه انجام می‌دهد؛ نتیجه در `MainActivity.OnActivityResult` دریافت می‌شود.
5. در صورت موفقیت، اپ پرداخت را به سرور اعلام می‌کند (`POST bill/confirm-payment`).
6. در نهایت رسید روی پرینتر دستگاه POS چاپ می‌شود (`bitmapPrint` از طریق AIDL).

## ساختار پروژه
```
src/Amard.App/
├── Aidl/                      فایل‌های .aidl سرویس POS
├── AidlShims.cs               کلاس‌های کمکی و Parcelable های موردنیاز کد تولیدی AIDL
├── Platforms/Android/         AndroidManifest و MainActivity
├── Views/MainPage.xaml        رابط کاربری (فارسی / RTL)
├── ViewModels/MainViewModel   منطق UI (CommunityToolkit.Mvvm)
├── Services/BillApiService    ارتباط با سرور
├── Services/PosService        اتصال، پرداخت، استعلام و چاپ
├── Models/                    مدل‌ها
└── AppConstants.cs            تنظیمات (آدرس سرور، پکیج POS، hostId و ...)
```

## قبل از اجرا — این موارد را تنظیم کنید
1. **AppConstants.ApiBaseUrl** — آدرس واقعی سرور (endpointهای `bill/search` و `bill/confirm-payment` sample هستند).
2. **AppConstants.PosServicePackage / PosServiceAction** — نام پکیج و اکشن سرویس POS طبق مستندات رسمی اسان‌پرداخت (PDF همراه).
3. **AppConstants.HostId** — شناسه host دریافتی از اسان‌پرداخت.
4. **hostSign** — در صورت نیاز به امضای دیجیتال درخواست، امضا در `PosService.StartPaymentAsync` محاسبه و پاس داده شود.
5. **ساختار JSON درخواست پرداخت** — مطابق «AsanPardakht POS Service Communication Document» اصلاح شود.
6. از آنجا که PDF مستندات قابل خواندن خودکار نبود، فرض‌های زیر با داکیومنت تطبیق داده شود:
   - کد تراکنش استفاده‌شده: `SERVICE_BILL (1503)` — در صورت نیاز به `PURCHASE (1502)` تغییر دهید.
   - کلیدهای نتیجه در `POSServiceResult.ResultKeys`.
   - جزئیات مارشال `bitmapPrint` در `PosService.BitmapPrintNative` (تراکنش شماره 14 / FirstCallTransaction + 13).

## بیلد و اجرا
```bash
dotnet build src/Amard.App/Amard.App.csproj -f net10.0-android
dotnet build -t:Run -f net10.0-android src/Amard.App/Amard.App.csproj   # اجرا روی دستگاه متصل
```

دستگاه باید دارای دستگاه POS فیزیکی (یا تستر `pos-service-tester`) با برنامه سرویس اسان‌پرداخت نصب‌شده باشد.
