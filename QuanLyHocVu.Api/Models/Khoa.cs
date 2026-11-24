using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class Khoa
{
    public int Id { get; set; }

    public string MaKhoa { get; set; } = null!;

    public string TenKhoa { get; set; } = null!;

    public virtual ICollection<GiangVien> GiangVien { get; set; } = new List<GiangVien>();

    public virtual ICollection<MonHoc> MonHoc { get; set; } = new List<MonHoc>();

    public virtual ICollection<Nganh> Nganh { get; set; } = new List<Nganh>();
}
