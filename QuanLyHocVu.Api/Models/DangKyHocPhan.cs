using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class DangKyHocPhan
{
    public int Id { get; set; }

    public int LopHocPhanId { get; set; }

    public int SinhVienId { get; set; }

    public DateTime NgayDangKy { get; set; }

    public virtual Diem? Diem { get; set; }

    public virtual LopHocPhan LopHocPhan { get; set; } = null!;

    public virtual SinhVien SinhVien { get; set; } = null!;
}
