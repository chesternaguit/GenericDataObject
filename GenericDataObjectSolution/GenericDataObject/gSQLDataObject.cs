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

        public bool Create(TModel newItem)
        {
            object tmp = null;
            return Create(newItem, System.Data.CommandType.Text, string.Empty, out tmp);
        }

        public bool Create(TModel newItem, System.Data.CommandType commandType, string commandText)
        {
            object tmp = null;
            return Create(newItem, commandType, commandText, out tmp);
        }

        public bool Create(TModel newItem, out object identity)
        {
            identity = null;
            return Create(newItem, System.Data.CommandType.Text, string.Empty, out identity);
        }

        public bool Create(TModel newItem, System.Data.CommandType commandType, string commandText, out object identity)
        {
            bool xBool = false;

            try
            {
                System.Reflection.PropertyInfo[] objParams = typeof(TModel).GetProperties();
                hasConnectionString();
                hasSqlTable();
                using (SqlConnection xCon = new SqlConnection(_connectionString))
                {
                    using (SqlCommand xCom = new SqlCommand())
                    {
                        xCom.Connection = xCon;
                        StringBuilder query = new StringBuilder();
                        #region query = "Insert Into sqlTable ([Name], ...) Values(@Value, ...)"

                        if (string.IsNullOrEmpty(commandText))
                        {
                            query.AppendFormat("Insert Into {0} ", _SQLTable);
                            StringBuilder fields = new StringBuilder("(");
                            StringBuilder values = new StringBuilder("Values(");
                            int initCtr = 0;
                            objParams.Each(objParam =>
                            {
                                if (!objParam.IsIdentity() && !objParam.IgnoreField() && !objParam.IgnoreOnWrite())
                                {
                                    string fieldName = objParam.GetFieldNameOrDefault();
                                    string separator = initCtr == 0 ? string.Empty : ",";
                                    fields.AppendFormat("{0}[{1}]", separator, fieldName);
                                    values.AppendFormat("{0}@{1}", separator, fieldName.Trim());
                                    initCtr = 1;
                                }
                            });
                            fields.Append(") ");
                            values.Append(")");
                            query.Append(fields.ToString());
                            query.Append(values.ToString());
                        }
                        else
                        {
                            query.Append(commandText);
                        }

                        #endregion
                        xCom.CommandText = query.ToString();
                        xCom.CommandType = commandType;
                        #region xCom.Parameters.AddWithValue("@Name",Value) ...

                        objParams.Each(objParam =>
                        {
                            if (!objParam.IsIdentity() && !objParam.IgnoreField() && !objParam.IgnoreOnWrite())
                            {
                                string fieldName = objParam.GetFieldNameOrDefault();
                                if (objParam.PropertyType == typeof(bool))
                                {
                                    xCom.Parameters.Add(new SqlParameter() { ParameterName = "@" + fieldName.Trim(), Value = objParam.GetValue(newItem, null), DbType = System.Data.DbType.Boolean });
                                }
                                else
                                {
                                    xCom.Parameters.Add(new SqlParameter("@" + fieldName.Trim(), objParam.GetValue(newItem, null) ?? DBNull.Value));
                                }
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
                            throw new Exception("Generic SQL Data Object Create Method: " + ex.Message);
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
                throw new Exception("Generic SQL Data Object Create Method: " + ex.Message);
            }

            return xBool;
        }

        public bool BatchCreate(List<TModel> items)
        {
            bool xBool = false;

            try
            {
                System.Reflection.PropertyInfo[] objParams = typeof(TModel).GetProperties();
                hasConnectionString();
                hasSqlTable();
                using (SqlConnection xCon = new SqlConnection(_connectionString))
                {
                    xCon.Open();
                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(xCon))
                    {
                        try
                        {
                            bulkCopy.DestinationTableName = (new TModel()).GetTableName();
                            DataTable dataTable = new DataTable();
                            objParams.Each(field =>
                            {
                                dataTable.Columns.Add(field.GetFieldNameOrDefault());
                            });
                            items.Each(item =>
                            {
                                DataRow row = dataTable.NewRow();
                                objParams.Each(field =>
                                {
                                    row[field.GetFieldNameOrDefault()] = field.GetValue(item, null);
                                });
                                dataTable.Rows.Add(row);
                            });
                            dataTable.AcceptChanges();
                            bulkCopy.WriteToServer(dataTable.Rows.Cast<DataRow>().ToArray());
                            xBool = true;
                        }
                        catch (SqlException ex)
                        {
                            throw new Exception("Generic SQL Data Object Create Method: " + ex.Message);
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
                throw new Exception("Generic SQL Data Object Create Method: " + ex.Message);
            }

            return xBool;
        }

        #endregion

        #region Read

        public TModel GetItemByID(int id)
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
                    using (SqlConnection xCon = new SqlConnection(_connectionString))
                    {
                        using (SqlCommand xCom = new SqlCommand())
                        {
                            xCom.Connection = xCon;
                            string identityName = theItem.GetIdentityName() ?? "ID";
                            xCom.CommandText = string.Format("Select * From {0} Where [{1}]={2}", _SQLTable, identityName, id);
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

                                        if (!objParam.IgnoreField() && !objParam.IgnoreOnRead())
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
                                            else if (objParam.PropertyType == typeof(Single))
                                            {
                                                value = Convert.ToSingle(xReader[fieldName]);
                                            }
                                            else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                            {
                                                value = Enum.Parse(objParam.PropertyType, xReader[fieldName].ToString());
                                            }
                                            else if (objParam.PropertyType == typeof(string))
                                            {
                                                value = xReader[fieldName].ToString();
                                            }
                                            else if (objParam.PropertyType == typeof(bool))
                                            {
                                                value = xReader[fieldName] == DBNull.Value ? false : Convert.ToBoolean(xReader[fieldName]);
                                            }
                                            else if (objParam.PropertyType == typeof(DateTime))
                                            {
                                                value = Convert.ToDateTime(xReader[fieldName]);
                                            }
                                            else if (objParam.PropertyType == typeof(DateTime?))
                                            {
                                                if (xReader[fieldName] != DBNull.Value)
                                                {
                                                    value = Convert.ToDateTime(xReader[fieldName]);
                                                }
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
                                throw new Exception("Generic SQL Data Object GetItemByID Method: " + ex.Message);
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
                throw new Exception("Generic SQL Data Object GetItemByID Method: " + ex.Message);
            }

            return theItem;
        }

        public List<TModel> GetAll()
        {
            return GetAll(System.Data.CommandType.Text, null, null, null);
        }

        public List<TModel> GetAll(Predicate<TModel> predicate)
        {
            return (from x in GetAll(System.Data.CommandType.Text, null, null, null)
                    where predicate.Invoke(x)
                    select x).ToList();
        }

        public List<TModel> GetAll(System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters)
        {
            return GetAll(commandType, commandText, commandParameters, null);
        }
        public List<TModel> GetAll(System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters, Func<SqlDataReader, TModel> mapperDelegate)
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
                    using (SqlConnection xCon = new SqlConnection(_connectionString))
                    {
                        using (SqlCommand xCom = new SqlCommand())
                        {
                            xCom.Connection = xCon;
                            string selectFields = string.Join(",", objParams.Where(op => !op.IgnoreField() && !op.IgnoreOnRead()).Select(op => "[" + op.GetFieldNameOrDefault() + "]").ToArray());
                            xCom.CommandText = commandText ?? string.Format("Select {0} From {1}", selectFields, _SQLTable);
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
                                    if (mapperDelegate == null)
                                    {
                                        objParams.Each(objParam =>
                                                                    {
                                                                        string fieldName = objParam.GetFieldNameOrDefault();

                                                                        if (!objParam.IgnoreField() && !objParam.IgnoreOnRead())
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
                                                                            else if (objParam.PropertyType == typeof(Single))
                                                                            {
                                                                                Single value = Convert.ToSingle(xReader[fieldName]);
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
                                                                            else if (objParam.PropertyType == typeof(bool))
                                                                            {
                                                                                bool bValue = xReader[fieldName] == DBNull.Value ? false : Convert.ToBoolean(xReader[fieldName]);
                                                                                objParam.SetValue(tmpItem, bValue, null);
                                                                            }
                                                                            else if (objParam.PropertyType == typeof(DateTime))
                                                                            {
                                                                                DateTime bValue = Convert.ToDateTime(xReader[fieldName]);
                                                                                objParam.SetValue(tmpItem, bValue, null);
                                                                            }
                                                                            else if (objParam.PropertyType == typeof(DateTime?))
                                                                            {
                                                                                DateTime? value = null;
                                                                                if (xReader[fieldName] != DBNull.Value)
                                                                                {
                                                                                    value = Convert.ToDateTime(xReader[fieldName]);
                                                                                }
                                                                                objParam.SetValue(tmpItem, value, null);
                                                                            }
                                                                            else
                                                                            {
                                                                                objParam.SetValue(tmpItem, xReader[fieldName], null);
                                                                            }

                                                                            #endregion
                                                                        }

                                                                    });
                                    }
                                    else
                                    {
                                        tmpItem = mapperDelegate(xReader);
                                    }
                                    allItems.Add(tmpItem);
                                }
                                xReader.Close();
                            }
                            catch (SqlException ex)
                            {
                                throw new Exception("Generic SQL Data Object GetAll Method: " + ex.Message);
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
                throw new Exception("Generic SQL Data Object GetAll Method: " + ex.Message);
            }

            return cachedItems;
        }

        #endregion

        #region Update

        public bool Update(TModel itemToUpdate)
        {
            int tmp = 0;
            return Update(itemToUpdate, System.Data.CommandType.Text, string.Empty, null, out tmp);
        }

        public bool Update(TModel itemToUpdate, out int rowsAffected)
        {
            return Update(itemToUpdate, System.Data.CommandType.Text, string.Empty, null, out rowsAffected);
        }

        public bool Update(TModel itemToUpdate, System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters)
        {
            int tmp = 0;
            return Update(itemToUpdate, commandType, commandText, commandParameters, out tmp);
        }

        public bool Update(TModel itemToUpdate, System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters, out int rowsAffected)
        {
            bool xBool = false;
            rowsAffected = 0;

            try
            {
                System.Reflection.PropertyInfo[] objParams = itemToUpdate.GetType().GetProperties();
                //hasID(itemToUpdate);
                hasConnectionString();
                hasSqlTable();
                using (SqlConnection xCon = new SqlConnection(_connectionString))
                {
                    using (SqlCommand xCom = new SqlCommand())
                    {
                        xCom.Connection = xCon;
                        StringBuilder query = new StringBuilder();
                        #region query = "Update sqlTable Set [Name] = @Value ... Where ID=@ID"

                        if (string.IsNullOrEmpty(commandText))
                        {
                            query.AppendFormat("Update {0} Set ", _SQLTable);
                            StringBuilder setValues = new StringBuilder();
                            StringBuilder condition = new StringBuilder();
                            int initCtr = 0;
                            objParams.Each(objParam =>
                            {
                                string fieldName = objParam.GetFieldNameOrDefault();
                                if (!objParam.IsIdentity() && !objParam.IgnoreField() && !objParam.IgnoreOnWrite())
                                {
                                    string separator = initCtr == 0 ? string.Empty : ",";
                                    setValues.AppendFormat("{0}[{1}] = @{2}", separator, fieldName, fieldName.Trim());
                                    initCtr++;
                                }
                                else
                                {
                                    if (objParam.IsIdentity())
                                    {
                                        condition.AppendFormat(" Where [{0}] = @{1}", fieldName, fieldName.Trim());
                                    }
                                }
                            });
                            query.Append(setValues.ToString());
                            query.Append(condition.ToString());
                        }
                        else
                        {
                            query.Append(commandText);
                        }

                        #endregion
                        xCom.CommandText = query.ToString();
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
                                if (!objParam.IgnoreField() && !objParam.IgnoreOnWrite())
                                {
                                    if (objParam.PropertyType == typeof(bool))
                                    {
                                        xCom.Parameters.Add(new SqlParameter() { ParameterName = "@" + objParam.GetFieldNameOrDefault().Trim(), Value = objParam.GetValue(itemToUpdate, null), DbType = System.Data.DbType.Boolean });
                                    }
                                    else
                                    {
                                        xCom.Parameters.AddWithValue("@" + objParam.GetFieldNameOrDefault().Trim(), objParam.GetValue(itemToUpdate, null));
                                    }

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
                            throw new Exception("Generic SQL Data Object Update Method: " + ex.Message);
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
                throw new Exception("Generic SQL Data Object Update Method: " + ex.Message);
            }

            return xBool;
        }

        #endregion

        #region Delete

        public bool Delete(TModel itemToDelete)
        {
            int tmp = 0;
            return Delete(itemToDelete, out tmp);
        }

        public bool Delete(TModel itemToDelete, out int rowsAffected)
        {
            return Delete(itemToDelete, System.Data.CommandType.Text, string.Empty, null, out rowsAffected);
        }

        public bool Delete(TModel itemToDelete, System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters)
        {
            int tmp = 0;
            return Delete(itemToDelete, commandType, commandText, commandParameters, out tmp);
        }

        public bool Delete(TModel itemToDelete, System.Data.CommandType commandType, string commandText, List<SqlParameter> commandParameters, out int rowsAffected)
        {
            bool xBool = false;
            rowsAffected = 0;

            try
            {
                //hasID(itemToDelete);
                hasConnectionString();
                hasSqlTable();
                using (SqlConnection xCon = new SqlConnection(_connectionString))
                {
                    using (SqlCommand xCom = new SqlCommand())
                    {
                        xCom.Connection = xCon;
                        string query = string.Format("Delete From {0}", _SQLTable);
                        foreach (System.Reflection.PropertyInfo item in itemToDelete.GetType().GetProperties())
                        {
                            if (item.IsIdentity())
                            {
                                int identityValue = Convert.ToInt32(item.GetValue(itemToDelete, null) ?? -1);
                                query = string.Format("Delete From {0} Where [{1}] = {2}", _SQLTable, item.GetFieldNameOrDefault(), identityValue);
                                break;
                            }
                        }
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
                            throw new Exception("Generic SQL Data Object Delete Method: " + ex.Message);
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
                throw new Exception("Generic SQL Data Object Delete Method: " + ex.Message);
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
