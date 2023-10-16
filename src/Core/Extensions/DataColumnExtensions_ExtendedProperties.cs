using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.Utils;
using System.Data;

namespace SqlBuildingBlocks.Extensions;

static class DataColumnExtensions_ExtendedProperties
{
    public class DataColumnExProps : ExProps<DataColumnExProps>
    {
        public DataColumnExProps(DataColumn dataColumn) : base(() => dataColumn.ExtendedProperties) { }

        public ISqlColumn Column
        {
            get => Get<ISqlColumn>(nameof(Column));
            set => Set(nameof(Column), value);
        }

        public SqlTable Table
        {
            get => Get<SqlTable>(nameof(Table));
            set => Set(nameof(Table), value);
        }

        public Func<object> ValueResolver
        {
            get => Get<Func<object>>(nameof(ValueResolver));
            set => Set(nameof(ValueResolver), value);
        }
    }

    public static DataColumnExProps ExProps(this DataColumn dataColumn) => new DataColumnExProps(dataColumn);
}
