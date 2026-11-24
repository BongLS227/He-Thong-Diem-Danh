using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class BuoiHoc
{
    public int Id { get; set; }

    public int LopHocPhanId { get; set; }

    public int SoBuoi { get; set; }

    public DateOnly NgayHoc { get; set; }

    public string TrangThai { get; set; } = null!;

    public string? MaBiMatQr { get; set; }

    public int ThoiGianDoiQr { get; set; }

    public decimal? ViDoPhong { get; set; }

    public decimal? KinhDoPhong { get; set; }

    public int BanKinhChoPhep { get; set; }

    public DateTime? GioMo { get; set; }

    public DateTime? GioDong { get; set; }

    public virtual ICollection<DiemDanh> DiemDanh { get; set; } = new List<DiemDanh>();

    public virtual LopHocPhan LopHocPhan { get; set; } = null!;
}
