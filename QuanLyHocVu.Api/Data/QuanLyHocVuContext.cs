using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QuanLyHocVu.Api.Models;

namespace QuanLyHocVu.Api.Data;

public partial class QuanLyHocVuContext : DbContext
{
    public QuanLyHocVuContext(DbContextOptions<QuanLyHocVuContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BuoiHoc> BuoiHoc { get; set; }

    public virtual DbSet<CanhBao> CanhBao { get; set; }

    public virtual DbSet<DangKyHocPhan> DangKyHocPhan { get; set; }

    public virtual DbSet<Diem> Diem { get; set; }

    public virtual DbSet<DiemDanh> DiemDanh { get; set; }

    public virtual DbSet<GiangVien> GiangVien { get; set; }

    public virtual DbSet<HocKy> HocKy { get; set; }

    public virtual DbSet<Khoa> Khoa { get; set; }

    public virtual DbSet<LopHocPhan> LopHocPhan { get; set; }

    public virtual DbSet<MonHoc> MonHoc { get; set; }

    public virtual DbSet<Nganh> Nganh { get; set; }

    public virtual DbSet<NguoiDung> NguoiDung { get; set; }

    public virtual DbSet<SinhVien> SinhVien { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Vietnamese_CI_AS");

        modelBuilder.Entity<BuoiHoc>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BuoiHoc__3214EC07B74CC63B");

            entity.HasIndex(e => new { e.LopHocPhanId, e.NgayHoc }, "IX_BuoiHoc_Lop_Ngay");

            entity.HasIndex(e => new { e.LopHocPhanId, e.SoBuoi }, "UQ_BuoiHoc").IsUnique();

            entity.Property(e => e.BanKinhChoPhep).HasDefaultValue(100);
            entity.Property(e => e.KinhDoPhong).HasColumnType("decimal(10, 7)");
            entity.Property(e => e.MaBiMatQr)
                .HasMaxLength(64)
                .IsUnicode(false)
                .HasColumnName("MaBiMatQR");
            entity.Property(e => e.ThoiGianDoiQr)
                .HasDefaultValue(30)
                .HasColumnName("ThoiGianDoiQR");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ChuaMo");
            entity.Property(e => e.ViDoPhong).HasColumnType("decimal(10, 7)");

            entity.HasOne(d => d.LopHocPhan).WithMany(p => p.BuoiHoc)
                .HasForeignKey(d => d.LopHocPhanId)
                .HasConstraintName("FK__BuoiHoc__LopHocP__7C4F7684");
        });

        modelBuilder.Entity<CanhBao>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CanhBao__3214EC075709E5CA");

            entity.Property(e => e.Loai)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.NoiDung).HasMaxLength(255);

            entity.HasOne(d => d.LopHocPhan).WithMany(p => p.CanhBao)
                .HasForeignKey(d => d.LopHocPhanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CanhBao__LopHocP__10566F31");

            entity.HasOne(d => d.SinhVien).WithMany(p => p.CanhBao)
                .HasForeignKey(d => d.SinhVienId)
                .HasConstraintName("FK__CanhBao__SinhVie__0F624AF8");
        });

        modelBuilder.Entity<DangKyHocPhan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DangKyHo__3214EC07EFC10753");

            entity.HasIndex(e => e.SinhVienId, "IX_DangKyHocPhan_SinhVien");

            entity.HasIndex(e => new { e.LopHocPhanId, e.SinhVienId }, "UQ_DangKyHocPhan").IsUnique();

            entity.Property(e => e.NgayDangKy).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.LopHocPhan).WithMany(p => p.DangKyHocPhan)
                .HasForeignKey(d => d.LopHocPhanId)
                .HasConstraintName("FK__DangKyHoc__LopHo__76969D2E");

            entity.HasOne(d => d.SinhVien).WithMany(p => p.DangKyHocPhan)
                .HasForeignKey(d => d.SinhVienId)
                .HasConstraintName("FK__DangKyHoc__SinhV__778AC167");
        });

        modelBuilder.Entity<Diem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Diem__3214EC0700F90DA8");

            entity.HasIndex(e => e.DangKyHocPhanId, "UQ__Diem__0083809D3E1942AB").IsUnique();

            entity.Property(e => e.DiemBoPhan).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.DiemChu)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.DiemChuyenCan).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.DiemCuoiKy).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.DiemGiuaKy).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.DiemHs2).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.DiemKt1).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.DiemKt2).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.DiemTongKet).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.NgayCapNhat).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.DangKyHocPhan).WithOne(p => p.Diem)
                .HasForeignKey<Diem>(d => d.DangKyHocPhanId)
                .HasConstraintName("FK__Diem__DangKyHocP__0B91BA14");
        });

        modelBuilder.Entity<DiemDanh>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DiemDanh__3214EC0767E0195B");

            entity.HasIndex(e => e.SinhVienId, "IX_DiemDanh_SinhVien");

            entity.HasIndex(e => new { e.BuoiHocId, e.SinhVienId }, "UQ_DiemDanh").IsUnique();

            entity.Property(e => e.GhiChu).HasMaxLength(255);
            entity.Property(e => e.KhoangCach).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.KinhDo).HasColumnType("decimal(10, 7)");
            entity.Property(e => e.PhuongThuc)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Vang");
            entity.Property(e => e.ViDo).HasColumnType("decimal(10, 7)");

            entity.HasOne(d => d.BuoiHoc).WithMany(p => p.DiemDanh)
                .HasForeignKey(d => d.BuoiHocId)
                .HasConstraintName("FK__DiemDanh__BuoiHo__03F0984C");

            entity.HasOne(d => d.SinhVien).WithMany(p => p.DiemDanh)
                .HasForeignKey(d => d.SinhVienId)
                .HasConstraintName("FK__DiemDanh__SinhVi__04E4BC85");
        });

        modelBuilder.Entity<GiangVien>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__GiangVie__3214EC0791F77AD1");

            entity.HasIndex(e => e.MaGiangVien, "UQ__GiangVie__C03BEEBBFC2813BE").IsUnique();

            entity.HasIndex(e => e.NguoiDungId, "UQ__GiangVie__C4BBA4BC82288758").IsUnique();

            entity.Property(e => e.MaGiangVien)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SoDienThoai)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Khoa).WithMany(p => p.GiangVien)
                .HasForeignKey(d => d.KhoaId)
                .HasConstraintName("FK__GiangVien__KhoaI__5FB337D6");

            entity.HasOne(d => d.NguoiDung).WithOne(p => p.GiangVien)
                .HasForeignKey<GiangVien>(d => d.NguoiDungId)
                .HasConstraintName("FK__GiangVien__Nguoi__5EBF139D");
        });

        modelBuilder.Entity<HocKy>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__HocKy__3214EC0754DDEFCF");

            entity.Property(e => e.TenHocKy).HasMaxLength(50);
        });

        modelBuilder.Entity<Khoa>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Khoa__3214EC07DA459B37");

            entity.HasIndex(e => e.MaKhoa, "UQ__Khoa__653904040F050E14").IsUnique();

            entity.Property(e => e.MaKhoa)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TenKhoa).HasMaxLength(100);
        });

        modelBuilder.Entity<LopHocPhan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__LopHocPh__3214EC07F89A6F28");

            entity.HasIndex(e => e.MaLop, "UQ__LopHocPh__3B98D272CEBB01C8").IsUnique();

            entity.Property(e => e.MaLop)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PhongHoc).HasMaxLength(30);
            entity.Property(e => e.SiSoToiDa).HasDefaultValue(60);
            entity.Property(e => e.SoBuoiVangToiDa).HasDefaultValue((byte)3);
            entity.Property(e => e.TyLeChuyenCan).HasDefaultValue((byte)10);
            entity.Property(e => e.TyLeCuoiKy).HasDefaultValue((byte)60);
            entity.Property(e => e.TyLeGiuaKy).HasDefaultValue((byte)30);

            entity.HasOne(d => d.GiangVien).WithMany(p => p.LopHocPhan)
                .HasForeignKey(d => d.GiangVienId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LopHocPha__Giang__6D0D32F4");

            entity.HasOne(d => d.HocKy).WithMany(p => p.LopHocPhan)
                .HasForeignKey(d => d.HocKyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LopHocPha__HocKy__6C190EBB");

            entity.HasOne(d => d.MonHoc).WithMany(p => p.LopHocPhan)
                .HasForeignKey(d => d.MonHocId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LopHocPha__MonHo__6B24EA82");
        });

        modelBuilder.Entity<MonHoc>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__MonHoc__3214EC076FFF0DA5");

            entity.HasIndex(e => e.MaMon, "UQ__MonHoc__3A5B29A965693CEC").IsUnique();

            entity.Property(e => e.MaMon)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SoTinChi).HasDefaultValue((byte)3);
            entity.Property(e => e.TenMon).HasMaxLength(150);

            entity.HasOne(d => d.Khoa).WithMany(p => p.MonHoc)
                .HasForeignKey(d => d.KhoaId)
                .HasConstraintName("FK__MonHoc__KhoaId__6477ECF3");
        });

        modelBuilder.Entity<Nganh>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Nganh__3214EC07A5F41CBE");

            entity.HasIndex(e => e.MaNganh, "UQ__Nganh__A2CEF50C6F59225C").IsUnique();

            entity.Property(e => e.MaNganh)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TenNganh).HasMaxLength(100);

            entity.HasOne(d => d.Khoa).WithMany(p => p.Nganh)
                .HasForeignKey(d => d.KhoaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Nganh__KhoaId__5441852A");
        });

        modelBuilder.Entity<NguoiDung>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__NguoiDun__3214EC07B99428CB");

            entity.HasIndex(e => e.Email, "UQ_NguoiDung_Email")
                .IsUnique()
                .HasFilter("([Email] IS NOT NULL)");

            entity.HasIndex(e => e.TenDangNhap, "UQ__NguoiDun__55F68FC0889E2647").IsUnique();

            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.HoatDong).HasDefaultValue(true);
            entity.Property(e => e.MatKhauHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TenDangNhap)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.VaiTro)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SinhVien>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SinhVien__3214EC07DB76A7C6");

            entity.HasIndex(e => e.MaSinhVien, "UQ__SinhVien__939AE774654C971B").IsUnique();

            entity.HasIndex(e => e.NguoiDungId, "UQ__SinhVien__C4BBA4BC1057A52B").IsUnique();

            entity.Property(e => e.LopSinhHoat).HasMaxLength(50);
            entity.Property(e => e.MaSinhVien)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SoDienThoai)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Nganh).WithMany(p => p.SinhVien)
                .HasForeignKey(d => d.NganhId)
                .HasConstraintName("FK__SinhVien__NganhI__59FA5E80");

            entity.HasOne(d => d.NguoiDung).WithOne(p => p.SinhVien)
                .HasForeignKey<SinhVien>(d => d.NguoiDungId)
                .HasConstraintName("FK__SinhVien__NguoiD__59063A47");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
