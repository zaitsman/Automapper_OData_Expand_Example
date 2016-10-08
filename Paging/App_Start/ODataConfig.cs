using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Paging.App_Start
{
  using System.Web.Http;
  using System.Web.Http.OData.Builder;

  using Paging.Controllers;

  public class ODataConfig
  {
    public static void Register(HttpConfiguration config)
    {
      config.MapHttpAttributeRoutes();

      //
      //ODataModelBuilder builder = new ODataConventionModelBuilder();
      //
      //builder.EntitySet<External>("Externals");
      //config.Routes.MapODataRoute(
      //    routeName: "stuff",
      //    routePrefix: "stuff",
      //    model: builder.GetEdmModel());
    }
  }
}