using System.Data;
using Npgsql;
using PlannerApi.Dtos.Users;
using PlannerApi.Models;

namespace PlannerApi.Repositories;

public class AuthRepository(IConfiguration configuration) : IAuthRepository
{
    private readonly string _connectionString = configuration.GetConnectionString("PlannerDb")
        ?? throw new InvalidOperationException("Connection string 'PlannerDb' not found.");

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<IReadOnlyList<UserListItem>> GetUsersAsync()
    {
        const string sql = """
            select user_id, full_name, email, user_role_code, vendor_id, is_active, is_first_login, is_locked, created_on
            from app_user
            order by user_id desc;
            """;
        var items = new List<UserListItem>();
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new UserListItem
            {
                UserId = reader.GetInt32(0),
                FullName = reader.GetString(1),
                Email = reader.GetString(2),
                RoleCode = reader.GetString(3),
                VendorId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                IsActive = reader.GetBoolean(5),
                IsFirstLogin = reader.GetBoolean(6),
                IsLocked = reader.GetBoolean(7),
                CreatedOn = reader.GetDateTime(8)
            });
        }
        return items;
    }

    public async Task<IReadOnlyList<RoleItem>> GetRolesAsync()
    {
        const string sql = "select user_role_code, user_role_name from user_role_master order by user_role_name;";
        var items = new List<RoleItem>();
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new RoleItem { RoleCode = reader.GetString(0), RoleName = reader.GetString(1) });
        }
        return items;
    }

    public async Task<AppUser?> GetUserByEmailAsync(string emailOrUserName)
    {
        const string sql = """
            select user_id, full_name, email, password_hash, user_role_code, vendor_id, is_active, is_first_login, is_locked, created_on, updated_on
            from app_user
            where lower(email) = lower(@login)
               or lower(user_name) = lower(@login)
            limit 1;
            """;
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("login", emailOrUserName);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        return await reader.ReadAsync() ? MapUser(reader) : null;
    }

    public async Task<AppUser?> GetUserByIdAsync(int userId)
    {
        const string sql = """
            select user_id, full_name, email, password_hash, user_role_code, vendor_id, is_active, is_first_login, is_locked, created_on, updated_on
            from app_user
            where user_id = @user_id
            limit 1;
            """;
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user_id", userId);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        return await reader.ReadAsync() ? MapUser(reader) : null;
    }

    public async Task<int> CreateUserAsync(string fullName, string email, string roleCode, int? vendorId, bool isActive)
    {
        const string sql = """
            insert into app_user (user_name, full_name, email, user_role_code, vendor_id, is_active, is_first_login, is_locked)
            values (@user_name, @full_name, @email, @user_role_code, @vendor_id, @is_active, true, false)
            returning user_id;
            """;
        var userName = email.Contains('@') ? email.Split('@')[0] : email;
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user_name", userName);
        cmd.Parameters.AddWithValue("full_name", fullName);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("user_role_code", roleCode);
        cmd.Parameters.AddWithValue("vendor_id", (object?)vendorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("is_active", isActive);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> CreateActivationAsync(int userId, Guid activationToken, string otpCode, DateTime otpExpiry)
    {
        const string sql = """
            insert into user_activation (user_id, activation_token, otp_code, otp_expiry, is_used)
            values (@user_id, @activation_token, @otp_code, @otp_expiry, false)
            returning activation_id;
            """;
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("activation_token", activationToken);
        cmd.Parameters.AddWithValue("otp_code", otpCode);
        cmd.Parameters.AddWithValue("otp_expiry", otpExpiry);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<UserActivation?> GetValidActivationAsync(string emailOrUserName, Guid token, string otpCode)
    {
        const string sql = """
            select ua.activation_id, ua.user_id, ua.activation_token, ua.otp_code, ua.otp_expiry, ua.is_used, ua.created_on
            from user_activation ua
            inner join app_user u on u.user_id = ua.user_id
            where (lower(u.email) = lower(@login) or lower(u.user_name) = lower(@login))
              and ua.activation_token = @activation_token
              and ua.otp_code = @otp_code
              and ua.is_used = false
              and ua.otp_expiry >= now()
            limit 1;
            """;
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("login", emailOrUserName);
        cmd.Parameters.AddWithValue("activation_token", token);
        cmd.Parameters.AddWithValue("otp_code", otpCode);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (!await reader.ReadAsync()) return null;
        return new UserActivation
        {
            ActivationId = reader.GetInt32(0),
            UserId = reader.GetInt32(1),
            ActivationToken = reader.GetGuid(2),
            OtpCode = reader.GetString(3),
            OtpExpiry = reader.GetDateTime(4),
            IsUsed = reader.GetBoolean(5),
            CreatedOn = reader.GetDateTime(6)
        };
    }

    public async Task SetInitialPasswordAsync(int userId, string passwordHash)
    {
        const string sql1 = """
            update app_user
            set password_hash = @password_hash,
                is_first_login = false,
                updated_on = now()
            where user_id = @user_id;
            """;
        const string sql2 = """
            update user_activation
            set is_used = true
            where user_id = @user_id and is_used = false;
            """;
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using (var cmd = new NpgsqlCommand(sql1, conn, tx))
        {
            cmd.Parameters.AddWithValue("password_hash", passwordHash);
            cmd.Parameters.AddWithValue("user_id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        await using (var cmd = new NpgsqlCommand(sql2, conn, tx))
        {
            cmd.Parameters.AddWithValue("user_id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async Task AddPasswordHistoryAsync(int userId, string passwordHash)
    {
        const string sql = """
            insert into password_history (user_id, password_hash)
            values (@user_id, @password_hash);
            """;
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("password_hash", passwordHash);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task WriteAuditAsync(int? userId, string actionType, string actionDetail, string? ipAddress)
    {
        const string sql = """
            insert into auth_audit_log (user_id, action_type, action_detail, ip_address)
            values (@user_id, @action_type, @action_detail, @ip_address);
            """;
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user_id", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("action_type", actionType);
        cmd.Parameters.AddWithValue("action_detail", actionDetail);
        cmd.Parameters.AddWithValue("ip_address", (object?)ipAddress ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static AppUser MapUser(IDataRecord reader)
    {
        return new AppUser
        {
            UserId = reader.GetInt32(0),
            FullName = reader.GetString(1),
            Email = reader.GetString(2),
            PasswordHash = reader.IsDBNull(3) ? null : reader.GetString(3),
            RoleCode = reader.GetString(4),
            VendorId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            IsActive = reader.GetBoolean(6),
            IsFirstLogin = reader.GetBoolean(7),
            IsLocked = reader.GetBoolean(8),
            CreatedOn = reader.GetDateTime(9),
            UpdatedOn = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
        };
    }
}
