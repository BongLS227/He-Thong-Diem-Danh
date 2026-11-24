using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuanLyHocVu.Api.Data;
using QuanLyHocVu.Api.Models;
using QuanLyHocVu.Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Kết nối SQL Server
builder.Services.AddDbContext<QuanLyHocVuContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("QuanLyHocVu")));

// 2. Dịch vụ tạo token đăng nhập
builder.Services.AddScoped<TokenService>();

// 3. Controllers + tài liệu API
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 4. Xác thực JWT
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            // Tên claim dùng trong token (xem TokenService)
            RoleClaimType = "role",
            NameClaimType = "name"
        };
    });

builder.Services.AddAuthorization();

// 5. Cho phép giao diện web (HTML/JS) gọi API khi đang phát triển
builder.Services.AddCors(opt =>
    opt.AddPolicy("Dev", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Tạo tài khoản admin mặc định nếu chưa có
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuanLyHocVuContext>();
    try
    {
        if (!db.NguoiDung.Any(u => u.VaiTro == "Admin"))
        {
            db.NguoiDung.Add(new NguoiDung
            {
                TenDangNhap = "admin",
                MatKhauHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                HoTen = "Quản trị viên",
                VaiTro = "Admin",
                HoatDong = true
            });
            db.SaveChanges();
            app.Logger.LogInformation("Đã tạo tài khoản admin mặc định: admin / Admin@123");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Không thể tạo tài khoản admin mặc định. Kiểm tra lại chuỗi kết nối.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();   // giao diện thử API tại /scalar
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("Dev");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();