using Amard.App.Models;
using Amard.App.Platforms.Android;
using Android.Content;
using Android.Graphics;
using Com.Persianswitch.Smartpos.Aidl;
using AColor = Android.Graphics.Color;
using APaint = Android.Graphics.Paint;

namespace Amard.App.Services;

/// <summary>
/// سرویس ارتباط با دستگاه POS اسان پرداخت از طریق AIDL:
/// اتصال به سرویس، ارسال پرداخت، استعلام وضعیت و چاپ رسید.
/// </summary>
public class PosService : IDisposable
{
    private IPosService? _posService;
    private bool _connected;
    private PosServiceConnection? _connection;
    private Android.OS.IBinder? _remoteBinder;

    /// <summary>آخرین دلیل ناموفق بودن اتصال (برای نمایش خطای دقیق به کاربر)</summary>
    public string LastConnectError { get; private set; } = string.Empty;

    /// <summary>
    /// کد تراکنش میزبان (فیلد hti) آخرین تراکنش آغازشده.
    /// برای استعلام نتیجه تراکنشی که وضعیت آن نامشخص است (getTransactionStatus) استفاده می‌شود.
    /// </summary>
    public long LastHostTranId { get; private set; }

    /// <summary>شمارنده یکتاساز کد تراکنش میزبان (مقدار اولیه: میلی‌ثانیه‌های epoch)</summary>
    private static long _hostTranIdCounter = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Context مناسب برای bind (ترجیحاً Activity فعلی، در نبود آن Application context)</summary>
    private static Context CurrentContext =>
        Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
        ?? (Context)Android.App.Application.Context;

    /// <summary>تولید کد یکتای ۶۴ بیتی تراکنش میزبان (hti) - طبق مستندات باید برای هر درخواست یکتا باشد</summary>
    private static long NextHostTranId() => System.Threading.Interlocked.Increment(ref _hostTranIdCounter);

    /// <summary>
    /// بررسی نصب بودن برنامه سرویس POS روی دستگاه.
    /// در اندروید 11+ به‌دلیل package visibility، باید پکیج در بخش &lt;queries&gt; منیفست اعلام شده باشد.
    /// </summary>
    public static bool IsPosServiceInstalled()
    {
        var package = AppConstants.PosServicePackage;
        if (string.IsNullOrWhiteSpace(package))
            return false;

        try
        {
            var pm = CurrentContext.PackageManager;
            if (pm is null)
                return false;

            pm.GetPackageInfo(package, 0);
            return true;
        }
        catch (Android.Content.PM.PackageManager.NameNotFoundException)
        {
            return false;
        }
        catch (Exception)
        {
            // در صورت هر خطای دیگر، فرض می‌کنیم شاید نصب باشد و اجازه می‌دهیم bind امتحان شود
            return true;
        }
    }

    /// <summary>
    /// کشف سرویس POS با استفاده از PackageManager.QueryIntentServices.
    /// این روش مستقل از نام دقیق پکیج/کلاس، سرویسی را که به اکشن پاسخ می‌دهد پیدا می‌کند
    /// و ComponentName واقعی آن را برمی‌گرداند (روش قابل‌اعتماد در اندروید 11+).
    /// </summary>
    private static ComponentName? ResolvePosServiceComponent()
    {
        var pm = CurrentContext.PackageManager;
        if (pm is null)
            return null;

        foreach (var action in new[] { AppConstants.PosServiceAction, AppSettingsService.LegacyPosServiceAction })
        {
            if (string.IsNullOrWhiteSpace(action))
                continue;

            try
            {
                var intent = new Intent(action);
                var services = pm.QueryIntentServices(intent, Android.Content.PM.PackageInfoFlags.MatchAll);
                var info = services?.FirstOrDefault()?.ServiceInfo;
                if (info?.PackageName is { } pkg && info.Name is { } cls)
                    return new ComponentName(pkg, cls);
            }
            catch (Exception)
            {
                // اکشن بعدی را امتحان کن
            }
        }

        return null;
    }

    /// <summary>
    /// اتصال (Bind) به سرویس POS دستگاه.
    /// طبق مستندات/نمونه رسمی اسان پرداخت، اتصال با اکشن «com.persianswitch.smartpos.service»
    /// انجام می‌شود؛ در صورت پاسخ ندادن، اتصال مستقیم به کلاس سرویس و سپس اکشن قدیمی امتحان می‌شود.
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        if (_connected && _posService is not null)
            return true;

        LastConnectError = string.Empty;
        var attemptErrors = new List<string>();

        foreach (var (intent, label) in BuildBindIntents())
        {
            var (ok, error) = await TryBindAsync(intent);
            if (ok)
                return true;

            attemptErrors.Add($"{label}: {error}");
        }

        // اگر همه روش‌ها ناموفق بود، بر اساس نصب بودن یا نبودن برنامه، پیام مناسب بساز
        if (!IsPosServiceInstalled() && ResolvePosServiceComponent() is null)
        {
            LastConnectError =
                $"برنامه سرویس پرداخت پایانه («{AppConstants.PosServicePackage}») روی دستگاه یافت نشد. " +
                "نام پکیج/اکشن روی این دستگاه با مقدار تنظیم‌شده فرق می‌کند. " +
                "در صفحه «تنظیمات» دکمه «🔍 شروع شناسایی» (شناسایی خودکار سرویس پرداخت) را بزنید " +
                "و سرویس واقعی دستگاه را انتخاب و ذخیره کنید.";
        }
        else
        {
            LastConnectError =
                "اتصال به سرویس POS برقرار نشد (برنامه سرویس نصب است اما پاسخ نداد). " +
                "معمولاً به این معناست که پکیج/امضای این برنامه هنوز نزد آسان‌پرداخت ثبت نشده است. جزئیات: "
                + string.Join(" | ", attemptErrors);
        }

        return false;
    }

    /// <summary>ساخت Intent های اتصال به سرویس POS به ترتیب اولویت</summary>
    private static List<(Intent Intent, string Label)> BuildBindIntents()
    {
        var package = AppConstants.PosServicePackage;
        var intents = new List<(Intent, string)>();

        // ۰) کامپوننت صریحِ انتخاب‌شده توسط کاربر (از «شناسایی خودکار» در تنظیمات) — بالاترین اولویت
        if (!string.IsNullOrWhiteSpace(package) && !string.IsNullOrWhiteSpace(AppConstants.PosServiceClass))
        {
            var pickedIntent = new Intent();
            pickedIntent.SetComponent(new ComponentName(package, AppConstants.PosServiceClass));
            if (!string.IsNullOrWhiteSpace(AppConstants.PosServiceAction))
                pickedIntent.SetAction(AppConstants.PosServiceAction);
            intents.Add((pickedIntent, "کامپوننت تنظیم‌شده"));
        }

        // ۱) کامپوننت کشف‌شده از خود سیستم (قابل‌اعتمادترین روش در اندروید 11+)
        var resolved = ResolvePosServiceComponent();
        if (resolved is not null)
        {
            var resolvedIntent = new Intent();
            resolvedIntent.SetComponent(resolved);
            if (!string.IsNullOrWhiteSpace(AppConstants.PosServiceAction))
                resolvedIntent.SetAction(AppConstants.PosServiceAction);
            intents.Add((resolvedIntent, "کامپوننت کشف‌شده"));
        }

        // ۲) اکشن سرویس + پکیج (روش اصلی طبق نمونه رسمی)
        if (!string.IsNullOrWhiteSpace(AppConstants.PosServiceAction))
            intents.Add((CreateActionIntent(AppConstants.PosServiceAction, package), "اکشن سرویس"));

        // ۳) اکشن قدیمی (نام اینترفیس AIDL) - فقط برای سازگاری با نسخه‌های قبلی
        intents.Add((CreateActionIntent(AppSettingsService.LegacyPosServiceAction, package), "اکشن قدیمی"));

        return intents;
    }

    private static Intent CreateActionIntent(string action, string? package)
    {
        var intent = new Intent(action);
        if (!string.IsNullOrWhiteSpace(package))
            intent.SetPackage(package);
        return intent;
    }

    private async Task<(bool Ok, string Error)> TryBindAsync(Intent intent)
    {
        var context = CurrentContext;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var connection = new PosServiceConnection(
            onConnected: binder =>
            {
                if (tcs.Task.IsCompleted)
                    return;

                _remoteBinder = binder;
                _posService = IPosServiceStub.AsInterface(binder);
                _connected = true;
                tcs.TrySetResult(true);
            },
            onDisconnected: () =>
            {
                _connected = false;
                _posService = null;
            });

        bool bound;
        try
        {
            bound = context.BindService(intent, connection, Bind.AutoCreate);
        }
        catch (Exception ex)
        {
            return (false, $"خطای bindService ({ex.Message})");
        }

        if (!bound)
        {
            try { context.UnbindService(connection); } catch { }
            return (false, "سرویس یافت نشد یا مجوز دسترسی وجود ندارد");
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        if (completed == tcs.Task && tcs.Task.Result)
        {
            _connection = connection;
            return (true, string.Empty);
        }

        try { context.UnbindService(connection); } catch { }
        return (false, "سرویس متصل نشد (احتمالاً پذیرنده/امضای برنامه نزد آسان‌پرداخت ثبت نشده است)");
    }



    private sealed class PosServiceConnection : Java.Lang.Object, IServiceConnection
    {
        private readonly Action<Android.OS.IBinder> _onConnected;
        private readonly Action? _onDisconnected;

        public PosServiceConnection(Action<Android.OS.IBinder> onConnected, Action? onDisconnected = null)
        {
            _onConnected = onConnected;
            _onDisconnected = onDisconnected;
        }

        public void OnServiceConnected(ComponentName? name, Android.OS.IBinder? binder)
        {
            if (binder != null)
                _onConnected(binder);
        }

        public void OnServiceDisconnected(ComponentName? name)
        {
            // اتصال قطع شده است؛ اتصال بعدی از نو برقرار می‌شود
            if (_onDisconnected is not null)
                _onDisconnected();
        }
    }

    /// <summary>
    /// شروع تراکنش پرداخت قبض خدماتی (کد 1503) روی دستگاه POS.
    /// طبق مستندات «اتصال به پایانه‌های فروش اندرویدی آسان پرداخت»:
    ///   - متد: startTransaction(transactionCode, hostRequest, hostSign, hostId, lang)
    ///   - hostRequest یک رشته JSON است که همه فیلدهای مشترک (hi, hti, hrt, hoc, hak, hav) و
    ///     فیلدهای اختصاصی پرداخت قبض (bi = شناسه قبض، pi = شناسه پرداخت) را شامل می‌شود.
    ///   - hostSign در حال حاضر برای همه پذیرنده‌ها رشته خالی است (چک Sign غیرفعال است).
    ///   - hostId همان مقدار فیلد hi است.
    /// </summary>
    public async Task StartPaymentAsync(BillInfo bill)
    {
        if (!await ConnectAsync())
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(LastConnectError)
                    ? "اتصال به سرویس POS برقرار نشد. مطمئن شوید برنامه سرویس POS نصب و فعال است."
                    : LastConnectError);

        LastHostTranId = NextHostTranId();

        var hostRequest = BuildServiceBillHostRequest(bill, LastHostTranId);

        // فراخوانی AIDL در یک ترد جداگانه انجام می‌شود تا رابط کاربری قفل نشود.
        // نکته: به‌جای پروکسی تولیدی AIDL (که فرمت پاسخ را اشتباه می‌خواند)، مستقیماً با
        // فرمت رسمی کامپایلر AIDL جاوا روی Binder فراخوانی می‌کنیم (StartTransactionNative).
        var pendingIntent = await Task.Run(() => StartTransactionNative(
            AppConstants.TransactionCodeServiceBill,
            hostRequest,
            string.Empty,      // hostSign - طبق مستندات فعلاً همیشه رشته خالی
            AppConstants.HostId,
            AppConstants.Lang));

        if (pendingIntent is null)
            throw new InvalidOperationException(
                "دستگاه POS قادر به شروع تراکنش نبود (سرویس POS درخواست را رد کرد و مقدار null برگرداند). " +
                "بررسی کنید: ۱) HostId و HostApiKey در تنظیمات، مقادیر واقعی دریافتی از اسان‌پرداخت باشند؛ " +
                "۲) پکیج و امضای این برنامه نزد اسان‌پرداخت برای این پایانه ثبت شده باشد؛ " +
                "۳) پایانه فعال/لاگین باشد و تراکنش دیگری در جریان نباشد؛ " +
                "۴) شناسه قبض (۱۳ رقمی) و شناسه پرداخت (۶ تا ۱۳ رقمی) معتبر باشند.");

        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?? throw new InvalidOperationException("Activity فعلی در دسترس نیست.");

        // طبق مستندات باید حتماً به‌صورت ForResult نمایش داده شود تا نتیجه در onActivityResult برگردد
        activity.StartIntentSenderForResult(
            pendingIntent.IntentSender!,
            MainActivity.PaymentRequestCode,
            null!, 0, 0, 0);
    }

    // ============================ فراخوانی مستقیم روی Binder ============================

    /// <summary>شناسه اینترفیس AIDL سرویس POS (برای WriteInterfaceToken)</summary>
    private const string PosInterfaceDescriptor = "com.persianswitch.smartpos.aidl.IPosService";

    /// <summary>
    /// فراخوانی مستقیم startTransaction روی Binder.
    /// چرا از پروکسی تولیدی AIDL استفاده نمی‌شود؟ کامپایلر AIDL دات‌نت نوع خروجی PendingIntent را
    /// StrongBinder فرض کرده (PendingIntentStub.AsInterface)، اما کامپایلر رسمی AIDL جاوا
    /// (UserDataType در type_java.cpp پروژه system/tools/aidl — یکسان در اندروید ۵ تا ۱۳)
    /// خروجی Parcelable را با فرمت «writeInt(1) + writeToParcel» می‌نویسد و خود PendingIntent هم
    /// در writeToParcel فقط یک StrongBinder (IIntentSender) می‌نویسد (PendingIntent.java).
    /// خواندن StrongBinder در جای فلگ int باعث می‌شد نتیجه همیشه null خوانده شود و
    /// خطای «دستگاه POS قادر به شروع تراکنش نبود» نمایش داده شود.
    /// </summary>
    private Android.App.PendingIntent? StartTransactionNative(
        int transactionCode, string hostRequest, string hostSign, int hostId, string lang)
    {
        if (_remoteBinder is null)
            return null;

        using var data = Android.OS.Parcel.Obtain();
        using var reply = Android.OS.Parcel.Obtain();
        try
        {
            data.WriteInterfaceToken(PosInterfaceDescriptor);
            data.WriteInt(transactionCode);
            data.WriteString(hostRequest);
            data.WriteString(hostSign);
            data.WriteInt(hostId);
            data.WriteString(lang);

            _remoteBinder.Transact(IPosServiceStub.TransactionStartTransaction, data, reply, 0);
            reply.ReadException();

            // پاسخ طبق کد تولیدی جاوا: در صورت موفقیت [int 1][StrongBinder] و در صورت رد [int 0]
            var flagPosition = reply.DataPosition();
            var flag = reply.ReadInt();

            if (flag == 0)
                return null; // سرویس درخواست را رد کرد

            if (flag != 1)
            {
                // سازگاری با سرویس‌های خیلی قدیمی که فلگ int نمی‌نویسند: بازگشت و خواندن مستقیم binder
                reply.SetDataPosition(flagPosition);
            }

            var binder = reply.ReadStrongBinder();
            if (binder is null)
                return null;

            // بازسازی PendingIntent اندروید از روی binder (مانند PendingIntent.CREATOR جاوا)
            using var parcel = Android.OS.Parcel.Obtain();
            parcel.WriteStrongBinder(binder);
            parcel.SetDataPosition(0);
            return Android.App.PendingIntent.ReadPendingIntentOrNullFromParcel(parcel);
        }
        finally
        {
            reply.Recycle();
            data.Recycle();
        }
    }

    /// <summary>
    /// فراخوانی مستقیم getTransactionStatus روی Binder.
    /// همان ناسازگاری StartTransactionNative اینجا هم وجود دارد: پروکسی تولیدی .NET پاسخ را با
    /// Bundle.CREATOR می‌خواند، در حالی که فرمت واقعی سمت سرویس «int 1 + فیلدهای
    /// POSTransactionInquiryResult» است. ترتیب فیلدها مطابق writeToParcel کلاس جاوای SDK.
    /// </summary>
    private POSTransactionInquiryResult? GetTransactionStatusNative(long hostTranId)
    {
        if (_remoteBinder is null)
            return null;

        using var data = Android.OS.Parcel.Obtain();
        using var reply = Android.OS.Parcel.Obtain();
        try
        {
            data.WriteInterfaceToken(PosInterfaceDescriptor);
            data.WriteLong(hostTranId);

            _remoteBinder.Transact(IPosServiceStub.TransactionGetTransactionStatus, data, reply, 0);
            reply.ReadException();

            if (reply.ReadInt() == 0)
                return null; // تراکنشی با این شناسه یافت نشد

            // مطابق POSTransactionInquiryResult.writeToParcel در SDK جاوا:
            // status(int), statusMessage(String), maskedCard(String), transactionStan(int),
            // RRN(String), transactionDate(Serializable), extraData(String)
            return new POSTransactionInquiryResult
            {
                Status = reply.ReadInt(),
                StatusMessage = reply.ReadString(),
                MaskedCard = reply.ReadString(),
                TransactionStan = reply.ReadInt(),
                RRN = reply.ReadString(),
                TransactionDate = (Java.Util.Date?)reply.ReadSerializable(),
                ExtraData = reply.ReadString()
            };
        }
        finally
        {
            reply.Recycle();
            data.Recycle();
        }
    }

    // ============================ ساخت HostRequest ============================

    /// <summary>
    /// ساخت JSON درخواست (HostRequest) برای پرداخت قبوض خدماتی (opCode = 1503).
    /// فیلدهای مشترک:
    ///   hi  → شناسه میزبان (Host Id)
    ///   hti → کد یکتای تراکنش میزبان (64 بیت)
    ///   hrt → زمان درخواست
    ///   hoc → کد عملیات (1503)
    ///   hak → کلید API میزبان (Host Api Key)
    ///   hav → نسخه اپلیکیشن میزبان
    /// فیلدهای اختصاصی:
    ///   bi  → شناسه قبض (13 رقمی)
    ///   pi  → شناسه پرداخت (6 تا 13 رقمی)
    /// </summary>
    public static string BuildServiceBillHostRequest(BillInfo bill, long hostTranId)
    {
        var fields = new Dictionary<string, object?>
        {
            ["hi"] = AppConstants.HostId,
            ["hti"] = hostTranId,
            ["hrt"] = BuildHostReqTime(),
            ["hoc"] = AppConstants.TransactionCodeServiceBill,
            ["hak"] = AppConstants.HostApiKey,
            ["hav"] = UpdateService.CurrentVersion,
            ["bi"] = ToJsonNumberOrString(bill.BillId),
            ["pi"] = ToJsonNumberOrString(bill.PayId)
        };

        return System.Text.Json.JsonSerializer.Serialize(fields);
    }

    /// <summary>
    /// زمان درخواست (fیلد hrt) با همان الگوی نمونه رسمی اسان پرداخت:
    /// SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS-Z", Locale.US)
    /// که در جاوا به شکل «2026-09-25T14:03:12.123-+0330» تولید می‌شود
    /// (کاراکتر Z یعنی آفست منطقه زمانی به فرمت RFC822).
    /// </summary>
    public static string BuildHostReqTime()
    {
        var now = DateTimeOffset.Now;
        var minutes = (int)now.Offset.TotalMinutes;
        var sign = minutes < 0 ? '-' : '+';
        var absolute = Math.Abs(minutes);

        var rfc822Offset = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0}{1:D2}{2:D2}", sign, absolute / 60, absolute % 60);

        return now.ToString(AppConstants.HostReqTimeFormat, System.Globalization.CultureInfo.InvariantCulture)
               + "-" + rfc822Offset;
    }

    /// <summary>
    /// اگر مقدار عددی باشد به صورت عدد (long) در JSON نوشته می‌شود؛
    /// چون طبق مستندات bi و pi از نوع long هستند. ارقام فارسی/عربی هم به ارقام لاتین تبدیل می‌شوند.
    /// </summary>
    private static object? ToJsonNumberOrString(string? value)
    {
        var normalized = NormalizeDigits(value ?? string.Empty).Trim();

        if (normalized.Length == 0)
            return null;

        if (long.TryParse(normalized, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var number))
            return number;

        return normalized;
    }

    /// <summary>تبدیل ارقام فارسی (۰-۹) و عربی (٠-٩) به ارقام لاتین</summary>
    public static string NormalizeDigits(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var buffer = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            buffer.Append(ch switch
            {
                >= '\u06F0' and <= '\u06F9' => (char)(ch - '\u06F0' + '0'), // ارقام فارسی
                >= '\u0660' and <= '\u0669' => (char)(ch - '\u0660' + '0'), // ارقام عربی
                _ => ch
            });
        }

        return buffer.ToString();
    }

    /// <summary>
    /// تفسیر نتیجه برگشتی از Activity پرداخت POS.
    /// طبق مستندات، Intent دریافتی در onActivityResult تنها در صورتی معتبر است که
    /// resultCode برابر Activity.RESULT_OK باشد؛ همچنین نتیجه نهایی وقتی «موفق» است که
    /// هم status_code بیرونی صفر باشد و هم status_code داخل HostResponse (فیلد sc) صفر باشد.
    /// </summary>
    public static PosPaymentResult ParsePaymentResult(Intent? data, bool resultOk)
    {
        var result = new PosPaymentResult { ResultOk = resultOk };

        if (data is null || !resultOk)
        {
            result.StatusCode = -1;
            result.StatusMessage = "نتیجه تراکنش از دستگاه POS دریافت نشد (تراکنش انجام نشده است).";
            return result;
        }

        result.StatusCode = ReadIntExtra(data, POSServiceResult.ResultKeys.STATUS_CODE, -1);
        result.StatusMessage = ReadStringExtra(data, POSServiceResult.ResultKeys.STATUS_MESSAGE) ?? string.Empty;
        result.MaskedCard = ReadStringExtra(data, POSServiceResult.ResultKeys.MASKED_CARD) ?? string.Empty;
        result.Stan = ReadIntExtra(data, POSServiceResult.ResultKeys.STAN, -1).ToString();
        result.TransactionDate = ReadTransactionDate(data);
        result.HostResponseJson = ReadStringExtra(data, POSServiceResult.ResultKeys.RESPONSE_DATA) ?? string.Empty;

        // پرداخت از محل اعتبار پذیرنده (کلید کلاس POSServiceResult و کلید جدول مستندات)
        result.PaidByCredit = ReadBoolExtra(data, POSServiceResult.ResultKeys.IS_PAYMENT_BY_CREDIT)
                              || ReadBoolExtra(data, "is_pay_by_credit");

        ReadHostResponse(result);

        result.Unknown = result.StatusCode == POSServiceResult.ResultCodes.TRANSACTION_UNKNOWN_RESULT;
        result.Success = result.StatusCode == POSServiceResult.ResultCodes.TRANSACTION_COMPLETED
                         && result.HostStatusCode == "0";

        // در پرداخت از محل اعتبار پذیرنده، شماره کارت معتبر نیست
        if (result.PaidByCredit)
            result.MaskedCard = string.Empty;

        if (string.IsNullOrWhiteSpace(result.StatusMessage))
            result.StatusMessage = DescribeStatusCode(result.StatusCode);

        return result;
    }


    /// <summary>خواندن فیلدهای مشترک HostResponse (JSON داخل response_data)</summary>
    private static void ReadHostResponse(PosPaymentResult result)
    {
        if (string.IsNullOrWhiteSpace(result.HostResponseJson))
            return;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(result.HostResponseJson);
            var root = document.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
                return;

            result.HostStatusCode = ReadJsonString(root, "sc") ?? string.Empty;

            var hostMessage = ReadJsonString(root, "sm");
            if (!string.IsNullOrWhiteSpace(hostMessage))
                result.StatusMessage = hostMessage!;

            result.Rrn = ReadJsonString(root, "rrn") ?? result.Rrn;
            result.AppliedAmountRials = ReadJsonLong(root, "aa");
            result.AppliedAmountDescription = ReadJsonString(root, "aad") ?? string.Empty;

            var hostTranId = ReadJsonLong(root, "hti");
            if (hostTranId != 0)
                result.HostTranId = hostTranId;
        }
        catch (System.Text.Json.JsonException)
        {
            // پاسخ JSON معتبر نبود؛ فقط فیلدهای Intent استفاده می‌شوند
        }
    }

    private static string? ReadJsonString(System.Text.Json.JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    private static long ReadJsonLong(System.Text.Json.JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return 0;

        return value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Number => value.TryGetInt64(out var number) ? number : (long)value.GetDouble(),
            System.Text.Json.JsonValueKind.String => long.TryParse(
                NormalizeDigits(value.GetString() ?? string.Empty),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
            _ => 0
        };
    }

    /// <summary>خواندن یک مقدار از Extras اینتنت (ممکن است از انواع مختلف جاوا باشد)</summary>
    private static Java.Lang.Object? ReadExtra(Intent data, string key)
    {
        try
        {
            return data.Extras?.Get(key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int ReadIntExtra(Intent data, string key, int defaultValue)
    {
        var value = ReadExtra(data, key);

        return value switch
        {
            null => defaultValue,
            Java.Lang.Integer intValue => intValue.IntValue(),
            Java.Lang.Long longValue => (int)longValue.LongValue(),
            Java.Lang.Double doubleValue => (int)doubleValue.DoubleValue(),
            Java.Lang.String text => TryParseInt(text.ToString(), defaultValue),
            _ => TryParseInt(value.ToString(), defaultValue)
        };
    }

    private static int TryParseInt(string? text, int defaultValue) =>
        int.TryParse(
            NormalizeDigits(text ?? string.Empty),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : defaultValue;

    private static string? ReadStringExtra(Intent data, string key) => ReadExtra(data, key) switch
    {
        null => null,
        Java.Lang.String text => text.ToString(),
        Java.Lang.Integer intValue => intValue.IntValue().ToString(System.Globalization.CultureInfo.InvariantCulture),
        Java.Lang.Long longValue => longValue.LongValue().ToString(System.Globalization.CultureInfo.InvariantCulture),
        Java.Lang.Boolean boolValue => boolValue.BooleanValue().ToString(),
        Java.Util.Date date => FormatJavaDate(date),
        var other => other.ToString()
    };

    private static bool ReadBoolExtra(Intent data, string key) => ReadExtra(data, key) switch
    {
        null => false,
        Java.Lang.Boolean boolValue => boolValue.BooleanValue(),
        Java.Lang.Integer intValue => intValue.IntValue() != 0,
        Java.Lang.String text => bool.TryParse(text.ToString(), out var parsed)
                                 || TryParseInt(text.ToString(), 0) != 0,
        _ => false
    };

    /// <summary>
    /// تاریخ تراکنش؛ طبق مستندات از نوع Date است، اما برخی نسخه‌های سرویس رشته یا میلی‌ثانیه برمی‌گردانند.
    /// </summary>
    private static string ReadTransactionDate(Intent data) =>
        ReadExtra(data, POSServiceResult.ResultKeys.TRANSACTION_DATE) switch
        {
            null => string.Empty,
            Java.Util.Date date => FormatJavaDate(date),
            Java.Lang.Long millis => DateTimeOffset.FromUnixTimeMilliseconds(millis.LongValue()).LocalDateTime
                .ToString("yyyy/MM/dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            var other => other.ToString() ?? string.Empty
        };

    private static string FormatJavaDate(Java.Util.Date date) =>
        DateTimeOffset.FromUnixTimeMilliseconds(date.Time).LocalDateTime
            .ToString("yyyy/MM/dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);


    /// <summary>
    /// شرح فارسی کد وضعیت تراکنش بر اساس جدول کدهای وضعیت مستندات اسان پرداخت (بخش ۴-۱-۱ و ۴-۱-۲).
    /// </summary>
    public static string DescribeStatusCode(int statusCode) => statusCode switch
    {
        POSServiceResult.ResultCodes.TRANSACTION_COMPLETED => "تراکنش با موفقیت انجام شد.",
        POSServiceResult.ResultCodes.TRANSACTION_UNKNOWN_RESULT =>
            "نتیجه تراکنش نامشخص است؛ لطفاً از سرورهای آسان پرداخت استعلام بگیرید.",
        POSServiceResult.ResultCodes.TRANSACTION_CANCELED_BY_USER => "کاربر از انجام تراکنش انصراف داده است.",
        POSServiceResult.ResultCodes.CARD_SWIPE_TIMED_OUT =>
            "تراکنش به علت عدم کشیدن کارت توسط کاربر در بازه زمانی مشخص آغاز نشده است.",
        POSServiceResult.ResultCodes.PIN_ENTRY_TIMED_OUT =>
            "تراکنش به علت عدم ورود رمز توسط کاربر در بازه زمانی مشخص آغاز نشده است.",
        POSServiceResult.ResultCodes.INVALID_DATA =>
            "تراکنش به علت عدم وجود داده‌های معتبر آغاز نشده است (مثلاً مبلغ صفر).",
        POSServiceResult.ResultCodes.GENERAL_ERROR =>
            "قبل از ارسال تراکنش به سرور، خطایی در سرویس پرداخت پایانه رخ داده است.",
        POSServiceResult.ResultCodes.POS_IS_NOT_READY_ERROR =>
            "تراکنش به علت آماده نبودن داده‌های سرویس پرداخت پایانه انجام نشده است.",
        POSServiceResult.ResultCodes.TRANSACTION_ERROR =>
            "انجام تراکنش با خطا مواجه شده است؛ تراکنش ناموفق است.",
        POSServiceResult.ResultCodes.WIPE_REQUIRED =>
            "پایانه متعلق به این پذیرنده نیست و تراکنش رد شده است.",
        -1 => "نتیجه تراکنش از دستگاه POS دریافت نشد.",
        _ => $"کد وضعیت تراکنش: {statusCode}"
    };

    /// <summary>شرح فارسی کد خطای پرینتر (بخش ۳-۱۲ مستندات)</summary>
    public static string DescribePrintErrorCode(int errorCode) => errorCode switch
    {
        POSServiceResult.PrintStatusCodes.PRINTER_PAPER_END => "کاغذ دستگاه تمام شده است.",
        POSServiceResult.PrintStatusCodes.PRINTER_PAPER_GENERAL_ERROR => "خطای مربوط به کاغذ دستگاه رخ داده است.",
        POSServiceResult.PrintStatusCodes.PRINTER_GENERAL_ERROR => "دستگاه پوز با مشکل روبرو شده است.",
        POSServiceResult.PrintStatusCodes.PRINTER_BUSY => "دستگاه پوز مشغول چاپ رسید دیگری است.",
        POSServiceResult.PrintStatusCodes.LOW_BATTERY_ERROR => "شارژ باطری دستگاه پوز کم است.",
        _ => "خطای نامشخص در چاپ رسید."
    };

    /// <summary>
    /// استعلام وضعیت نهایی تراکنشی که نتیجه آن نامشخص مانده است (getTransactionStatus).
    /// ورودی همان مقدار hti است که در HostRequest ارسال شده است.
    /// </summary>
    public async Task<PosPaymentResult?> InquireAsync(long hostTranId)
    {
        if (!await ConnectAsync() || _posService is null)
            return null;

        var inquiry = await Task.Run(() => GetTransactionStatusNative(hostTranId));
        if (inquiry is null)
            return null;

        var status = inquiry.Status switch
        {
            POSServiceResult.InquiryTransactionStatus.SUCCESS => POSServiceResult.ResultCodes.TRANSACTION_COMPLETED,
            POSServiceResult.InquiryTransactionStatus.FAILED => POSServiceResult.ResultCodes.TRANSACTION_ERROR,
            _ => POSServiceResult.ResultCodes.TRANSACTION_UNKNOWN_RESULT
        };

        return new PosPaymentResult
        {
            HostTranId = hostTranId,
            ResultOk = true,
            StatusCode = status,
            Success = inquiry.Status == POSServiceResult.InquiryTransactionStatus.SUCCESS,
            Unknown = inquiry.Status == POSServiceResult.InquiryTransactionStatus.UNKNOWN,
            StatusMessage = string.IsNullOrWhiteSpace(inquiry.StatusMessage)
                ? DescribeStatusCode(status)
                : inquiry.StatusMessage!,
            MaskedCard = inquiry.MaskedCard ?? string.Empty,
            Stan = inquiry.TransactionStan.ToString(),
            Rrn = inquiry.RRN ?? string.Empty,
            TransactionDate = inquiry.TransactionDate is null ? string.Empty : FormatJavaDate(inquiry.TransactionDate)
        };
    }

    /// <summary>شناسه چاپ رسید (به ازای هر رسید یکتا) که در Callback برگردانده می‌شود</summary>
    private const int ReceiptPrintId = 1;

    /// <summary>
    /// چاپ رسید پرداخت روی پرینتر دستگاه POS.
    /// طبق مستندات، پارامترهای bitmapPrint عبارتند از: lang ("fa"/"en")، bitmap رسید،
    /// یک شناسه یکتا برای رسید (printId) و callback وضعیت چاپ.
    /// </summary>
    public async Task<bool> PrintReceiptAsync(BillInfo bill, PosPaymentResult posResult)
    {
        if (_posService is null)
            return false;

        var bitmap = BuildReceiptBitmap(bill, posResult);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var callback = new PrintStatusCallback(
            onStart: _ => { },
            onSuccess: _ => tcs.TrySetResult(true),
            onError: (code, _) => tcs.TrySetException(new InvalidOperationException(
                $"خطا در چاپ رسید: {DescribePrintErrorCode(code)} (کد {code})")));

        var started = await Task.Run(() => BitmapPrintNative(AppConstants.Lang, bitmap, ReceiptPrintId, callback));
        if (!started)
            return false;

        return await tcs.Task;
    }

    /// <summary>
    /// فراخوانی bitmapPrint از طریق Binder.
    /// نکته: در فایل AIDL رسمی، پارامتر Bitmap یک Parcelable است (نه interface)؛ بنابراین
    /// مطابق کد تولیدشده جاوا (UserDataType در کامپایلر رسمی AIDL اندروید) باید به شکل
    /// «writeInt(1) + writeToParcel» نوشته شود. نوشتن آن به‌صورت StrongBinder یا WriteParcelable
    /// با فرمت سمت سرویس ناسازگار است و باعث خطای مارشال می‌شود.
    /// </summary>
    private bool BitmapPrintNative(string lang, Bitmap bitmap, int printId, IPOSPrintStatusCallBack callback)
    {
        if (_remoteBinder is null)
            return false;

        using var data = Android.OS.Parcel.Obtain();
        using var reply = Android.OS.Parcel.Obtain();
        try
        {
            data.WriteInterfaceToken(PosInterfaceDescriptor);
            data.WriteString(lang);
            data.WriteInt(1); // پرچم «مقدار موجود» مطابق کد تولیدی جاوا برای پارامتر Parcelable
            bitmap.WriteToParcel(data, Android.OS.ParcelableWriteFlags.None);
            data.WriteInt(printId);
            data.WriteStrongBinder(callback.AsBinder());
            _remoteBinder.Transact(IPosServiceStub.TransactionBitmapPrint, data, reply, 0);
            reply.ReadException();
            return true;
        }
        finally
        {
            reply.Recycle();
            data.Recycle();
        }
    }

    private static Bitmap BuildReceiptBitmap(BillInfo bill, PosPaymentResult pos)
    {
        const int width = 384;
        var lines = BuildReceiptLines(bill, pos);
        var paint = new APaint { AntiAlias = true, Color = AColor.Black };
        paint.TextSize = 22f;
        paint.SetTypeface(Typeface.DefaultBold);

        var lineHeight = 32;
        var height = (lines.Count + 1) * lineHeight + 40;

        var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bitmap);
        canvas.DrawColor(AColor.White);

        float y = 40;
        foreach (var (text, isTitle, isRtl) in lines)
        {
            if (isTitle)
            {
                paint.TextAlign = APaint.Align.Center;
                canvas.DrawText(text, width / 2f, y, paint);
            }
            else if (isRtl)
            {
                paint.TextAlign = APaint.Align.Right;
                canvas.DrawText(text, width - 10, y, paint);
            }
            else
            {
                paint.TextAlign = APaint.Align.Left;
                canvas.DrawText(text, 10, y, paint);
            }

            y += lineHeight;
        }

        return bitmap;
    }

    private static List<(string Text, bool IsTitle, bool IsRtl)> BuildReceiptLines(BillInfo bill, PosPaymentResult pos)
    {
        var lines = new List<(string, bool, bool)>
        {
            ("رسید پرداخت قبض", true, false),
            ("--------------------------", false, false),
            ($"صاحب قبض: {bill.OwnerName}", false, true),
            ($"شناسه قبض: {bill.BillId}", false, true),
            ($"شناسه پرداخت: {bill.PayId}", false, true),
            ($"مبلغ: {bill.AmountRials:N0} ریال", false, true)
        };

        // مبلغ اعمال‌شده پس از تخفیفات (فیلد aa در HostResponse)
        if (pos.AppliedAmountRials > 0 && pos.AppliedAmountRials != bill.AmountRials)
        {
            lines.Add(($"مبلغ اعمال‌شده: {pos.AppliedAmountRials:N0} ریال", false, true));

            if (!string.IsNullOrWhiteSpace(pos.AppliedAmountDescription))
                lines.Add(($"توضیح: {pos.AppliedAmountDescription}", false, true));
        }

        lines.Add(("--------------------------", false, false));
        lines.Add((pos.PaidByCredit
            ? "پرداخت موفق (از اعتبار پذیرنده)"
            : pos.Success ? "پرداخت موفق" : "وضعیت: ناموفق", true, false));

        if (!pos.PaidByCredit && !string.IsNullOrWhiteSpace(pos.MaskedCard))
            lines.Add(($"کارت: {pos.MaskedCard}", false, false));

        if (!string.IsNullOrWhiteSpace(pos.Stan) && pos.Stan != "-1")
            lines.Add(($"STAN: {pos.Stan}", false, false));

        if (!string.IsNullOrWhiteSpace(pos.Rrn))
            lines.Add(($"RRN: {pos.Rrn}", false, false));

        lines.Add(($"تاریخ: {pos.TransactionDate}", false, false));

        return lines;
    }

    private sealed class PrintStatusCallback : IPOSPrintStatusCallBackStub
    {
        private readonly Action<int> _onStart;
        private readonly Action<int> _onSuccess;
        private readonly Action<int, int> _onError;

        public PrintStatusCallback(Action<int> onStart, Action<int> onSuccess, Action<int, int> onError)
        {
            _onStart = onStart;
            _onSuccess = onSuccess;
            _onError = onError;
        }

        public override void OnSuccess(int printId) => _onSuccess(printId);

        public override void OnStartPrinting(int printId) => _onStart(printId);

        public override void OnError(int errorCode, int printId) => _onError(errorCode, printId);
    }

    public void Dispose()
    {
        if (_connected && _connection is not null)
        {
            try { CurrentContext.UnbindService(_connection); } catch { }
            _connected = false;
        }
    }
}



