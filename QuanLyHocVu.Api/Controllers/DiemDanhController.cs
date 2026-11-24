using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyHocVu.Api.Data;
using QuanLyHocVu.Api.Models;
using QuanLyHocVu.Api.Services;

namespace QuanLyHocVu.Api.Controllers;

// =====================================================
// PHÍA GIẢNG VIÊN / ADMIN: quản lý buổi học
//
// Luồng: Tạo buổi (ChuaMo) -> Mở buổi (DangMo, sinh mã bí mật, tạo sẵn
// danh sách "Vang" cho cả lớp) -> Hiển thị QR động -> Đóng buổi (DaDong).
// Giảng viên chỉ thao tác được với lớp do mình phụ trách.
// =====================================================
[ApiController]
[Route("api/buoi-hoc")]
[Authorize(Roles = "Admin,GiangVien")]
public class BuoiHocController(QuanLyHocVuContext db) : ControllerBase
{
    private static readonly string[] CacTrangThaiDiemDanh = ["CoMat", "Tre", "Vang", "CoPhep"];

    public record TaoBuoiRequest(
        int LopHocPhanId, DateOnly NgayHoc, int? SoBuoi,
        decimal? ViDoPhong, decimal? KinhDoPhong,
        int BanKinhChoPhep = 100, int ThoiGianDoiQr = 30);

    public record MoBuoiRequest(decimal? ViDoPhong, decimal? KinhDoPhong, int? BanKinhChoPhep);

    public record SuaDiemDanhRequest(string TrangThai, string? GhiChu);

    private int NguoiDungId => int.Parse(User.FindFirst("sub")!.Value);
    private bool LaAdmin => User.FindFirst("role")?.Value == "Admin";

    // Admin quản lý mọi lớp; giảng viên chỉ quản lý lớp của mình
    private async Task<bool> QuanLyDuocLop(int lopHocPhanId) =>
        LaAdmin || await db.LopHocPhan.AnyAsync(l =>
            l.Id == lopHocPhanId && l.GiangVien.NguoiDungId == NguoiDungId);

    private static bool ToaDoHopLe(decimal? vd, decimal? kd) =>
        (vd == null && kd == null) ||
        (vd != null && kd != null && vd >= -90 && vd <= 90 && kd >= -180 && kd <= 180);

    // ---------------------------------------------------
    // GET /api/buoi-hoc?lopHocPhanId=1  -> các buổi học của một lớp
    // ---------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> LayDanhSach([FromQuery] int lopHocPhanId)
    {
        if (!await db.LopHocPhan.AnyAsync(l => l.Id == lopHocPhanId))
            return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });
        if (!await QuanLyDuocLop(lopHocPhanId)) return Forbid();

        var ds = await db.BuoiHoc
            .Where(b => b.LopHocPhanId == lopHocPhanId)
            .OrderBy(b => b.SoBuoi)
            .Select(b => new
            {
                b.Id,
                b.SoBuoi,
                b.NgayHoc,
                b.TrangThai,
                b.GioMo,
                b.GioDong,
                b.ViDoPhong,
                b.KinhDoPhong,
                b.BanKinhChoPhep,
                TongSinhVien = b.DiemDanh.Count,
                DaDiemDanh = b.DiemDanh.Count(d => d.TrangThai == "CoMat" || d.TrangThai == "Tre")
            })
            .ToListAsync();

        return Ok(ds);
    }

    // ---------------------------------------------------
    // POST /api/buoi-hoc  -> tạo buổi học mới (chưa mở)
    // ---------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Them(TaoBuoiRequest req)
    {
        if (!await db.LopHocPhan.AnyAsync(l => l.Id == req.LopHocPhanId))
            return NotFound(new { thongBao = "Không tìm thấy lớp học phần." });
        if (!await QuanLyDuocLop(req.LopHocPhanId)) return Forbid();

        if (!ToaDoHopLe(req.ViDoPhong, req.KinhDoPhong))
            return BadRequest(new { thongBao = "Tọa độ không hợp lệ: nhập cả vĩ độ (-90..90) và kinh độ (-180..180), hoặc để trống cả hai." });
        if (req.BanKinhChoPhep < 10 || req.BanKinhChoPhep > 2000)
            return BadRequest(new { thongBao = "Bán kính cho phép phải từ 10 đến 2000 mét." });
        if (req.ThoiGianDoiQr < 10 || req.ThoiGianDoiQr > 300)
            return BadRequest(new { thongBao = "Chu kỳ đổi mã QR phải từ 10 đến 300 giây." });

        // Không nhập số buổi thì tự lấy số tiếp theo
        var soBuoi = req.SoBuoi ??
            (await db.BuoiHoc.Where(b => b.LopHocPhanId == req.LopHocPhanId)
                .MaxAsync(b => (int?)b.SoBuoi) ?? 0) + 1;

        if (soBuoi < 1)
            return BadRequest(new { thongBao = "Số buổi phải từ 1 trở lên." });
        if (await db.BuoiHoc.AnyAsync(b => b.LopHocPhanId == req.LopHocPhanId && b.SoBuoi == soBuoi))
            return Conflict(new { thongBao = $"Buổi số {soBuoi} của lớp này đã tồn tại." });

        var buoi = new BuoiHoc
        {
            LopHocPhanId = req.LopHocPhanId,
            SoBuoi = soBuoi,
            NgayHoc = req.NgayHoc,
            TrangThai = "ChuaMo",
            ViDoPhong = req.ViDoPhong,
            KinhDoPhong = req.KinhDoPhong,
            BanKinhChoPhep = req.BanKinhChoPhep,
            ThoiGianDoiQr = req.ThoiGianDoiQr
        };
        db.BuoiHoc.Add(buoi);
        await db.SaveChangesAsync();

        return Ok(new { buoi.Id, buoi.LopHocPhanId, buoi.SoBuoi, buoi.NgayHoc, buoi.TrangThai });
    }

    // ---------------------------------------------------
    // POST /api/buoi-hoc/5/mo  -> mở buổi để sinh viên bắt đầu điểm danh
    // Có thể gửi kèm tọa độ phòng học (thường lấy từ GPS máy giảng viên).
    // ---------------------------------------------------
    [HttpPost("{id:int}/mo")]
    public async Task<IActionResult> Mo(int id, [FromBody] MoBuoiRequest? req)
    {
        var buoi = await db.BuoiHoc.FindAsync(id);
        if (buoi is null) return NotFound(new { thongBao = "Không tìm thấy buổi học." });
        if (!await QuanLyDuocLop(buoi.LopHocPhanId)) return Forbid();

        if (buoi.TrangThai != "ChuaMo")
            return Conflict(new { thongBao = "Buổi học này đã được mở hoặc đã đóng." });

        // Mỗi lớp chỉ được mở một buổi tại một thời điểm
        if (await db.BuoiHoc.AnyAsync(b => b.LopHocPhanId == buoi.LopHocPhanId && b.TrangThai == "DangMo"))
            return Conflict(new { thongBao = "Lớp này đang có một buổi học khác đang mở. Hãy đóng buổi đó trước." });

        if (req != null && (req.ViDoPhong != null || req.KinhDoPhong != null))
        {
            if (req.ViDoPhong == null || req.KinhDoPhong == null || !ToaDoHopLe(req.ViDoPhong, req.KinhDoPhong))
                return BadRequest(new { thongBao = "Tọa độ không hợp lệ: cần cả vĩ độ (-90..90) và kinh độ (-180..180)." });
            buoi.ViDoPhong = req.ViDoPhong;
            buoi.KinhDoPhong = req.KinhDoPhong;
        }
        if (req?.BanKinhChoPhep != null)
        {
            if (req.BanKinhChoPhep < 10 || req.BanKinhChoPhep > 2000)
                return BadRequest(new { thongBao = "Bán kính cho phép phải từ 10 đến 2000 mét." });
            buoi.BanKinhChoPhep = req.BanKinhChoPhep.Value;
        }

        // Khóa bí mật dùng để ký mã QR (64 ký tự hex), chỉ tồn tại trong lúc buổi đang mở
        buoi.MaBiMatQr = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        buoi.TrangThai = "DangMo";
        buoi.GioMo = DateTime.Now;
        buoi.GioDong = null;

        // Tạo sẵn dòng "Vang" cho mọi sinh viên của lớp; ai quét QR thì đổi thành CoMat/Tre
        var cacSinhVien = await db.DangKyHocPhan
            .Where(d => d.LopHocPhanId == buoi.LopHocPhanId)
            .Select(d => d.SinhVienId).ToListAsync();
        var daCo = await db.DiemDanh.Where(d => d.BuoiHocId == id).Select(d => d.SinhVienId).ToListAsync();

        db.DiemDanh.AddRange(cacSinhVien.Except(daCo)
            .Select(svId => new DiemDanh { BuoiHocId = id, SinhVienId = svId, TrangThai = "Vang" }));

        await db.SaveChangesAsync();

        var coGps = buoi.ViDoPhong != null && buoi.KinhDoPhong != null;
        return Ok(new
        {
            thongBao = "Đã mở buổi học. Sinh viên có thể quét mã QR để điểm danh.",
            soSinhVien = cacSinhVien.Count,
            kiemTraGps = coGps,
            banKinhMet = buoi.BanKinhChoPhep,
            canhBao = coGps ? null : "Buổi học chưa có tọa độ phòng học nên hệ thống CHỈ kiểm tra mã QR, không kiểm tra vị trí."
        });
    }

    // ---------------------------------------------------
    // GET /api/buoi-hoc/5/qr  -> mã QR hiện tại (màn hình giảng viên gọi lặp lại mỗi vài giây)
    // ---------------------------------------------------
    [HttpGet("{id:int}/qr")]
    public async Task<IActionResult> LayMaQr(int id)
    {
        var buoi = await db.BuoiHoc.FindAsync(id);
        if (buoi is null) return NotFound(new { thongBao = "Không tìm thấy buổi học." });
        if (!await QuanLyDuocLop(buoi.LopHocPhanId)) return Forbid();

        if (buoi.TrangThai != "DangMo" || buoi.MaBiMatQr is null)
            return BadRequest(new { thongBao = "Buổi học chưa mở hoặc đã đóng, chưa có mã QR." });

        var chuKy = buoi.ThoiGianDoiQr;
        return Ok(new
        {
            maQr = DiemDanhHelper.TaoMaQr(buoi.Id, buoi.MaBiMatQr, chuKy),
            doiSauGiay = DiemDanhHelper.GiayConLai(chuKy),
            chuKyGiay = chuKy
        });
    }

    // ---------------------------------------------------
    // POST /api/buoi-hoc/5/dong  -> đóng buổi, hủy khóa bí mật
    // ---------------------------------------------------
    [HttpPost("{id:int}/dong")]
    public async Task<IActionResult> Dong(int id)
    {
        var buoi = await db.BuoiHoc.FindAsync(id);
        if (buoi is null) return NotFound(new { thongBao = "Không tìm thấy buổi học." });
        if (!await QuanLyDuocLop(buoi.LopHocPhanId)) return Forbid();

        if (buoi.TrangThai != "DangMo")
            return Conflict(new { thongBao = "Chỉ đóng được buổi học đang mở." });

        buoi.TrangThai = "DaDong";
        buoi.GioDong = DateTime.Now;
        buoi.MaBiMatQr = null;   // mã QR cũ không còn dùng được nữa
        await db.SaveChangesAsync();
        await ThongKeHelper.TaoCanhBaoAsync(db, buoi.LopHocPhanId);
        var vang = await db.DiemDanh.CountAsync(d => d.BuoiHocId == id && d.TrangThai == "Vang");
        return Ok(new { thongBao = "Đã đóng buổi học.", soSinhVienVang = vang });
    }

    // ---------------------------------------------------
    // GET /api/buoi-hoc/5/diem-danh  -> danh sách điểm danh của buổi + thống kê
    // ---------------------------------------------------
    [HttpGet("{id:int}/diem-danh")]
    public async Task<IActionResult> XemDiemDanh(int id)
    {
        var buoi = await db.BuoiHoc.FindAsync(id);
        if (buoi is null) return NotFound(new { thongBao = "Không tìm thấy buổi học." });
        if (!await QuanLyDuocLop(buoi.LopHocPhanId)) return Forbid();

        var ds = await db.DiemDanh
            .Where(d => d.BuoiHocId == id)
            .OrderBy(d => d.SinhVien.MaSinhVien)
            .Select(d => new
            {
                d.SinhVienId,
                d.SinhVien.MaSinhVien,
                d.SinhVien.NguoiDung.HoTen,
                d.TrangThai,
                d.PhuongThuc,
                d.ThoiGianQuet,
                d.KhoangCach,
                d.GhiChu
            })
            .ToListAsync();

        return Ok(new
        {
            buoi = new { buoi.Id, buoi.SoBuoi, buoi.NgayHoc, buoi.TrangThai },
            thongKe = new
            {
                tong = ds.Count,
                coMat = ds.Count(x => x.TrangThai == "CoMat"),
                tre = ds.Count(x => x.TrangThai == "Tre"),
                vang = ds.Count(x => x.TrangThai == "Vang"),
                coPhep = ds.Count(x => x.TrangThai == "CoPhep")
            },
            danhSach = ds
        });
    }

    // ---------------------------------------------------
    // PUT /api/buoi-hoc/5/diem-danh/3  -> giảng viên sửa tay (sinh viên hỏng điện thoại,
    // có giấy phép...). Body: { "trangThai": "CoPhep", "ghiChu": "Có đơn xin nghỉ" }
    // ---------------------------------------------------
    [HttpPut("{id:int}/diem-danh/{sinhVienId:int}")]
    public async Task<IActionResult> SuaDiemDanh(int id, int sinhVienId, SuaDiemDanhRequest req)
    {
        var buoi = await db.BuoiHoc.FindAsync(id);
        if (buoi is null) return NotFound(new { thongBao = "Không tìm thấy buổi học." });
        if (!await QuanLyDuocLop(buoi.LopHocPhanId)) return Forbid();

        if (buoi.TrangThai == "ChuaMo")
            return BadRequest(new { thongBao = "Buổi học chưa mở nên chưa có danh sách điểm danh." });
        if (!CacTrangThaiDiemDanh.Contains(req.TrangThai))
            return BadRequest(new { thongBao = "Trạng thái phải là CoMat, Tre, Vang hoặc CoPhep." });

        var dd = await db.DiemDanh.FirstOrDefaultAsync(d => d.BuoiHocId == id && d.SinhVienId == sinhVienId);
        if (dd is null)
        {
            // Sinh viên đăng ký lớp sau khi buổi đã mở thì chưa có dòng điểm danh: tạo mới nếu có đăng ký
            var thuocLop = await db.DangKyHocPhan.AnyAsync(d =>
                d.LopHocPhanId == buoi.LopHocPhanId && d.SinhVienId == sinhVienId);
            if (!thuocLop)
                return NotFound(new { thongBao = "Sinh viên này không thuộc lớp học phần." });

            dd = new DiemDanh { BuoiHocId = id, SinhVienId = sinhVienId };
            db.DiemDanh.Add(dd);
        }

        dd.TrangThai = req.TrangThai;
        dd.PhuongThuc = "ThuCong";
        dd.GhiChu = req.GhiChu?.Trim();
        if (req.TrangThai is "CoMat" or "Tre") dd.ThoiGianQuet ??= DateTime.Now;

        await db.SaveChangesAsync();
        return Ok(new { thongBao = "Đã cập nhật điểm danh.", dd.SinhVienId, dd.TrangThai, dd.PhuongThuc });
    }
}

// =====================================================
// PHÍA SINH VIÊN: quét QR, xem lịch sử điểm danh của mình
// =====================================================
[ApiController]
[Route("api/diem-danh")]
[Authorize(Roles = "SinhVien")]
public class DiemDanhController(QuanLyHocVuContext db) : ControllerBase
{
    public record QuetRequest(string MaQr, double? ViDo, double? KinhDo);

    private int NguoiDungId => int.Parse(User.FindFirst("sub")!.Value);

    // ---------------------------------------------------
    // POST /api/diem-danh/quet
    // Body: { "maQr": "QLHV|5|58123456|ab12...", "viDo": 10.7770, "kinhDo": 106.7010 }
    // ---------------------------------------------------
    [HttpPost("quet")]
    public async Task<IActionResult> Quet(QuetRequest req)
    {
        // 1. Đọc mã QR: QLHV|idBuoi|khe|chuKy
        var phan = req.MaQr?.Split('|');
        if (phan is null || phan.Length != 4 || phan[0] != "QLHV" ||
            !int.TryParse(phan[1], out var buoiId) || !long.TryParse(phan[2], out var khe))
            return BadRequest(new { thongBao = "Mã QR không hợp lệ." });

        // 2. Buổi học phải đang mở
        var buoi = await db.BuoiHoc.FindAsync(buoiId);
        if (buoi is null || buoi.TrangThai != "DangMo" || buoi.MaBiMatQr is null)
            return BadRequest(new { thongBao = "Buổi học chưa mở hoặc đã đóng." });

        // 3. Chữ ký phải đúng (không thể tự bịa mã)
        if (!DiemDanhHelper.ChuKyHopLe(buoiId, khe, buoi.MaBiMatQr, phan[3]))
            return BadRequest(new { thongBao = "Mã QR không hợp lệ." });

        // 4. Mã còn hạn: chỉ nhận khe hiện tại hoặc khe liền trước (bù độ trễ mạng)
        var kheHienTai = DiemDanhHelper.KheHienTai(buoi.ThoiGianDoiQr);
        if (khe > kheHienTai || kheHienTai - khe > 1)
            return BadRequest(new { thongBao = "Mã QR đã hết hạn. Hãy quét lại mã đang hiển thị trên màn hình." });

        // 5. Sinh viên phải thuộc lớp này
        var sv = await db.SinhVien.FirstOrDefaultAsync(s => s.NguoiDungId == NguoiDungId);
        if (sv is null)
            return NotFound(new { thongBao = "Tài khoản này chưa có hồ sơ sinh viên." });

        var dd = await db.DiemDanh.FirstOrDefaultAsync(d => d.BuoiHocId == buoiId && d.SinhVienId == sv.Id);
        if (dd is null)
            return StatusCode(403, new { thongBao = "Bạn không thuộc lớp học phần của buổi học này." });
        if (dd.TrangThai != "Vang")
            return Conflict(new { thongBao = $"Bạn đã được ghi nhận điểm danh (trạng thái: {dd.TrangThai})." });

        // 6. Kiểm tra vị trí GPS (nếu buổi học có tọa độ phòng)
        double? khoangCach = null;
        if (buoi.ViDoPhong != null && buoi.KinhDoPhong != null)
        {
            if (req.ViDo is null || req.KinhDo is null)
                return BadRequest(new { thongBao = "Buổi học yêu cầu vị trí GPS. Hãy cho phép trình duyệt truy cập vị trí." });
            if (req.ViDo < -90 || req.ViDo > 90 || req.KinhDo < -180 || req.KinhDo > 180)
                return BadRequest(new { thongBao = "Tọa độ GPS không hợp lệ." });

            khoangCach = DiemDanhHelper.KhoangCachMet(
                (double)buoi.ViDoPhong.Value, (double)buoi.KinhDoPhong.Value,
                req.ViDo.Value, req.KinhDo.Value);

            if (khoangCach > buoi.BanKinhChoPhep)
                return StatusCode(403, new
                {
                    thongBao = $"Bạn đang cách phòng học khoảng {Math.Round(khoangCach.Value)} m, vượt bán kính cho phép ({buoi.BanKinhChoPhep} m).",
                    khoangCachMet = Math.Round(khoangCach.Value, 1)
                });
        }

        // 7. Hợp lệ: ghi nhận có mặt hoặc đi muộn
        var bayGio = DateTime.Now;
        var diMuon = buoi.GioMo != null &&
                     bayGio > buoi.GioMo.Value.AddMinutes(DiemDanhHelper.PhutChoPhepDiMuon);

        dd.TrangThai = diMuon ? "Tre" : "CoMat";
        dd.PhuongThuc = "QR";
        dd.ThoiGianQuet = bayGio;
        dd.ViDo = req.ViDo != null ? (decimal)req.ViDo.Value : null;
        dd.KinhDo = req.KinhDo != null ? (decimal)req.KinhDo.Value : null;
        dd.KhoangCach = khoangCach != null ? (decimal)Math.Round(khoangCach.Value, 2) : null;

        await db.SaveChangesAsync();

        return Ok(new
        {
            thongBao = diMuon ? "Điểm danh thành công (đi muộn)." : "Điểm danh thành công.",
            trangThai = dd.TrangThai,
            thoiGianQuet = dd.ThoiGianQuet,
            khoangCachMet = khoangCach != null ? Math.Round(khoangCach.Value, 1) : (double?)null
        });
    }

    // ---------------------------------------------------
    // GET /api/diem-danh/cua-toi?lopHocPhanId=1  -> lịch sử điểm danh của chính mình
    // ---------------------------------------------------
    [HttpGet("cua-toi")]
    public async Task<IActionResult> CuaToi([FromQuery] int? lopHocPhanId)
    {
        var uid = NguoiDungId;
        var q = db.DiemDanh.Where(d => d.SinhVien.NguoiDungId == uid);
        if (lopHocPhanId != null) q = q.Where(d => d.BuoiHoc.LopHocPhanId == lopHocPhanId);

        var ds = await q
            .OrderBy(d => d.BuoiHoc.LopHocPhanId).ThenBy(d => d.BuoiHoc.SoBuoi)
            .Select(d => new
            {
                d.BuoiHocId,
                d.BuoiHoc.LopHocPhanId,
                MaLop = d.BuoiHoc.LopHocPhan.MaLop,
                d.BuoiHoc.SoBuoi,
                d.BuoiHoc.NgayHoc,
                TrangThaiBuoi = d.BuoiHoc.TrangThai,
                d.TrangThai,
                d.PhuongThuc,
                d.ThoiGianQuet,
                d.KhoangCach
            })
            .ToListAsync();

        // Buổi còn đang mở thì "Vang" chỉ là chưa điểm danh, chưa tính là vắng
        return Ok(new
        {
            tongKet = new
            {
                soBuoi = ds.Count,
                coMat = ds.Count(x => x.TrangThai == "CoMat"),
                tre = ds.Count(x => x.TrangThai == "Tre"),
                coPhep = ds.Count(x => x.TrangThai == "CoPhep"),
                vang = ds.Count(x => x.TrangThai == "Vang" && x.TrangThaiBuoi == "DaDong")
            },
            danhSach = ds
        });
    }
}
