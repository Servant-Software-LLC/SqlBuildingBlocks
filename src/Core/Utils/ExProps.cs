using System.Data;
using System.Linq.Expressions;

namespace SqlBuildingBlocks.Utils;

public class ExProps<TDerived> where TDerived : ExProps<TDerived>
{
    private Func<PropertyCollection> extendedProperties;

    public ExProps(Func<PropertyCollection> extendedProperties)
    {
        this.extendedProperties = extendedProperties ?? throw new ArgumentNullException(nameof(extendedProperties));
    }

    public bool Contains<TProperty>(Expression<Func<TDerived, TProperty>> extendedPropertySelector)
    {
        var propertyInfo = LambdaHelper<TDerived>.GetPropertyInfo(extendedPropertySelector);
        return extendedProperties().ContainsKey(propertyInfo.Name);
    }

    public bool TryGet<TProperty>(Expression<Func<TDerived, TProperty>> extendedPropertySelector, out TProperty property)
    {
        var propertyInfo = LambdaHelper<TDerived>.GetPropertyInfo(extendedPropertySelector);
        if (!extendedProperties().ContainsKey(propertyInfo.Name))
        {
            property = default;
            return false;
        }

        property = (TProperty)propertyInfo.GetValue(this);
        return true;
    }

    public void Remove<TProperty>(Expression<Func<TDerived, TProperty>> extendedPropertySelector)
    {
        var propertyInfo = LambdaHelper<TDerived>.GetPropertyInfo(extendedPropertySelector);
        if (extendedProperties().ContainsKey(propertyInfo.Name))
        {
            extendedProperties().Remove(propertyInfo.Name);
        }
    }

    protected TExtendedPropertyType Get<TExtendedPropertyType>(string key) where TExtendedPropertyType : class
            => extendedProperties()[key] as TExtendedPropertyType;

    protected TExtendedPropertyType GetValueType<TExtendedPropertyType>(string key) where TExtendedPropertyType : struct
            => (TExtendedPropertyType)extendedProperties()[key];

    protected void Set<TExtendedPropertyType>(string key, TExtendedPropertyType value)
            => extendedProperties()[key] = value;
}
