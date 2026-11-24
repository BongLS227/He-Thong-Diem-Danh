using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class LopHocPhan
{
    public int Id { get; set; }

    public string MaLop { get; set; } = null!;

    public int MonHocId { get; set; }

    public int HocKyId { get; set; }

    public int GiangVienId { get; set; }

    public string? PhongHoc { get; set; }

    public byte? Thu { get; set; }

    public byte? TietBatDau { get; set; }

    public byte? TietKetThuc { get; set; }

    public int SiSoToiDa { get; set; }

    public byte SoBuoiVangToiDa { get; set; }

    public byte TyLeChuyenCan { get; set; }

    public byte TyLeGiuaKy { get; set; }

    public byte TyLeCuoiKy { get; set; }

    public virtual ICollection<BuoiHoc> BuoiHoc { get; set; } = new List<BuoiHoc>();

    public virtual ICollection<CanhBao> CanhBao { get; set; } = new List<CanhBao>();

    public virtual ICollection<DangKyHocPhan> DangKyHocPhan { get; set; } = new List<DangKyHocPhan>();

    public virtual GiangVien GiangVien { get; set; } = null!;

    public virtual HocKy HocKy { get; set; } = null!;

    public virtual MonHoc MonHoc { get; set; } = null!;
}
