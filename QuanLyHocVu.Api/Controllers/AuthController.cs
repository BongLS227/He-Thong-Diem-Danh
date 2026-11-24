using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyHocVu.Api.Data;
using QuanLyHocVu.Api.Models;
using QuanLyHocVu.Api.Services;

namespace QuanLyHocVu.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(QuanLyHocVuContext db, TokenService tokenService) : ControllerBase
{
    private static readonly string[] CacVaiTro = ["Admin", "GiangVien", "SinhVien"];

    public record DangNhapRequest(string TenDangNhap, string MatKhau);

    public record TaoTaiKhoanRequest(
        string TenDangNhap, string MatKhau, string HoTen, string? Email, string VaiTro);

    // POST /api/auth/dang-nhap
    [HttpPost("dang-nhap")]
    public async Task<IActionResult> DangNhap(DangNhapRequest req)
    {
        var nd = await db.NguoiDung
            .FirstOrDefaultAsync(u => u.TenDangNhap == req.TenDangNhap && u.HoatDong);

        if (nd is null || !BCrypt.Net.BCrypt.Verify(req.MatKhau, nd.MatKhauHash))
            return Unauthorized(new { thongBao = "Sai tên đăng nhập hoặc mật khẩu." });

        var (token, hetHan) = tokenService.TaoToken(nd);

        return Ok(new
        {
            token,
            hetHan,
            nguoiDung = new { nd.Id, nd.TenDangNhap, nd.HoTen, nd.VaiTro }
        });
    }

    // GET /api/auth/toi  (cần đăng nhập, mọi vai trò)
    [Authorize]
    [HttpGet("toi")]
    public IActionResult ThongTinCuaToi()
    {
        return Ok(new
        {
            id = User.FindFirst("sub")?.Value,
            tenDangNhap = User.FindFirst("name")?.Value,
            hoTen = User.FindFirst("hoTen")?.Value,
            vaiTro = User.FindFirst("role")?.Value
        });
    }

    // POST /api/auth/tao-tai-khoan  (chỉ Admin)
    [Authorize(Roles = "Admin")]
    [HttpPost("tao-tai-khoan")]
    public async Task<IActionResult> TaoTaiKhoan(TaoTaiKhoanRequest req)
    {
        if (!CacVaiTro.Contains(req.VaiTro))
            return BadRequest(new { thongBao = "Vai trò phải là Admin, GiangVien hoặc SinhVien." });

        if (string.IsNullOrWhiteSpace(req.TenDangNhap) || req.MatKhau is null || req.MatKhau.Length < 6)
            return BadRequest(new { thongBao = "Tên đăng nhập không được trống, mật khẩu tối thiểu 6 ký tự." });

        if (await db.NguoiDung.AnyAsync(u => u.TenDangNhap == req.TenDangNhap))
            return Conflict(new { thongBao = "Tên đăng nhập đã tồn tại." });

        var nd = new NguoiDung
        {
            TenDangNhap = req.TenDangNhap.Trim(),
            MatKhauHash = BCrypt.Net.BCrypt.HashPassword(req.MatKhau),
            HoTen = req.HoTen,
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            VaiTro = req.VaiTro,
            HoatDong = true
        };

        db.NguoiDung.Add(nd);
        await db.SaveChangesAsync();

        return Ok(new { nd.Id, nd.TenDangNhap, nd.HoTen, nd.VaiTro });
    }

    // ---------------------------------------------------
    // ĐỔI MẬT KHẨU
    // ---------------------------------------------------
    public record DoiMatKhauRequest(string MatKhauCu, string MatKhauMoi);
    public record DatLaiMatKhauRequest(string TenDangNhap, string MatKhauMoi);

    // Quy tắc mật khẩu mới: tối thiểu 8 ký tự, gồm cả chữ và số
    private static string? KiemTraMatKhauMoi(string? mk)
    {
        if (string.IsNullOrEmpty(mk) || mk.Length < 8)
            return "Mật khẩu mới phải có ít nhất 8 ký tự.";
        if (!mk.Any(char.IsLetter) || !mk.Any(char.IsDigit))
            return "Mật khẩu mới phải gồm cả chữ và số.";
        return null;
    }

    // POST /api/auth/doi-mat-khau  (người dùng tự đổi mật khẩu của mình)
    // Lưu ý: trả 400 (không phải 401) khi sai mật khẩu cũ, vì 401 làm giao diện tự đăng xuất.
    [Authorize]
    [HttpPost("doi-mat-khau")]
    public async Task<IActionResult> DoiMatKhau(DoiMatKhauRequest req)
    {
        var id = int.Parse(User.FindFirst("sub")!.Value);
        var nd = await db.NguoiDung.FindAsync(id);
        if (nd is null || !nd.HoatDong)
            return BadRequest(new { thongBao = "Tài khoản không tồn tại hoặc đã bị vô hiệu hóa." });

        if (!BCrypt.Net.BCrypt.Verify(req.MatKhauCu ?? "", nd.MatKhauHash))
            return BadRequest(new { thongBao = "Mật khẩu hiện tại không đúng." });

        var loi = KiemTraMatKhauMoi(req.MatKhauMoi);
        if (loi != null) return BadRequest(new { thongBao = loi });

        if (req.MatKhauMoi == req.MatKhauCu)
            return BadRequest(new { thongBao = "Mật khẩu mới phải khác mật khẩu hiện tại." });

        nd.MatKhauHash = BCrypt.Net.BCrypt.HashPassword(req.MatKhauMoi);
        await db.SaveChangesAsync();
        return Ok(new { thongBao = "Đã đổi mật khẩu thành công." });
    }

    // POST /api/auth/dat-lai-mat-khau  (Admin đặt lại mật khẩu cho người khác, ví dụ khi sinh viên quên)
    [Authorize(Roles = "Admin")]
    [HttpPost("dat-lai-mat-khau")]
    public async Task<IActionResult> DatLaiMatKhau(DatLaiMatKhauRequest req)
    {
        var loi = KiemTraMatKhauMoi(req.MatKhauMoi);
        if (loi != null) return BadRequest(new { thongBao = loi });

        var nd = await db.NguoiDung.FirstOrDefaultAsync(u => u.TenDangNhap == req.TenDangNhap);
        if (nd is null) return NotFound(new { thongBao = "Không tìm thấy tên đăng nhập này." });

        nd.MatKhauHash = BCrypt.Net.BCrypt.HashPassword(req.MatKhauMoi);
        await db.SaveChangesAsync();
        return Ok(new { thongBao = $"Đã đặt lại mật khẩu cho {nd.TenDangNhap}." });
    }
}