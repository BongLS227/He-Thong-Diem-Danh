# =====================================================
# THÊM NHIỀU SINH VIÊN VÀ ĐĂNG KÝ VÀO MỘT LỚP HỌC PHẦN
#
# Điều kiện: server đang chạy (dotnet run) ở http://localhost:5231
# Cách chạy (trong thư mục chứa file này):
#   powershell -ExecutionPolicy Bypass -File .\them-sinh-vien.ps1 -Csv .\sinhvien-mau.csv -LopId 1
# Script hỏi mật khẩu admin, sau đó:
#   1. Tạo từng sinh viên trong file CSV (tên đăng nhập = mã sinh viên, mật khẩu mặc định bên dưới)
#   2. Đăng ký tất cả vào lớp học phần có Id = LopId
# Sinh viên đã tồn tại thì bỏ qua bước tạo nhưng vẫn được đăng ký vào lớp.
# =====================================================
param(
    [string]$Csv = ".\sinhvien-mau.csv",
    [int]$LopId = 1,
    [string]$MatKhauMacDinh = "Pdu@2026",
    [string]$BaseUrl = "http://localhost:5231"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Goi-Api($method, $duongDan, $body, $token) {
    $headers = @{}
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    $params = @{ Uri = "$BaseUrl$duongDan"; Method = $method; Headers = $headers; ContentType = "application/json; charset=utf-8" }
    if ($null -ne $body) {
        $json = $body | ConvertTo-Json -Depth 5 -Compress
        $params["Body"] = [System.Text.Encoding]::UTF8.GetBytes($json)   # gửi UTF-8 để giữ dấu tiếng Việt
    }
    Invoke-RestMethod @params
}

function Lay-ThongBaoLoi($loi) {
    try { return ($loi.ErrorDetails.Message | ConvertFrom-Json).thongBao } catch { return $loi.Exception.Message }
}

if (-not (Test-Path $Csv)) { Write-Host "Không tìm thấy file CSV: $Csv" -ForegroundColor Red; exit 1 }
$ds = Import-Csv -Path $Csv -Encoding UTF8

# 1. Đăng nhập bằng tài khoản admin
$mk = Read-Host "Mật khẩu của tài khoản admin" -AsSecureString
$mkThuong = [System.Net.NetworkCredential]::new("", $mk).Password
try {
    $dn = Goi-Api "POST" "/api/auth/dang-nhap" @{ tenDangNhap = "admin"; matKhau = $mkThuong } $null
} catch {
    Write-Host "Đăng nhập thất bại: $(Lay-ThongBaoLoi $_)" -ForegroundColor Red; exit 1
}
$token = $dn.token

# 2. Tạo từng sinh viên
$ids = @()
foreach ($sv in $ds) {
    $body = @{
        maSinhVien  = $sv.MaSinhVien
        matKhau     = $MatKhauMacDinh
        hoTen       = $sv.HoTen
        email       = $(if ($sv.Email) { $sv.Email } else { $null })
        lopSinhHoat = $sv.LopSinhHoat
        ngaySinh    = $(if ($sv.NgaySinh) { $sv.NgaySinh } else { $null })
    }
    try {
        $kq = Goi-Api "POST" "/api/sinh-vien" $body $token
        $ids += $kq.id
        Write-Host "Đã tạo: $($sv.MaSinhVien) - $($sv.HoTen)" -ForegroundColor Green
    } catch {
        $loi = Lay-ThongBaoLoi $_
        # Có thể đã tồn tại: tìm lại để vẫn đăng ký vào lớp
        $tim = Goi-Api "GET" "/api/sinh-vien?tuKhoa=$($sv.MaSinhVien)" $null $token |
               Where-Object { $_.maSinhVien -eq $sv.MaSinhVien } | Select-Object -First 1
        if ($tim) { $ids += $tim.id; Write-Host "Đã có sẵn: $($sv.MaSinhVien) ($loi)" -ForegroundColor Yellow }
        else      { Write-Host "Lỗi ($($sv.MaSinhVien)): $loi" -ForegroundColor Red }
    }
}

# 3. Đăng ký tất cả vào lớp học phần
if ($ids.Count -gt 0) {
    try {
        $r = Goi-Api "POST" "/api/lop-hoc-phan/$LopId/dang-ky" @{ sinhVienIds = @($ids) } $token
        Write-Host "Đăng ký vào lớp $LopId : thêm $($r.daThem), bỏ qua $($r.boQua)" -ForegroundColor Cyan
    } catch {
        Write-Host "Không đăng ký được vào lớp $LopId : $(Lay-ThongBaoLoi $_)" -ForegroundColor Red
    }
}
Write-Host "Xong. Mật khẩu mặc định của các sinh viên mới: $MatKhauMacDinh" -ForegroundColor Cyan