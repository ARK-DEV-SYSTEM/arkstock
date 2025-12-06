using BCrypt.Net;
using Microsoft.Data.SqlClient;
using System;

namespace ark_app1
{
    public static class DatabaseManager
    {
        public static string? ConnectionString { get; set; }

        public static void UpdateUserPassword(int userId, string newPassword)
        {
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Usuarios SET PasswordHash = @PasswordHash WHERE Id = @Id";
                cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                cmd.Parameters.AddWithValue("@Id", userId);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
