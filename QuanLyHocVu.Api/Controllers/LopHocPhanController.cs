using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyHocVu.Api.Data;
using QuanLyHocVu.Api.Models;

namespace QuanLyHocVu.Api.Controllers;

// Quy tắc:
//  - Admin: thêm/sửa/xóa lớp, đăng ký/hủy đăng ký sinh viên vào lớp.
//  - Admin + GiangVien: xem danh sách lớp, xem sinh viên của lớp
//    (giảng viên chỉ xem được lớp do mình phụ trách).
//  - Mọi người: GET /api/lop-hoc-phan/cua-toi (giảng viên: lớp mình dạy,
//    sinh viên: lớp mình đã đăng ký).

[ApiController]
[Route("api/lop-hoc-phan")]
[Authorize]
public class LopHocPhanController(QuanLyHocVuContext db) : ControllerBase
{
    public record LopRequest(
        string MaLop, int MonHocId, int HocKyId, int GiangVienId,
        string? PhongHoc, int? Thu, int? TietBatDau, int? TietKetThuc,
        int SiSoToiDa = 60, int SoBuoiVangToiDa = 3,
        int TyLeChuyenCan = 10, int TyLeGiuaKy = 30, int TyLeCuoiKy = 60);

    public record DangKyRequest(List<int> SinhVienIds);

    public record LopDto(
        int Id, string MaLop,
        int MonHocId, string MaMon, string TenMon, int SoTinChi,
        int HocKyId, string TenHocKy,
        int GiangVienId, string TenGiangVien,
        string? PhongHoc, int? Thu, int? TietBatDau, int? TietKetThuc,
        int SiSoToiDa, int SiSoHienTai, int SoBuoiVangToiDa,
        int TyLeChuyenCan, int TyLeGiuaKy, int TyLeCuoiKy);

    private int NguoiDungId => int.Parse(User.FindFirst("sub")!.Value);
    private string? VaiTro => User.FindFirst("role")?.Value;

    private static IQueryable<LopDto> ChieuDto(IQueryable<LopHocPhan> q) =>
        q.Select(l => new LopDto(
            l.Id, l.MaLop,
            l.MonHocId, l.MonHoc.MaMon, l.MonHoc.TenMon, (int)l.MonHoc.SoTinChi,
            l.HocKyId, l.HocKy.TenHocKy,
            l.GiangVienId, l.GiangVien.NguoiDung.HoTen,
            l.PhongHoc, (int?)l.Thu, (int?)l.TietBatDau, (int?)l.TietKetThuc,
            l.SiSoToiDa, l.DangKyHocPhan.Count, (int)l.SoBuoiVangToiDa,
            (int)l.TyLeChuyenCan, (int)l.TyLeGiuaKy, (int)l.TyLeCuoiKy));

    // ---------------------------------------------------
    // XEM
    // ---------------------------------------------------

    // GET /api/lop-hoc-phan?hocKyId=&giangVienId=&monHocId=
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpGet]
    public async Task<IActionResult> LayDanhSach(
        [FromQuery] int? hocKyId, [FromQuery] int? giangVienId, [FromQuery] int? monHocId)
    {
        var q = db.LopHocPhan.AsQueryable();
        if (hocKyId != null) q = q.Where(l => l.HocKyId == hocKyId);
        if (giangVienId != null) q = q.Where(l => l.GiangVienId == giangVienId);
        if (monHocId != null) q = q.Where(l => l.MonHocId == monHocId);

        return Ok(await ChieuDto(q.OrderBy(l => l.MaLop)).ToListAsync());
    }

    // GET /api/lop-hoc-phan/cua-toi
    [HttpGet("cua-toi")]
    public async Task<IActionResult> CuaToi([FromQuery] int? hocKyId)
    {
        var uid = NguoiDungId;
        var q = db.LopHocPhan.AsQueryable();

        if (VaiTro == "GiangVien")
            q = q.Where(l => l.GiangVien.NguoiDungId == uid);
        else if (VaiTro == "SinhVien")
            q = q.Where(l => l.DangKyHocPhan.Any(d => d.SinhVien.NguoiDungId == uid));
        else
            return BadRequest(new { thongBao = "Admin xem toàn bộ lớp tại GET /api/lop-hoc-phan." });

        if (hocKyId != null) q = q.Where(l => l.HocKyId == hocKyId);

        return Ok(await ChieuDto(q.OrderBy(l => l.MaLop)).ToListAsync());
    }

    // GET /api/lop-hoc-phan/5
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> LayTheoId(int id)
    {
        var lop = await ChieuDto(db.LopHocPhan.Where(l => l.Id == id)).FirstOrDefaultAsync();
        return lop is null ? NotFound(new { thongBao = "Không tìm thấy lớp học phần." }) : Ok(lop);
    }

    // GET /api/lop-hoc-phan/5/sinh-vien
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpGet("{id:int}/sinh-vien")]
    public async Task<IActionResult> SinhVienCuaLop(int id)
    {
        var lop = await db.LopHocPhan.Include(l => l.GiangVien).FirstOrDefaultAsync(l => l.Id == id);
        if (lop is null) return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });

        // Giảng viên chỉ xem được lớp của mình
        if (VaiTro == "GiangVien" && lop.GiangVien.NguoiDungId != NguoiDungId)
            return Forbid();

        var ds = await db.DangKyHocPhan
            .Where(d => d.LopHocPhanId == id)
            .OrderBy(d => d.SinhVien.MaSinhVien)
            .Select(d => new
            {
                d.SinhVienId,
                d.SinhVien.MaSinhVien,
                d.SinhVien.NguoiDung.HoTen,
                d.SinhVien.LopSinhHoat,
                d.NgayDangKy
            })
            .ToListAsync();

        return Ok(ds);
    }

    // ---------------------------------------------------
    // THÊM / SỬA / XÓA LỚP (Admin)
    // ---------------------------------------------------

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Them(LopRequest req)
    {
        var loi = await KiemTra(req, null);
        if (loi != null) return loi;

        var lop = new LopHocPhan();
        GanDuLieu(lop, req);
        db.LopHocPhan.Add(lop);
        await db.SaveChangesAsync();

        return Ok(await ChieuDto(db.LopHocPhan.Where(l => l.Id == lop.Id)).FirstAsync());
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Sua(int id, LopRequest req)
    {
        var lop = await db.LopHocPhan.FindAsync(id);
        if (lop is null) return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });

        var loi = await KiemTra(req, id);
        if (loi != null) return loi;

        var siSoHienTai = await db.DangKyHocPhan.CountAsync(d => d.LopHocPhanId == id);
        if (req.SiSoToiDa < siSoHienTai)
            return BadRequest(new { thongBao = $"Sĩ số tối đa không được nhỏ hơn số sinh viên đã đăng ký ({siSoHienTai})." });

        GanDuLieu(lop, req);
        await db.SaveChangesAsync();

        return Ok(await ChieuDto(db.LopHocPhan.Where(l => l.Id == id)).FirstAsync());
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Xoa(int id)
    {
        var lop = await db.LopHocPhan.FindAsync(id);
        if (lop is null) return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });

        if (await db.DangKyHocPhan.AnyAsync(d => d.LopHocPhanId == id) ||
            await db.BuoiHoc.AnyAsync(b => b.LopHocPhanId == id))
            return Conflict(new { thongBao = "Không thể xóa: lớp đã có sinh viên đăng ký hoặc đã có buổi học." });

        try
        {
            db.LopHocPhan.Remove(lop);
            await db.SaveChangesAsync();
            return Ok(new { thongBao = "Đã xóa lớp học phần." });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { thongBao = "Không thể xóa: lớp đang được sử dụng." });
        }
    }

    // ---------------------------------------------------
    // ĐĂNG KÝ HỌC PHẦN (Admin)
    // ---------------------------------------------------

    // POST /api/lop-hoc-phan/5/dang-ky   body: { "sinhVienIds": [1, 2, 3] }
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/dang-ky")]
    public async Task<IActionResult> DangKy(int id, DangKyRequest req)
    {
        var lop = await db.LopHocPhan.FindAsync(id);
        if (lop is null) return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });
        if (req.SinhVienIds is null || req.SinhVienIds.Count == 0)
            return BadRequest(new { thongBao = "Danh sách sinh viên không được trống." });

        var yeuCau = req.SinhVienIds.Distinct().ToList();

        var daCo = await db.DangKyHocPhan
            .Where(d => d.LopHocPhanId == id).Select(d => d.SinhVienId).ToListAsync();

        // Chỉ nhận sinh viên có thật và tài khoản còn hoạt động
        var hopLe = await db.SinhVien
            .Where(s => yeuCau.Contains(s.Id) && s.NguoiDung.HoatDong)
            .Select(s => s.Id).ToListAsync();

        var moi = hopLe.Except(daCo).ToList();

        if (daCo.Count + moi.Count > lop.SiSoToiDa)
            return Conflict(new
            {
                thongBao = $"Vượt sĩ số tối đa ({lop.SiSoToiDa}). Hiện có {daCo.Count}, định thêm {moi.Count}."
            });

        await using var giaoDich = await db.Database.BeginTransactionAsync();

        var cacDangKy = moi.Select(svId => new DangKyHocPhan { LopHocPhanId = id, SinhVienId = svId }).ToList();
        db.DangKyHocPhan.AddRange(cacDangKy);
        await db.SaveChangesAsync();

        // Tạo sẵn dòng điểm rỗng cho từng đăng ký để sau này chỉ việc nhập điểm
        db.Diem.AddRange(cacDangKy.Select(dk => new Diem { DangKyHocPhanId = dk.Id }));
        await db.SaveChangesAsync();

        await giaoDich.CommitAsync();

        return Ok(new
        {
            daThem = moi.Count,
            boQua = yeuCau.Count - moi.Count,
            ghiChu = "Bỏ qua: sinh viên không tồn tại, bị vô hiệu hóa hoặc đã đăng ký lớp này rồi."
        });
    }

    // DELETE /api/lop-hoc-phan/5/dang-ky/3  (hủy đăng ký sinh viên Id=3 khỏi lớp 5)
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}/dang-ky/{sinhVienId:int}")]
    public async Task<IActionResult> HuyDangKy(int id, int sinhVienId)
    {
        var dk = await db.DangKyHocPhan
            .FirstOrDefaultAsync(d => d.LopHocPhanId == id && d.SinhVienId == sinhVienId);
        if (dk is null) return NotFound(new { thongBao = "Sinh viên này chưa đăng ký lớp." });

        db.DangKyHocPhan.Remove(dk);   // dòng điểm đi kèm bị xóa theo (cascade)
        await db.SaveChangesAsync();
        return Ok(new { thongBao = "Đã hủy đăng ký (điểm của sinh viên trong lớp này cũng bị xóa)." });
    }

    // ---------------------------------------------------
    // HÀM PHỤ
    // ---------------------------------------------------

    private async Task<IActionResult?> KiemTra(LopRequest r, int? boQuaId)
    {
        if (string.IsNullOrWhiteSpace(r.MaLop))
            return BadRequest(new { thongBao = "Mã lớp không được trống." });
        if (!await db.MonHoc.AnyAsync(m => m.Id == r.MonHocId))
            return BadRequest(new { thongBao = "Môn học không tồn tại." });
        if (!await db.HocKy.AnyAsync(h => h.Id == r.HocKyId))
            return BadRequest(new { thongBao = "Học kỳ không tồn tại." });
        if (!await db.GiangVien.AnyAsync(g => g.Id == r.GiangVienId && g.NguoiDung.HoatDong))
            return BadRequest(new { thongBao = "Giảng viên không tồn tại hoặc đã bị vô hiệu hóa." });

        if (r.Thu != null && (r.Thu < 2 || r.Thu > 8))
            return BadRequest(new { thongBao = "Thứ phải từ 2 (thứ hai) đến 8 (chủ nhật)." });
        if ((r.TietBatDau == null) != (r.TietKetThuc == null))
            return BadRequest(new { thongBao = "Phải nhập cả tiết bắt đầu và tiết kết thúc, hoặc để trống cả hai." });
        if (r.TietBatDau != null && (r.TietBatDau < 1 || r.TietKetThuc < r.TietBatDau))
            return BadRequest(new { thongBao = "Tiết bắt đầu phải từ 1 và không lớn hơn tiết kết thúc." });

        if (r.SiSoToiDa < 1 || r.SiSoToiDa > 500)
            return BadRequest(new { thongBao = "Sĩ số tối đa phải từ 1 đến 500." });
        if (r.SoBuoiVangToiDa < 0 || r.SoBuoiVangToiDa > 50)
            return BadRequest(new { thongBao = "Số buổi vắng tối đa phải từ 0 đến 50." });

        if (r.TyLeChuyenCan < 0 || r.TyLeGiuaKy < 0 || r.TyLeCuoiKy < 0 ||
            r.TyLeChuyenCan + r.TyLeGiuaKy + r.TyLeCuoiKy != 100)
            return BadRequest(new { thongBao = "Tỷ lệ chuyên cần + giữa kỳ + cuối kỳ phải bằng 100." });

        var ma = r.MaLop.Trim();
        if (await db.LopHocPhan.AnyAsync(l => l.MaLop == ma && l.Id != boQuaId))
            return Conflict(new { thongBao = "Mã lớp đã tồn tại." });

        return null;
    }

    private static void GanDuLieu(LopHocPhan lop, LopRequest r)
    {
        lop.MaLop = r.MaLop.Trim();
        lop.MonHocId = r.MonHocId;
        lop.HocKyId = r.HocKyId;
        lop.GiangVienId = r.GiangVienId;
        lop.PhongHoc = r.PhongHoc?.Trim();
        lop.Thu = (byte?)r.Thu;
        lop.TietBatDau = (byte?)r.TietBatDau;
        lop.TietKetThuc = (byte?)r.TietKetThuc;
        lop.SiSoToiDa = r.SiSoToiDa;
        lop.SoBuoiVangToiDa = (byte)r.SoBuoiVangToiDa;
        lop.TyLeChuyenCan = (byte)r.TyLeChuyenCan;
        lop.TyLeGiuaKy = (byte)r.TyLeGiuaKy;
        lop.TyLeCuoiKy = (byte)r.TyLeCuoiKy;
    }
}