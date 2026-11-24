using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class HocKy
{
    public int Id { get; set; }

    public string TenHocKy { get; set; } = null!;

    public DateOnly NgayBatDau { get; set; }

    public DateOnly NgayKetThuc { get; set; }

    public bool LaHienTai { get; set; }

    public virtual ICollection<LopHocPhan> LopHocPhan { get; set; } = new List<LopHocPhan>();
}
