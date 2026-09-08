namespace Amard.App.Models;

/// <summary>نوع جستجوی قبض</summary>
public enum BillSearchType
{
    /// <summary>کد پستی</summary>
    PostalCode = 0,
    /// <summary>کد ملی</summary>
    NationalCode = 1,
    /// <summary>کد نوسازی</summary>
    RenovationCode = 2
}

/// <summary>درخواست جستجوی قبض</summary>
public class BillSearchRequest
{
    public BillSearchType SearchType { get; set; }
    public string SearchValue { get; set; } = string.Empty;
}

/// <summary>نتیجه جستجوی قبض از سرور</summary>
public class Owners
{
    public decimal shop { get; set; }
    public decimal d_radif { get; set; }
    public decimal id { get; set; }
    public int M_ID { get; set; }
    public string mtable_name { get; set; }
    public Nullable<int> c_noemalek { get; set; }
    public string noemalek { get; set; }
    public string name { get; set; }
    public string family { get; set; }
    public string father { get; set; }
    public string sh_sh { get; set; }
    public string sodor { get; set; }
    public string kodemeli { get; set; }
    public string tel { get; set; }
    public string mob { get; set; }
    public string sh_sanad { get; set; }
    public Nullable<double> sahm_a { get; set; }
    public Nullable<double> dong_a { get; set; }
    public Nullable<double> habbeh_a { get; set; }
    public Nullable<double> sahm_b { get; set; }
    public Nullable<double> dong_b { get; set; }
    public Nullable<double> habbeh_b { get; set; }
    public string address { get; set; }
    public Nullable<double> sahmkol_a { get; set; }
    public Nullable<double> sahmkol_b { get; set; }
    public Nullable<double> Darsad_a { get; set; }
    public Nullable<double> Darsad_b { get; set; }
    public string tozihat { get; set; }
    public Nullable<bool> MohasebeAvarez { get; set; }
    public Nullable<bool> MohasebeKhadamat { get; set; }
    public Nullable<double> ArzeshArse { get; set; }
    public Nullable<double> ArzeshAyan { get; set; }
    public Nullable<double> AvalDoreh { get; set; }
    public long Adder { get; set; }
    public Nullable<double> sir_a { get; set; }
    public Nullable<double> sir_b { get; set; }
    public Nullable<double> meghdarsahmarse { get; set; }
    public Nullable<double> meghdarsahmayan { get; set; }
    public Nullable<bool> Chap_Fish { get; set; }
    public Nullable<int> BirthYear { get; set; }
    public Nullable<bool> submitRequest { get; set; }
    public Nullable<decimal> mablaghDarkhast { get; set; }
    public string OldTradeCode { get; set; }
    public string ISIC { get; set; }
    public Nullable<System.DateTime> FromDateTime { get; set; }
    public Nullable<System.DateTime> ToDateTime { get; set; }
    public string postalcode { get; set; }

    /// <summary>مبلغ قبض به ریال</summary>
    public long AmountRials { get; set; }
    public string BillId { get; set; } = string.Empty;
    public string PayId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string NationalCode { get; set; } = string.Empty;
    public string RenovationCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
}
//public class BillInfo
//{
//    public string BillId { get; set; } = string.Empty;
//    public string PayId { get; set; } = string.Empty;
//    public string OwnerName { get; set; } = string.Empty;
//    public string NationalCode { get; set; } = string.Empty;
//    public string RenovationCode { get; set; } = string.Empty;
//    /// <summary>مبلغ قبض به ریال</summary>
//    public long AmountRials { get; set; }
//    public string ServiceName { get; set; } = string.Empty;
//    public string Period { get; set; } = string.Empty;
//}

/// <summary>
/// اطلاعات قبض دریافتی از سرور برای مالک انتخاب‌شده
/// (مبلغ به همراه شناسه قبض و شناسه پرداخت)
/// </summary>
public class BillInfo
{
    /// <summary>مبلغ قبض به ریال</summary>
    public long AmountRials { get; set; }
    public string BillId { get; set; } = string.Empty;
    public string PayId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>نتیجه تأیید پرداخت سمت سرور</summary>
public class PaymentConfirmResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>نتیجه پرداخت روی دستگاه POS</summary>
public class PosPaymentResult
{
    public bool Success { get; set; }
    public bool Unknown { get; set; }
    public int StatusCode { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string MaskedCard { get; set; } = string.Empty;
    public string Stan { get; set; } = string.Empty;
    public string Rrn { get; set; } = string.Empty;
    public string TransactionDate { get; set; } = string.Empty;
}

public class EstelamGhabz
{
    // شماره پرونده
    public long ParvandeNo { get; set; }
    // شناسه مالک
    public int IdMalek { get; set; }
    public int Year { get; set; }
    public string ShenaseGhabz { get; set; }
    // شناسه پرداخت 
    public string ShenasePardakht { get; set; }
    // کد نوسازی
    public string CodeNosazi { get; set; }
    // نام مالک
    public string NameOwner { get; set; }
    // آدرس
    public string Address { get; set; }
    // توضیحات روی قبض
    public string Description { get; set; }
    // تاریخ صدور قبض
    public string DateSodor { get; set; }
    // مبلغ قبض
    public decimal Price { get; set; }
    // مبلغ معوقه قبض
    public decimal DelayedPrice { get; set; }
    // وضعیت پرداختی قبض
    public bool PaymentStatus { get; set; }

}

public class GetRenovationCodeViewModel
{
    public decimal FolderId { get; set; }
    public decimal ProprtyFolderId { get; set; }
    public string RenovationCode { get; set; }
}

public class ParvandehViewModel
{
    public double shop { get; set; }
    public Nullable<decimal> mantaghe { get; set; }
    public Nullable<decimal> hoze { get; set; }
    public Nullable<decimal> blok { get; set; }
    public Nullable<decimal> shomelk { get; set; }
    public Nullable<decimal> sakhteman { get; set; }
    public Nullable<decimal> apar { get; set; }
    public Nullable<decimal> senfi { get; set; }
    public Nullable<int> idparent { get; set; }
    public Nullable<int> code_tree { get; set; }
    public Nullable<bool> sws { get; set; }
    public Nullable<int> Formol { get; set; }
    public string e_code { get; set; }
    public string ghabli { get; set; }
    public string codeN { get; set; }
    public Nullable<int> AreaId { get; set; }
    public bool locked { get; set; }
    public long Id { get; set; }
    public string FullName { get; set; }
    // آدرس ملک (نمایش محدود به کاربر)
    public string Address { get; set; }
}

