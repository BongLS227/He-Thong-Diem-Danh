using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyHocVu.Api.Data;
using QuanLyHocVu.Api.Models;

namespace QuanLyHocVu.Api.Controllers;

// Quy tắc chung: đăng nhập là xem được; thêm/sửa/xóa chỉ Admin.
// Trả về object gọn (không trả thẳng entity) để tránh lỗi vòng lặp JSON.

// =====================================================
// KHOA
// =====================================================
[ApiController]
[Route("api/khoa")]
[Authorize]
public class KhoaController(QuanLyHocVuContext db) : ControllerBase
{
    public record KhoaRequest(string MaKhoa, string TenKhoa);

    [HttpGet]
    public async Task<IActionResult> LayTatCa() =>
        Ok(await db.Khoa.OrderBy(k => k.MaKhoa)
            .Select(k => new { k.Id, k.MaKhoa, k.TenKhoa }).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> LayTheoId(int id)
    {
        var k = await db.Khoa.Where(x => x.Id == id)
            .Select(x => new { x.Id, x.MaKhoa, x.TenKhoa }).FirstOrDefaultAsync();
        return k is null ? NotFound(new { thongBao = "Không tìm thấy khoa." }) : Ok(k);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Them(KhoaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MaKhoa) || string.IsNullOrWhiteSpace(req.TenKhoa))
            return BadRequest(new { thongBao = "Mã khoa và tên khoa không được trống." });
        if (await db.Khoa.AnyAsync(k => k.MaKhoa == req.MaKhoa))
            return Conflict(new { thongBao = "Mã khoa đã tồn tại." });

        var k = new Khoa { MaKhoa = req.MaKhoa.Trim(), TenKhoa = req.TenKhoa.Trim() };
        db.Khoa.Add(k);
        await db.SaveChangesAsync();
        return Ok(new { k.Id, k.MaKhoa, k.TenKhoa });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Sua(int id, KhoaRequest req)
    {
        var k = await db.Khoa.FindAsync(id);
        if (k is null) return NotFound(new { thongBao = "Không tìm thấy khoa." });
        if (await db.Khoa.AnyAsync(x => x.MaKhoa == req.MaKhoa && x.Id != id))
            return Conflict(new { thongBao = "Mã khoa đã tồn tại." });

        k.MaKhoa = req.MaKhoa.Trim();
        k.TenKhoa = req.TenKhoa.Trim();
        await db.SaveChangesAsync();
        return Ok(new { k.Id, k.MaKhoa, k.TenKhoa });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Xoa(int id)
    {
        var k = await db.Khoa.FindAsync(id);
        if (k is null) return NotFound(new { thongBao = "Không tìm thấy khoa." });
        try
        {
            db.Khoa.Remove(k);
            await db.SaveChangesAsync();
            return Ok(new { thongBao = "Đã xóa khoa." });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { thongBao = "Không thể xóa: khoa đang được sử dụng." });
        }
    }
}

// =====================================================
// NGÀNH
// =====================================================
[ApiController]
[Route("api/nganh")]
[Authorize]
public class NganhController(QuanLyHocVuContext db) : ControllerBase
{
    public record NganhRequest(int KhoaId, string MaNganh, string TenNganh);

    [HttpGet]
    public async Task<IActionResult> LayTatCa([FromQuery] int? khoaId) =>
        Ok(await db.Nganh
            .Where(n => khoaId == null || n.KhoaId == khoaId)
            .OrderBy(n => n.MaNganh)
            .Select(n => new { n.Id, n.KhoaId, TenKhoa = n.Khoa.TenKhoa, n.MaNganh, n.TenNganh })
            .ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> LayTheoId(int id)
    {
        var n = await db.Nganh.Where(x => x.Id == id)
            .Select(x => new { x.Id, x.KhoaId, TenKhoa = x.Khoa.TenKhoa, x.MaNganh, x.TenNganh })
            .FirstOrDefaultAsync();
        return n is null ? NotFound(new { thongBao = "Không tìm thấy ngành." }) : Ok(n);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Them(NganhRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MaNganh) || string.IsNullOrWhiteSpace(req.TenNganh))
            return BadRequest(new { thongBao = "Mã ngành và tên ngành không được trống." });
        if (!await db.Khoa.AnyAsync(k => k.Id == req.KhoaId))
            return BadRequest(new { thongBao = "Khoa không tồn tại." });
        if (await db.Nganh.AnyAsync(n => n.MaNganh == req.MaNganh))
            return Conflict(new { thongBao = "Mã ngành đã tồn tại." });

        var n = new Nganh { KhoaId = req.KhoaId, MaNganh = req.MaNganh.Trim(), TenNganh = req.TenNganh.Trim() };
        db.Nganh.Add(n);
        await db.SaveChangesAsync();
        return Ok(new { n.Id, n.KhoaId, n.MaNganh, n.TenNganh });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Sua(int id, NganhRequest req)
    {
        var n = await db.Nganh.FindAsync(id);
        if (n is null) return NotFound(new { thongBao = "Không tìm thấy ngành." });
        if (!await db.Khoa.AnyAsync(k => k.Id == req.KhoaId))
            return BadRequest(new { thongBao = "Khoa không tồn tại." });
        if (await db.Nganh.AnyAsync(x => x.MaNganh == req.MaNganh && x.Id != id))
            return Conflict(new { thongBao = "Mã ngành đã tồn tại." });

        n.KhoaId = req.KhoaId;
        n.MaNganh = req.MaNganh.Trim();
        n.TenNganh = req.TenNganh.Trim();
        await db.SaveChangesAsync();
        return Ok(new { n.Id, n.KhoaId, n.MaNganh, n.TenNganh });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Xoa(int id)
    {
        var n = await db.Nganh.FindAsync(id);
        if (n is null) return NotFound(new { thongBao = "Không tìm thấy ngành." });
        try
        {
            db.Nganh.Remove(n);
            await db.SaveChangesAsync();
            return Ok(new { thongBao = "Đã xóa ngành." });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { thongBao = "Không thể xóa: ngành đang được sử dụng." });
        }
    }
}

// =====================================================
// MÔN HỌC
// =====================================================
[ApiController]
[Route("api/mon-hoc")]
[Authorize]
public class MonHocController(QuanLyHocVuContext db) : ControllerBase
{
    public record MonHocRequest(string MaMon, string TenMon, int SoTinChi, int? KhoaId);

    [HttpGet]
    public async Task<IActionResult> LayTatCa([FromQuery] string? tuKhoa) =>
        Ok(await db.MonHoc
            .Where(m => tuKhoa == null || m.MaMon.Contains(tuKhoa) || m.TenMon.Contains(tuKhoa))
            .OrderBy(m => m.MaMon)
            .Select(m => new { m.Id, m.MaMon, m.TenMon, m.SoTinChi, m.KhoaId })
            .ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> LayTheoId(int id)
    {
        var m = await db.MonHoc.Where(x => x.Id == id)
            .Select(x => new { x.Id, x.MaMon, x.TenMon, x.SoTinChi, x.KhoaId })
            .FirstOrDefaultAsync();
        return m is null ? NotFound(new { thongBao = "Không tìm thấy môn học." }) : Ok(m);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Them(MonHocRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MaMon) || string.IsNullOrWhiteSpace(req.TenMon))
            return BadRequest(new { thongBao = "Mã môn và tên môn không được trống." });
        if (req.SoTinChi < 1 || req.SoTinChi > 10)
            return BadRequest(new { thongBao = "Số tín chỉ phải từ 1 đến 10." });
        if (req.KhoaId != null && !await db.Khoa.AnyAsync(k => k.Id == req.KhoaId))
            return BadRequest(new { thongBao = "Khoa không tồn tại." });
        if (await db.MonHoc.AnyAsync(m => m.MaMon == req.MaMon))
            return Conflict(new { thongBao = "Mã môn đã tồn tại." });

        var m = new MonHoc
        {
            MaMon = req.MaMon.Trim(),
            TenMon = req.TenMon.Trim(),
            SoTinChi = (byte)req.SoTinChi,
            KhoaId = req.KhoaId
        };
        db.MonHoc.Add(m);
        await db.SaveChangesAsync();
        return Ok(new { m.Id, m.MaMon, m.TenMon, m.SoTinChi, m.KhoaId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Sua(int id, MonHocRequest req)
    {
        var m = await db.MonHoc.FindAsync(id);
        if (m is null) return NotFound(new { thongBao = "Không tìm thấy môn học." });
        if (req.SoTinChi < 1 || req.SoTinChi > 10)
            return BadRequest(new { thongBao = "Số tín chỉ phải từ 1 đến 10." });
        if (req.KhoaId != null && !await db.Khoa.AnyAsync(k => k.Id == req.KhoaId))
            return BadRequest(new { thongBao = "Khoa không tồn tại." });
        if (await db.MonHoc.AnyAsync(x => x.MaMon == req.MaMon && x.Id != id))
            return Conflict(new { thongBao = "Mã môn đã tồn tại." });

        m.MaMon = req.MaMon.Trim();
        m.TenMon = req.TenMon.Trim();
        m.SoTinChi = (byte)req.SoTinChi;
        m.KhoaId = req.KhoaId;
        await db.SaveChangesAsync();
        return Ok(new { m.Id, m.MaMon, m.TenMon, m.SoTinChi, m.KhoaId });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Xoa(int id)
    {
        var m = await db.MonHoc.FindAsync(id);
        if (m is null) return NotFound(new { thongBao = "Không tìm thấy môn học." });
        try
        {
            db.MonHoc.Remove(m);
            await db.SaveChangesAsync();
            return Ok(new { thongBao = "Đã xóa môn học." });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { thongBao = "Không thể xóa: môn học đang được sử dụng." });
        }
    }
}

// =====================================================
// HỌC KỲ
// =====================================================
[ApiController]
[Route("api/hoc-ky")]
[Authorize]
public class HocKyController(QuanLyHocVuContext db) : ControllerBase
{
    public record HocKyRequest(string TenHocKy, DateOnly NgayBatDau, DateOnly NgayKetThuc, bool LaHienTai);

    [HttpGet]
    public async Task<IActionResult> LayTatCa() =>
        Ok(await db.HocKy.OrderByDescending(h => h.NgayBatDau)
            .Select(h => new { h.Id, h.TenHocKy, h.NgayBatDau, h.NgayKetThuc, h.LaHienTai })
            .ToListAsync());

    [HttpGet("hien-tai")]
    public async Task<IActionResult> LayHienTai()
    {
        var h = await db.HocKy.Where(x => x.LaHienTai)
            .Select(x => new { x.Id, x.TenHocKy, x.NgayBatDau, x.NgayKetThuc, x.LaHienTai })
            .FirstOrDefaultAsync();
        return h is null ? NotFound(new { thongBao = "Chưa đặt học kỳ hiện tại." }) : Ok(h);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Them(HocKyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TenHocKy))
            return BadRequest(new { thongBao = "Tên học kỳ không được trống." });
        if (req.NgayKetThuc <= req.NgayBatDau)
            return BadRequest(new { thongBao = "Ngày kết thúc phải sau ngày bắt đầu." });

        if (req.LaHienTai) await BoDanhDauHienTai();

        var h = new HocKy
        {
            TenHocKy = req.TenHocKy.Trim(),
            NgayBatDau = req.NgayBatDau,
            NgayKetThuc = req.NgayKetThuc,
            LaHienTai = req.LaHienTai
        };
        db.HocKy.Add(h);
        await db.SaveChangesAsync();
        return Ok(new { h.Id, h.TenHocKy, h.NgayBatDau, h.NgayKetThuc, h.LaHienTai });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Sua(int id, HocKyRequest req)
    {
        var h = await db.HocKy.FindAsync(id);
        if (h is null) return NotFound(new { thongBao = "Không tìm thấy học kỳ." });
        if (req.NgayKetThuc <= req.NgayBatDau)
            return BadRequest(new { thongBao = "Ngày kết thúc phải sau ngày bắt đầu." });

        if (req.LaHienTai) await BoDanhDauHienTai();

        h.TenHocKy = req.TenHocKy.Trim();
        h.NgayBatDau = req.NgayBatDau;
        h.NgayKetThuc = req.NgayKetThuc;
        h.LaHienTai = req.LaHienTai;
        await db.SaveChangesAsync();
        return Ok(new { h.Id, h.TenHocKy, h.NgayBatDau, h.NgayKetThuc, h.LaHienTai });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Xoa(int id)
    {
        var h = await db.HocKy.FindAsync(id);
        if (h is null) return NotFound(new { thongBao = "Không tìm thấy học kỳ." });
        try
        {
            db.HocKy.Remove(h);
            await db.SaveChangesAsync();
            return Ok(new { thongBao = "Đã xóa học kỳ." });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { thongBao = "Không thể xóa: học kỳ đang được sử dụng." });
        }
    }

    // Chỉ có một học kỳ hiện tại tại một thời điểm
    private async Task BoDanhDauHienTai()
    {
        var cacHocKy = await db.HocKy.Where(x => x.LaHienTai).ToListAsync();
        foreach (var x in cacHocKy) x.LaHienTai = false;
    }
}