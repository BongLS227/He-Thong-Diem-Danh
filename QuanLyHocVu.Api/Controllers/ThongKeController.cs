using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyHocVu.Api.Data;
using QuanLyHocVu.Api.Services;

namespace QuanLyHocVu.Api.Controllers;

// =====================================================
// THỐNG KÊ CHUYÊN CẦN
// =====================================================
[ApiController]
[Route("api/thong-ke")]
[Authorize]
public class ThongKeController(QuanLyHocVuContext db) : ControllerBase
{
    private int NguoiDungId => int.Parse(User.FindFirst("sub")!.Value);
    private bool LaAdmin => User.FindFirst("role")?.Value == "Admin";

    // GET /api/thong-ke/lop/5/chuyen-can  (giảng viên của lớp hoặc Admin)
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpGet("lop/{lopId:int}/chuyen-can")]
    public async Task<IActionResult> ChuyenCanLop(int lopId)
    {
        var lop = await db.LopHocPhan.Where(l => l.Id == lopId)
            .Select(l => new { l.Id, l.MaLop, TenMon = l.MonHoc.TenMon, SoBuoiVangToiDa = (int)l.SoBuoiVangToiDa })
            .FirstOrDefaultAsync();
        if (lop is null) return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });
        if (!await ThongKeHelper.QuanLyDuocLopAsync(db, lopId, NguoiDungId, LaAdmin)) return Forbid();

        var sv = await ThongKeHelper.TinhAsync(db, lopId);

        // Tỷ lệ có mặt (gồm cả đi muộn) của từng buổi, dùng để vẽ biểu đồ
        var buoi = await db.BuoiHoc
            .Where(b => b.LopHocPhanId == lopId && b.TrangThai != "ChuaMo")
            .OrderBy(b => b.SoBuoi)
            .Select(b => new
            {
                b.SoBuoi,
                b.NgayHoc,
                b.TrangThai,
                Tong = b.DiemDanh.Count,
                CoMat = b.DiemDanh.Count(d => d.TrangThai == "CoMat" || d.TrangThai == "Tre")
            })
            .ToListAsync();

        var theoBuoi = buoi.Select(b => new
        {
            b.SoBuoi,
            b.NgayHoc,
            b.TrangThai,
            b.Tong,
            b.CoMat,
            TyLe = b.Tong == 0 ? 0 : Math.Round(100.0 * b.CoMat / b.Tong, 1)
        });

        return Ok(new
        {
            lop,
            tongKet = new
            {
                soSinhVien = sv.Count,
                tyLeTrungBinh = sv.Count == 0 ? 0 : Math.Round(sv.Average(x => x.TyLe) * 100, 1),
                vuot = sv.Count(x => x.MucDo == "VuotGioiHan"),
                sat = sv.Count(x => x.MucDo == "SatGioiHan"),
                chuY = sv.Count(x => x.MucDo == "ChuY"),
                tyLeThap = sv.Count(x => x.MucDo == "TyLeThap")
            },
            sinhVien = sv,
            theoBuoi
        });
    }

    // GET /api/thong-ke/cua-toi  (sinh viên xem chuyên cần của mình ở từng lớp)
    [Authorize(Roles = "SinhVien")]
    [HttpGet("cua-toi")]
    public async Task<IActionResult> CuaToi()
    {
        var sv = await db.SinhVien.FirstOrDefaultAsync(s => s.NguoiDungId == NguoiDungId);
        if (sv is null) return NotFound(new { thongBao = "Tài khoản này chưa có hồ sơ sinh viên." });

        var cacLop = await db.DangKyHocPhan.Where(d => d.SinhVienId == sv.Id)
            .Select(d => new
            {
                d.LopHocPhanId,
                d.LopHocPhan.MaLop,
                TenMon = d.LopHocPhan.MonHoc.TenMon,
                SoBuoiVangToiDa = (int)d.LopHocPhan.SoBuoiVangToiDa
            })
            .ToListAsync();

        var kq = new List<object>();
        foreach (var lop in cacLop)
        {
            var x = (await ThongKeHelper.TinhAsync(db, lop.LopHocPhanId, sv.Id)).FirstOrDefault();
            if (x is null) continue;
            kq.Add(new
            {
                lop.LopHocPhanId,
                lop.MaLop,
                lop.TenMon,
                lop.SoBuoiVangToiDa,
                x.SoBuoi,
                x.CoMat,
                x.Tre,
                x.CoPhep,
                x.Vang,
                x.TyLe,
                x.DiemChuyenCan,
                x.MucDo,
                x.NoiDung
            });
        }
        return Ok(kq);
    }
}

// =====================================================
// CẢNH BÁO
// =====================================================
[ApiController]
[Route("api/canh-bao")]
[Authorize]
public class CanhBaoController(QuanLyHocVuContext db) : ControllerBase
{
    private int NguoiDungId => int.Parse(User.FindFirst("sub")!.Value);
    private bool LaAdmin => User.FindFirst("role")?.Value == "Admin";

    // GET /api/canh-bao/cua-toi  (sinh viên xem cảnh báo của mình, mới nhất trước)
    [Authorize(Roles = "SinhVien")]
    [HttpGet("cua-toi")]
    public async Task<IActionResult> CuaToi()
    {
        var uid = NguoiDungId;
        var ds = await db.CanhBao
            .Where(c => c.SinhVien.NguoiDungId == uid)
            .OrderByDescending(c => c.NgayTao)
            .Take(50)
            .Select(c => new
            {
                c.Id,
                c.LopHocPhanId,
                MaLop = c.LopHocPhan.MaLop,
                TenMon = c.LopHocPhan.MonHoc.TenMon,
                c.Loai,
                c.NoiDung,
                c.DaDoc,
                c.NgayTao
            })
            .ToListAsync();
        return Ok(ds);
    }

    // POST /api/canh-bao/da-doc-tat-ca  (sinh viên bấm "Đã hiểu")
    [Authorize(Roles = "SinhVien")]
    [HttpPost("da-doc-tat-ca")]
    public async Task<IActionResult> DaDocTatCa()
    {
        var uid = NguoiDungId;
        var ds = await db.CanhBao.Where(c => c.SinhVien.NguoiDungId == uid && !c.DaDoc).ToListAsync();
        foreach (var c in ds) c.DaDoc = true;
        await db.SaveChangesAsync();
        return Ok(new { daDanhDau = ds.Count });
    }

    // GET /api/canh-bao/lop/5  (giảng viên xem các cảnh báo đã gửi cho lớp)
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpGet("lop/{lopId:int}")]
    public async Task<IActionResult> CuaLop(int lopId)
    {
        if (!await db.LopHocPhan.AnyAsync(l => l.Id == lopId))
            return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });
        if (!await ThongKeHelper.QuanLyDuocLopAsync(db, lopId, NguoiDungId, LaAdmin)) return Forbid();

        var ds = await db.CanhBao
            .Where(c => c.LopHocPhanId == lopId)
            .OrderByDescending(c => c.NgayTao)
            .Take(100)
            .Select(c => new
            {
                c.Id,
                c.SinhVien.MaSinhVien,
                c.SinhVien.NguoiDung.HoTen,
                c.Loai,
                c.NoiDung,
                c.DaDoc,
                c.NgayTao
            })
            .ToListAsync();
        return Ok(ds);
    }

    // POST /api/canh-bao/lop/5/tao  (tạo cảnh báo ngay, không cần chờ đóng buổi)
    [Authorize(Roles = "Admin,GiangVien")]
    [HttpPost("lop/{lopId:int}/tao")]
    public async Task<IActionResult> TaoChoLop(int lopId)
    {
        if (!await db.LopHocPhan.AnyAsync(l => l.Id == lopId))
            return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });
        if (!await ThongKeHelper.QuanLyDuocLopAsync(db, lopId, NguoiDungId, LaAdmin)) return Forbid();

        var soMoi = await ThongKeHelper.TaoCanhBaoAsync(db, lopId);
        return Ok(new
        {
            soCanhBaoMoi = soMoi,
            thongBao = soMoi == 0
                ? "Không có cảnh báo mới (sinh viên đã được cảnh báo hoặc chưa ai ở mức cần chú ý)."
                : $"Đã tạo {soMoi} cảnh báo mới."
        });
    }
}