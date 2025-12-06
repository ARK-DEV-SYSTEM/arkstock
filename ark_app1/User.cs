namespace ark_app1
{
   public class User
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string CI { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Telefono { get; set; }
    }
}