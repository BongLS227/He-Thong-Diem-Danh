USE QuanLyHocVu;
SELECT sv.MaSinhVien, nd.HoTen, l.MaLop,
       d.DiemKt1        AS KT_HeSo1,
       d.DiemHs2        AS KT_HeSo2,
       d.DiemBoPhan,
       d.DiemCuoiKy     AS ThiKetThuc,
       d.DiemTongKet    AS DiemHocPhan,
       d.DiemChu,
       d.NgayCapNhat
FROM Diem d
JOIN DangKyHocPhan dk ON dk.Id = d.DangKyHocPhanId
JOIN SinhVien sv      ON sv.Id = dk.SinhVienId
JOIN NguoiDung nd     ON nd.Id = sv.NguoiDungId
JOIN LopHocPhan l     ON l.Id = dk.LopHocPhanId;