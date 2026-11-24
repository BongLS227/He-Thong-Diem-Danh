using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class Nganh
{
    public int Id { get; set; }

    public int KhoaId { get; set; }

    public string MaNganh { get; set; } = null!;

    public string TenNganh { get; set; } = null!;

    public virtual Khoa Khoa { get; set; } = null!;

    public virtual ICollection<SinhVien> SinhVien { get; set; } = new List<SinhVien>();
}
