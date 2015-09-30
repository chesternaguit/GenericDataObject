using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Collections;

namespace GenericDataObject
{
    public static class Helper
    {
        
        //created additional helper to resolve null object returned from querying the splist - february 4, 2015 at 11:24am 
        /// <summary>
        /// returns the SPUser specified by the key within the given ListItem
        /// </summary>
        /// <param name="item">the SPListItem</param>
        /// <param name="key">the FieldName</param>
        /// <returns>the value of SPUser type</returns>
        public static SPUser GetSPUser(SPListItem item, string key)
        {
            SPFieldUser field = item.Fields.GetField(key) as SPFieldUser;
            if (field != null)
            {
                SPFieldUserValue fieldValue = field.GetFieldValue(item[key].ToString()) as SPFieldUserValue;
                if (fieldValue != null)
                {
                    return fieldValue.User;
                }
            }
            return null;
        }

        public static SPGroup GetSPGroup(SPListItem item, string key)
        {
            SPFieldUser field = item.Fields.GetField(key) as SPFieldUser;
            if (field != null)
            {
                SPFieldUserValue fieldValue = field.GetFieldValue(item[key].ToString()) as SPFieldUserValue;
                if (fieldValue != null)
                {
                    return SPContext.Current.Web.Groups[fieldValue.LookupValue];
                }
            }
            return null;
        }

        public static string GetSPUserName(string fieldValue, string urlSite)
        {
            return GetSPUserName(fieldValue, new SPSite(urlSite));
        }

        private static string GetSPUserName(string fieldValue, SPSite site)
        {
            string userName = fieldValue;
            if (!string.IsNullOrEmpty(fieldValue) && fieldValue.Contains(";#"))
            {
                using (SPSite _site = site)
                {
                    using (SPWeb _web = _site.OpenWeb())
                    {
                        SPFieldUserValue fuv = new SPFieldUserValue(_web, fieldValue);
                        userName = fuv.User.Name;
                    }
                }
            }
            return userName;
        }

        public static byte[] ImageToByteArray(System.Drawing.Image imageIn)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                imageIn.Save(ms, imageIn.RawFormat);
                string y = Convert.ToBase64String(ms.ToArray());
                return ms.ToArray();
            }
        }

        public static System.Drawing.Image byteArrayToImage(byte[] byteArrayIn)
        {
            using (MemoryStream ms = new MemoryStream(byteArrayIn))
            {
                System.Drawing.Image returnImage = System.Drawing.Image.FromStream(ms);
                return returnImage;
            }
        }

        public static string GetParameters(MethodBase method, string[] parameters)
        {
            StringBuilder parameter = new StringBuilder();
            ParameterInfo[] errorParameter = method.GetParameters();
            for (int i = 0; i < errorParameter.Count(); i++)
            {
                parameter.AppendFormat("{0}={1};", errorParameter[i].Name, parameters[i].ToString());
            }
            string studentData = parameter.ToString();
            return studentData;
        }

        public static T GetEnumValue<T>(SPListItem item, string columnName)
        {
            string value = GetSpListItemValue(item, columnName);
            return (T)Enum.Parse(typeof(T), value);
        }

        public static string GetSpListItemValue(SPListItem item, string columnName)
        {
            string value = string.Empty;
            try
            {
                var itemValue = item[columnName];
                if (itemValue != null)
                {
                    value = itemValue.ToString();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Helper: GetSpListItemValue:" + ex.Message);
            }
            return value;
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        public static void Each<TSource>(this IEnumerable<TSource> items, Action<TSource> itemAction)
        {
            foreach (TSource item in items)
            {
                itemAction(item);
            }
        }

        public static string GetFieldNameOrDefault(this PropertyInfo propertyInfo)
        {
            FieldNameAttribute fieldNameAttribute = (FieldNameAttribute)propertyInfo.GetCustomAttributes(typeof(FieldNameAttribute), false).FirstOrDefault();
            string fieldName = propertyInfo.Name;
            if (fieldNameAttribute != null) fieldName = fieldNameAttribute.fieldName ?? fieldName;
            return fieldName;
        }

    }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class FieldNameAttribute : Attribute
    {
        public string fieldName { get; set; }
        public FieldNameAttribute() { }
        public FieldNameAttribute(string fieldName)
        {
            this.fieldName = fieldName;
        }
    }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class SPListNameAttribute : Attribute
    {
        public string listName { get; set; }
        public bool useClassName { get; set; }
        public SPListNameAttribute() { }
        public SPListNameAttribute(string listName)
        {
            this.listName = listName;
        }
        public SPListNameAttribute(bool useClassName)
        {
            this.useClassName = useClassName;
        }
    }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class SQLTableNameAttribute : Attribute
    {
        public string tableName { get; set; }
        public bool useClassName { get; set; }
        public SQLTableNameAttribute() { }
        public SQLTableNameAttribute(string tableName)
        {
            this.tableName = tableName;
        }
        public SQLTableNameAttribute(bool useClassName)
        {
            this.useClassName = useClassName;
        }
    }
}
