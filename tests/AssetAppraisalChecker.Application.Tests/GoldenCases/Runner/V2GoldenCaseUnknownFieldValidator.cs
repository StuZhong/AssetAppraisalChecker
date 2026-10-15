using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    internal static class V2GoldenCaseUnknownFieldValidator
    {
        public static void Validate<T>(string json, string description)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            object raw;
            try
            {
                raw = new JavaScriptSerializer().DeserializeObject(json);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Invalid " + description + " JSON.",
                    exception);
            }

            ValidateValue(raw, typeof(T), "$", description);
        }

        private static void ValidateValue(
            object raw,
            Type expectedType,
            string path,
            string description)
        {
            if (raw == null)
            {
                return;
            }

            Type nullableType = Nullable.GetUnderlyingType(expectedType);
            if (nullableType != null)
            {
                ValidateValue(raw, nullableType, path, description);
                return;
            }

            if (expectedType.IsArray)
            {
                var items = raw as IEnumerable;
                if (items == null || raw is string || raw is IDictionary<string, object>)
                {
                    throw CreateShapeException(path, "array", description);
                }

                int index = 0;
                foreach (object item in items)
                {
                    ValidateValue(
                        item,
                        expectedType.GetElementType(),
                        path + "[" + index + "]",
                        description);
                    index++;
                }

                return;
            }

            if (IsStringDictionary(expectedType))
            {
                var dictionary = raw as IDictionary<string, object>;
                if (dictionary == null)
                {
                    throw CreateShapeException(path, "object", description);
                }

                if (dictionary.Any(item => item.Value != null && !(item.Value is string)))
                {
                    throw new InvalidDataException(
                        description + " JSON field '" + path
                        + "' must contain string values only.");
                }

                return;
            }

            if (IsScalar(expectedType))
            {
                if (raw is IDictionary<string, object>
                    || (raw is IEnumerable && !(raw is string)))
                {
                    throw CreateShapeException(path, "scalar", description);
                }

                return;
            }

            var rawObject = raw as IDictionary<string, object>;
            if (rawObject == null)
            {
                throw CreateShapeException(path, "object", description);
            }

            IDictionary<string, PropertyInfo> properties = expectedType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanWrite)
                .ToDictionary(property => property.Name, StringComparer.Ordinal);

            foreach (KeyValuePair<string, object> field in rawObject)
            {
                PropertyInfo property;
                if (!properties.TryGetValue(field.Key, out property))
                {
                    throw new InvalidDataException(
                        description + " JSON contains unknown field '"
                        + path + "." + field.Key + "'.");
                }

                ValidateValue(
                    field.Value,
                    property.PropertyType,
                    path + "." + field.Key,
                    description);
            }
        }

        private static bool IsStringDictionary(Type type)
        {
            return typeof(IDictionary<string, string>).IsAssignableFrom(type);
        }

        private static bool IsScalar(Type type)
        {
            return type == typeof(string)
                || type == typeof(bool)
                || type == typeof(byte)
                || type == typeof(short)
                || type == typeof(int)
                || type == typeof(long)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal)
                || type.IsEnum;
        }

        private static InvalidDataException CreateShapeException(
            string path,
            string shape,
            string description)
        {
            return new InvalidDataException(
                description + " JSON field '" + path + "' must be a JSON " + shape + ".");
        }
    }
}
