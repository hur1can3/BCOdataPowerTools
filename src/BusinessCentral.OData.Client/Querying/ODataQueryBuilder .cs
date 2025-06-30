using System.Linq.Expressions;
using System.Net;
using System.Text;

using BusinessCentral.OData.Client.Querying.Visitors;

namespace BusinessCentral.OData.Client.Querying;

/// <summary>
/// An abstract base class for the ODataQueryBuilder to support non-generic collections,
/// primarily for use in batch operations.
/// </summary>
public abstract class ODataQueryBuilder
{
    /// <summary>
    /// Gets the entity type name, typically used for constructing the URL.
    /// </summary>
    public abstract string GetEntityTypeName();

    /// <summary>
    /// Converts the builder's state into a URL-encoded OData query string.
    /// </summary>
    public abstract string ToQueryString();
}

/// <summary>
/// A fluent builder for creating strongly-typed OData query strings. This class is designed to be extensible;
/// developers can inherit from it to add custom query logic or modify existing behavior.
/// </summary>
/// <typeparam name="T">The entity type to query.</typeparam>
public class ODataQueryBuilder<T> : ODataQueryBuilder
{
    /// <summary>
    /// Holds the standard OData query parameters like $filter, $top, etc.
    /// Accessible by derived classes to allow for custom query option modifications.
    /// </summary>
    protected readonly Dictionary<string, string> _queryParams = new();

    /// <summary>
    /// Holds the clauses for the $expand query option.
    /// Accessible by derived classes to allow for custom expand logic.
    /// </summary>
    protected readonly List<string> _expandClauses = new();

    /// <summary>
    /// Specifies the properties to return using the $select query option.
    /// This method is virtual and can be overridden in a derived class.
    /// </summary>
    /// <typeparam name="TResult">The type of the result projected by the selector.</typeparam>
    /// <param name="selector">An expression that selects the properties to include.</param>
    /// <example>
    /// builder.Select(c => new { c.No, c.DisplayName })
    /// </example>
    public virtual ODataQueryBuilder<T> Select<TResult>(Expression<Func<T, TResult>> selector)
    {
        if (selector.Body is NewExpression newExpression)
        {
            var propertyNames = newExpression.Members?.Select(m => m.Name) ?? Enumerable.Empty<string>();
            _queryParams["$select"] = string.Join(",", propertyNames);
        }
        else
        {
            throw new ArgumentException("Selector must be a 'new' expression (e.g., x => new { x.Prop1, x.Prop2 }).", nameof(selector));
        }

        return this;
    }

    /// <summary>
    /// Filters a collection of resources using the $filter query option.
    /// This method is virtual and can be overridden in a derived class.
    /// </summary>
    /// <param name="predicate">An expression to test each entity.</param>
    public virtual ODataQueryBuilder<T> Filter(Expression<Func<T, bool>> predicate)
    {
        var visitor = new ODataFilterExpressionVisitor();
        _queryParams["$filter"] = visitor.ToODataFilter(predicate);
        return this;
    }

    /// <summary>
    /// Includes a related resource in line with the retrieved resources using the $expand query option.
    /// This method is virtual and can be overridden in a derived class.
    /// </summary>
    /// <param name="navigationProperty">An expression specifying the navigation property to expand.</param>
    /// <param name="configure">An optional action to configure nested query options for the expanded entity.</param>
    public virtual ODataQueryBuilder<T> Expand<TProperty>(Expression<Func<T, TProperty>> navigationProperty, Action<ODataQueryBuilder<TProperty>>? configure = null) where TProperty : class
    {
        if (navigationProperty.Body is not MemberExpression memberExpression)
        {
            throw new ArgumentException("Expand selector must be a member property expression.", nameof(navigationProperty));
        }

        var propertyName = memberExpression.Member.Name;

        if (configure == null)
        {
            _expandClauses.Add(propertyName);
        }
        else
        {
            var nestedBuilder = new ODataQueryBuilder<TProperty>();
            configure(nestedBuilder);
            var subQuery = nestedBuilder.ToQueryString().Replace("&", ";");
            _expandClauses.Add($"{propertyName}({subQuery})");
        }

        return this;
    }

    /// <summary>
    /// Includes a related resource collection and applies nested query options (like $select or $filter) to it.
    /// This method is virtual and can be overridden in a derived class.
    /// </summary>
    /// <param name="navigationProperty">An expression specifying the collection navigation property to expand.</param>
    /// <param name="configure">An action to configure the nested query options for the expanded collection.</param>
    public virtual ODataQueryBuilder<T> Expand<TSubEntity>(Expression<Func<T, IEnumerable<TSubEntity>>> navigationProperty, Action<ODataQueryBuilder<TSubEntity>> configure)
    {
        if (navigationProperty.Body is not MemberExpression memberExpression)
        {
            throw new ArgumentException("Expand selector must be a member property expression.", nameof(navigationProperty));
        }

        var propertyName = memberExpression.Member.Name;

        // Create a nested builder to configure the sub-query
        var nestedBuilder = new ODataQueryBuilder<TSubEntity>();
        configure(nestedBuilder);

        // Convert the nested builder's options to a query string (without the leading '&')
        // and replace '&' with ';' for nested OData options.
        var subQuery = nestedBuilder.ToQueryString().Replace("&", ";");

        _expandClauses.Add($"{propertyName}({subQuery})");

        return this;
    }

    /// <summary>
    /// Applies data aggregation transformations using the $apply query option.
    /// This is useful for grouping and summarizing data.
    /// </summary>
    /// <param name="configure">An action to configure the aggregation and grouping logic.</param>
    public virtual ODataQueryBuilder<T> Apply(Action<AggregationBuilder<T>> configure)
    {
        var builder = new AggregationBuilder<T>();
        configure(builder);
        _queryParams["$apply"] = builder.Build();
        return this;
    }

    /// <summary>
    /// Specifies the primary sort order for the results using the $orderby query option.
    /// This will overwrite any existing OrderBy clauses.
    /// This method is virtual and can be overridden in a derived class.
    /// </summary>
    public virtual ODataQueryBuilder<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        if (keySelector.Body is MemberExpression memberExpression)
        {
            _queryParams["$orderby"] = memberExpression.Member.Name;
        }
        else
        {
            throw new ArgumentException("Key selector must be a member property expression.", nameof(keySelector));
        }

        return this;
    }

    /// <summary>
    /// Specifies the primary sort order (descending) for the results using the $orderby query option.
    /// This will overwrite any existing OrderBy clauses.
    /// This method is virtual and can be overridden in a derived class.
    /// </summary>
    public virtual ODataQueryBuilder<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        if (keySelector.Body is MemberExpression memberExpression)
        {
            _queryParams["$orderby"] = $"{memberExpression.Member.Name} desc";
        }
        else
        {
            throw new ArgumentException("Key selector must be a member property expression.", nameof(keySelector));
        }

        return this;
    }

    /// <summary>
    /// Specifies a subsequent sort order for the results. Must be used after OrderBy or OrderByDescending.
    /// This method is virtual and can be overridden in a derived class.
    /// </summary>
    public virtual ODataQueryBuilder<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        if (!_queryParams.ContainsKey("$orderby"))
        {
            throw new InvalidOperationException("ThenBy can only be used after an OrderBy or OrderByDescending clause.");
        }

        if (keySelector.Body is MemberExpression memberExpression)
        {
            _queryParams["$orderby"] += $",{memberExpression.Member.Name}";
        }
        else
        {
            throw new ArgumentException("Key selector must be a member property expression.", nameof(keySelector));
        }

        return this;
    }

    /// <summary>
    /// Specifies a subsequent sort order (descending) for the results. Must be used after OrderBy or OrderByDescending.
    /// This method is virtual and can be overridden in a derived class.
    /// </summary>
    public virtual ODataQueryBuilder<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        if (!_queryParams.ContainsKey("$orderby"))
        {
            throw new InvalidOperationException("ThenByDescending can only be used after an OrderBy or OrderByDescending clause.");
        }

        if (keySelector.Body is MemberExpression memberExpression)
        {
            _queryParams["$orderby"] += $",{memberExpression.Member.Name} desc";
        }
        else
        {
            throw new ArgumentException("Key selector must be a member property expression.", nameof(keySelector));
        }

        return this;
    }

    /// <summary>
    /// Limits the number of items returned using the $top query option.
    /// This method is virtual and can be overridden in a derived class.
    /// Set to 0 or less to remove the option.
    /// </summary>
    public virtual ODataQueryBuilder<T> Top(int count)
    {
        if (count > 0)
        {
            _queryParams["$top"] = count.ToString();
        }
        else
        {
            _queryParams.Remove("$top");
        }

        return this;
    }

    /// <summary>
    /// Skips a number of items in the collection using the $skip query option.
    /// This method is virtual and can be overridden in a derived class.
    /// Set to 0 or less to remove the option.
    /// </summary>
    public virtual ODataQueryBuilder<T> Skip(int count)
    {
        if (count > 0)
        {
            _queryParams["$skip"] = count.ToString();
        }
        else
        {
            _queryParams.Remove("$skip");
        }

        return this;
    }

    /// <summary>
    /// Requests a total count of matching items to be included in the response.
    /// This method is virtual and can be overridden in a derived class.
    /// </summary>
    /// <param name="value">Set to true to include the count, false to remove it.</param>
    public virtual ODataQueryBuilder<T> Count(bool value = true)
    {
        if (value)
        {
            _queryParams["$count"] = "true";
        }
        else
        {
            _queryParams.Remove("$count");
        }

        return this;
    }

    /// <inheritdoc/>
    public override string GetEntityTypeName()
    {
        return $"{typeof(T).Name.ToLowerInvariant()}s";
    }

    /// <inheritdoc/>
    public override string ToQueryString()
    {
        var allParams = new Dictionary<string, string>(_queryParams);
        if (_expandClauses.Any())
        {
            allParams["$expand"] = string.Join(",", _expandClauses);
        }

        // URL-encode the value part of each query parameter to handle special characters correctly.
        return string.Join("&", allParams.Select(kvp => $"{kvp.Key}={WebUtility.UrlEncode(kvp.Value)}"));
    }
}

/// <summary>
/// A helper class for fluently building OData $apply clauses for aggregation.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class AggregationBuilder<T>
{
    private readonly List<string> _groupByProperties = new();
    private readonly List<string> _aggregations = new();

    /// <summary>
    /// Groups the results by one or more properties.
    /// </summary>
    /// <param name="selectors">Expressions selecting the properties to group by.</param>
    public AggregationBuilder<T> GroupBy(params Expression<Func<T, object>>[] selectors)
    {
        foreach (var selector in selectors)
        {
            if (selector.Body is MemberExpression memberExpression)
            {
                _groupByProperties.Add(memberExpression.Member.Name);
            }

            // Handle cases where the property is boxed in a Convert expression
            else if (selector.Body is UnaryExpression { NodeType: ExpressionType.Convert, Operand: MemberExpression innerMemberExpression })
            {
                _groupByProperties.Add(innerMemberExpression.Member.Name);
            }
            else
            {
                throw new ArgumentException("GroupBy selectors must be member property expressions.", nameof(selectors));
            }
        }

        return this;
    }

    /// <summary>
    /// Applies an aggregation function (e.g., sum, average) to a property.
    /// </summary>
    /// <param name="selector">An expression selecting the property to aggregate.</param>
    /// <param name="with">The aggregation method (e.g., "sum", "average").</param>
    /// <param name="as">The alias for the aggregated result.</param>
    public AggregationBuilder<T> Aggregate<TProperty>(Expression<Func<T, TProperty>> selector, string with, string @as)
    {
        if (selector.Body is MemberExpression memberExpression)
        {
            _aggregations.Add($"{memberExpression.Member.Name} with {with} as {@as}");
        }
        else
        {
            throw new ArgumentException("Aggregate selector must be a member property expression.", nameof(selector));
        }

        return this;
    }

    internal string Build()
    {
        var parts = new List<string>();
        if (_groupByProperties.Any())
        {
            var aggregationClause = _aggregations.Any() ? $",aggregate({string.Join(",", _aggregations)})" : "";
            parts.Add($"groupby(({string.Join(",", _groupByProperties)}){aggregationClause})");
        }
        else if (_aggregations.Any())
        {
            parts.Add($"aggregate({string.Join(",", _aggregations)})");
        }

        return string.Join("/", parts);
    }
}
