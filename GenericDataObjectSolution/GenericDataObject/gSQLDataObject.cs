using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

namespace GenericDataObject
{
    public class gSQLDataObject<TModel> where TModel : new()
    {

        #region Properties

        public static string connectionString = string.Empty;
        public static string sqlTable = string.Empty;
        //custom queries for future use
        public static string insertQuery = string.Empty;
        public static string selectQuery = string.Empty;
        public static string updateQuery = string.Empty;
        public static string deleteQuery = string.Empty;
        //cache variables
        private static List<TModel> cachedItems = null;
        private static DateTime? timeRefresh = (DateTime?)null;
        public static int refreshInterval = 0;

        #endregion

        #region Create

        public static bool Create(TModel newItem)
        {
            object tmp = null;
            return Create(newItem, System.Data.CommandType.Text, string.Empty, out tmp);
        }

        public static bool Create(TModel newItem, System.Data.CommandType commandType, string commandText)
        {
            object tmp = null;
            return Create(newItem, commandType, commandText, out tmp);
        }

        public static bool Create(TModel newItem, out object identity)
        {
            identity = null;
            return Create(newItem, System.Data.CommandType.Text, string.Empty, out identity);
        }

        public static bool Create(TModel newItem, System.Data.CommandType commandType, string commandText, out object identity)
        {
            bool xBool = false;

            try
            {
                System.Reflection.PropertyInfo[] objParams = typeof(TModel).GetProperties();
                hasConnectionString();
                hasSqlTable();
                using (SqlConnection xCon = new SqlConnection(connectionString))
                {
                    using (SqlCommand xCom = new SqlCommand())
                    {
                        xCom.Connection = xCon;
                        string query = string.Empty;
                        #region query = "Insert Into sqlTable ([Name], ...) Values(@Value, ...)"

                        if (string.IsNullOrEmpty(commandText))
                        {
                            query = string.Format("Insert Into [{0}] ", sqlTable);
                            string fields = "(";
                            string values = "Values(";
                            int initCtr = 0;
                            objParams.Each(objParam =>
                            {
                                if (objParam.Name != "ID" && !objParam.IgnoreField())
                                {
                                    string fieldName = objParam.GetFieldNameOrDefault();
                                    string separator = initCtr == 0 ? string.Empty : ",";
                                    fields += separator + "[" + fieldName + "]";
                                    values += separator + "@" + fieldName;
                                    initCtr = 1;
                                }
                            });
                            fields += ") ";
                            values += ")";
                            query += fields + values;
                        }
                        else
                        {
                            query = commandText;
                        }

                        #endregion
                        xCom.CommandText = query;
                        xCom.CommandType = commandType;
                        #region xCom.Parameters.AddWithValue("@Name",Value) ...

                        objParams.Each(objParam =>
                        {
                            if (objParam.Name != "ID" && !objParam.IgnoreField())
                            {
                                string fieldName = objParam.GetFieldNameOrDefault();
                                xCom.Parameters.AddWithValue("@" + fieldName, objParam.GetValue(newItem, null));
                            }
                        });

                        #endregion
                        try
                        {
                            xCon.Open();
                            identity = xCom.ExecuteScalar();
                            xBool = true;
                        }
                        catch (SqlException ex)
                        {
                            throw new Exception("Generic SQL Data Object Create Method: " + ex.Message + "\n" + ex.StackTrace);
                        }
                        finally
                        {
                            xCon.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SQL Data Object Create Method: " + ex.Message + "\n" + ex.StackTrace);
            }

            return xBool;
        }

        #endregion

        #region Read

        public static TModel GetItemByID(int id)
        {
            TModel theItem = new TModel();
            try
            {
                if (!isCached())
                {
                    #region Try to get the item from the server

                    var objParams = typeof(TModel).GetProperties();
                    hasConnectionString();
                    hasSqlTable();
                    using (SqlConnection xCon = new SqlConnection(connectionString))
                    {
                        using (SqlCommand xCom = new SqlCommand())
                        {
                            xCom.Connection = xCon;
                            xCom.CommandText = string.Format("Select * From {0} Where ID={1}", sqlTable, id);
                            xCom.CommandType = System.Data.CommandType.Text;
                            SqlDataReader xReader = null;
                            try
                            {
                                xCon.Open();
                                xReader = xCom.ExecuteReader();
                                while (xReader.Read())
                                {
                                    foreach (var objParam in objParams)
                                    {
                                        Object value = null;
                                        string fieldName = objParam.GetFieldNameOrDefault();

                                        if (!objParam.IgnoreField())
                                        {
                                            #region value = Convert.ToType(xReader[fieldName]);

                                            if (objParam.PropertyType == typeof(int))
                                            {
                                                value = Convert.ToInt32(xReader[fieldName]);
                                            }
                                            else if (objParam.PropertyType == typeof(decimal))
                                            {
                                                value = Convert.ToDecimal(xReader[fieldName]);
                                            }
                                            else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                            {
                                                value = Enum.Parse(objParam.PropertyType, xReader[fieldName].ToString());
                                            }
                                            else if (objParam.PropertyType == typeof(string))
                                            {
                                                value = xReader[fieldName].ToString();
                                            }
                                            else
                                            {
                                                value = xReader[fieldName];
                                            }

                                            #endregion 
                                        }

                                        objParam.SetValue(theItem, value, null);
                                    }
                                }
                                xReader.Close();
                            }
                            catch (SqlException ex)
                            {
                                throw new Exception("Generic SQL Data Object GetItemByID Method: " + ex.Message + "\n" + ex.StackTrace);
                            }
                            finally
                            {
                                xCon.Close();
                            }
                        }
                    }

                    #endregion
                }
                else
                {
                    theItem = cachedItems.Where(ci => (int)ci.GetType().GetProperty("ID").GetValue(ci, null) == id).FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SQL Data Object GetItemByID Method: " + ex.InnerException + ex.Message + "\n" + ex.StackTrace);
            }

            return theItem;
        }

        public static List<TModel> GetAll()
        {
            return GetAll(System.Data.CommandType.Text, null, null);
        }

        public static List<TModel> GetAll(Predicate<TModel> predicate)
        {
            return (from x in GetAll(System.Data.CommandType.Text, null, null)
                    where predicate.Invoke(x)
                    select x).ToList();
        }

        public static List<TModel> GetAll(System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters)
        {
            List<TModel> allItems = new List<TModel>();

            try
            {
                if (!isCached())
                {
                    #region Try to get the list of data from server

                    System.Reflection.PropertyInfo[] objParams = typeof(TModel).GetProperties();
                    hasConnectionString();
                    hasSqlTable();
                    using (SqlConnection xCon = new SqlConnection(connectionString))
                    {
                        using (SqlCommand xCom = new SqlCommand())
                        {
                            xCom.Connection = xCon;
                            xCom.CommandText = commandText ?? string.Format("Select * From {0}", sqlTable);
                            xCom.CommandType = commandType;
                            if (commandParameters != null)
                            {
                                foreach (SqlParameter cmdParam in commandParameters)
                                {
                                    xCom.Parameters.AddWithValue(cmdParam.ParameterName, cmdParam.Value);
                                }
                            }
                            SqlDataReader xReader = null;
                            try
                            {
                                xCon.Open();
                                xReader = xCom.ExecuteReader();
                                while (xReader.Read())
                                {
                                    TModel tmpItem = new TModel();
                                    objParams.Each(objParam =>
                                    {
                                        string fieldName = objParam.GetFieldNameOrDefault();

                                        if (!objParam.IgnoreField())
                                        {
                                            #region value = Convert.ToType(xReader[fieldName]);

                                            if (objParam.PropertyType == typeof(int))
                                            {
                                                int value = Convert.ToInt32(xReader[fieldName]);
                                                objParam.SetValue(tmpItem, value, null);
                                            }
                                            else if (objParam.PropertyType == typeof(decimal))
                                            {
                                                decimal value = Convert.ToDecimal(xReader[fieldName]);
                                                objParam.SetValue(tmpItem, value, null);
                                            }
                                            else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                            {
                                                var value = Enum.Parse(objParam.PropertyType, xReader[fieldName].ToString());
                                                objParam.SetValue(tmpItem, value, null);
                                            }
                                            else if (objParam.PropertyType == typeof(string))
                                            {
                                                objParam.SetValue(tmpItem, xReader[fieldName].ToString(), null);
                                            }
                                            else
                                            {
                                                objParam.SetValue(tmpItem, xReader[fieldName], null);
                                            }

                                            #endregion 
                                        }

                                    });
                                    allItems.Add(tmpItem);
                                }
                                xReader.Close();
                            }
                            catch (SqlException ex)
                            {
                                throw new Exception("Generic SQL Data Object GetAll Method: " + ex.Message + "\n" + ex.StackTrace);
                            }
                            finally
                            {
                                xCon.Close();
                            }
                        }
                    }

                    #endregion
                    CacheList(allItems);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SQL Data Object GetAll Method: " + ex.Message + "\n" + ex.StackTrace);
            }

            return cachedItems;
        }

        #endregion

        #region Update

        public static bool Update(TModel itemToUpdate)
        {
            int tmp = 0;
            return Update(itemToUpdate, System.Data.CommandType.Text, string.Empty, null, out tmp);
        }

        public static bool Update(TModel itemToUpdate, out int rowsAffected)
        {
            return Update(itemToUpdate, System.Data.CommandType.Text, string.Empty, null, out rowsAffected);
        }

        public static bool Update(TModel itemToUpdate, System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters)
        {
            int tmp = 0;
            return Update(itemToUpdate, commandType, commandText, commandParameters, out tmp);
        }

        public static bool Update(TModel itemToUpdate, System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters, out int rowsAffected)
        {
            bool xBool = false;
            rowsAffected = 0;

            try
            {
                System.Reflection.PropertyInfo[] objParams = itemToUpdate.GetType().GetProperties();
                hasID(itemToUpdate);
                hasConnectionString();
                hasSqlTable();
                using (SqlConnection xCon = new SqlConnection(connectionString))
                {
                    using (SqlCommand xCom = new SqlCommand())
                    {
                        xCom.Connection = xCon;
                        string query = string.Empty;
                        #region query = "Update sqlTable Set [Name] = @Value ... Where ID=@ID"

                        if (string.IsNullOrEmpty(commandText))
                        {
                            query = string.Format("Update [{0}] Set ", sqlTable);
                            string setValues = string.Empty;
                            string condition = string.Empty;
                            int initCtr = 0;
                            objParams.Each(objParam =>
                            {
                                if (objParam.Name != "ID" && !objParam.IgnoreField())
                                {
                                    string fieldName = objParam.GetFieldNameOrDefault();
                                    string separator = initCtr == 0 ? string.Empty : ",";

                                    setValues += separator + string.Format("[{0}] = @{0}", fieldName);

                                    initCtr++;
                                }
                                else
                                {
                                    condition = " Where ID=" + objParam.GetValue(itemToUpdate, null);
                                }
                            });
                            query = query + setValues + condition;
                        }
                        else
                        {
                            query = commandText;
                        }

                        #endregion
                        xCom.CommandText = query;
                        xCom.CommandType = commandType;
                        #region xCom.Parameters.AddWithValue("@Name",Value)

                        if (commandParameters != null)
                        {
                            foreach (SqlParameter cmdParam in commandParameters)
                            {
                                xCom.Parameters.AddWithValue(cmdParam.ParameterName, cmdParam.Value);
                            }
                        }
                        else
                        {
                            objParams.Each(objParam =>
                            {
                                if (!objParam.IgnoreField())
                                {
                                    xCom.Parameters.AddWithValue("@" + objParam.GetFieldNameOrDefault(), objParam.GetValue(itemToUpdate, null)); 
                                }
                            });
                        }

                        #endregion
                        try
                        {
                            xCon.Open();
                            rowsAffected = xCom.ExecuteNonQuery();
                            xBool = true;
                        }
                        catch (SqlException ex)
                        {
                            throw new Exception("Generic SQL Data Object Update Method: " + ex.Message + "\n" + ex.StackTrace);
                        }
                        finally
                        {
                            xCon.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SQL Data Object Update Method: " + ex.Message + "\n" + ex.StackTrace);
            }

            return xBool;
        }

        #endregion

        #region Delete

        public static bool Delete(TModel itemToDelete)
        {
            int tmp = 0;
            return Delete(itemToDelete, out tmp);
        }

        public static bool Delete(TModel itemToDelete, out int rowsAffected)
        {
            return Delete(itemToDelete, System.Data.CommandType.Text, string.Empty, null, out rowsAffected);
        }

        public static bool Delete(TModel itemToDelete, System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters)
        {
            int tmp = 0;
            return Delete(itemToDelete, commandType, commandText, commandParameters, out tmp);
        }

        public static bool Delete(TModel itemToDelete, System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters, out int rowsAffected)
        {
            bool xBool = false;
            rowsAffected = 0;

            try
            {
                hasID(itemToDelete);
                hasConnectionString();
                hasSqlTable();
                using (SqlConnection xCon = new SqlConnection(connectionString))
                {
                    using (SqlCommand xCom = new SqlCommand())
                    {
                        xCom.Connection = xCon;
                        string query = string.Format("Delete From [{0}] Where [ID] = {1}", sqlTable, Convert.ToInt32(itemToDelete.GetType().GetProperty("ID").GetValue(itemToDelete, null)));
                        xCom.CommandText = string.IsNullOrEmpty(commandText) ? query : commandText;
                        xCom.CommandType = commandType;
                        if (commandParameters != null)
                        {
                            foreach (SqlParameter cmdParam in commandParameters)
                            {
                                xCom.Parameters.AddWithValue(cmdParam.ParameterName, cmdParam.Value);
                            }
                        }
                        try
                        {
                            xCon.Open();
                            rowsAffected = xCom.ExecuteNonQuery();
                            xBool = true;
                        }
                        catch (SqlException ex)
                        {
                            throw new Exception("Generic SQL Data Object Delete Method: " + ex.Message + "\n" + ex.StackTrace);
                        }
                        finally
                        {
                            xCon.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SQL Data Object Delete Method: " + ex.Message + "\n" + ex.StackTrace);
            }

            return xBool;
        }

        #endregion

        #region Private Methods

        private static bool hasID(TModel item)
        {
            if (item.GetType().GetProperty("ID") == null)
            {
                throw new Exception(string.Format("Operation Failed, The Object of Type ({0}) does not have a property named \"ID\" of Type Int32", typeof(TModel).Name));
            }
            return true;
        }

        private static bool hasConnectionString()
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("Operation Aborted, Data Object connectionString has not yet been configured. Please make sure the connectionString has been set Before calling any operation.");
            }
            return true;
        }

        private static bool hasSqlTable()
        {
            SQLTableNameAttribute tableNameAttribute = (SQLTableNameAttribute)typeof(TModel).GetCustomAttributes(typeof(SQLTableNameAttribute), false).FirstOrDefault();
            if (tableNameAttribute != null)
            {
                sqlTable = tableNameAttribute.useClassName ? typeof(TModel).Name : (tableNameAttribute.tableName ?? sqlTable);
            }
            if (string.IsNullOrEmpty(sqlTable))
            {
                throw new Exception("Operation Aborted, Data Object sqlTable has not yet been configured. Please make sure the sqlTable has been set Before calling any operation.");
            }
            return true;
        }

        private static bool isCached()
        {
            if (cachedItems == null || timeRefresh == (DateTime?)null || timeRefresh <= DateTime.Now)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private static void CacheList(List<TModel> items)
        {
            cachedItems = items;
            timeRefresh = DateTime.Now.AddMinutes(refreshInterval);
        }

        #endregion

    }
}
