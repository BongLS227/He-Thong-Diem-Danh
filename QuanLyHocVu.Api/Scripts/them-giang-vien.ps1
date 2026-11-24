# =====================================================
# THÊM NHIỀU GIẢNG VIÊN TỪ FILE CSV
#
# Điều kiện: server đang chạy (dotnet run) ở http://localhost:5231
# Cách chạy (trong thư mục chứa file này):
#   powershell -ExecutionPolicy Bypass -File .\them-giang-vien.ps1 -Csv .\giangvien-mau.csv
# File CSV có các cột: MaGiangVien, HoTen, Email, MaKhoa, SoDienThoai
#   - Tên đăng nhập của giảng viên chính là MaGiangVien.
#   - MaKhoa là mã khoa đã có trong hệ thống (ví dụ CNTT), có thể để trống.
# Giảng viên đã tồn tại thì được bỏ qua, không bị tạo trùng.
# =====================================================
param(
    [string]$Csv = ".\giangvien-mau.csv",
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

# Đăng nhập bằng tài khoản admin
$mk = Read-Host "Mật khẩu của tài khoản admin" -AsSecureString
$mkThuong = [System.Net.NetworkCredential]::new("", $mk).Password
try {
    $dn = Goi-Api "POST" "/api/auth/dang-nhap" @{ tenDangNhap = "admin"; matKhau = $mkThuong } $null
} catch {
    Write-Host "Đăng nhập thất bại: $(Lay-ThongBaoLoi $_)" -ForegroundColor Red; exit 1
}
$token = $dn.token

# Bảng tra mã khoa -> Id khoa (để trong CSV chỉ cần ghi mã khoa, ví dụ CNTT)
$mapKhoa = @{}
foreach ($k in @(Goi-Api "GET" "/api/khoa" $null $token)) { $mapKhoa[$k.maKhoa] = $k.id }
function Tim-KhoaId($maKhoa) {
    if ([string]::IsNullOrWhiteSpace($maKhoa)) { return $null }
    if ($mapKhoa.ContainsKey($maKhoa.Trim())) { return $mapKhoa[$maKhoa.Trim()] }
    return -1   # có ghi mã khoa nhưng không tồn tại
}

$taoMoi = 0; $daCo = 0; $loi = 0
foreach ($g in $ds) {
    $khoaId = Tim-KhoaId $g.MaKhoa
    if ($khoaId -eq -1) {
        Write-Host "Lỗi ($($g.MaGiangVien)): mã khoa '$($g.MaKhoa)' chưa có trong hệ thống" -ForegroundColor Red; $loi++; continue
    }
    $body = @{
        maGiangVien = $g.MaGiangVien
        matKhau     = $MatKhauMacDinh
        hoTen       = $g.HoTen
        email       = $(if ($g.Email) { $g.Email } else { $null })
        khoaId      = $khoaId
        soDienThoai = $(if ($g.SoDienThoai) { $g.SoDienThoai } else { $null })
    }
    try {
        $null = Goi-Api "POST" "/api/giang-vien" $body $token
        Write-Host "Đã tạo: $($g.MaGiangVien) - $($g.HoTen)" -ForegroundColor Green; $taoMoi++
    } catch {
        $tb = Lay-ThongBaoLoi $_
        $tim = Goi-Api "GET" "/api/giang-vien?tuKhoa=$($g.MaGiangVien)" $null $token |
               Where-Object { $_.maGiangVien -eq $g.MaGiangVien } | Select-Object -First 1
        if ($tim) { Write-Host "Đã có sẵn: $($g.MaGiangVien) ($tb)" -ForegroundColor Yellow; $daCo++ }
        else      { Write-Host "Lỗi ($($g.MaGiangVien)): $tb" -ForegroundColor Red; $loi++ }
    }
}
Write-Host "Xong: tạo mới $taoMoi, đã có sẵn $daCo, lỗi $loi. Mật khẩu mặc định của giảng viên mới: $MatKhauMacDinh" -ForegroundColor Cyan