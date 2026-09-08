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
    private readonly Context _context;
    private IPosService? _posService;
    private bool _connected;
    private PosServiceConnection? _connection;
    private Android.OS.IBinder? _remoteBinder;
    private readonly TaskCompletionSource<bool> _connectTcs = new();

    public PosService()
    {
        _context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?? Microsoft.Maui.ApplicationModel.Platform.AppContext;
    }

    /// <summary>اتصال (Bind) به سرویس POS دستگاه</summary>
    public Task<bool> ConnectAsync()
    {
        if (_connected)
            return Task.FromResult(true);

        var intent = new Intent(AppConstants.PosServiceAction);
        intent.SetPackage(AppConstants.PosServicePackage);

        var tcs = _connectTcs;
        _connection = new PosServiceConnection(binder =>
        {
            _remoteBinder = binder;
            _posService = IPosServiceStub.AsInterface(binder);
            _connected = true;
            tcs.TrySetResult(true);
        });

        var bound = _context.BindService(intent, _connection, Bind.AutoCreate);
        if (!bound)
            tcs.TrySetResult(false);

        return tcs.Task;
    }

    private sealed class PosServiceConnection : Java.Lang.Object, IServiceConnection
    {
        private readonly Action<Android.OS.IBinder> _onConnected;

        public PosServiceConnection(Action<Android.OS.IBinder> onConnected)
        {
            _onConnected = onConnected;
        }

        public void OnServiceConnected(ComponentName? name, Android.OS.IBinder? binder)
        {
            if (binder != null)
                _onConnected(binder);
        }

        public void OnServiceDisconnected(ComponentName? name)
        {
        }
    }

    /// <summary>
    /// شروع تراکنش پرداخت قبض روی دستگاه POS.
    /// </summary>
    public async Task StartPaymentAsync(BillInfo bill)
    {
        if (!await ConnectAsync())
            throw new InvalidOperationException(
                "اتصال به سرویس POS برقرار نشد. مطمئن شوید برنامه سرویس POS نصب و فعال است.");

        // JSON درخواست سمت host - ساختار باید مطابق مستندات AsanPardakht تنظیم شود
        var hostRequest = System.Text.Json.JsonSerializer.Serialize(new
        {
            amount = bill.AmountRials,
            pay_id = bill.PayId,
            bill_id = bill.BillId,
            extra_data = bill.OwnerName
        });

        var pendingIntent = _posService!.StartTransaction(
            AppConstants.TransactionCodeServiceBill,
            hostRequest,
            string.Empty,      // hostSign - امضای دیجیتال درخواست در صورت نیاز
            AppConstants.HostId,
            AppConstants.Lang);

        if (pendingIntent is null)
            throw new InvalidOperationException("دستگاه POS قادر به شروع تراکنش نبود.");

        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?? throw new InvalidOperationException("Activity فعلی در دسترس نیست.");

        activity.StartIntentSenderForResult(
            pendingIntent.Native!.IntentSender!,
            MainActivity.PaymentRequestCode,
            null!, 0, 0, 0);
    }

    /// <summary>تفسیر نتیجه برگشتی از Activity پرداخت POS</summary>
    public static PosPaymentResult ParsePaymentResult(Intent? data)
    {
        var result = new PosPaymentResult();

        if (data is null)
        {
            result.StatusCode = -1;
            result.StatusMessage = "نتیجه‌ای از دستگاه POS دریافت نشد.";
            return result;
        }

        result.StatusCode = data.GetIntExtra(POSServiceResult.ResultKeys.STATUS_CODE, -1);
        result.StatusMessage = data.GetStringExtra(POSServiceResult.ResultKeys.STATUS_MESSAGE) ?? string.Empty;
        result.MaskedCard = data.GetStringExtra(POSServiceResult.ResultKeys.MASKED_CARD) ?? string.Empty;
        result.Stan = data.GetIntExtra(POSServiceResult.ResultKeys.STAN, -1).ToString();
        result.TransactionDate = data.GetStringExtra(POSServiceResult.ResultKeys.TRANSACTION_DATE) ?? string.Empty;
        result.Rrn = data.GetStringExtra("rrn") ?? string.Empty;

        result.Success = result.StatusCode == POSServiceResult.ResultCodes.TRANSACTION_COMPLETED;
        result.Unknown = result.StatusCode == POSServiceResult.ResultCodes.TRANSACTION_UNKNOWN_RESULT;
        return result;
    }

    /// <summary>استعلام وضعیت تراکنش در صورت نامشخص بودن نتیجه</summary>
    public PosPaymentResult? InquireTransaction(long hostTranId)
    {
        if (_posService is null) return null;

        var r = _posService.GetTransactionStatus(hostTranId);
        if (r is null) return null;

        return new PosPaymentResult
        {
            Success = r.Status == POSServiceResult.InquiryTransactionStatus.SUCCESS,
            Unknown = r.Status == POSServiceResult.InquiryTransactionStatus.UNKNOWN,
            StatusMessage = r.StatusMessage ?? string.Empty,
            MaskedCard = r.MaskedCard ?? string.Empty,
            Stan = r.TransactionStan.ToString(),
            Rrn = r.RRN ?? string.Empty
        };
    }

    /// <summary>
    /// چاپ رسید پرداخت روی پرینتر دستگاه POS.
    /// </summary>
    public Task<bool> PrintReceiptAsync(BillInfo bill, PosPaymentResult posResult)
    {
        var tcs = new TaskCompletionSource<bool>();
        if (_posService is null)
        {
            tcs.TrySetResult(false);
            return tcs.Task;
        }

        var bitmap = BuildReceiptBitmap(bill, posResult);

        var callback = new PrintStatusCallback(
            onStart: _ => { },
            onSuccess: _ => tcs.TrySetResult(true),
            onError: (code, _) => tcs.TrySetException(
                new InvalidOperationException($"خطا در چاپ رسید (کد {code}). مثلاً کاغذ تمام شده است.")));

        if (!BitmapPrintNative(AppConstants.Lang, bitmap, 1, callback))
            tcs.TrySetResult(false);
        return tcs.Task;
    }

    /// <summary>
    /// فراخوانی دستی bitmapPrint از طریق Binder.
    /// (کد تولید شده Bitmap را به‌صورت interface مارشال می‌کند که با سرویس Java سازگار نیست؛
    /// اینجا Bitmap به‌عنوان Parcelable نوشته می‌شود مطابق پیاده‌سازی Java)
    /// </summary>
    private bool BitmapPrintNative(string lang, Bitmap bitmap, int printId, IPOSPrintStatusCallBack callback)
    {
        if (_remoteBinder is null)
            return false;

        using var data = Android.OS.Parcel.Obtain();
        using var reply = Android.OS.Parcel.Obtain();
        try
        {
            data.WriteInterfaceToken("com.persianswitch.smartpos.aidl.IPosService");
            data.WriteString(lang);
            data.WriteTypedObject(bitmap, 0);
            data.WriteInt(printId);
            data.WriteStrongBinder(callback.AsBinder());
            _remoteBinder.Transact((int)Android.OS.BinderConsts.FirstCallTransaction + 13 /* bitmapPrint */, data, reply, 0);
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
        return new List<(string, bool, bool)>
        {
            ("رسید پرداخت قبض", true, false),
            ("--------------------------", false, false),
            ($"صاحب قبض: {bill.OwnerName}", false, true),
            ($"شناسه قبض: {bill.BillId}", false, true),
            ($"شناسه پرداخت: {bill.PayId}", false, true),
            ($"مبلغ: {bill.AmountRials:N0} ریال", false, true),
            ("--------------------------", false, false),
            (pos.Success ? "پرداخت موفق" : "وضعیت: ناموفق", true, false),
            ($"کارت: {pos.MaskedCard}", false, false),
            ($"STAN: {pos.Stan}", false, false),
            ($"RRN: {pos.Rrn}", false, false),
            ($"تاریخ: {pos.TransactionDate}", false, false)
        };
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
            try { _context.UnbindService(_connection); } catch { }
            _connected = false;
        }
    }
}



