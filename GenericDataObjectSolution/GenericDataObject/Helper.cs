using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using System.Configuration;
using System.IO;
using System.Reflection;

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

    }
}
