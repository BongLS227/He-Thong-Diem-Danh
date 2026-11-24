using Microsoft.EntityFrameworkCore;
using QuanLyHocVu.Api.Data;
using QuanLyHocVu.Api.Models;

namespace QuanLyHocVu.Api.Services;

public record ChuyenCanSv(
    int SinhVienId, string MaSinhVien, string HoTen,
    int SoBuoi, int CoMat, int Tre, int CoPhep, int Vang,
    double TyLe, double DiemChuyenCan, string MucDo, string NoiDung);

/// <summary>
/// Tính chuyên cần theo luật đơn giản, dễ giải thích:
///  - Chỉ tính các buổi ĐÃ ĐÓNG (buổi đang mở thì "vắng" chỉ là chưa điểm danh).
///  - Tỷ lệ tham gia = (Có mặt + Đi muộn) / số buổi của sinh viên đó. Dưới 80% thì không đủ điều kiện dự thi
///    (Sổ tay sinh viên PDU 2023, mục 2.6). Chuyên cần KHÔNG được tính vào điểm học phần.
///  - DiemChuyenCan (10 x tỷ lệ) chỉ để tham khảo.
///  - Số buổi vắng = chỉ tính "Vắng" không phép, so với SoBuoiVangToiDa của lớp.
/// </summary>
public static class ThongKeHelper
{
    public const double NguongTyLe = 0.8;   // dưới 80% là chuyên cần thấp

    public static (string Muc, string NoiDung) DanhGia(int vang, int toiDa, int soBuoi, double tyLe)
    {
        if (vang > toiDa)
            return ("VuotGioiHan", $"Đã vắng {vang} buổi, vượt giới hạn cho phép ({toiDa} buổi). Có nguy cơ bị cấm thi.");
        if (toiDa > 0 && vang == toiDa)
            return ("SatGioiHan", $"Đã vắng {vang}/{toiDa} buổi cho phép. Nếu vắng thêm sẽ vượt giới hạn.");
        if (toiDa >= 2 && vang == toiDa - 1)
            return ("ChuY", $"Đã vắng {vang}/{toiDa} buổi cho phép. Hãy đi học đầy đủ.");
        if (soBuoi >= 3 && tyLe < NguongTyLe)
            return ("TyLeThap", $"Tỷ lệ tham gia chỉ còn {Math.Round(tyLe * 100)}%, thấp hơn {NguongTyLe * 100}%. Dưới mức này sẽ không đủ điều kiện dự thi kết thúc học phần.");
        return ("BinhThuong", "");
    }

    // Tính chuyên cần cho cả lớp (hoặc chỉ một sinh viên nếu truyền chiSinhVienId)
    public static async Task<List<ChuyenCanSv>> TinhAsync(QuanLyHocVuContext db, int lopId, int? chiSinhVienId = null)
    {
        var lop = await db.LopHocPhan.Where(l => l.Id == lopId)
            .Select(l => new { ToiDa = (int)l.SoBuoiVangToiDa }).FirstOrDefaultAsync();
        if (lop is null) return new();

        var dd = await db.DiemDanh
            .Where(d => d.BuoiHoc.LopHocPhanId == lopId && d.BuoiHoc.TrangThai == "DaDong"
                        && (chiSinhVienId == null || d.SinhVienId == chiSinhVienId))
            .Select(d => new { d.SinhVienId, d.TrangThai })
            .ToListAsync();

        var dsSv = await db.DangKyHocPhan
            .Where(d => d.LopHocPhanId == lopId && (chiSinhVienId == null || d.SinhVienId == chiSinhVienId))
            .OrderBy(d => d.SinhVien.MaSinhVien)
            .Select(d => new { d.SinhVienId, d.SinhVien.MaSinhVien, d.SinhVien.NguoiDung.HoTen })
            .ToListAsync();

        var kq = new List<ChuyenCanSv>();
        foreach (var s in dsSv)
        {
            var cuaSv = dd.Where(x => x.SinhVienId == s.SinhVienId).ToList();
            int coMat = cuaSv.Count(x => x.TrangThai == "CoMat");
            int tre = cuaSv.Count(x => x.TrangThai == "Tre");
            int coPhep = cuaSv.Count(x => x.TrangThai == "CoPhep");
            int vang = cuaSv.Count(x => x.TrangThai == "Vang");
            int soBuoi = cuaSv.Count;

            // Theo quy chế: "trực tiếp tham gia học tập" từ 80% số tiết trở lên. Có mặt và đi muộn đều là có tham gia,
            // nghỉ có phép vẫn là vắng mặt. Muốn tính nghỉ có phép là có tham gia thì đổi thành (coMat + tre + coPhep).
            double tyLe = soBuoi == 0 ? 1 : (double)(coMat + tre) / soBuoi;
            var (muc, noiDung) = DanhGia(vang, lop.ToiDa, soBuoi, tyLe);

            kq.Add(new ChuyenCanSv(s.SinhVienId, s.MaSinhVien, s.HoTen, soBuoi, coMat, tre, coPhep, vang,
                Math.Round(tyLe, 4), Math.Round(10 * tyLe, 2), muc, noiDung));
        }
        return kq;
    }

    // Tạo cảnh báo cho các sinh viên ở mức cần chú ý. Không tạo trùng nội dung đã có.
    // Trả về số cảnh báo mới được tạo.
    public static async Task<int> TaoCanhBaoAsync(QuanLyHocVuContext db, int lopId)
    {
        var ds = await TinhAsync(db, lopId);
        int dem = 0;
        foreach (var x in ds.Where(x => x.MucDo != "BinhThuong"))
        {
            var loai = x.MucDo == "TyLeThap" ? "NguyCoRot" : "VangNhieu";
            bool daCo = await db.CanhBao.AnyAsync(c =>
                c.SinhVienId == x.SinhVienId && c.LopHocPhanId == lopId && c.Loai == loai && c.NoiDung == x.NoiDung);
            if (daCo) continue;

            db.CanhBao.Add(new CanhBao
            {
                SinhVienId = x.SinhVienId,
                LopHocPhanId = lopId,
                Loai = loai,
                NoiDung = x.NoiDung
            });
            dem++;
        }
        if (dem > 0) await db.SaveChangesAsync();
        return dem;
    }

    // Admin quản lý mọi lớp; giảng viên chỉ quản lý lớp do mình phụ trách
    public static async Task<bool> QuanLyDuocLopAsync(QuanLyHocVuContext db, int lopId, int nguoiDungId, bool laAdmin) =>
        laAdmin || await db.LopHocPhan.AnyAsync(l => l.Id == lopId && l.GiangVien.NguoiDungId == nguoiDungId);
}