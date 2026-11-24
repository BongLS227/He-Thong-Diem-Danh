# =====================================================
# THÊM NHIỀU LỚP HỌC PHẦN TỪ FILE CSV
#
# Lớp học phần = một MÔN HỌC + một HỌC KỲ + một GIẢNG VIÊN phụ trách.
# Thêm giảng viên hoặc môn học KHÔNG tự tạo lớp; giảng viên chỉ thấy các lớp được gán cho mình.
#
# Điều kiện: server đang chạy (dotnet run) ở http://localhost:5231, đã có môn học và giảng viên.
# Cách chạy (trong thư mục chứa file này):
#   powershell -ExecutionPolicy Bypass -File .\them-lop-hoc-phan.ps1 -Csv .\lophocphan-mau.csv
#   thêm -DangKyTatCa để đăng ký luôn tất cả sinh viên hiện có vào từng lớp (tiện khi demo)
# File CSV có các cột: MaLop, MaMon, HocKy, MaGiangVien, PhongHoc, Thu, TietBatDau, TietKetThuc
#   - HocKy ghi đúng tên học kỳ (ví dụ HK1 2026-2027); để trống thì dùng học kỳ hiện tại.
#   - Thu là 2 đến 8 (thứ hai đến chủ nhật). Các cột PhongHoc, Thu, Tiết có thể để trống.
# Lớp đã tồn tại (trùng MaLop) thì được bỏ qua, không bị tạo trùng.
# =====================================================
param(
    [string]$Csv = ".\lophocphan-mau.csv",
    [string]$BaseUrl = "http://localhost:5231",
    [switch]$DangKyTatCa
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


# Bảng tra mã -> Id
$mapMon = @{}; foreach ($x in @(Goi-Api "GET" "/api/mon-hoc" $null $token)) { $mapMon[$x.maMon] = $x.id }
$mapGv  = @{}; foreach ($x in @(Goi-Api "GET" "/api/giang-vien" $null $token)) { $mapGv[$x.maGiangVien] = $x.id }
$mapHk  = @{}; foreach ($x in @(Goi-Api "GET" "/api/hoc-ky" $null $token)) { $mapHk[$x.tenHocKy] = $x.id }
$mapLop = @{}; foreach ($x in @(Goi-Api "GET" "/api/lop-hoc-phan" $null $token)) { $mapLop[$x.maLop] = $x.id }
$hkHienTai = $null
try { $hkHienTai = (Goi-Api "GET" "/api/hoc-ky/hien-tai" $null $token).id } catch {}
$tatCaSv = @()
if ($DangKyTatCa) { $tatCaSv = @(Goi-Api "GET" "/api/sinh-vien" $null $token | ForEach-Object { $_.id }) }

function Tra($map, $khoa) {
    if ([string]::IsNullOrWhiteSpace($khoa)) { return $null }
    if ($map.ContainsKey($khoa.Trim())) { return $map[$khoa.Trim()] }
    return $null
}

$taoMoi = 0; $daCo = 0; $loi = 0
foreach ($r in $ds) {
    $lopId = Tra $mapLop $r.MaLop
    if ($lopId) {
        Write-Host "Đã có sẵn: $($r.MaLop)" -ForegroundColor Yellow; $daCo++
    } else {
        $monId = Tra $mapMon $r.MaMon
        $gvId  = Tra $mapGv $r.MaGiangVien
        $hkId  = if ([string]::IsNullOrWhiteSpace($r.HocKy)) { $hkHienTai } else { Tra $mapHk $r.HocKy }
        if (-not $monId) { Write-Host "Lỗi ($($r.MaLop)): môn '$($r.MaMon)' chưa có trong hệ thống" -ForegroundColor Red; $loi++; continue }
        if (-not $gvId)  { Write-Host "Lỗi ($($r.MaLop)): giảng viên '$($r.MaGiangVien)' chưa có trong hệ thống" -ForegroundColor Red; $loi++; continue }
        if (-not $hkId)  { Write-Host "Lỗi ($($r.MaLop)): không tìm thấy học kỳ (kiểm tra tên học kỳ hoặc đặt học kỳ hiện tại)" -ForegroundColor Red; $loi++; continue }

        $body = @{ maLop = $r.MaLop; monHocId = $monId; hocKyId = $hkId; giangVienId = $gvId }
        if ($r.PhongHoc)    { $body.phongHoc = $r.PhongHoc }
        if ($r.Thu)         { $body.thu = [int]$r.Thu }
        if ($r.TietBatDau)  { $body.tietBatDau = [int]$r.TietBatDau }
        if ($r.TietKetThuc) { $body.tietKetThuc = [int]$r.TietKetThuc }
        try {
            $kq = Goi-Api "POST" "/api/lop-hoc-phan" $body $token
            $lopId = $kq.id; $mapLop[$r.MaLop] = $lopId
            Write-Host "Đã tạo: $($r.MaLop) - $($kq.tenMon) - GV $($kq.tenGiangVien)" -ForegroundColor Green; $taoMoi++
        } catch {
            Write-Host "Lỗi ($($r.MaLop)): $(Lay-ThongBaoLoi $_)" -ForegroundColor Red; $loi++; continue
        }
    }
    if ($DangKyTatCa -and $tatCaSv.Count -gt 0) {
        try {
            $d = Goi-Api "POST" "/api/lop-hoc-phan/$lopId/dang-ky" @{ sinhVienIds = @($tatCaSv) } $token
            Write-Host "    Đăng ký sinh viên vào $($r.MaLop): thêm $($d.daThem), bỏ qua $($d.boQua)" -ForegroundColor Cyan
        } catch { Write-Host "    Không đăng ký được sinh viên vào $($r.MaLop): $(Lay-ThongBaoLoi $_)" -ForegroundColor Red }
    }
}
Write-Host "Xong: tạo mới $taoMoi, đã có sẵn $daCo, lỗi $loi." -ForegroundColor Cyan