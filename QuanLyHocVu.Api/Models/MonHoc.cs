using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class MonHoc
{
    public int Id { get; set; }

    public string MaMon { get; set; } = null!;

    public string TenMon { get; set; } = null!;

    public byte SoTinChi { get; set; }

    public int? KhoaId { get; set; }

    public virtual Khoa? Khoa { get; set; }

    public virtual ICollection<LopHocPhan> LopHocPhan { get; set; } = new List<LopHocPhan>();
}
