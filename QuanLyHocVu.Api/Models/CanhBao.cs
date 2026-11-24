using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class CanhBao
{
    public int Id { get; set; }

    public int SinhVienId { get; set; }

    public int LopHocPhanId { get; set; }

    public string Loai { get; set; } = null!;

    public string NoiDung { get; set; } = null!;

    public bool DaDoc { get; set; }

    public DateTime NgayTao { get; set; }

    public virtual LopHocPhan LopHocPhan { get; set; } = null!;

    public virtual SinhVien SinhVien { get; set; } = null!;
}
