using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

namespace GenericDataObject
{
    public class gSQLDataObject<TBusinessObject> where TBusinessObject : new()
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
        private static List<TBusinessObject> cachedItems = null;
        private static DateTime? timeRefresh = (DateTime?)null;
        public static int refreshInterval = 0;

        #endregion

        #region Create

        public static bool Create(TBusinessObject newItem)
        {
            return Create(newItem, System.Data.CommandType.Text, string.Empty);
        }

        public static bool Create(TBusinessObject newItem, System.Data.CommandType commandType, string commandText)
        {
            object tmp = null;
            return Create(newItem, commandType, commandText, out tmp);
        }

        public static bool Create(TBusinessObject newItem, out object identity)
        {
            identity = null;
            return Create(newItem, System.Data.CommandType.Text, string.Empty, out identity);
        }

        public static bool Create(TBusinessObject newItem, System.Data.CommandType commandType, string commandText, out object identity)
        {
            bool xBool = false;

            try
            {
                System.Reflection.PropertyInfo[] objParams = typeof(TBusinessObject).GetProperties();
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
                            foreach (System.Reflection.PropertyInfo objParam in objParams)
                            {
                                if (objParam.Name != "ID")
                                {
                                    string separator = initCtr == 0 ? string.Empty : ",";
                                    fields += separator + "[" + objParam.Name + "]";
                                    values += separator + "@" + objParam.Name;
                                    initCtr = 1;
                                }
                            }
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

                        foreach (System.Reflection.PropertyInfo objParam in objParams)
                        {
                            if (objParam.Name != "ID")
                            {
                                xCom.Parameters.AddWithValue("@" + objParam.Name, objParam.GetValue(newItem, null));
                            }
                        }

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

        public static TBusinessObject GetItemByID(int id)
        {
            TBusinessObject theItem = new TBusinessObject();
            try
            {
                if (!isCached())
                {
                    #region Try to get the item from the server

                    var objParams = typeof(TBusinessObject).GetProperties();
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

                                        #region value = Convert.ToType(xReader[objParam.Name]);

                                        if (objParam.PropertyType == typeof(int))
                                        {
                                            value = Convert.ToInt32(xReader[objParam.Name]);
                                        }
                                        else if (objParam.PropertyType == typeof(decimal))
                                        {
                                            value = Convert.ToDecimal(xReader[objParam.Name]);
                                        }
                                        else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                        {
                                            value = Enum.Parse(objParam.PropertyType, xReader[objParam.Name].ToString());
                                        }
                                        else
                                        {
                                            value = xReader[objParam.Name];
                                        }

                                        #endregion

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

        public static List<TBusinessObject> GetAll()
        {
            return GetAll(System.Data.CommandType.Text, "", null);
        }

        public static List<TBusinessObject> GetAll(Predicate<TBusinessObject> predicate)
        {
            return (from x in GetAll(System.Data.CommandType.Text, "", null)
                    where predicate.Invoke(x)
                    select x).ToList();
        }

        public static List<TBusinessObject> GetAll(System.Data.CommandType commandType, string commandText, SqlParameterCollection commandParameters)
        {
            List<TBusinessObject> allItems = new List<TBusinessObject>();

            try
            {
                if (!isCached())
                {
                    #region Try to get the list of data from server

                    System.Reflection.PropertyInfo[] objParams = typeof(TBusinessObject).GetProperties();
                    hasConnectionString();
                    hasSqlTable();
                    using (SqlConnection xCon = new SqlConnection(connectionString))
                    {
                        using (SqlCommand xCom = new SqlCommand())
                        {
                            xCom.Connection = xCon;
                            xCom.CommandText = string.IsNullOrEmpty(commandText) ? string.Format("Select * From {0}", sqlTable) : commandText;
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
                                    TBusinessObject tmpItem = new TBusinessObject();
                                    foreach (System.Reflection.PropertyInfo objParam in objParams)
                                    {
                                        Object value = null;

                                        #region value = Convert.ToType(xReader[objParam.Name]);

                                        if (objParam.PropertyType == typeof(int))
                                        {
                                            value = Convert.ToInt32(xReader[objParam.Name]);
                                        }
                                        else if (objParam.PropertyType == typeof(decimal))
                                        {
                                            value = Convert.ToDecimal(xReader[objParam.Name]);
                                        }
                                        else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                        {
                                            value = Enum.Parse(objParam.PropertyType, xReader[objParam.Name].ToString());
                                        }
                                        else
                                        {
                                            value = xReader[objParam.Name];
                                        }

                                        #endregion

                                        objParam.SetValue(tmpItem, value, null);
                                    }
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

        public static bool Update(TBusinessObject itemToUpdate)
        {
            int tmp = 0;
            return Update(itemToUpdate, out tmp);
        }

        public static bool Update(TBusinessObject itemToUpdate, out int rowsAffected)
        {
            return Update(itemToUpdate, System.Data.CommandType.Text, string.Empty, null, out rowsAffected);
        }

        public static bool Update(TBusinessObject itemToUpdate, System.Data.CommandType commandType, string commandText, SqlParameterCollection commandParameters)
        {
            int tmp = 0;
            return Update(itemToUpdate, commandType, commandText, commandParameters, out tmp);
        }

        public static bool Update(TBusinessObject itemToUpdate, System.Data.CommandType commandType, string commandText, SqlParameterCollection commandParameters, out int rowsAffected)
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
                            foreach (System.Reflection.PropertyInfo objParam in objParams)
                            {
                                if (objParam.Name != "ID")
                                {
                                    string separator = initCtr == 0 ? string.Empty : ",";

                                    setValues += separator + string.Format("[{0}] = @{0}", objParam.Name);

                                    initCtr++;
                                }
                                else
                                {
                                    condition = " Where ID=" + objParam.GetValue(itemToUpdate, null);
                                }
                            }
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
                            foreach (System.Reflection.PropertyInfo objParam in objParams)
                            {
                                xCom.Parameters.AddWithValue("@" + objParam.Name, objParam.GetValue(itemToUpdate, null));
                            }
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

        public static bool Delete(TBusinessObject itemToDelete)
        {
            int tmp = 0;
            return Delete(itemToDelete, out tmp);
        }

        public static bool Delete(TBusinessObject itemToDelete, out int rowsAffected)
        {
            return Delete(itemToDelete, System.Data.CommandType.Text, string.Empty, null, out rowsAffected);
        }

        public static bool Delete(TBusinessObject itemToDelete, System.Data.CommandType commandType, string commandText, SqlParameterCollection commandParameters)
        {
            int tmp = 0;
            return Delete(itemToDelete, commandType, commandText, commandParameters, out tmp);
        }

        public static bool Delete(TBusinessObject itemToDelete, System.Data.CommandType commandType, string commandText, SqlParameterCollection commandParameters, out int rowsAffected)
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

        private static bool hasID(TBusinessObject item)
        {
            if (item.GetType().GetProperty("ID") == null)
            {
                throw new Exception(string.Format("Operation Failed, The Object of Type ({0}) does not have a property named \"ID\" of Type Int32", typeof(TBusinessObject).Name));
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

        private static void CacheList(List<TBusinessObject> items)
        {
            cachedItems = items;
            timeRefresh = DateTime.Now.AddMinutes(refreshInterval);
        }

        #endregion

    }
}
