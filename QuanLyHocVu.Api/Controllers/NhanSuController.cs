using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyHocVu.Api.Data;
using QuanLyHocVu.Api.Models;

namespace QuanLyHocVu.Api.Controllers;

// Quy tắc:
//  - Thêm/sửa/xóa: chỉ Admin.
//  - Xem danh sách: Admin và GiangVien.
//  - "Xóa" thực chất là VÔ HIỆU HÓA tài khoản (HoatDong = false) để không mất
//    lịch sử điểm danh, điểm số của sinh viên/giảng viên đó.

// =====================================================
// SINH VIÊN
// =====================================================
[ApiController]
[Route("api/sinh-vien")]
[Authorize]
public class SinhVienController(QuanLyHocVuContext db) : ControllerBase
{
    public record TaoSinhVienRequest(
        string MaSinhVien, string MatKhau, string HoTen, string? Email,
        int? NganhId, string? LopSinhHoat, DateOnly? NgaySinh, string? SoDienThoai);

    public record SuaSinhVienRequest(
        string HoTen, string? Email,
        int? NganhId, string? LopSinhHoat, DateOnly? NgaySinh, string? SoDienThoai);

    // GET /api/sinh-vien?tuKhoa=...&lop=...
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpGet]
    public async Task<IActionResult> LayDanhSach([FromQuery] string? tuKhoa, [FromQuery] string? lop) =>
        Ok(await db.SinhVien
            .Where(s => (tuKhoa == null || s.MaSinhVien.Contains(tuKhoa) || s.NguoiDung.HoTen.Contains(tuKhoa))
                     && (lop == null || s.LopSinhHoat == lop))
            .OrderBy(s => s.MaSinhVien)
            .Select(s => new
            {
                s.Id,
                s.MaSinhVien,
                s.NguoiDung.HoTen,
                s.NguoiDung.Email,
                s.LopSinhHoat,
                s.NganhId,
                TenNganh = s.Nganh != null ? s.Nganh.TenNganh : null,
                s.NgaySinh,
                s.SoDienThoai,
                s.NguoiDung.HoatDong
            })
            .ToListAsync());

    // GET /api/sinh-vien/5
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> LayTheoId(int id)
    {
        var s = await db.SinhVien.Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.MaSinhVien,
                x.NguoiDung.HoTen,
                x.NguoiDung.Email,
                x.LopSinhHoat,
                x.NganhId,
                TenNganh = x.Nganh != null ? x.Nganh.TenNganh : null,
                x.NgaySinh,
                x.SoDienThoai,
                x.NguoiDung.HoatDong
            })
            .FirstOrDefaultAsync();
        return s is null ? NotFound(new { thongBao = "Không tìm thấy sinh viên." }) : Ok(s);
    }

    // GET /api/sinh-vien/ho-so-cua-toi  (sinh viên xem hồ sơ của chính mình)
    [Authorize(Roles = "SinhVien")]
    [HttpGet("ho-so-cua-toi")]
    public async Task<IActionResult> HoSoCuaToi()
    {
        var nguoiDungId = int.Parse(User.FindFirst("sub")!.Value);
        var s = await db.SinhVien.Where(x => x.NguoiDungId == nguoiDungId)
            .Select(x => new
            {
                x.Id,
                x.MaSinhVien,
                x.NguoiDung.HoTen,
                x.NguoiDung.Email,
                x.LopSinhHoat,
                TenNganh = x.Nganh != null ? x.Nganh.TenNganh : null,
                x.NgaySinh,
                x.SoDienThoai
            })
            .FirstOrDefaultAsync();
        return s is null ? NotFound(new { thongBao = "Tài khoản này chưa có hồ sơ sinh viên." }) : Ok(s);
    }

    // POST /api/sinh-vien  -> tạo tài khoản + hồ sơ cùng lúc. Tên đăng nhập = mã sinh viên.
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Them(TaoSinhVienRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MaSinhVien) || string.IsNullOrWhiteSpace(req.HoTen))
            return BadRequest(new { thongBao = "Mã sinh viên và họ tên không được trống." });
        if (req.MatKhau is null || req.MatKhau.Length < 6)
            return BadRequest(new { thongBao = "Mật khẩu tối thiểu 6 ký tự." });
        if (req.NganhId != null && !await db.Nganh.AnyAsync(n => n.Id == req.NganhId))
            return BadRequest(new { thongBao = "Ngành không tồn tại." });

        var ma = req.MaSinhVien.Trim();
        if (await db.SinhVien.AnyAsync(s => s.MaSinhVien == ma) ||
            await db.NguoiDung.AnyAsync(u => u.TenDangNhap == ma))
            return Conflict(new { thongBao = "Mã sinh viên đã tồn tại." });

        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (email != null && await db.NguoiDung.AnyAsync(u => u.Email == email))
            return Conflict(new { thongBao = "Email đã được sử dụng." });

        await using var giaoDich = await db.Database.BeginTransactionAsync();

        var nd = new NguoiDung
        {
            TenDangNhap = ma,
            MatKhauHash = BCrypt.Net.BCrypt.HashPassword(req.MatKhau),
            HoTen = req.HoTen.Trim(),
            Email = email,
            VaiTro = "SinhVien",
            HoatDong = true
        };
        db.NguoiDung.Add(nd);
        await db.SaveChangesAsync();

        var sv = new SinhVien
        {
            NguoiDungId = nd.Id,
            MaSinhVien = ma,
            NganhId = req.NganhId,
            LopSinhHoat = req.LopSinhHoat?.Trim(),
            NgaySinh = req.NgaySinh,
            SoDienThoai = req.SoDienThoai?.Trim()
        };
        db.SinhVien.Add(sv);
        await db.SaveChangesAsync();

        await giaoDich.CommitAsync();

        return Ok(new { sv.Id, sv.MaSinhVien, nd.HoTen, TenDangNhap = nd.TenDangNhap, nd.VaiTro });
    }

    // PUT /api/sinh-vien/5
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Sua(int id, SuaSinhVienRequest req)
    {
        var sv = await db.SinhVien.Include(s => s.NguoiDung).FirstOrDefaultAsync(s => s.Id == id);
        if (sv is null) return NotFound(new { thongBao = "Không tìm thấy sinh viên." });
        if (string.IsNullOrWhiteSpace(req.HoTen))
            return BadRequest(new { thongBao = "Họ tên không được trống." });
        if (req.NganhId != null && !await db.Nganh.AnyAsync(n => n.Id == req.NganhId))
            return BadRequest(new { thongBao = "Ngành không tồn tại." });

        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (email != null && await db.NguoiDung.AnyAsync(u => u.Email == email && u.Id != sv.NguoiDungId))
            return Conflict(new { thongBao = "Email đã được sử dụng." });

        sv.NguoiDung.HoTen = req.HoTen.Trim();
        sv.NguoiDung.Email = email;
        sv.NganhId = req.NganhId;
        sv.LopSinhHoat = req.LopSinhHoat?.Trim();
        sv.NgaySinh = req.NgaySinh;
        sv.SoDienThoai = req.SoDienThoai?.Trim();
        await db.SaveChangesAsync();

        return Ok(new { sv.Id, sv.MaSinhVien, sv.NguoiDung.HoTen });
    }

    // DELETE /api/sinh-vien/5  -> vô hiệu hóa tài khoản, không xóa dữ liệu
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> VoHieuHoa(int id)
    {
        var sv = await db.SinhVien.Include(s => s.NguoiDung).FirstOrDefaultAsync(s => s.Id == id);
        if (sv is null) return NotFound(new { thongBao = "Không tìm thấy sinh viên." });

        sv.NguoiDung.HoatDong = false;
        await db.SaveChangesAsync();
        return Ok(new { thongBao = "Đã vô hiệu hóa tài khoản sinh viên (dữ liệu học tập vẫn được giữ)." });
    }
}

// =====================================================
// GIẢNG VIÊN
// =====================================================
[ApiController]
[Route("api/giang-vien")]
[Authorize]
public class GiangVienController(QuanLyHocVuContext db) : ControllerBase
{
    public record TaoGiangVienRequest(
        string MaGiangVien, string MatKhau, string HoTen, string? Email,
        int? KhoaId, string? SoDienThoai);

    public record SuaGiangVienRequest(
        string HoTen, string? Email, int? KhoaId, string? SoDienThoai);

    // GET /api/giang-vien?tuKhoa=...
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpGet]
    public async Task<IActionResult> LayDanhSach([FromQuery] string? tuKhoa) =>
        Ok(await db.GiangVien
            .Where(g => tuKhoa == null || g.MaGiangVien.Contains(tuKhoa) || g.NguoiDung.HoTen.Contains(tuKhoa))
            .OrderBy(g => g.MaGiangVien)
            .Select(g => new
            {
                g.Id,
                g.MaGiangVien,
                g.NguoiDung.HoTen,
                g.NguoiDung.Email,
                g.KhoaId,
                TenKhoa = g.Khoa != null ? g.Khoa.TenKhoa : null,
                g.SoDienThoai,
                g.NguoiDung.HoatDong
            })
            .ToListAsync());

    // GET /api/giang-vien/5
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> LayTheoId(int id)
    {
        var g = await db.GiangVien.Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.MaGiangVien,
                x.NguoiDung.HoTen,
                x.NguoiDung.Email,
                x.KhoaId,
                TenKhoa = x.Khoa != null ? x.Khoa.TenKhoa : null,
                x.SoDienThoai,
                x.NguoiDung.HoatDong
            })
            .FirstOrDefaultAsync();
        return g is null ? NotFound(new { thongBao = "Không tìm thấy giảng viên." }) : Ok(g);
    }

    // POST /api/giang-vien  -> tạo tài khoản + hồ sơ cùng lúc. Tên đăng nhập = mã giảng viên.
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Them(TaoGiangVienRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MaGiangVien) || string.IsNullOrWhiteSpace(req.HoTen))
            return BadRequest(new { thongBao = "Mã giảng viên và họ tên không được trống." });
        if (req.MatKhau is null || req.MatKhau.Length < 6)
            return BadRequest(new { thongBao = "Mật khẩu tối thiểu 6 ký tự." });
        if (req.KhoaId != null && !await db.Khoa.AnyAsync(k => k.Id == req.KhoaId))
            return BadRequest(new { thongBao = "Khoa không tồn tại." });

        var ma = req.MaGiangVien.Trim();
        if (await db.GiangVien.AnyAsync(g => g.MaGiangVien == ma) ||
            await db.NguoiDung.AnyAsync(u => u.TenDangNhap == ma))
            return Conflict(new { thongBao = "Mã giảng viên đã tồn tại." });

        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (email != null && await db.NguoiDung.AnyAsync(u => u.Email == email))
            return Conflict(new { thongBao = "Email đã được sử dụng." });

        await using var giaoDich = await db.Database.BeginTransactionAsync();

        var nd = new NguoiDung
        {
            TenDangNhap = ma,
            MatKhauHash = BCrypt.Net.BCrypt.HashPassword(req.MatKhau),
            HoTen = req.HoTen.Trim(),
            Email = email,
            VaiTro = "GiangVien",
            HoatDong = true
        };
        db.NguoiDung.Add(nd);
        await db.SaveChangesAsync();

        var gv = new GiangVien
        {
            NguoiDungId = nd.Id,
            MaGiangVien = ma,
            KhoaId = req.KhoaId,
            SoDienThoai = req.SoDienThoai?.Trim()
        };
        db.GiangVien.Add(gv);
        await db.SaveChangesAsync();

        await giaoDich.CommitAsync();

        return Ok(new { gv.Id, gv.MaGiangVien, nd.HoTen, TenDangNhap = nd.TenDangNhap, nd.VaiTro });
    }

    // PUT /api/giang-vien/5
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Sua(int id, SuaGiangVienRequest req)
    {
        var gv = await db.GiangVien.Include(g => g.NguoiDung).FirstOrDefaultAsync(g => g.Id == id);
        if (gv is null) return NotFound(new { thongBao = "Không tìm thấy giảng viên." });
        if (string.IsNullOrWhiteSpace(req.HoTen))
            return BadRequest(new { thongBao = "Họ tên không được trống." });
        if (req.KhoaId != null && !await db.Khoa.AnyAsync(k => k.Id == req.KhoaId))
            return BadRequest(new { thongBao = "Khoa không tồn tại." });

        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (email != null && await db.NguoiDung.AnyAsync(u => u.Email == email && u.Id != gv.NguoiDungId))
            return Conflict(new { thongBao = "Email đã được sử dụng." });

        gv.NguoiDung.HoTen = req.HoTen.Trim();
        gv.NguoiDung.Email = email;
        gv.KhoaId = req.KhoaId;
        gv.SoDienThoai = req.SoDienThoai?.Trim();
        await db.SaveChangesAsync();

        return Ok(new { gv.Id, gv.MaGiangVien, gv.NguoiDung.HoTen });
    }

    // DELETE /api/giang-vien/5  -> vô hiệu hóa tài khoản, không xóa dữ liệu
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> VoHieuHoa(int id)
    {
        var gv = await db.GiangVien.Include(g => g.NguoiDung).FirstOrDefaultAsync(g => g.Id == id);
        if (gv is null) return NotFound(new { thongBao = "Không tìm thấy giảng viên." });

        gv.NguoiDung.HoatDong = false;
        await db.SaveChangesAsync();
        return Ok(new { thongBao = "Đã vô hiệu hóa tài khoản giảng viên (dữ liệu vẫn được giữ)." });
    }
}