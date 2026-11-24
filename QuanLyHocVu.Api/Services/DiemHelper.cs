namespace QuanLyHocVu.Api.Services;

/// <summary>
/// Công thức điểm theo Sổ tay sinh viên PDU 2023 (mục 2.6, 2.8, 2.9, 2.10):
///  - Điểm bộ phận (ĐBP) = trung bình có hệ số của các bài kiểm tra thường xuyên.
///  - Điểm học phần (ĐHP) = 0,4 x ĐBP + 0,6 x Điểm thi kết thúc, làm tròn 1 chữ số thập phân.
///  - Chuyên cần KHÔNG tính vào điểm, chỉ là điều kiện được dự thi (tham gia từ 80% số tiết).
/// </summary>
public static class DiemHelper
{
    public const decimal TrongSoBoPhan = 0.4m;
    public const decimal TrongSoThi = 0.6m;

    // Số bài kiểm tra phụ thuộc số tín chỉ. Thiếu bài nào thì chưa tính được điểm bộ phận.
    public static decimal? TinhDiemBoPhan(int tinChi, decimal? kt1, decimal? kt2, decimal? hs2)
    {
        decimal tong, tongHeSo;
        if (tinChi <= 1)
        {
            if (kt1 is null) return null;
            tong = kt1.Value; tongHeSo = 1;
        }
        else if (tinChi <= 3)
        {
            if (kt1 is null || hs2 is null) return null;
            tong = kt1.Value + 2 * hs2.Value; tongHeSo = 3;
        }
        else
        {
            if (kt1 is null || kt2 is null || hs2 is null) return null;
            tong = kt1.Value + kt2.Value + 2 * hs2.Value; tongHeSo = 4;
        }
        return Math.Round(tong / tongHeSo, 1, MidpointRounding.AwayFromZero);
    }

    public static (decimal? DiemHp, string? Chu, decimal? He4) TinhDiemHocPhan(decimal? diemBoPhan, decimal? diemThi)
    {
        if (diemBoPhan is null || diemThi is null) return (null, null, null);
        var d = Math.Round(TrongSoBoPhan * diemBoPhan.Value + TrongSoThi * diemThi.Value, 1, MidpointRounding.AwayFromZero);
        var (chu, he4) = XepLoai(d);
        return (d, chu, he4);
    }

    // Bảng quy đổi của trường (mục 2.9). Dưới 4,0 là F (không đạt).
    public static (string Chu, decimal He4) XepLoai(decimal d) =>
        d >= 8.5m ? ("A", 4.0m) :
        d >= 7.8m ? ("B+", 3.5m) :
        d >= 7.0m ? ("B", 3.0m) :
        d >= 6.3m ? ("C+", 2.5m) :
        d >= 5.5m ? ("C", 2.0m) :
        d >= 4.8m ? ("D+", 1.5m) :
        d >= 4.0m ? ("D", 1.0m) :
                    ("F", 0.0m);

    // Điểm trung bình chung (thang 4) = tổng (điểm thang 4 x tín chỉ) / tổng tín chỉ, làm tròn 2 chữ số
    public static decimal? TinhTrungBinhChung(IEnumerable<(decimal He4, int TinChi)> cacMon)
    {
        var ds = cacMon.ToList();
        var tc = ds.Sum(x => x.TinChi);
        return tc == 0 ? null : Math.Round(ds.Sum(x => x.He4 * x.TinChi) / tc, 2, MidpointRounding.AwayFromZero);
    }
}