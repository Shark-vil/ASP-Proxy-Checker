namespace ProxyChecker.Database.Models
{
    /// <summary>
    /// Модель пользователя
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
