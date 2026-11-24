using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using QuanLyHocVu.Api.Models;

namespace QuanLyHocVu.Api.Services;

public class TokenService(IConfiguration config)
{
    public (string Token, DateTime HetHan) TaoToken(NguoiDung nguoiDung)
    {
        var jwt = config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var hetHan = DateTime.UtcNow.AddMinutes(int.Parse(jwt["ExpireMinutes"]!));

        var claims = new[]
        {
            new Claim("sub", nguoiDung.Id.ToString()),
            new Claim("name", nguoiDung.TenDangNhap),
            new Claim("hoTen", nguoiDung.HoTen),
            new Claim("role", nguoiDung.VaiTro)
        };

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: hetHan,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), hetHan);
    }
}