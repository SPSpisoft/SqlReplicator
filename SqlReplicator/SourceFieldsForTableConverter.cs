using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Collections.Generic;

namespace SqlReplicator
{
    public class SourceFieldsForTableConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var tableName = values[0] as string;
            var tables = values[1] as IEnumerable<TableInfo>;
            if (tableName != null && tables != null)
            {
                var table = tables.FirstOrDefault(t => t.TableName == tableName);
                if (table != null)
                    return table.Fields.Select(f => f.FieldName).ToList();
            }
            return new List<string>();
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
} 