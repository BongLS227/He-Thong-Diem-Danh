using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyHocVu.Api.Data;
using QuanLyHocVu.Api.Models;
using QuanLyHocVu.Api.Services;

namespace QuanLyHocVu.Api.Controllers;

// Quy tắc (Sổ tay sinh viên PDU 2023):
//  - Giảng viên của lớp (hoặc Admin) nhập: kiểm tra hệ số 1 (và bài 2 nếu từ 4 tín chỉ), kiểm tra hệ số 2
//    (nếu từ 2 tín chỉ) và điểm thi kết thúc.
//  - Hệ thống tự tính điểm bộ phận, điểm học phần, điểm chữ, điểm thang 4.
//  - Sinh viên chỉ xem được điểm của chính mình.
[ApiController]
[Route("api/diem")]
[Authorize]
public class DiemController(QuanLyHocVuContext db) : ControllerBase
{
    public record LuuDiemSv(int SinhVienId, decimal? Kt1, decimal? Kt2, decimal? Hs2, decimal? ThiKetThuc);
    public record LuuDiemRequest(List<LuuDiemSv> DanhSach);

    public record DongDiem(
        int SinhVienId, string MaSinhVien, string HoTen,
        decimal? Kt1, decimal? Kt2, decimal? Hs2, decimal? DiemBoPhan, decimal? ThiKetThuc,
        decimal? DiemHocPhan, string? DiemChu, double TyLeChuyenCan, int SoBuoi, bool DuDieuKienThi);

    private static readonly string[] ThuTuChu = ["A", "B+", "B", "C+", "C", "D+", "D", "F"];

    private int NguoiDungId => int.Parse(User.FindFirst("sub")!.Value);
    private bool LaAdmin => User.FindFirst("role")?.Value == "Admin";

    private async Task<List<DongDiem>> DungDongAsync(int lopId, int tinChi)
    {
        var dangKy = await db.DangKyHocPhan
            .Where(d => d.LopHocPhanId == lopId)
            .OrderBy(d => d.SinhVien.MaSinhVien)
            .Select(d => new { d.Id, d.SinhVienId, d.SinhVien.MaSinhVien, d.SinhVien.NguoiDung.HoTen })
            .ToListAsync();
        var diemMap = await db.Diem
            .Where(x => x.DangKyHocPhan.LopHocPhanId == lopId)
            .ToDictionaryAsync(x => x.DangKyHocPhanId);
        var cc = (await ThongKeHelper.TinhAsync(db, lopId)).ToDictionary(x => x.SinhVienId);

        var kq = new List<DongDiem>();
        foreach (var d in dangKy)
        {
            diemMap.TryGetValue(d.Id, out var diem);
            cc.TryGetValue(d.SinhVienId, out var c);

            var kt1 = diem?.DiemKt1;
            var kt2 = tinChi >= 4 ? diem?.DiemKt2 : null;
            var hs2 = tinChi >= 2 ? diem?.DiemHs2 : null;
            var thi = diem?.DiemCuoiKy;
            var dbp = DiemHelper.TinhDiemBoPhan(tinChi, kt1, kt2, hs2);
            var (hp, chu, _) = DiemHelper.TinhDiemHocPhan(dbp, thi);

            // Không đủ điều kiện dự thi: tham gia dưới 80% số buổi (khi đã có từ 3 buổi) hoặc vắng quá giới hạn
            bool duDieuKien = !(c != null &&
                ((c.SoBuoi >= 3 && c.TyLe < ThongKeHelper.NguongTyLe) || c.MucDo == "VuotGioiHan"));

            kq.Add(new DongDiem(d.SinhVienId, d.MaSinhVien, d.HoTen, kt1, kt2, hs2, dbp, thi, hp, chu,
                c?.TyLe ?? 1, c?.SoBuoi ?? 0, duDieuKien));
        }
        return kq;
    }

    private static object TongHop(List<DongDiem> ds)
    {
        var co = ds.Where(x => x.DiemHocPhan != null).ToList();
        return new
        {
            soCoDiem = co.Count,
            trungBinh = co.Count == 0 ? (decimal?)null : Math.Round(co.Average(x => x.DiemHocPhan!.Value), 2),
            soDat = co.Count(x => x.DiemChu != "F"),
            soRot = co.Count(x => x.DiemChu == "F"),
            phanBo = ThuTuChu.Select(c => new { chu = c, soLuong = co.Count(x => x.DiemChu == c) })
        };
    }

    // GET /api/diem/lop/5  (giảng viên của lớp hoặc Admin)
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpGet("lop/{lopId:int}")]
    public async Task<IActionResult> XemLop(int lopId)
    {
        var lop = await db.LopHocPhan.Where(l => l.Id == lopId)
            .Select(l => new { l.Id, l.MaLop, TenMon = l.MonHoc.TenMon, SoTinChi = (int)l.MonHoc.SoTinChi })
            .FirstOrDefaultAsync();
        if (lop is null) return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });
        if (!await ThongKeHelper.QuanLyDuocLopAsync(db, lopId, NguoiDungId, LaAdmin)) return Forbid();

        var ds = await DungDongAsync(lopId, lop.SoTinChi);
        return Ok(new { lop, sinhVien = ds, thongKe = TongHop(ds) });
    }

    // PUT /api/diem/lop/5   body: { "danhSach": [ { "sinhVienId": 1, "kt1": 8, "kt2": null, "hs2": 7.5, "thiKetThuc": 9 } ] }
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpPut("lop/{lopId:int}")]
    public async Task<IActionResult> Luu(int lopId, LuuDiemRequest req)
    {
        var lop = await db.LopHocPhan.Where(l => l.Id == lopId)
            .Select(l => new { l.Id, SoTinChi = (int)l.MonHoc.SoTinChi })
            .FirstOrDefaultAsync();
        if (lop is null) return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });
        if (!await ThongKeHelper.QuanLyDuocLopAsync(db, lopId, NguoiDungId, LaAdmin)) return Forbid();
        if (req.DanhSach is null || req.DanhSach.Count == 0)
            return BadRequest(new { thongBao = "Danh sách điểm không được trống." });

        // Điểm phải từ 0 đến 10 (thang điểm 10)
        foreach (var x in req.DanhSach)
            foreach (var (ten, v) in new[] { ("kiểm tra hệ số 1", x.Kt1), ("kiểm tra hệ số 1 (bài 2)", x.Kt2), ("kiểm tra hệ số 2", x.Hs2), ("thi kết thúc", x.ThiKetThuc) })
                if (v != null && (v < 0 || v > 10))
                    return BadRequest(new { thongBao = $"Điểm {ten} phải từ 0 đến 10 (sinh viên Id {x.SinhVienId})." });

        static decimal? Lam1(decimal? v) => v is null ? null : Math.Round(v.Value, 1, MidpointRounding.AwayFromZero);

        var dangKy = await db.DangKyHocPhan.Where(d => d.LopHocPhanId == lopId).ToListAsync();
        var diemMap = await db.Diem.Where(x => x.DangKyHocPhan.LopHocPhanId == lopId)
            .ToDictionaryAsync(x => x.DangKyHocPhanId);

        int dem = 0;
        foreach (var x in req.DanhSach)
        {
            var dk = dangKy.FirstOrDefault(d => d.SinhVienId == x.SinhVienId);
            if (dk is null) continue;

            if (!diemMap.TryGetValue(dk.Id, out var diem))
            {
                diem = new Diem { DangKyHocPhanId = dk.Id };
                db.Diem.Add(diem);
                diemMap[dk.Id] = diem;
            }

            var kt1 = Lam1(x.Kt1);
            var kt2 = lop.SoTinChi >= 4 ? Lam1(x.Kt2) : null;
            var hs2 = lop.SoTinChi >= 2 ? Lam1(x.Hs2) : null;
            var thi = Lam1(x.ThiKetThuc);

            var dbp = DiemHelper.TinhDiemBoPhan(lop.SoTinChi, kt1, kt2, hs2);
            var (hp, chu, _) = DiemHelper.TinhDiemHocPhan(dbp, thi);

            diem.DiemKt1 = kt1;
            diem.DiemKt2 = kt2;
            diem.DiemHs2 = hs2;
            diem.DiemBoPhan = dbp;
            diem.DiemCuoiKy = thi;       // điểm thi kết thúc học phần
            diem.DiemTongKet = hp;       // điểm học phần
            diem.DiemChu = chu;
            diem.NgayCapNhat = DateTime.Now;
            dem++;
        }
        await db.SaveChangesAsync();

        var ds = await DungDongAsync(lopId, lop.SoTinChi);
        return Ok(new { thongBao = $"Đã lưu điểm cho {dem} sinh viên.", thongKe = TongHop(ds) });
    }

    public record MonDiem(
        int LopHocPhanId, string MaLop, string TenMon, int HocKyId, string TenHocKy, int SoTinChi,
        decimal? DiemBoPhan, decimal? ThiKetThuc, decimal? DiemHocPhan, string? DiemChu, decimal? He4);

    // GET /api/diem/cua-toi  (sinh viên xem bảng điểm, điểm trung bình chung học kỳ và tích lũy)
    [Authorize(Roles = "SinhVien")]
    [HttpGet("cua-toi")]
    public async Task<IActionResult> CuaToi()
    {
        var sv = await db.SinhVien.FirstOrDefaultAsync(s => s.NguoiDungId == NguoiDungId);
        if (sv is null) return NotFound(new { thongBao = "Tài khoản này chưa có hồ sơ sinh viên." });

        var dangKy = await db.DangKyHocPhan
            .Where(d => d.SinhVienId == sv.Id)
            .Select(d => new
            {
                d.Id,
                d.LopHocPhanId,
                d.LopHocPhan.MaLop,
                TenMon = d.LopHocPhan.MonHoc.TenMon,
                SoTinChi = (int)d.LopHocPhan.MonHoc.SoTinChi,
                d.LopHocPhan.HocKyId,
                d.LopHocPhan.HocKy.TenHocKy,
                NgayBatDau = d.LopHocPhan.HocKy.NgayBatDau
            })
            .ToListAsync();
        var diemMap = await db.Diem.Where(x => x.DangKyHocPhan.SinhVienId == sv.Id)
            .ToDictionaryAsync(x => x.DangKyHocPhanId);

        var cacMon = new List<(DateOnly Ngay, MonDiem Mon)>();
        foreach (var d in dangKy)
        {
            diemMap.TryGetValue(d.Id, out var diem);
            var kt2 = d.SoTinChi >= 4 ? diem?.DiemKt2 : null;
            var hs2 = d.SoTinChi >= 2 ? diem?.DiemHs2 : null;
            var dbp = DiemHelper.TinhDiemBoPhan(d.SoTinChi, diem?.DiemKt1, kt2, hs2);
            var (hp, chu, he4) = DiemHelper.TinhDiemHocPhan(dbp, diem?.DiemCuoiKy);
            cacMon.Add((d.NgayBatDau, new MonDiem(d.LopHocPhanId, d.MaLop, d.TenMon, d.HocKyId, d.TenHocKy,
                d.SoTinChi, dbp, diem?.DiemCuoiKy, hp, chu, he4)));
        }

        static IEnumerable<(decimal, int)> ChoGpa(IEnumerable<MonDiem> ds) =>
            ds.Where(m => m.He4 != null).Select(m => (m.He4!.Value, m.SoTinChi));

        var hocKy = cacMon
            .GroupBy(x => new { x.Mon.HocKyId, x.Mon.TenHocKy })
            .OrderByDescending(g => g.Max(x => x.Ngay))
            .Select(g => new
            {
                tenHocKy = g.Key.TenHocKy,
                gpa4 = DiemHelper.TinhTrungBinhChung(ChoGpa(g.Select(x => x.Mon))),
                tinChiDangKy = g.Sum(x => x.Mon.SoTinChi),
                tinChiDat = g.Where(x => x.Mon.DiemChu != null && x.Mon.DiemChu != "F").Sum(x => x.Mon.SoTinChi),
                monHoc = g.Select(x => x.Mon)
            })
            .ToList();

        var tatCa = cacMon.Select(x => x.Mon).ToList();
        return Ok(new
        {
            tichLuy = new
            {
                gpa4 = DiemHelper.TinhTrungBinhChung(ChoGpa(tatCa)),
                tinChiTichLuy = tatCa.Where(m => m.DiemChu != null && m.DiemChu != "F").Sum(m => m.SoTinChi)
            },
            hocKy
        });
    }
}