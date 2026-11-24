using System.Security.Cryptography;
using System.Text;

namespace QuanLyHocVu.Api.Services;

/// <summary>
/// Các hàm hỗ trợ điểm danh:
///  - Sinh mã QR động: mã đổi sau mỗi "chu kỳ" (mặc định 30 giây), có chữ ký HMAC-SHA256
///    nên sinh viên không thể tự bịa mã.
///  - Tính khoảng cách giữa hai tọa độ GPS (công thức Haversine).
/// </summary>
public static class DiemDanhHelper
{
    // Quét sau giờ mở buổi quá số phút này thì ghi nhận là "đi muộn"
    public const int PhutChoPhepDiMuon = 15;

    // Số giây Unix chia cho chu kỳ = "khe thời gian" hiện tại. Cứ hết một chu kỳ thì khe tăng 1.
    public static long KheHienTai(int chuKyGiay) =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds() / chuKyGiay;

    // Số giây còn lại trước khi mã QR hiện tại đổi
    public static int GiayConLai(int chuKyGiay) =>
        chuKyGiay - (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % chuKyGiay);

    // Chữ ký = 16 ký tự đầu của HMAC-SHA256(biMat, "idBuoi:khe")
    public static string Ky(int buoiHocId, long khe, string biMat)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(biMat));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{buoiHocId}:{khe}"));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    // Nội dung mã QR: QLHV|idBuoi|khe|chuKy
    public static string TaoMaQr(int buoiHocId, string biMat, int chuKyGiay)
    {
        var khe = KheHienTai(chuKyGiay);
        return $"QLHV|{buoiHocId}|{khe}|{Ky(buoiHocId, khe, biMat)}";
    }

    // So sánh chữ ký theo thời gian cố định (tránh bị dò chữ ký qua thời gian phản hồi)
    public static bool ChuKyHopLe(int buoiHocId, long khe, string biMat, string chuKyNhan)
    {
        var dung = Encoding.UTF8.GetBytes(Ky(buoiHocId, khe, biMat));
        var nhan = Encoding.UTF8.GetBytes((chuKyNhan ?? "").ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(dung, nhan);
    }

    // Khoảng cách giữa hai điểm trên mặt đất, đơn vị mét (công thức Haversine)
    public static double KhoangCachMet(double vd1, double kd1, double vd2, double kd2)
    {
        const double banKinhTraiDat = 6371000;
        static double Rad(double doDo) => doDo * Math.PI / 180;

        var dVd = Rad(vd2 - vd1);
        var dKd = Rad(kd2 - kd1);
        var a = Math.Sin(dVd / 2) * Math.Sin(dVd / 2) +
                Math.Cos(Rad(vd1)) * Math.Cos(Rad(vd2)) *
                Math.Sin(dKd / 2) * Math.Sin(dKd / 2);

        return banKinhTraiDat * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}