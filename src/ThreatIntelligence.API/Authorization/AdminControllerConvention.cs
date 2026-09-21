using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace ThreatIntelligence.API.Authorization;

public sealed class AdminControllerConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        if (controller.Selectors.Any(selector =>
                selector.AttributeRouteModel?.Template?.StartsWith("api/admin", StringComparison.OrdinalIgnoreCase) == true))
        {
            controller.Filters.Add(new AuthorizeFilter("Admin"));
        }
    }
}
