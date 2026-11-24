using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class GiangVien
{
    public int Id { get; set; }

    public int NguoiDungId { get; set; }

    public string MaGiangVien { get; set; } = null!;

    public int? KhoaId { get; set; }

    public string? SoDienThoai { get; set; }

    public virtual Khoa? Khoa { get; set; }

    public virtual ICollection<LopHocPhan> LopHocPhan { get; set; } = new List<LopHocPhan>();

    public virtual NguoiDung NguoiDung { get; set; } = null!;
}
