# Amard.Android — اپلیکیشن پرداخت قبض (MAUI + دستگاه POS)

اپلیکیشن .NET MAUI (فقط اندروید) برای: جستجوی قبض از سرور، پرداخت با دستگاه POS اسان‌پرداخت (AsanPardakht) از طریق AIDL، اعلام پرداخت به سرور و چاپ رسید.

## فلوی کار
1. کاربر نوع جستجو را انتخاب می‌کند (نام مالک / کد ملی / کد نوسازی) و دکمه «جستجو» را می‌زند.
2. اپ به سرور درخواست می‌زند و مبلغ قبض را دریافت می‌کند. (`Services/BillApiService.cs`)
3. با زدن «پرداخت با دستگاه POS»، اپ از طریق AIDL به سرویس POS متصل شده و تراکنش را شروع می‌کند. (`Services/PosService.cs`)
   - JSON درخواست (`HostRequest`) با فیلدهای `hi/hti/hrt/hoc/hak/hav` و شناسه‌های قبض و پرداخت (`bi/pi`) ساخته می‌شود.
4. کاربر پرداخت را روی دستگاه انجام می‌دهد؛ نتیجه در `MainActivity.OnActivityResult` دریافت می‌شود.
   در صورت نامشخص بودن نتیجه (`9999`)، وضعیت تراکنش با `getTransactionStatus` استعلام می‌شود.
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
├── Services/UpdateService     به‌روزرسانی خودکار از GitHub Releases
├── Models/                    مدل‌ها
└── AppConstants.cs            تنظیمات (آدرس سرور، پکیج POS، hostId، hostApiKey و ...)
```

## به‌روزرسانی خودکار
برنامه نسخه‌های جدید را از GitHub Releases بررسی، دانلود و نصب می‌کند.
روش انتشار نسخه جدید: `.\scripts\publish-update.ps1 -Version 1.1.1 -Notes "..."` — راهنمای کامل در [UPDATE.md](UPDATE.md).

## قبل از اجرا — این موارد را تنظیم کنید
تمام موارد زیر از داخل برنامه و در صفحه «⚙ تنظیمات» قابل تغییر و ذخیره هستند:

1. **سرور API** — پورت، مسیر پایه، آدرس دستگاه واقعی/شبیه‌ساز.
2. **سرویس POS** — پکیج (`com.persianswitch.smartpos`) و اکشن سرویس
   (`com.persianswitch.smartpos.service`) طبق نمونه رسمی اسان‌پرداخت. در صورت ناموفق بودن،
   برنامه به‌صورت خودکار اتصال مستقیم به کلاس سرویس
   (`com.persianswitch.smartpos.coreservice.ApplicationService`) را هم امتحان می‌کند.
3. **HostId** — شناسه host دریافتی از اسان‌پرداخت.
4. **HostApiKey** — کلید API میزبان دریافتی از اسان‌پرداخت (در فیلد `hak` ارسال می‌شود).
5. **کدهای تراکنش** — خرید `1502` و قبض خدماتی `1503`.
6. **زبان SDK** — `fa` یا `en`.

## پروتکل پرداخت اسان‌پرداخت (طبق مستندات «اتصال به پایانه‌های فروش اندرویدی» V1.0.0)

**اتصال:** `bindService` با اکشن `com.persianswitch.smartpos.service` و پکیج
`com.persianswitch.smartpos` (به مجوز `com.persianswitch.smartpos.permission.REMOTE` در
`AndroidManifest.xml` نیاز دارد).

**شروع تراکنش:**
```csharp
PendingIntent pi = service.StartTransaction(
    transactionCode : 1503,     // SERVICE_BILL
    hostRequest     : "{...}",  // JSON درخواست
    hostSign        : "",       // طبق مستندات فعلاً همیشه رشته خالی
    hostId          : 2040,     // همان مقدار hi
    lang            : "fa");
// سپس حتماً با StartIntentSenderForResult نمایش داده شود
```

**نکته پیاده‌سازی (فرمت wire سمت Binder):** فراخوانی‌های `startTransaction`،
`getTransactionStatus` و `bitmapPrint` در `PosService` به‌جای پروکسی تولیدی AIDL دات‌نت،
مستقیماً روی Binder انجام می‌شوند (`StartTransactionNative` و `GetTransactionStatusNative`).
دلیل: کامپایلر AIDL دات‌نت خروجی `PendingIntent` را StrongBinder فرض می‌کند، در حالی که
کامپایلر رسمی AIDL جاوا (کد تولید `UserDataType` در `system/tools/aidl` — یکسان در اندروید ۵ تا ۱۳)
پارامتر/خروجی‌های Parcelable را به شکل «`writeInt(1) + writeToParcel`» روی wire می‌نویسد
(و `PendingIntent` هم در `writeToParcel` خود فقط یک StrongBinder از `IIntentSender` می‌نویسد).
استفاده از پروکسی تولیدی باعث می‌شد پاسخ اشتباه خوانده شود و همیشه خطای
«دستگاه POS قادر به شروع تراکنش نبود» ظاهر شود.

**HostRequest (JSON) برای پرداخت قبوض خدماتی:**

| کلید | نوع | توضیح |
|---|---|---|
| `hi` | long | شناسه host (Host Id) |
| `hti` | long | کد یکتای تراکنش میزبان (64 بیت) |
| `hrt` | string | زمان درخواست با الگوی `yyyy-MM-dd'T'HH:mm:ss.SSS-Z` |
| `hoc` | int | کد عملیات (`1503` برای قبض خدماتی) |
| `hak` | string | کلید API میزبان (Host Api Key) |
| `hav` | string | نسخه اپلیکیشن میزبان |
| `bi` | long | شناسه قبض (13 رقمی) |
| `pi` | long | شناسه پرداخت (6 تا 13 رقمی) |

**نتیجه تراکنش (`onActivityResult`)** — فقط وقتی `resultCode == RESULT_OK` باشد معتبر است و
کلیدهای زیر در `Intent` برمی‌گردند (کلاس `POSServiceResult.ResultKeys`):

| کلید | توضیح |
|---|---|
| `status_code` | وضعیت انجام تراکنش (0 = پایان موفق فرآیند، 9999 = نامشخص، 10001..10004 و 11000..11003 خطاها) |
| `status_message` | پیام وضعیت |
| `response_data` | JSON پاسخ سرور (HostResponse) با فیلدهای `sc`, `sm`, `am`, `aa`, `aad`, `rrn`, `hti` |
| `masked_card` / `stan` / `transaction_date` / `is_payment_by_credit` | اطلاعات تکمیلی تراکنش |

تراکنش تنها زمانی **موفق** است که هم `status_code` بیرونی صفر باشد و هم `sc` داخل
`response_data` صفر باشد. در حالت `status_code == 9999` برنامه به‌صورت خودکار
`getTransactionStatus(hti)` را برای استعلام نتیجه صدا می‌زند.

**چاپ رسید:** `bitmapPrint(lang, bitmap, printId, IPOSPrintStatusCallBack)` — این متد در
`PosService.BitmapPrintNative` به‌صورت مستقیم روی Binder فراخوانی می‌شود، چون پارامتر
`Bitmap` در AIDL یک Parcelable است و باید مطابق کد تولیدی جاوا به شکل
«`writeInt(1)` + `Bitmap.writeToParcel`» نوشته شود (StrongBinder یا `writeParcelable` ناسازگار است).


## بیلد و اجرا
```bash
dotnet build src/Amard.App/Amard.App.csproj -f net10.0-android
dotnet build -t:Run -f net10.0-android src/Amard.App/Amard.App.csproj   # اجرا روی دستگاه متصل
```

دستگاه باید دارای دستگاه POS فیزیکی (یا تستر `pos-service-tester`) با برنامه سرویس اسان‌پرداخت نصب‌شده باشد.
