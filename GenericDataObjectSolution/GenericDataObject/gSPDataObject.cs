using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;

namespace GenericDataObject
{
    public class gSPDataObject<TModel> where TModel : new()
    {

        #region Properties

        public static string ConnectionString = string.Empty;
        public static string spList = string.Empty;
        public static SPUserToken userToken = null;
        //caching variables
        private static List<TModel> cachedItems = null;
        private static DateTime? timeRefresh = (DateTime?)null;
        public static int refreshInterval = 0;

        #endregion

        #region Create

        public static bool Create(TModel newItem)
        {
            return Create(newItem, null);
        }

        public static bool Create(TModel newItem, Action<TModel, SPListItem> mapperDelegate)
        {
            int _dumpIdentity = 0;
            return Create(newItem, mapperDelegate, out _dumpIdentity);
        }
        public static bool Create(TModel newItem, Action<TModel, SPListItem> mapperDelegate, out int identity)
        {
            bool xBool = false;
            int _identity = 0;
            try
            {
                hasConnectionString();
                hasSpList();
                Action secureCode = () =>
                    {
                        using (SPSite site = userToken == null ? new SPSite(ConnectionString) : new SPSite(ConnectionString, userToken))
                        {
                            using (SPWeb web = site.OpenWeb())
                            {
                                System.Reflection.PropertyInfo[] objParams = newItem.GetType().GetProperties();
                                SPList list = web.Lists.TryGetList(spList);
                                if (list == null)
                                {
                                    throw new Exception(string.Format("there was no list named \"{0}\" in {1}", spList, ConnectionString));
                                }
                                SPListItem item = list.AddItem();
                                if (mapperDelegate == null)
                                {
                                    objParams.Each(objParam =>
                                    {
                                        if (objParam.Name != "ID" && !objParam.IgnoreField())
                                        {
                                            string fieldName = objParam.GetFieldNameOrDefault();
                                            item[fieldName] = objParam.GetValue(newItem, null);
                                        }
                                    });
                                }
                                else
                                {
                                    mapperDelegate(newItem, item);
                                }
                                item.Update();
                                _identity = item.ID;
                                xBool = true;
                            }
                        }
                    };
                if (userToken == null)
                {
                    SPSecurity.RunWithElevatedPrivileges(delegate() { secureCode.Invoke(); });
                }
                else
                {
                    secureCode.Invoke();
                }
                identity = _identity;
            }
            catch (Exception ex)
            {
                identity = _identity;
                throw new Exception("Generic SP Data Object Create Method: " + ex.Message + "\n" + ex.StackTrace);
            }
            
            return xBool;
        }

        public static bool BatchCreate(IEnumerable<TModel> newItems, string createBuilder, ref string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                hasConnectionString();
                hasSpList();
                StringBuilder _createBuilder = new StringBuilder();
                Action secureCode = () =>
                {
                    using (SPSite site = userToken == null ? new SPSite(ConnectionString) : new SPSite(ConnectionString, userToken))
                    {
                        using (SPWeb web = site.OpenWeb())
                        {
                            web.AllowUnsafeUpdates = true;
                            System.Reflection.PropertyInfo[] objParams = typeof(TModel).GetProperties();
                            SPList list = web.Lists.TryGetList(spList);
                            if (list == null)
                            {
                                throw new Exception(string.Format("there was no list named \"{0}\" in {1}", spList, ConnectionString));
                            }
                            if (createBuilder == null)
                            {
                                _createBuilder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Batch>");
                                foreach (TModel newItem in newItems)
                                {
                                    _createBuilder.AppendFormat("<Method><SetList Scope=\"Request\">{0}</SetList><SetVar Name=\"ID\">New</SetVar><SetVar Name=\"Cmd\">Save</SetVar>",
                                        list.ID);
                                    objParams.Each(objParam =>
                                    {
                                        if (objParam.Name != "ID")
                                        {
                                            string fieldName = objParam.GetFieldNameOrDefault();
                                            _createBuilder.AppendFormat("<SetVar Name=\"urn:schemas-microsoft-com:office:office#{0}\">{1}</SetVar>", fieldName, objParam.GetValue(newItem, null));
                                        }
                                    });
                                    _createBuilder.Append("</Method>");
                                }
                                _createBuilder.Append("</Batch>");
                                web.ProcessBatchData(_createBuilder.ToString());
                            }
                            else
                            {
                                web.ProcessBatchData(createBuilder);
                            }
                            web.AllowUnsafeUpdates = false;
                        }
                    }
                };//end secure code
                if (userToken == null)
                {
                    SPSecurity.RunWithElevatedPrivileges(delegate() { secureCode.Invoke(); });
                }
                else
                {
                    secureCode.Invoke();
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        #endregion

        #region Read

        public static TModel GetItemByID(int id)
        {
            return GetItemByID(id, null);
        }

        public static TModel GetItemByID(int id, Func<SPListItem, TModel> mapperDelegate)
        {
            TModel theItem = new TModel();

            try
            {
                if (!isCached())
                {
                    #region Try to get the item from SharePoint

                    hasConnectionString();
                    hasSpList();
                    Action secureCode = () =>
                        {
                            using (SPSite site = userToken == null ? new SPSite(ConnectionString) : new SPSite(ConnectionString, userToken))
                            {
                                using (SPWeb web = site.OpenWeb())
                                {
                                    System.Reflection.PropertyInfo[] objParams = typeof(TModel).GetProperties();
                                    SPList list = web.Lists.TryGetList(spList);
                                    if (list == null)
                                    {
                                        throw new Exception(string.Format("there was no list named \"{0}\" in {1}", spList, ConnectionString));
                                    }
                                    SPQuery query = new SPQuery();
                                    query.Query = @"<Where>
                                        <Eq>
                                            <FieldRef Name=""ID"" LookupId=""TRUE""/>
                                            <Value Type=""Integer"">" + id + @"</value>
                                        </Eq>
                                    </Where>";
                                    query.ViewFields = string.Empty;
                                    objParams.Each(objParam =>
                                    {
                                        query.ViewFields += string.Format(@"<FieldRef Name=""{0}""/>", objParam.GetFieldNameOrDefault());
                                    });
                                    SPListItemCollection items = list.GetItems(query);
                                    foreach (SPListItem item in items)
                                    {
                                        if (mapperDelegate == null)
                                        {
                                            objParams.Each(objParam =>
                                            {
                                                string fieldName = objParam.GetFieldNameOrDefault();

                                                if (!objParam.IgnoreField())
                                                {
                                                    if (objParam.PropertyType == typeof(int))
                                                    {
                                                        int value = Convert.ToInt32(item[fieldName]);
                                                        objParam.SetValue(theItem, value, null);
                                                    }
                                                    else if (objParam.PropertyType == typeof(decimal))
                                                    {
                                                        decimal value = Convert.ToDecimal(item[fieldName]);
                                                        objParam.SetValue(theItem, value, null);
                                                    }
                                                    else if (objParam.PropertyType == typeof(SPUser))
                                                    {
                                                        SPUser value = Helper.GetSPUser(item, fieldName);
                                                        objParam.SetValue(theItem, value, null);
                                                    }
                                                    else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                                    {
                                                        var value = Enum.Parse(objParam.PropertyType, item[fieldName].ToString());
                                                        objParam.SetValue(theItem, value, null);
                                                    }
                                                    else
                                                    {
                                                        objParam.SetValue(theItem, item[fieldName], null);
                                                    } 
                                                }
                                            });
                                        }
                                        else
                                        {
                                            theItem = mapperDelegate(item);
                                        }
                                    }
                                }
                            }
                        };
                    if (userToken == null)
                    {
                        SPSecurity.RunWithElevatedPrivileges(delegate() { secureCode.Invoke(); });
                    }
                    else
                    {
                        secureCode.Invoke();
                    }
                    #endregion
                }
                else
                {
                    theItem = cachedItems.Where(ci => Convert.ToInt32(ci.GetType().GetProperty("ID").GetValue(ci, null)) == id).FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SP Data Object GetItemByID Method: " + ex.InnerException + ex.Message + "\n" + ex.StackTrace);
            }

            return theItem;
        }

        public static TModel GetItemByTitle(string title)
        {
            return GetItemByTitle(title, null);
        }

        public static TModel GetItemByTitle(string title, Func<SPListItem, TModel> mapperDelegate)
        {
            TModel theItem = new TModel();

            try
            {
                if (!isCached())
                {
                    #region Try to get the item from SharePoint

                    hasConnectionString();
                    hasSpList();
                    Action secureCode = () =>
                        {
                            using (SPSite site = userToken == null ? new SPSite(ConnectionString) : new SPSite(ConnectionString, userToken))
                            {
                                using (SPWeb web = site.OpenWeb())
                                {
                                    System.Reflection.PropertyInfo[] objParams = typeof(TModel).GetProperties();
                                    SPList list = web.Lists.TryGetList(spList);
                                    if (list == null)
                                    {
                                        throw new Exception(string.Format("there was no list named \"{0}\" in {1}", spList, ConnectionString));
                                    }
                                    SPQuery query = new SPQuery();
                                    query.Query = @"<Where>
                                        <Eq>
                                            <FieldRef Name=""Title"" LookupId=""TRUE""/>
                                            <Value Type=""Text"">" + title + @"</value>
                                        </Eq>
                                    </Where>";
                                    query.ViewFields = string.Empty;
                                    objParams.Each(objParam =>
                                    {
                                        query.ViewFields += string.Format(@"<FieldRef Name=""{0}""/>", objParam.GetFieldNameOrDefault());
                                    });
                                    SPListItemCollection items = list.GetItems(query);
                                    foreach (SPListItem item in items)
                                    {
                                        if (mapperDelegate == null)
                                        {
                                            objParams.Each(objParam =>
                                            {
                                                string fieldName = objParam.GetFieldNameOrDefault();

                                                if (!objParam.IgnoreField())
                                                {
                                                    if (objParam.PropertyType == typeof(int))
                                                    {
                                                        int value = Convert.ToInt32(item[fieldName]);
                                                        objParam.SetValue(theItem, value, null);
                                                    }
                                                    else if (objParam.PropertyType == typeof(decimal))
                                                    {
                                                        decimal value = Convert.ToDecimal(item[fieldName]);
                                                        objParam.SetValue(theItem, value, null);
                                                    }
                                                    else if (objParam.PropertyType == typeof(SPUser))
                                                    {
                                                        SPUser value = Helper.GetSPUser(item, fieldName);
                                                        objParam.SetValue(theItem, value, null);
                                                    }
                                                    else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                                    {
                                                        var value = Enum.Parse(objParam.PropertyType, item[fieldName].ToString());
                                                        objParam.SetValue(theItem, value, null);
                                                    }
                                                    else
                                                    {
                                                        objParam.SetValue(theItem, item[fieldName], null);
                                                    } 
                                                }
                                            });
                                        }
                                        else
                                        {
                                            theItem = mapperDelegate(item);
                                        }
                                    }
                                }
                            }
                        };
                    if (userToken == null)
                    {
                        SPSecurity.RunWithElevatedPrivileges(delegate() { secureCode.Invoke(); });
                    }
                    else
                    {
                        secureCode.Invoke();
                    }

                    #endregion
                }
                else
                {
                    theItem = cachedItems.Where(ci => ci.GetType().GetProperty("Title").GetValue(ci, null).ToString().ToLower().Contains(title.ToLower())).FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SP Data Object GetItemByID Method: " + ex.InnerException + ex.Message + "\n" + ex.StackTrace);
            }

            return theItem;
        }

        public static List<TModel> GetAll()
        {
            return GetAll(null, null);
        }

        public static List<TModel> GetAll(Predicate<TModel> predicate)
        {
            return (from x in GetAll(null, null)
                    where predicate.Invoke(x)
                    select x).ToList();
        }

        public static List<TModel> GetAll(SPQuery query)
        {
            return GetAll(query, null);
        }

        public static List<TModel> GetAll(SPQuery query, Func<SPListItem, TModel> mapperDelegate)
        {
            List<TModel> allItems = new List<TModel>();

            try
            {
                if (!isCached())
                {
                    #region Try to get list of data from sharepoint

                    hasConnectionString();
                    hasSpList();
                    Action secureCode = () =>
                        {
                            using (SPSite site = userToken == null ? new SPSite(ConnectionString) : new SPSite(ConnectionString, userToken))
                            {
                                using (SPWeb web = site.OpenWeb())
                                {
                                    System.Reflection.PropertyInfo[] objParams = typeof(TModel).GetProperties();
                                    SPList list = web.Lists.TryGetList(spList);
                                    if (list == null)
                                    {
                                        throw new Exception(string.Format("there was no list named \"{0}\" in {1}", spList, ConnectionString));
                                    }
                                    SPQuery spQuery = new SPQuery();
                                    #region Set defaults if query parameter is null

                                    if (query == null)
                                    {
                                        spQuery.Query = @"<Where>
                                        <Gt>
                                            <FieldRef Name=""ID"" LookupId=""TRUE""/>
                                            <Value Type=""Integer"">0</value>
                                        </Gt>
                                    </Where>";
                                        spQuery.ViewFields = string.Empty;
                                        objParams.Each(objParam =>
                                        {
                                            spQuery.ViewFields += string.Format(@"<FieldRef Name=""{0}""/>", objParam.GetFieldNameOrDefault());
                                        });
                                    }
                                    else
                                    {
                                        spQuery = query;
                                    }

                                    #endregion
                                    SPListItemCollection items = list.GetItems(spQuery);
                                    foreach (SPListItem item in items)
                                    {
                                        TModel tmpItem = new TModel();

                                        if (mapperDelegate == null)
                                        {

                                            objParams.Each(objParam =>
                                            {
                                                string fieldName = objParam.GetFieldNameOrDefault();
                                                if (!objParam.IgnoreField())
                                                {
                                                    #region TheType value = Convert.ToType(item[fieldName]); objParam.SetValue(tmpItem, value, null);

                                                    if (objParam.PropertyType == typeof(int))
                                                    {
                                                        int value = Convert.ToInt32(item[fieldName]);
                                                        objParam.SetValue(tmpItem, value, null);
                                                    }
                                                    else if (objParam.PropertyType == typeof(decimal))
                                                    {
                                                        decimal value = Convert.ToDecimal(item[fieldName]);
                                                        objParam.SetValue(tmpItem, value, null);
                                                    }
                                                    else if (objParam.PropertyType == typeof(SPUser))
                                                    {
                                                        SPUser value = Helper.GetSPUser(item, fieldName);
                                                        objParam.SetValue(tmpItem, value, null);
                                                    }
                                                    else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                                    {
                                                        var value = Enum.Parse(objParam.PropertyType, item[fieldName].ToString());
                                                        objParam.SetValue(tmpItem, value, null);
                                                    }
                                                    else
                                                    {
                                                        objParam.SetValue(tmpItem, item[fieldName], null);
                                                    }

                                                    #endregion 
                                                }
                                            });
                                        }
                                        else
                                        {
                                            tmpItem = mapperDelegate(item);
                                        }
                                        allItems.Add(tmpItem);
                                    }
                                }
                            }
                        };
                    if (userToken == null)
                    {
                        SPSecurity.RunWithElevatedPrivileges(delegate() { secureCode.Invoke(); });
                    }
                    else
                    {
                        secureCode.Invoke();
                    }
                    #endregion
                    CacheList(allItems);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SP Data Object GetAll Method: " + ex.Message + "\n" + ex.StackTrace);
            }

            return cachedItems;
        }

        #endregion

        #region Update

        public static bool Update(TModel itemToUpdate)
        {
            return Update(itemToUpdate, null);
        }

        public static bool Update(TModel itemToUpdate, Action<TModel, SPListItem> mapperDelegate)
        {
            bool xBool = false;

            try
            {
                hasConnectionString();
                hasSpList();
                hasID(itemToUpdate);
                Action secureCode = () =>
                    {
                        using (SPSite site = userToken == null ? new SPSite(ConnectionString) : new SPSite(ConnectionString, userToken))
                        {
                            using (SPWeb web = site.OpenWeb())
                            {
                                System.Reflection.PropertyInfo[] objParams = itemToUpdate.GetType().GetProperties();
                                SPList list = web.Lists.TryGetList(spList);
                                if (list == null)
                                {
                                    throw new Exception(string.Format("there was no list named \"{0}\" in {1}", spList, ConnectionString));
                                }
                                SPListItem item = list.GetItemById(Convert.ToInt32(itemToUpdate.GetType().GetProperty("ID").GetValue(itemToUpdate, null)));
                                if (mapperDelegate == null)
                                {
                                    objParams.Each(objParam =>
                                    {
                                        if (objParam.Name != "ID" && !objParam.IgnoreField())
                                        {
                                            string fieldName = objParam.GetFieldNameOrDefault();
                                            item[fieldName] = objParam.GetValue(itemToUpdate, null);
                                        }
                                    });
                                }
                                else
                                {
                                    mapperDelegate(itemToUpdate, item);
                                }
                                item.Update();
                                xBool = true;
                            }
                        }
                    };
                if (userToken == null)
                {
                    SPSecurity.RunWithElevatedPrivileges(delegate() { secureCode.Invoke(); });
                }
                else
                {
                    secureCode.Invoke();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SP Data Object Update Method: " + ex.Message + "\n" + ex.StackTrace);
            }

            return xBool;
        }

        public static bool BatchUpdate(IEnumerable<TModel> newItems, string updateBuilder, ref string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                hasConnectionString();
                hasSpList();
                StringBuilder _updateBuilder = new StringBuilder();
                Action secureCode = () =>
                {
                    using (SPSite site = userToken == null ? new SPSite(ConnectionString) : new SPSite(ConnectionString, userToken))
                    {
                        using (SPWeb web = site.OpenWeb())
                        {
                            web.AllowUnsafeUpdates = true;
                            System.Reflection.PropertyInfo[] objParams = typeof(TModel).GetProperties();
                            SPList list = web.Lists.TryGetList(spList);
                            if (list == null)
                            {
                                throw new Exception(string.Format("there was no list named \"{0}\" in {1}", spList, ConnectionString));
                            }
                            if (updateBuilder == null)
                            {
                                _updateBuilder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Batch>");
                                foreach (TModel newItem in newItems)
                                {
                                    _updateBuilder.AppendFormat("<Method><SetList Scope=\"Request\">{0}</SetList><SetVar Name=\"Cmd\">Save</SetVar>",
                                        list.ID);
                                    objParams.Each(objParam =>
                                    {
                                        if (objParam.Name == "ID")
                                        {
                                            _updateBuilder.AppendFormat("<SetVar Name=\"ID\">{0}</SetVar>", objParam.GetValue(newItem, null).ToString());
                                        }
                                        else
                                        {
                                            string fieldName = objParam.GetFieldNameOrDefault();
                                            _updateBuilder.AppendFormat("<SetVar Name=\"urn:schemas-microsoft-com:office:office#{0}\">{1}</SetVar>", fieldName, objParam.GetValue(newItem, null));
                                        }
                                    });
                                    _updateBuilder.Append("</Method>");
                                }
                                _updateBuilder.Append("</Batch>");
                                web.ProcessBatchData(_updateBuilder.ToString());
                            }
                            else
                            {
                                web.ProcessBatchData(updateBuilder);
                            }
                            web.AllowUnsafeUpdates = false;
                        }
                    }
                };//end secure code
                if (userToken == null)
                {
                    SPSecurity.RunWithElevatedPrivileges(delegate() { secureCode.Invoke(); });
                }
                else
                {
                    secureCode.Invoke();
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
        
        #endregion

        #region Delete

        public static bool Delete(TModel itemToDelete)
        {
            bool xBool = false;

            try
            {
                hasConnectionString();
                hasSpList();
                hasID(itemToDelete);
                Action secureCode = () =>
                    {
                        using (SPSite site = userToken == null ? new SPSite(ConnectionString) : new SPSite(ConnectionString, userToken))
                        {
                            using (SPWeb web = site.OpenWeb())
                            {
                                SPList list = web.Lists.TryGetList(spList);
                                if (list == null)
                                {
                                    throw new Exception(string.Format("there was no list named \"{0}\" in {1}", spList, ConnectionString));
                                }
                                list.Items.DeleteItemById(Convert.ToInt32(itemToDelete.GetType().GetProperty("ID").GetValue(itemToDelete, null)));
                                xBool = true;
                            }
                        }
                    };
                if (userToken == null)
                {
                    SPSecurity.RunWithElevatedPrivileges(delegate() { secureCode.Invoke(); });
                }
                else
                {
                    secureCode.Invoke();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SP Data Object Update Method: " + ex.Message + "\n" + ex.StackTrace);
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
            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception("Operation Aborted, Data Object ConnectionString has not yet been configured. Please make sure the ConnectionString has been set Before calling any operation.");
            }
            return true;
        }

        private static bool hasSpList()
        {
            SPListNameAttribute listNameAttribute = (SPListNameAttribute)typeof(TModel).GetCustomAttributes(typeof(SPListNameAttribute), false).FirstOrDefault();
            if (listNameAttribute != null)
            {
                spList = listNameAttribute.useClassName ? typeof(TModel).Name : (listNameAttribute.listName ?? spList);
            }
            if (string.IsNullOrEmpty(spList))
            {
                throw new Exception("Operation Aborted, Data Object spList has not yet been configured. Please make sure the spList has been set Before calling any operation.");
            }
            return true;
        }

        private static bool hasSPField(SPList list, string fieldName)
        {
            return list.Fields.ContainsField(fieldName);
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
