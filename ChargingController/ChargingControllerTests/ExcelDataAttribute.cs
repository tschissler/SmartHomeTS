using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit.Sdk;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ExcelDataAttribute : DataAttribute
{
    public string FileName { get; private set; }
    public int NumberOfRowsToSkip { get; private set; }

    public ExcelDataAttribute(string fileName, int numberOfRowsToSkip)
    {
        FileName = fileName;
        NumberOfRowsToSkip = numberOfRowsToSkip;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        if (testMethod == null)
            throw new ArgumentNullException("testMethod is null");

        ParameterInfo[] pars = testMethod.GetParameters();
        return DataSource(FileName, NumberOfRowsToSkip, pars.Select(par => par.ParameterType).ToArray());
    }

    private IEnumerable<object[]> DataSource(string fileName, int numberOfRowsToSkip, Type[] parameterTypes)
    {
        using (var stream = File.Open(fileName, FileMode.Open, FileAccess.Read))
        {
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var rows = result.Tables[0].Rows.Cast<DataRow>().Skip(numberOfRowsToSkip);
                foreach (DataRow row in rows)
                    yield return ConvertParameters(row.ItemArray, parameterTypes);
            }
        }
    }

    private static object[] ConvertParameters(object[] values, Type[] parameterTypes)
    {
        object[] result = new object[values.Length];

        for (int idx = 0; idx < values.Length; idx++)
            result[idx] = ConvertParameter(values[idx], idx >= parameterTypes.Length ? null : parameterTypes[idx]);

        return result;
    }

    /// <summary>
    /// Converts a parameter to its destination parameter type, if necessary.
    /// </summary>
    /// <param name="parameter">The parameter value</param>
    /// <param name="parameterType">The destination parameter type (null if not known)</param>
    /// <returns>The converted parameter value</returns>
    private static object ConvertParameter(object parameter, Type parameterType)
    {
        if ((parameter is double || parameter is float) &&
            (parameterType == typeof(int) || parameterType == typeof(int?)))
        {
            int intValue;
            string floatValueAsString = parameter.ToString();

            if (Int32.TryParse(floatValueAsString, out intValue))
                return intValue;
        }

        return parameter;
    }
}