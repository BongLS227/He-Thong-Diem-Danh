using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class SinhVien
{
    public int Id { get; set; }

    public int NguoiDungId { get; set; }

    public string MaSinhVien { get; set; } = null!;

    public int? NganhId { get; set; }

    public string? LopSinhHoat { get; set; }

    public DateOnly? NgaySinh { get; set; }

    public string? SoDienThoai { get; set; }

    public string? DuLieuKhuonMat { get; set; }

    public virtual ICollection<CanhBao> CanhBao { get; set; } = new List<CanhBao>();

    public virtual ICollection<DangKyHocPhan> DangKyHocPhan { get; set; } = new List<DangKyHocPhan>();

    public virtual ICollection<DiemDanh> DiemDanh { get; set; } = new List<DiemDanh>();

    public virtual Nganh? Nganh { get; set; }

    public virtual NguoiDung NguoiDung { get; set; } = null!;
}
