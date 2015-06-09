using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;

namespace GenericDataObject
{
    public class gSPDataObject<TBusinessObject> where TBusinessObject : new()
    {

        #region Properties

        public static string ConnectionString = string.Empty;
        public static string spList = string.Empty;
        //caching variables
        private static List<TBusinessObject> cachedItems = null;
        private static DateTime? timeRefresh = (DateTime?)null;
        public static int refreshInterval = 0;

        #endregion

        #region Create

        public static bool Create(TBusinessObject newItem)
        {
            bool xBool = false;

            try
            {
                hasConnectionString();
                hasSpList();
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite site = new SPSite(ConnectionString))
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
                            foreach (System.Reflection.PropertyInfo objParam in objParams)
                            {
                                if (objParam.Name != "ID")
                                {
                                    item[objParam.Name] = objParam.GetValue(newItem, null);
                                }
                            }
                            item.Update();
                            xBool = true;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SP Data Object Create Method: " + ex.Message + "\n" + ex.StackTrace);
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
                    #region Try to get the item from SharePoint

                    hasConnectionString();
                    hasSpList();
                    SPSecurity.RunWithElevatedPrivileges(delegate()
                    {
                        using (SPSite site = new SPSite(ConnectionString))
                        {
                            using (SPWeb web = site.OpenWeb())
                            {
                                System.Reflection.PropertyInfo[] objParams = typeof(TBusinessObject).GetProperties();
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
                                foreach (System.Reflection.PropertyInfo objParam in objParams)
                                {
                                    query.ViewFields += string.Format(@"<FieldRef Name=""{0}""/>", objParam.Name);
                                }
                                SPListItemCollection items = list.GetItems(query);
                                foreach (SPListItem item in items)
                                {
                                    foreach (System.Reflection.PropertyInfo objParam in objParams)
                                    {
                                        if (objParam.PropertyType == typeof(int))
                                        {
                                            var value = Convert.ToInt32(item[objParam.Name]);
                                            objParam.SetValue(theItem, value, null);
                                        }
                                        else if (objParam.PropertyType == typeof(decimal))
                                        {
                                            var value = Convert.ToDecimal(item[objParam.Name]);
                                            objParam.SetValue(theItem, value, null);
                                        }
                                        else if (objParam.PropertyType == typeof(SPUser))
                                        {
                                            var value = Helper.GetSPUser(item, objParam.Name);
                                            objParam.SetValue(theItem, value, null);
                                        }
                                        else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                        {
                                            var value = Enum.Parse(objParam.PropertyType, item[objParam.Name].ToString());
                                            objParam.SetValue(theItem, value, null);
                                        }
                                        else
                                        {
                                            objParam.SetValue(theItem, item[objParam.Name], null);
                                        }
                                    }
                                }
                            }
                        }
                    });

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

        public static TBusinessObject GetItemByTitle(string title)
        {
            TBusinessObject theItem = new TBusinessObject();


            try
            {
                if (!isCached())
                {
                    #region Try to get the item from SharePoint

                    hasConnectionString();
                    hasSpList();
                    SPSecurity.RunWithElevatedPrivileges(delegate()
                    {
                        using (SPSite site = new SPSite(ConnectionString))
                        {
                            using (SPWeb web = site.OpenWeb())
                            {
                                System.Reflection.PropertyInfo[] objParams = typeof(TBusinessObject).GetProperties();
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
                                foreach (System.Reflection.PropertyInfo objParam in objParams)
                                {
                                    query.ViewFields += string.Format(@"<FieldRef Name=""{0}""/>", objParam.Name);
                                }
                                SPListItemCollection items = list.GetItems(query);
                                foreach (SPListItem item in items)
                                {
                                    foreach (System.Reflection.PropertyInfo objParam in objParams)
                                    {
                                        if (objParam.PropertyType == typeof(int))
                                        {
                                            var value = Convert.ToInt32(item[objParam.Name]);
                                            objParam.SetValue(theItem, value, null);
                                        }
                                        else if (objParam.PropertyType == typeof(decimal))
                                        {
                                            var value = Convert.ToDecimal(item[objParam.Name]);
                                            objParam.SetValue(theItem, value, null);
                                        }
                                        else if (objParam.PropertyType == typeof(SPUser))
                                        {
                                            var value = Helper.GetSPUser(item, objParam.Name);
                                            objParam.SetValue(theItem, value, null);
                                        }
                                        else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                        {
                                            var value = Enum.Parse(objParam.PropertyType, item[objParam.Name].ToString());
                                            objParam.SetValue(theItem, value, null);
                                        }
                                        else
                                        {
                                            objParam.SetValue(theItem, item[objParam.Name], null);
                                        }
                                    }
                                }
                            }
                        }
                    });

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

        public static List<TBusinessObject> GetAll()
        {
            return GetAll(query: null);
        }

        public static List<TBusinessObject> GetAll(Predicate<TBusinessObject> predicate)
        {
            return (from x in GetAll(query: null)
                    where predicate.Invoke(x)
                    select x).ToList();
        }

        public static List<TBusinessObject> GetAll(SPQuery query)
        {
            List<TBusinessObject> allItems = new List<TBusinessObject>();

            try
            {
                if (!isCached())
                {
                    #region Try to get list of data from sharepoint

                    hasConnectionString();
                    hasSpList();
                    SPSecurity.RunWithElevatedPrivileges(delegate()
                    {
                        using (SPSite site = new SPSite(ConnectionString))
                        {
                            using (SPWeb web = site.OpenWeb())
                            {
                                System.Reflection.PropertyInfo[] objParams = typeof(TBusinessObject).GetProperties();
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
                                    foreach (System.Reflection.PropertyInfo objParam in objParams)
                                    {
                                        spQuery.ViewFields += string.Format(@"<FieldRef Name=""{0}""/>", objParam.Name);
                                    }
                                }
                                else
                                {
                                    spQuery = query;
                                } 

                                #endregion
                                SPListItemCollection items = list.GetItems(spQuery);
                                foreach (SPListItem item in items)
                                {
                                    TBusinessObject tmpItem = new TBusinessObject();

                                    foreach (System.Reflection.PropertyInfo objParam in objParams)
                                    {

                                        object value = null;

                                        #region value = Convert.ToType(item[objParam.Name]);

                                        if (objParam.PropertyType == typeof(int))
                                        {
                                            value = Convert.ToInt32(item[objParam.Name]);
                                        }
                                        else if (objParam.PropertyType == typeof(decimal))
                                        {
                                            value = Convert.ToDecimal(item[objParam.Name]);
                                        }
                                        else if (objParam.PropertyType == typeof(SPUser))
                                        {
                                            value = Helper.GetSPUser(item, objParam.Name);
                                        }
                                        else if (objParam.PropertyType.UnderlyingSystemType.IsEnum)
                                        {
                                            value = Enum.Parse(objParam.PropertyType, item[objParam.Name].ToString());
                                        }
                                        else
                                        {
                                            value = item[objParam.Name];
                                        }

                                        #endregion

                                        objParam.SetValue(tmpItem, value, null);
                                    }
                                    allItems.Add(tmpItem);
                                }
                            }
                        }
                    });

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

        public static bool Update(TBusinessObject itemToUpdate)
        {
            bool xBool = false;

            try
            {
                hasConnectionString();
                hasSpList();
                hasID(itemToUpdate);
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite site = new SPSite(ConnectionString))
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
                            foreach (System.Reflection.PropertyInfo objParam in objParams)
                            {
                                if (objParam.Name != "ID")
                                {
                                    item[objParam.Name] = objParam.GetValue(itemToUpdate, null);
                                }
                            }
                            item.Update();
                            xBool = true;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SP Data Object Update Method: " + ex.Message + "\n" + ex.StackTrace);
            }

            return xBool;
        }

        #endregion

        #region Delete

        public static bool Delete(TBusinessObject itemToDelete)
        {
            bool xBool = false;

            try
            {
                hasConnectionString();
                hasSpList();
                hasID(itemToDelete);
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite site = new SPSite(ConnectionString))
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
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Generic SP Data Object Update Method: " + ex.Message + "\n" + ex.StackTrace);
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
            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception("Operation Aborted, Data Object ConnectionString has not yet been configured. Please make sure the ConnectionString has been set Before calling any operation.");
            }
            return true;
        }

        private static bool hasSpList()
        {
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

        private static void CacheList(List<TBusinessObject> items)
        {
            cachedItems = items;
            timeRefresh = DateTime.Now.AddMinutes(refreshInterval);
        }

        #endregion

    }
}
