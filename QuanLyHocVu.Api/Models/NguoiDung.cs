using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class NguoiDung
{
    public int Id { get; set; }

    public string TenDangNhap { get; set; } = null!;

    public string MatKhauHash { get; set; } = null!;

    public string HoTen { get; set; } = null!;

    public string? Email { get; set; }

    public string VaiTro { get; set; } = null!;

    public bool HoatDong { get; set; }

    public DateTime NgayTao { get; set; }

    public virtual GiangVien? GiangVien { get; set; }

    public virtual SinhVien? SinhVien { get; set; }
}
