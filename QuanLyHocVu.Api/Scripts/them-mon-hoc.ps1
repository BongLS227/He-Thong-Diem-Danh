# =====================================================
# THÊM NHIỀU MÔN HỌC TỪ FILE CSV
#
# Điều kiện: server đang chạy (dotnet run) ở http://localhost:5231
# Cách chạy (trong thư mục chứa file này):
#   powershell -ExecutionPolicy Bypass -File .\them-mon-hoc.ps1 -Csv .\monhoc-mau.csv
# File CSV có các cột: MaMon, TenMon, SoTinChi, MaKhoa
#   - SoTinChi là số từ 1 đến 10.
#   - MaKhoa là mã khoa đã có trong hệ thống (ví dụ CNTT), để trống nếu là môn đại cương.
# Môn đã tồn tại (trùng MaMon) thì được bỏ qua, không bị tạo trùng.
# =====================================================
param(
    [string]$Csv = ".\monhoc-mau.csv",
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
foreach ($m in $ds) {
    $khoaId = Tim-KhoaId $m.MaKhoa
    if ($khoaId -eq -1) {
        Write-Host "Lỗi ($($m.MaMon)): mã khoa '$($m.MaKhoa)' chưa có trong hệ thống" -ForegroundColor Red; $loi++; continue
    }
    $tc = 0
    if (-not [int]::TryParse($m.SoTinChi, [ref]$tc)) {
        Write-Host "Lỗi ($($m.MaMon)): số tín chỉ '$($m.SoTinChi)' không hợp lệ" -ForegroundColor Red; $loi++; continue
    }
    $body = @{ maMon = $m.MaMon; tenMon = $m.TenMon; soTinChi = $tc; khoaId = $khoaId }
    try {
        $null = Goi-Api "POST" "/api/mon-hoc" $body $token
        Write-Host "Đã tạo: $($m.MaMon) - $($m.TenMon) ($tc tín chỉ)" -ForegroundColor Green; $taoMoi++
    } catch {
        $tb = Lay-ThongBaoLoi $_
        $tim = Goi-Api "GET" "/api/mon-hoc?tuKhoa=$($m.MaMon)" $null $token |
               Where-Object { $_.maMon -eq $m.MaMon } | Select-Object -First 1
        if ($tim) { Write-Host "Đã có sẵn: $($m.MaMon) ($tb)" -ForegroundColor Yellow; $daCo++ }
        else      { Write-Host "Lỗi ($($m.MaMon)): $tb" -ForegroundColor Red; $loi++ }
    }
}
Write-Host "Xong: tạo mới $taoMoi, đã có sẵn $daCo, lỗi $loi." -ForegroundColor Cyan