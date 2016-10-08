namespace Paging.Controllers
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Linq.Expressions;
  using System.Net.Http;
  using System.Web.Http;
  using System.Web.Http.Filters;
  using System.Web.Http.OData;
  using System.Web.Http.OData.Extensions;
  using System.Web.Http.OData.Properties;
  using System.Web.Http.OData.Query;
  using System.Web.Http.Results;

  using AutoMapper;
  using AutoMapper.QueryableExtensions;
  using AutoMapper.QueryableExtensions.Impl;

  using Microsoft.Data.Edm;

  using Newtonsoft.Json;

  [RoutePrefix("api/stuff")]
  public class StuffController : ApiController
  {
    [Route]
    [ExpandableQuery(PageSize = 1, AllowedQueryOptions = AllowedQueryOptions.All, DTOType = typeof(External))]
    [HttpGet]
    public dynamic Get()
    {
      // return this.NotFound();
      Mapper.Initialize(cfg =>
      {
        cfg.CreateMap<InternalP, ExternalP>();
        cfg.CreateMap<Internal, External>().ForMember(x => x.Stuff, z => z.MapFrom(y => y.StuffI))
        .ForMember(x => x.Ps,
          z =>
          {
            z.MapFrom(y => y.Ps);
            z.ExplicitExpansion();
          })
          .ForMember(x => x.P2s,
          z =>
          {
            z.MapFrom(y => y.P2s);
            z.ExplicitExpansion();
          });

      });

      return
        new List<Internal>
          {
            new Internal
              {
                StuffI = "a2",
                Id = 2,
                Ps = new List<InternalP> { new InternalP { Shoes = "a2" } },
                P2s = new List<InternalP> { new InternalP { Shoes = "42" } }
              },
            new Internal
              {
                StuffI = "a",
                Id = 1,
                Ps = new List<InternalP> { new InternalP { Shoes = "a1" } },
                P2s = new List<InternalP>()
              }
          }.AsQueryable();
      // ProjectTo<External>(new [] {"Ps"});
    }
  }

  public class Internal
  {
    public string StuffI { get; set; }
    public int Id { get; set; }
    public List<InternalP> Ps { get; set; }
    public List<InternalP> P2s { get; set; }
  }

  public class InternalP
  {
    public string Shoes { get; set; }
  }

  public class ExternalP
  {
    public string Shoes { get; set; }
  }

  public class External
  {
    public string Stuff { get; set; }

    public int Id { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<ExternalP> Ps { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<ExternalP> P2s { get; set; }
  }

  public class ExpandableQuery : EnableQueryAttribute
  {
    public Type DTOType { get; set; }

    public IConfiguration MapperConfiguration;

    public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
    {
      var responseContent = actionExecutedContext.Response.Content;
      var objectContent = (responseContent as System.Net.Http.ObjectContent);
      var resultBeforeOdata = objectContent?.Value as IQueryable;

      // If we find that the action returned an IQueryable, we need to apply the ProjectTo<> Automapper method to perform mapping for us.
      if (resultBeforeOdata != null)
      {
        // The ApplyTo<> Automapper Projection is only applicable to queryable collections.
        var projectToMethod =
          typeof(AutoMapper.QueryableExtensions.Extensions).GetMethods()
          .Where(
            x =>
            x.Name == nameof(Extensions.ProjectTo) && x.GetParameters().Count() == 3
            && x.GetParameters()[1].ParameterType == typeof(AutoMapper.IConfigurationProvider))
          .ToList()[0].MakeGenericMethod(this.DTOType); // Unfortunatuely, Automapper does nto provide a ProjectTo() method with Type suppliable as paramter, we have to use reflection for this.

        // the below code is taken from the base class
        var model = this.GetModel(this.DTOType, actionExecutedContext.Request, actionExecutedContext.ActionContext.ActionDescriptor);

        // We need to construct query Options so we can get our hands on $expand raw value and not parse strings.
        var queryOptions = new ODataQueryOptions(new ODataQueryContext(model, this.DTOType), actionExecutedContext.Request);

        // This will hold Expression<Func<DTOType, object>> that we need to supply to Automapper to indicate that we want these expanded.
        var expansions = new List<object>();
        var expand = queryOptions.SelectExpand?.RawExpand;
        var funcType = typeof(Func<,>).MakeGenericType(this.DTOType, typeof(object));
        if (!string.IsNullOrEmpty(expand))
        {
          foreach (var exp in expand.Split(','))
          {
            var parameterExpression = Expression.Parameter(this.DTOType, "x");
            var property = Expression.Property(parameterExpression, exp);

            var lambda = Expression.Lambda(
              funcType,
              property,
              parameterExpression);

            expansions.Add(lambda);
          }
        }

        // This will give us a IEnumerable<Expression<Func<DTOType, object>>. What we need, though, is Expression<Func<DTOType, object>>[].
        var enumerableExpressionFuncs =
          typeof(Enumerable)
            .GetMethod("Cast")
            .MakeGenericMethod(typeof(Expression<>).MakeGenericType(funcType))
            .Invoke(null, new object[] { expansions });

        var expressionFuncsArray = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(typeof(Expression<>).MakeGenericType(funcType)).Invoke(null, new object[] { enumerableExpressionFuncs });

        var result = projectToMethod.Invoke(null, new object[] { resultBeforeOdata, Mapper.Configuration, expressionFuncsArray });
        objectContent.Value = result;
      }

      // Now that we modified the content returned by controller, let's call base. At this point IQueryable is not executed yet.
      // The base call will take care of $select, $skip, $top and $orderby.
      base.OnActionExecuted(actionExecutedContext);

      // Now, we want to rewrap the result providing the next page link (if required) and inlineCount (if $inlinecount was requested).
      var properties = actionExecutedContext.Request.ODataProperties();

      var responseObject = actionExecutedContext.Response?.Content as System.Net.Http.ObjectContent;

      var typeOfResponseObject = responseObject?.Value?.GetType();

      // If response is not a generic result, e.g. IQueryable<DTOType> or SelectSome<DTOType> then there is no point to proceed.
      if (typeOfResponseObject == null || !typeOfResponseObject.IsGenericType)
      {
        return;
      }

      // We want to know what to call the collection, we use the Clr DTO type name for this.
      var pluralCollectionName = this.GetFirstInnerMostTypeName(typeOfResponseObject);

      // Now, we will re-wrap the result as Dictionary, adding total count and next page query, if present.
      var newResult = new Dictionary<string, object> { [pluralCollectionName] = responseObject.Value };

      if (properties?.TotalCount != null)
        newResult["inlineCount"] = properties.TotalCount;

      if (properties?.NextLink != null)
        newResult["nextPageQuery"] = properties.NextLink.Query;

      // As a last step, we will set the response content to our new dictionary.
      responseObject.Value = newResult;
    }

    public string GetFirstInnerMostTypeName(Type type)
    {
      if (!type.IsGenericType)
      {
        return type.Name.EndsWith("s") ? $"{type.Name}es" : $"{type.Name}s";
      }

      return this.GetFirstInnerMostTypeName(type.GenericTypeArguments[0]);
    }
  }
}
