using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace BusinessCentral.OData.Client.Querying.Visitors;

/// <summary>
/// An expression visitor that translates a .NET LINQ expression tree into an OData filter string.
/// This class is used internally by the ODataQueryBuilder.
/// </summary>
public class ODataFilterExpressionVisitor : ExpressionVisitor
{
    private static readonly ConcurrentDictionary<Expression, string> _cache = new();
    private StringBuilder _sb = new();

    /// <summary>
    /// Translates the given expression to an OData filter string.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="expression">The LINQ expression to translate.</param>
    /// <returns>An OData-compliant filter string.</returns>
    public string ToODataFilter<T>(Expression<Func<T, bool>> expression)
    {
        if (_cache.TryGetValue(expression, out var cachedFilter))
        {
            return cachedFilter;
        }

        _sb = new StringBuilder();
        Visit(expression.Body);
        var result = _sb.ToString();

        _cache.TryAdd(expression, result);
        return result;
    }

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Handle string functions: Contains, StartsWith, EndsWith
        if (node.Method.DeclaringType == typeof(string))
        {
            var functionName = node.Method.Name.ToLowerInvariant();
            if (functionName == "contains" || functionName == "startswith" || functionName == "endswith")
            {
                _sb.Append(functionName).Append('(');
                Visit(node.Object); // The member, e.g., DisplayName
                _sb.Append(',');
                Visit(node.Arguments[0]); // The value
                _sb.Append(')');
                return node;
            }
        }

        // Handle collection.Contains(item) -> OData 'item in collection'
        if (node.Method.Name == "Contains" && node.Object != null && typeof(IEnumerable).IsAssignableFrom(node.Object.Type))
        {
            var collection = GetValueFromExpression(node.Object);
            if (collection is IEnumerable enumerable)
            {
                Visit(node.Arguments[0]);
                _sb.Append(" in (");
                var values = new List<string>();
                foreach (var item in enumerable)
                {
                    values.Add(FormatValue(item));
                }

                _sb.Append(string.Join(",", values));
                _sb.Append(')');
                return node;
            }
        }

        throw new NotSupportedException($"The method '{node.Method.Name}' is not supported in an OData filter expression.");
    }

    /// <inheritdoc />
    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sb.Append('(');
        Visit(node.Left);

        switch (node.NodeType)
        {
            case ExpressionType.AndAlso: _sb.Append(" and "); break;
            case ExpressionType.OrElse: _sb.Append(" or "); break;
            case ExpressionType.Equal: _sb.Append(" eq "); break;
            case ExpressionType.NotEqual: _sb.Append(" ne "); break;
            case ExpressionType.LessThan: _sb.Append(" lt "); break;
            case ExpressionType.LessThanOrEqual: _sb.Append(" le "); break;
            case ExpressionType.GreaterThan: _sb.Append(" gt "); break;
            case ExpressionType.GreaterThanOrEqual: _sb.Append(" ge "); break;
            default: throw new NotSupportedException($"The binary operator '{node.NodeType}' is not supported.");
        }

        Visit(node.Right);
        _sb.Append(')');
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node)
    {
        // This checks if the expression is a parameter of the lambda (e.g., 'c' in c => c.Name)
        // or a property of a parameter. This identifies it as a field name.
        if (node.Expression?.NodeType == ExpressionType.Parameter || node.Expression?.NodeType == ExpressionType.MemberAccess)
        {
            var attr = node.Member.GetCustomAttribute<JsonPropertyNameAttribute>();
            _sb.Append(attr?.Name ?? node.Member.Name);
            return node;
        }

        // Otherwise, the member is likely a local variable or property that needs to be evaluated for its value.
        var value = GetValueFromExpression(node);
        _sb.Append(FormatValue(value));
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node)
    {
        _sb.Append(FormatValue(node.Value));
        return node;
    }

    private static object? GetValueFromExpression(Expression expression)
    {
        // This compiles and invokes the expression to get its value.
        // It's used for capturing the value of local variables.
        return Expression.Lambda(expression).Compile().DynamicInvoke();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            string s => $"'{s.Replace("'", "''")}'", // Escape single quotes
            bool b => b.ToString().ToLowerInvariant(),
            DateTime dt => dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            DateTimeOffset dto => dto.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Guid guid => guid.ToString(),
            Enum e => $"'{e.ToString()}'",
            _ when IsNumeric(value) => value.ToString() ?? string.Empty, // Ensure non-null return
            _ => throw new NotSupportedException($"The constant value '{value}' of type '{value?.GetType()}' is not supported in OData queries."),
        };
    }

    private static bool IsNumeric(object? value)
    {
        if (value == null)
        {
            return false;
        }

        var typeCode = Type.GetTypeCode(value.GetType());
        return typeCode >= TypeCode.SByte && typeCode <= TypeCode.Decimal;
    }
}
