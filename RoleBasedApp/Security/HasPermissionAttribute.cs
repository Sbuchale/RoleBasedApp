using Microsoft.AspNetCore.Authorization;

namespace RoleBasedApp.Security
{
    public class HasPermissionAttribute: AuthorizeAttribute
    {
        public HasPermissionAttribute(string permission)
        {
            Policy = $"Permission:{permission}";
        }
    }
}
