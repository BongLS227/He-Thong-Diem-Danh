using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class DiemDanh
{
    public int Id { get; set; }

    public int BuoiHocId { get; set; }

    public int SinhVienId { get; set; }

    public string TrangThai { get; set; } = null!;

    public string? PhuongThuc { get; set; }

    public DateTime? ThoiGianQuet { get; set; }

    public decimal? ViDo { get; set; }

    public decimal? KinhDo { get; set; }

    public decimal? KhoangCach { get; set; }

    public string? GhiChu { get; set; }

    public virtual BuoiHoc BuoiHoc { get; set; } = null!;

    public virtual SinhVien SinhVien { get; set; } = null!;
}
