using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Paging
{
  using System.Diagnostics.Contracts;
  using System.Web.Http.OData.Query;

  using AutoMapper.QueryableExtensions;

  public static class E
  {
    static IQueryable<T> ToWithOptionalExpand<T>(this IProjectionExpression expression,
            IDictionary<string, object> parameters, params string[] optionalExpand)
    {
      Contract.Requires<ArgumentNullException>(expression != null);
      return optionalExpand == null ? expression.To<T>() : expression.To<T>(parameters, optionalExpand);
    }

    public static IQueryable<T> ToFromQueryOptions<T>(this IProjectionExpression expression,
        IDictionary<string, object> parameters, ODataQueryOptions<T> queryOptions)
    {
      Contract.Requires<ArgumentNullException>(expression != null);
      Contract.Requires<ArgumentNullException>(queryOptions != null);
      return queryOptions.SelectExpand == null
          ? expression.To<T>()
          : expression.ToWithOptionalExpand<T>(parameters, queryOptions.SelectExpand.RawExpand.Split(','));
    }

    public static IQueryable<T> ToFromQueryOptions<T>(this IProjectionExpression expression,
        ODataQueryOptions<T> queryOptions)
    {
      Contract.Requires<ArgumentNullException>(expression != null);
      Contract.Requires<ArgumentNullException>(queryOptions != null);
      return expression.ToFromQueryOptions(null, queryOptions);
    }
  }
}