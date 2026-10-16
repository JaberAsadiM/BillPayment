using Amard.App.Models;
using System.Text.Json;

namespace Amard.App.Services;

/// <summary>
/// ذخیره‌سازی نتیجه‌ی پرداخت‌های موفقی که هنوز به سرور اعلام نشده‌اند
/// (مثلاً به‌دلیل قطع بودن ارتباط با سرور).
/// داده‌ها در فایل JSON داخل حافظه‌ی داخلی برنامه (AppDataDirectory) نگهداری می‌شوند
/// و در اجراهای بعدی برنامه هم باقی می‌مانند تا کاربر بتواند آن‌ها را دونه‌دونه ارسال کند.
/// </summary>
public class PendingConfirmationStore
{
    private const string FileName = "pending_confirmations.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _filePath;
    private readonly object _lock = new();
    private List<PendingBillConfirmation> _items = new();

    /// <summary>با هر تغییر در لیست (افزایش/کاهش فیش در انتظار) صدا زده می‌شود</summary>
    public event Action? Changed;

    public PendingConfirmationStore()
    {
        _filePath = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, FileName);
        Load();
    }

    /// <summary>تعداد فیش‌های در انتظار اعلام به سرور</summary>
    public int Count
    {
        get { lock (_lock) return _items.Count; }
    }

    /// <summary>رونوشتی از لیست فیش‌های در انتظار</summary>
    public List<PendingBillConfirmation> GetAll()
    {
        lock (_lock) return new List<PendingBillConfirmation>(_items);
    }

    /// <summary>ثبت یک فیش پرداخت‌شده برای ارسال مجدد (بدون تکراری شدن)</summary>
    public void Add(PendingBillConfirmation item)
    {
        lock (_lock)
        {
            var isDuplicate = _items.Any(i =>
                i.BillId == item.BillId &&
                i.PayId == item.PayId &&
                i.TrackingNo == item.TrackingNo);

            if (isDuplicate)
                return;

            _items.Add(item);
            Save();
        }

        Changed?.Invoke();
    }

    /// <summary>حذف فیشی که با موفقیت به سرور اعلام شد</summary>
    public void Remove(PendingBillConfirmation item)
    {
        lock (_lock)
        {
            var removed = _items.RemoveAll(i =>
                i.BillId == item.BillId &&
                i.PayId == item.PayId &&
                i.TrackingNo == item.TrackingNo) > 0;

            if (!removed)
                return;

            Save();
        }

        Changed?.Invoke();
    }

    /// <summary>خواندن لیست از فایل (در شروع برنامه). فایل خراب = شروع با لیست خالی.</summary>
    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return;

            var json = File.ReadAllText(_filePath);
            var items = JsonSerializer.Deserialize<List<PendingBillConfirmation>>(json, JsonOptions);
            if (items is not null)
                _items = items;
        }
        catch (Exception)
        {
            _items = new List<PendingBillConfirmation>();
        }
    }

    /// <summary>نوشتن اتمیک لیست در فایل (اول فایل موقت، بعد جایگزینی) تا داده خراب نشود</summary>
    private void Save()
    {
        try
        {
            var temp = _filePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_items, JsonOptions));
            File.Move(temp, _filePath, overwrite: true);
        }
        catch (Exception)
        {
            // اگر ذخیره روی دیسک ناموفق بود، آیتم حداقل در حافظه باقی می‌ماند تا در همین اجرا ارسال شود
        }
    }
}