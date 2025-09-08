using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Haptics.Core.Models;

namespace Haptics.Core.Services
{
    public static class DataLoader
    {
        // Flexible loader: works if headers contain spaces (as in your file).
        public static List<DataPoint> LoadCsv(string path)
        {
            using var reader = new StreamReader(path);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null,
                MissingFieldFound = null,
                DetectDelimiter = true
            };
            using var csv = new CsvReader(reader, config);
            using var dr = new CsvDataReader(csv);

            // we’ll map by normalized header names
            var table = new System.Data.DataTable();
            table.Load(dr);

            // Normalize column names
            string norm(string s) => s.Trim().ToLowerInvariant();

            var colIndex = norm("index");
            var colForce = norm("force (n)");
            var colVolt = norm("voltage (v)");
            var colLinear = norm("linear (mm)");
            var colTime = norm("date time");

            var map = table.Columns.Cast<System.Data.DataColumn>()
                .ToDictionary(c => norm(c.ColumnName));

            int iIndex = map[colIndex].Ordinal;
            int iForce = map[colForce].Ordinal;
            int iVolt = map[colVolt].Ordinal;
            int iLin = map[colLinear].Ordinal;
            int iTime = map.ContainsKey(colTime) ? map[colTime].Ordinal : -1;

            var list = new List<DataPoint>(table.Rows.Count);
            foreach (System.Data.DataRow row in table.Rows)
            {
                list.Add(new DataPoint
                {
                    Index = Convert.ToInt32(row[iIndex]),
                    ForceN = Convert.ToDouble(row[iForce], CultureInfo.InvariantCulture),
                    VoltageV = Convert.ToDouble(row[iVolt], CultureInfo.InvariantCulture),
                    LinearMm = Convert.ToDouble(row[iLin], CultureInfo.InvariantCulture),
                    TimeRaw = iTime >= 0 ? row[iTime]?.ToString() : null
                });
            }
            return list;
        }
    }
}
