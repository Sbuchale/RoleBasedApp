namespace RoleBasedApp.Models
{
    public class Menu
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Route { get; set; } = "";
        public int? ParentMenuId { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
