using Android.OS;
using Android.Runtime;

namespace Android.Graphics
{
    // Shim: ابزار AIDL نوع Bitmap را به‌صورت interface مارشال می‌کند و به BitmapStub نیاز دارد.
    public static class BitmapStub
    {
        public static Bitmap? AsInterface(global::Android.OS.IBinder? binder)
        {
            if (binder is null || binder.Handle == global::System.IntPtr.Zero)
                return null;

            return global::Java.Lang.Object.GetObject<Bitmap>(binder.Handle, JniHandleOwnership.DoNotTransfer);
        }
    }
}

namespace Com.Persianswitch.Smartpos.Aidl
{
    /// <summary>کدهای وضعیت و کلیدهای نتیجه سرویس POS (معادل POSServiceResult.java)</summary>
    public class POSServiceResult
    {
        public static class ResultKeys
        {
            public const string STATUS_MESSAGE = "status_message";
            public const string STATUS_CODE = "status_code";
            public const string RESPONSE_DATA = "response_data";
            public const string RESPONSE_SIGN = "response_sign";
            public const string MASKED_CARD = "masked_card";
            public const string STAN = "stan";
            public const string TRANSACTION_DATE = "transaction_date";
            public const string IS_PAYMENT_BY_CREDIT = "is_payment_by_credit";
        }

        public static class ResultCodes
        {
            public const int TRANSACTION_COMPLETED = 0;
            public const int TRANSACTION_UNKNOWN_RESULT = 9999;
            public const int TRANSACTION_CANCELED_BY_USER = 10001;
            public const int CARD_SWIPE_TIMED_OUT = 10002;
            public const int PIN_ENTRY_TIMED_OUT = 10003;
            public const int INVALID_DATA = 10004;
            public const int GENERAL_ERROR = 11000;
            public const int POS_IS_NOT_READY_ERROR = 11001;
            public const int TRANSACTION_ERROR = 11002;
            public const int WIPE_REQUIRED = 11003;
        }

        public static class InquiryTransactionStatus
        {
            public const int SUCCESS = 0;
            public const int FAILED = 1;
            public const int UNKNOWN = 2;
        }

        public static class PrintStatusCodes
        {
            public const int UNKNOWN_ERROR = -1;
            public const int PRINTER_PAPER_END = 101;
            public const int PRINTER_PAPER_GENERAL_ERROR = 102;
            public const int PRINTER_GENERAL_ERROR = 103;
            public const int PRINTER_BUSY = 104;
            public const int LOW_BATTERY_ERROR = 105;
        }
    }

    // Shim: کد تولید شده AIDL به نوع PendingIntent در همین namespace ارجاع می‌دهد.
    // مقدار binder برگشتی از سرویس به Android.App.PendingIntent تبدیل می‌شود.
    public class PendingIntent
    {
        private readonly IBinder? _binder;

        internal PendingIntent(IBinder? binder)
        {
            _binder = binder;
        }

        /// <summary>binder اصلی (مورد استفاده توسط کد تولید شده AIDL)</summary>
        public IBinder? AsBinder() => _binder;

        /// <summary>PendingIntent واقعی اندروید که از سرویس POS برگشته است</summary>
        public Android.App.PendingIntent? Native
        {
            get
            {
                if (_binder is null)
                    return null;

                using var parcel = Parcel.Obtain();
                parcel.WriteStrongBinder(_binder);
                parcel.SetDataPosition(0);
                return Android.App.PendingIntent.ReadPendingIntentOrNullFromParcel(parcel);
            }
        }
    }

    public static class PendingIntentStub
    {
        public static PendingIntent? AsInterface(IBinder? binder) =>
            binder is null ? null : new PendingIntent(binder);
    }

    // Shim: کد تولید شده AIDL برای ارسال Bitmap متد AsBinder صدا می‌زند.
    // (این مسیر در زمان اجرا استفاده نمی‌شود؛ چاپ از طریق BitmapPrintNative انجام می‌شود)
    public static class BitmapBinderExtensions
    {
        public static Android.OS.IBinder? AsBinder(this global::Android.Graphics.Bitmap bitmap) => null;
    }


    /// <summary>معادل C# کلاس Parcelable سمت Java (com.persianswitch.smartpos.aidl.POSMerchantInfo)</summary>
    [Register("com/persianswitch/smartpos/aidl/POSMerchantInfo", DoNotGenerateAcw = true)]
    public class POSMerchantInfo : Java.Lang.Object, IParcelable
    {
        public string? MerchantId { get; set; }
        public string? TerminalId { get; set; }
        public string? TelepardazCode { get; set; }
        public string? MerchantNameEn { get; set; }
        public string? MerchantNameFa { get; set; }
        public int PaymentIdStatus { get; set; }
        public bool IsCreditMenuActive { get; set; }

        public POSMerchantInfo() { }

        private POSMerchantInfo(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer) { }

        private POSMerchantInfo(Parcel source)
        {
            MerchantId = source.ReadString();
            TerminalId = source.ReadString();
            TelepardazCode = source.ReadString();
            MerchantNameEn = source.ReadString();
            MerchantNameFa = source.ReadString();
            PaymentIdStatus = source.ReadInt();
            IsCreditMenuActive = source.ReadByte() != 0;
        }

        public int DescribeContents() => 0;

        public void WriteToParcel(Parcel dest, ParcelableWriteFlags flags)
        {
            dest.WriteString(MerchantId);
            dest.WriteString(TerminalId);
            dest.WriteString(TelepardazCode);
            dest.WriteString(MerchantNameEn);
            dest.WriteString(MerchantNameFa);
            dest.WriteInt(PaymentIdStatus);
            dest.WriteByte((sbyte)(IsCreditMenuActive ? 1 : 0));
        }
    }

    /// <summary>معادل C# کلاس Parcelable سمت Java (com.persianswitch.smartpos.aidl.POSTransactionInquiryResult)</summary>
    [Register("com/persianswitch/smartpos/aidl/POSTransactionInquiryResult", DoNotGenerateAcw = true)]
    public class POSTransactionInquiryResult : Java.Lang.Object, IParcelable
    {
        public int Status { get; set; }
        public string? StatusMessage { get; set; }
        public string? MaskedCard { get; set; }
        public int TransactionStan { get; set; }
        public string? RRN { get; set; }
        public Java.Util.Date? TransactionDate { get; set; }
        public string? ExtraData { get; set; }

        public POSTransactionInquiryResult() { }

        private POSTransactionInquiryResult(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer) { }

        private POSTransactionInquiryResult(Parcel source)
        {
            Status = source.ReadInt();
            StatusMessage = source.ReadString();
            MaskedCard = source.ReadString();
            TransactionStan = source.ReadInt();
            RRN = source.ReadString();
            TransactionDate = (Java.Util.Date?)source.ReadSerializable();
            ExtraData = source.ReadString();
        }

        public int DescribeContents() => 0;

        public void WriteToParcel(Parcel dest, ParcelableWriteFlags flags)
        {
            dest.WriteInt(Status);
            dest.WriteString(StatusMessage);
            dest.WriteString(MaskedCard);
            dest.WriteInt(TransactionStan);
            dest.WriteString(RRN);
            dest.WriteSerializable(TransactionDate);
            dest.WriteString(ExtraData);
        }
    }

    /// <summary>معادل C# کلاس Parcelable سمت Java (com.persianswitch.smartpos.aidl.POSTransactionData)</summary>
    [Register("com/persianswitch/smartpos/aidl/POSTransactionData", DoNotGenerateAcw = true)]
    public class POSTransactionData : Java.Lang.Object, IParcelable
    {
        public Android.App.PendingIntent? PendingActivity { get; set; }
        public int Stan { get; set; }

        public POSTransactionData() { }

        private POSTransactionData(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer) { }

        private POSTransactionData(Parcel source)
        {
            PendingActivity = (Android.App.PendingIntent?)source.ReadParcelable(Java.Lang.ClassLoader.SystemClassLoader!);
            Stan = source.ReadInt();
        }

        public int DescribeContents() => 0;

        public void WriteToParcel(Parcel dest, ParcelableWriteFlags flags)
        {
            dest.WriteParcelable(PendingActivity, flags);
            dest.WriteInt(Stan);
        }
    }
}

