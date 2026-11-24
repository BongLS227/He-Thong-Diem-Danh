using System;
using System.Collections.Generic;

namespace QuanLyHocVu.Api.Models;

public partial class Diem
{
    public int Id { get; set; }

    public int DangKyHocPhanId { get; set; }

    public decimal? DiemChuyenCan { get; set; }

    public decimal? DiemGiuaKy { get; set; }

    public decimal? DiemCuoiKy { get; set; }

    public decimal? DiemTongKet { get; set; }

    public string? DiemChu { get; set; }

    public DateTime NgayCapNhat { get; set; }

    public decimal? DiemKt1 { get; set; }

    public decimal? DiemKt2 { get; set; }

    public decimal? DiemHs2 { get; set; }

    public decimal? DiemBoPhan { get; set; }

    public virtual DangKyHocPhan DangKyHocPhan { get; set; } = null!;
}
