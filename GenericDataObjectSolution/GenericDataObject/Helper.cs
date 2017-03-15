using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using System.Net.Mail;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ComponentModel;
using System.Web;

namespace MDG.PAF.Core.Common
{
    public static class Helper
    {
        #region properties

        public static string DBPafConnection { get { return @"Data Source=MNL07SPDB01\SQLSPDB02;Initial Catalog=DBPAF;Integrated Security=True"; } }

        #endregion

        #region SharePoint Helpers

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
        /// <summary>
        /// returns a List of type SPUser
        /// </summary>
        /// <param name="item">the SPListItem where the list of SPUsers will be extracted</param>
        /// <param name="key">the field name</param>
        /// <returns></returns>
        public static List<SPUser> GetSPUsers(SPListItem item, string key)
        {
            List<SPUser> users = new List<SPUser>();
            string value = item[key] as string;
            SPFieldUserValueCollection userVals = new SPFieldUserValueCollection(item.Web, key);
            foreach (SPFieldUserValue userVal in userVals)
            {
                users.Add(userVal.User);
            }
            return users;
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
        public static void SendEmail(string siteURL, string emailFrom, string emailTo, string emailSubject, string htmlBody)
        {
            try
            {
                if (!string.IsNullOrEmpty(emailTo))
                {
                    bool result = false;

                    using (SPSite _site = new SPSite(siteURL))
                    {
                        using (SPWeb _web = _site.OpenWeb())
                        {
                            bool appendHtmlTag = true;
                            bool htmlEncode = false;

                            SPSecurity.RunWithElevatedPrivileges(delegate()
                            {
                                result = SPUtility.SendEmail(_web, appendHtmlTag, htmlEncode, emailTo, emailSubject, htmlBody);
                            });
                        }
                    }

                    if (result == false)
                    {
                        SPSecurity.RunWithElevatedPrivileges(delegate()
                        {
                            using (MailMessage message = new MailMessage(SPContext.Current.Site.WebApplication.OutboundMailSenderAddress, emailTo, emailSubject, htmlBody))
                            {
                                message.IsBodyHtml = true;
                                SmtpClient client = new SmtpClient(SPContext.Current.Site.WebApplication.OutboundMailServiceInstance.Server.Address);
                                client.Send(message);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Helper: SendEmail:" + ex.Message);
            }
        }
        public static SPAuditEntryCollection GetListAuditEntries(string siteUrl, string listName, SPAuditEventType eventType, int userId, DateTime dateFrom, DateTime dateTo, out string errorMessage)
        {
            SPAuditEntryCollection _audits;
            errorMessage = string.Empty;
            try
            {
                using (SPSite _site = new SPSite(siteUrl))
                {
                    using (SPWeb _web = _site.OpenWeb())
                    {
                        SPList _list = _web.Lists.TryGetList(listName);
                        SPAuditQuery _auditQuery = new SPAuditQuery(_site);
                        _auditQuery.RestrictToList(_list);
                        _auditQuery.AddEventRestriction(eventType);
                        _auditQuery.RestrictToUser(userId);
                        _auditQuery.SetRangeStart(dateFrom);
                        _auditQuery.SetRangeEnd(dateTo);
                        SPAudit _audit = _site.Audit;
                        _audits = _audit.GetEntries(_auditQuery);
                    }
                }
                return _audits;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return null;
            }
        }
        public static int ClearListAuditEntries(string siteUrl, SPUserToken userToken, DateTime deleteEndDate, out string errorMessage)
        {
            errorMessage = string.Empty;
            int _itemsDeleted = 0;
            try
            {
                using (SPSite _site = new SPSite(siteUrl, userToken))
                {
                    using (SPWeb _web = _site.OpenWeb())
                    {
                        SPAudit _audit = _site.Audit;
                        _itemsDeleted = _audit.DeleteEntries(deleteEndDate);
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }
            return _itemsDeleted;
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
        public static bool CurrentIsMemberOf(SPWeb web, string groupName)
        {
            try
            {
                return !(string.IsNullOrEmpty(groupName)) ? web.IsCurrentUserMemberOfGroup(web.Groups[groupName].ID) : false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static T GetEnumValue<T>(SPListItem item, string columnName)
        {
            string value = GetSpListItemValue(item, columnName);
            return (T)Enum.Parse(typeof(T), value);
        }

        #endregion

        #region Extension Methods

        /// <summary>
        /// Returns a set of items that are unique by the specified key
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Checks wether the Date is within the range of dates specified in the parameter
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startRange"></param>
        /// <param name="endRange"></param>
        /// <returns>true if the value is within startRange and endRange</returns>
        public static bool WithinDateRange(this DateTime value, DateTime startRange, DateTime endRange)
        {
            return (startRange <= value && value <= endRange);
        }
        /// <summary>
        /// Finds each value within the source that satisfies the condition specified by the predicate
        /// </summary>
        /// <typeparam name="TSource">The data type of the source</typeparam>
        /// <typeparam name="TValue">The data type of the values</typeparam>
        /// <param name="source">IEnumerable where the values will be searched</param>
        /// <param name="values">IEnumerable of values to be searched</param>
        /// <param name="predicate">specifies the condition for the comparison</param>
        /// <returns>Returns IEnumerable of items from source where predicate returns true</returns>
        public static IEnumerable<TSource> FindEach<TSource, TValue>(this IEnumerable<TSource> source, IEnumerable<TValue> values, Func<TSource, TValue, bool> predicate)
        {
            foreach (TValue value in values)
            {
                foreach (TSource item in source)
                {
                    if (predicate.Invoke(item, value))
                    {
                        yield return item;
                    }
                }
            }
        }
        /// <summary>
        /// Performs specified action on iteration of each item
        /// </summary>
        /// <typeparam name="TSource">Type of the Enumerable item</typeparam>
        /// <param name="items">Enumerable items where the action is to be performed</param>
        /// <param name="itemAction">A Function that will be invoked for every iteration and takes the individual item as its parameter</param>
        public static void Each<TSource>(this IEnumerable<TSource> items, Action<TSource> itemAction)
        {
            foreach (TSource item in items)
            {
                itemAction(item);
            }
        }
        public static string NullIfEmpty(this string value)
        {
            return value == string.Empty ? null : value;
        }
        public static string CleanJSON(this string source)
        {
            return source.Replace("\"", "&quot;").Replace("\\", "&#92;").Replace("\n", " ").Replace("\r", "");
        }
        public static string ToCsvString<TSource>(this IEnumerable<TSource> items)
        {
            StringBuilder stringBuilder = new StringBuilder();
            PropertyInfo[] props = typeof(TSource).GetProperties();
            foreach (TSource item in items)
            {
                foreach (PropertyInfo prop in props)
                {
                    stringBuilder.AppendFormat("{0}, ", prop.GetValue(item, null));
                }
                stringBuilder.AppendLine();
            }
            return stringBuilder.ToString();
        }
        public static string ToHtmlTable<TSource>(this IEnumerable<TSource> items)
        {
            System.Xml.Linq.XElement _table = new System.Xml.Linq.XElement("table");
            PropertyInfo[] props = typeof(TSource).GetProperties();

            System.Xml.Linq.XElement _header = new System.Xml.Linq.XElement("tr");
            foreach (PropertyInfo prop in props)
            {
                System.Xml.Linq.XElement _cell = new System.Xml.Linq.XElement("th", prop.Name);
                _header.Add(_cell);
            }
            _table.Add(_header);

            foreach (TSource item in items)
            {
                System.Xml.Linq.XElement _row = new System.Xml.Linq.XElement("tr");
                foreach (PropertyInfo prop in props)
                {
                    System.Xml.Linq.XElement _cell = new System.Xml.Linq.XElement("td", prop.GetValue(item,null));
                    _row.Add(_cell);
                }
                _table.Add(_row);
            }

            return _table.ToString();
        }
        //based from http://stackoverflow.com/questions/13074202/passing-strongly-typed-property-name-as-argument
        public static IEnumerable<TSource> FilterBy<TSource, TProperty, TValue>(this IEnumerable<TSource> source, Expression<Func<TSource, TProperty>> property, TValue value)
        {
            string propName = getMemberInfo(property).Name;
            return source.Where(src => src.GetType().GetProperty(propName).GetValue(src, null).Equals(value));
        }
        private static MemberInfo getMemberInfo<TObject, TProperty>(Expression<Func<TObject, TProperty>> expression)
        {
            var member = expression.Body as MemberExpression;
            if (member != null)
            {
                return member.Member;
            }
            throw new ArgumentException("Member does not exist.");
        }

        #endregion

        #region Custom Attribute Helpers

        /// <summary>
        /// Gets the Field name specified by FieldNameAttribute, if the attribute does not exist returns PropertyInfo.Name as default
        /// </summary>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        public static string GetFieldNameOrDefault(this PropertyInfo propertyInfo)
        {
            FieldNameAttribute fieldNameAttribute = (FieldNameAttribute)propertyInfo.GetCustomAttributes(typeof(FieldNameAttribute), false).FirstOrDefault();
            string fieldName = propertyInfo.Name;
            if (fieldNameAttribute != null) fieldName = fieldNameAttribute.fieldName ?? fieldName;
            return fieldName;
        }
        public static string GetSPListName(MemberInfo member)
        {
            SPListNameAttribute listNameAttribute = (SPListNameAttribute)member.GetCustomAttributes(typeof(SPListNameAttribute), false).FirstOrDefault();
            string listName = string.Empty;
            if (listNameAttribute != null)
            {
                listName = listNameAttribute.useClassName ? member.Name : (listNameAttribute.listName ?? listName);
            }
            return listName;
        }
        public static string GetTableName<TSource>(this TSource source)
        {
            SQLTableNameAttribute tableNameAttribute = (SQLTableNameAttribute)source.GetType().GetCustomAttributes(typeof(SQLTableNameAttribute), false).FirstOrDefault();
            string tableName = string.Empty;
            if (tableNameAttribute != null)
            {
                tableName = tableNameAttribute.useClassName ? typeof(TSource).Name : (tableNameAttribute.tableName ?? tableName);
            }
            return tableName;
        }
        public static string GetIdentityName<TSource>(this TSource source)
        {
            System.Reflection.PropertyInfo[] objParams = typeof(TSource).GetProperties();
            string identityName = null;
            foreach (PropertyInfo propInfo in objParams)
            {
                if (propInfo.IsIdentity())
                {
                    identityName = propInfo.GetFieldNameOrDefault();
                    break;
                }
            }
            return identityName;
        }
        public static bool IgnoreField(this PropertyInfo propertyInfo)
        {
            IgnorePropertyAttribute ignorePropertyAttribute = (IgnorePropertyAttribute)propertyInfo.GetCustomAttributes(typeof(IgnorePropertyAttribute), false).FirstOrDefault();
            return ignorePropertyAttribute == null ? false : (ignorePropertyAttribute.ignoreAccess == IgnoreAccess.ReadWrite ? ignorePropertyAttribute.ignoreProperty : false);
        }
        public static bool IgnoreOnRead(this PropertyInfo propertyInfo)
        {
            IgnorePropertyAttribute ignorePropertyAttribute = (IgnorePropertyAttribute)propertyInfo.GetCustomAttributes(typeof(IgnorePropertyAttribute), false).FirstOrDefault();
            return ignorePropertyAttribute == null ? false : (ignorePropertyAttribute.ignoreAccess == IgnoreAccess.OnRead ? ignorePropertyAttribute.ignoreProperty : false);
        }
        public static bool IgnoreOnWrite(this PropertyInfo propertyInfo)
        {
            IgnorePropertyAttribute ignorePropertyAttribute = (IgnorePropertyAttribute)propertyInfo.GetCustomAttributes(typeof(IgnorePropertyAttribute), false).FirstOrDefault();
            return ignorePropertyAttribute == null ? false : (ignorePropertyAttribute.ignoreAccess == IgnoreAccess.OnWrite ? ignorePropertyAttribute.ignoreProperty : false);
        }
        public static bool IsIdentity(this PropertyInfo propertyInfo)
        {
            IsIdentityAttribute isIdentityAttribute = (IsIdentityAttribute)propertyInfo.GetCustomAttributes(typeof(IsIdentityAttribute), false).FirstOrDefault();
            return isIdentityAttribute == null ? false : isIdentityAttribute.isIdentity;
        }

        #endregion

        #region Others

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
        public static DateTime TimeToFullDate(string timePart, string datePart, int timeZone)
        {
            string _dugtong = datePart + " " + timePart + ":00";
            return Convert.ToDateTime(_dugtong.Trim()).AddHours(timeZone);
        }
        public static DateTime IncrementDayIfTimeToIsLess(string timeFrom, string timeTo, string datePart, int timeZone)
        {
            string _strDateFrom = datePart + " " + timeFrom + ":00";
            string _strDateTo = datePart + " " + timeTo + ":00";
            DateTime _dateFrom = Convert.ToDateTime(_strDateFrom.Trim());
            DateTime _dateTo = Convert.ToDateTime(_strDateTo.Trim());
            if (_dateTo < _dateFrom)
            {
                _dateTo = _dateTo.AddDays(1);
            }
            return _dateTo.AddHours(timeZone);
        }
        public static void ExportToPDF(string htmlContent, string fileName)
        {
            try
            {
                using (iTextSharp.text.Document document = new iTextSharp.text.Document(new iTextSharp.text.Rectangle(792f, 612f)))
                {
                    using (iTextSharp.text.html.simpleparser.HTMLWorker htmlWorker = new iTextSharp.text.html.simpleparser.HTMLWorker(document))
                    {
                        iTextSharp.text.pdf.PdfWriter.GetInstance(document, HttpContext.Current.Response.OutputStream);
                        document.Open();
                        htmlWorker.Parse(new System.IO.StringReader(htmlContent));
                        document.Close();
                        HttpContext.Current.Response.ContentType = "application/pdf";
                        HttpContext.Current.Response.AddHeader("content-disposition", string.Format("attachment; filename={0}.pdf", fileName));
                        HttpContext.Current.Response.Cache.SetCacheability(HttpCacheability.NoCache);
                        HttpContext.Current.Response.Write(document);
                        HttpContext.Current.Response.Flush();
                        HttpContext.Current.Response.End();
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public static void ExportToXLSX(string htmlTable, string sheetName, string fileName)
        {
            try
            {
                using (System.IO.MemoryStream memoryStream = new System.IO.MemoryStream())
                {
                    using (DocumentFormat.OpenXml.Packaging.SpreadsheetDocument excelDocument = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Create(memoryStream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
                    {
                        DocumentFormat.OpenXml.Packaging.WorkbookPart workbookPart = excelDocument.AddWorkbookPart();
                        workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();
                        DocumentFormat.OpenXml.Packaging.WorksheetPart worksheetPart = workbookPart.AddNewPart<DocumentFormat.OpenXml.Packaging.WorksheetPart>();
                        worksheetPart.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet();
                        DocumentFormat.OpenXml.Spreadsheet.Sheets sheets = excelDocument.WorkbookPart.Workbook.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Sheets());
                        DocumentFormat.OpenXml.Spreadsheet.Sheet sheet = new DocumentFormat.OpenXml.Spreadsheet.Sheet() { Id = excelDocument.WorkbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = sheetName ?? "sheet1" };
                        sheets.Append(sheet);

                        #region Populate SheetData object

                        DocumentFormat.OpenXml.Spreadsheet.SheetData sheetData = worksheetPart.Worksheet.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.SheetData());
                        htmlTable = System.Text.RegularExpressions.Regex.Replace(htmlTable, "\\sdata-bind\\s*=\\s*['|\"].*?['|\"]", string.Empty);
                        string formattedText = System.Text.RegularExpressions.Regex.Replace(htmlTable, "(?<attribute>\\w+)\\s*=\\s*(?<value>[^'|^\"][\\w*-]*)", "${attribute}=\"${value}\"");
                        System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Parse(formattedText);
                        string ns = doc.Root.GetDefaultNamespace().NamespaceName;
                        foreach (var rowElement in doc.Root.Descendants().Where(xmld => xmld.Name.LocalName.ToLower() == "tr"))//rows
                        {
                            DocumentFormat.OpenXml.Spreadsheet.Row currentRow = sheetData.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Row());
                            foreach (var cellElement in rowElement.Elements())//cells
                            {
                                DocumentFormat.OpenXml.Spreadsheet.Cell currentCell = currentRow.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Cell());
                                if (cellElement.HasElements)
                                {
                                    DocumentFormat.OpenXml.Spreadsheet.CellValue cellValue = currentCell.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.CellValue(cellElement.Elements().First().Value));
                                }
                                else
                                {
                                    DocumentFormat.OpenXml.Spreadsheet.CellValue cellValue = currentCell.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.CellValue(cellElement.Value));
                                }

                                currentCell.DataType = new DocumentFormat.OpenXml.EnumValue<DocumentFormat.OpenXml.Spreadsheet.CellValues>(DocumentFormat.OpenXml.Spreadsheet.CellValues.String);
                            }
                        }

                        #endregion

                        workbookPart.Workbook.Save();

                        #region Send Excel File to User for Download via httpcontext response

                        HttpContext.Current.Response.Clear();
                        HttpContext.Current.Response.AddHeader("Content-disposition", string.Format("attachment; filename={0}.xlsx", fileName));
                        HttpContext.Current.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        HttpContext.Current.Response.AddHeader("Content-Length", memoryStream.Length.ToString());
                        HttpContext.Current.Response.Cache.SetCacheability(HttpCacheability.NoCache);
                        HttpContext.Current.Response.BinaryWrite(memoryStream.ToArray());
                        HttpContext.Current.Response.End();

                        #endregion
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion
    }

    #region Custom Attributes

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class IsIdentityAttribute : Attribute
    {
        public bool isIdentity { get; set; }
        public IsIdentityAttribute()
        {
            isIdentity = true;
        }
    }
    public enum IgnoreAccess
    {
        ReadWrite = 0,
        OnRead = 1,
        OnWrite = -1
    }
    /// <summary>
    /// a property or field attribute that specifies if the field is to be ignored when retreiving data. best use for computed field
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class IgnorePropertyAttribute : Attribute
    {
        public bool ignoreProperty { get; set; }
        public IgnoreAccess ignoreAccess { get; set; }
        public IgnorePropertyAttribute()
        {
            this.ignoreProperty = true;
            this.ignoreAccess = IgnoreAccess.ReadWrite;
        }
        public IgnorePropertyAttribute(bool ignoreProperty)
        {
            this.ignoreProperty = ignoreProperty;
            this.ignoreAccess = IgnoreAccess.ReadWrite;
        }
        public IgnorePropertyAttribute(bool ignoreProperty, IgnoreAccess ignoreAccess)
        {
            this.ignoreProperty = ignoreProperty;
            this.ignoreAccess = ignoreAccess;
        }
    }
    /// <summary>
    /// a property or field attribute that specifies the internal name of the field that the target is associated
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class FieldNameAttribute : Attribute
    {
        public string fieldName { get; set; }
        public Guid fieldID { get; set; }
        public int index { get; set; }
        public FieldNameAttribute() { }
        public FieldNameAttribute(string fieldName)
        {
            this.fieldName = fieldName;
        }
        public FieldNameAttribute(Guid fieldID)
        {
            this.fieldID = fieldID;
        }
        public FieldNameAttribute(int index)
        {
            this.index = index;
        }
    }
    /// <summary>
    /// a class attribute that specifies the name of the SharePoint List that the target is associated
    /// </summary>
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
    /// <summary>
    /// a class attribute that specifies the name of the Database table that the target is associated
    /// </summary>
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

    #endregion

    //credits to Matthew Yarlett
    //https://social.msdn.microsoft.com/Forums/office/en-US/92c1a750-0624-4887-b0f0-1c61234ab6b3/saving-file-to-another-server-using-c?forum=sharepointdevelopmentprevious#11691439-640e-49a7-a185-3a87328910d0
    public class Impersonator : IDisposable
    {
        public Impersonator(string userName, string domainName, string password)
        {
            ImpersonateValidUser(userName, domainName, password);
        }

        public void Dispose()
        {
            UndoImpersonation();
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int LogonUser(string lpszUserName, string lpszDomain, string lpszPassword, int dwLogonType, int dwLogonProvider, ref IntPtr phToken);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int DuplicateToken(IntPtr hToken, int impersonationLevel, ref IntPtr hNewToken);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool RevertToSelf();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);

        private const int LOGON32_LOGON_INTERACTIVE = 2;
        private const int LOGON32_PROVIDER_DEFAULT = 0;

        private WindowsImpersonationContext _impersonationContext = null;

        private void ImpersonateValidUser(string userName, string domain, string password)
        {
            IntPtr token = IntPtr.Zero;
            IntPtr tokenDuplicate = IntPtr.Zero;

            try
            {
                if (!RevertToSelf())
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (LogonUser(userName, domain, password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, ref token) == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                if (DuplicateToken(token, 2, ref tokenDuplicate) == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                var tempWindowsIdentity = new WindowsIdentity(tokenDuplicate);
                _impersonationContext = tempWindowsIdentity.Impersonate();
            }
            finally
            {
                if (token != IntPtr.Zero)
                {
                    CloseHandle(token);
                }
                if (tokenDuplicate != IntPtr.Zero)
                {
                    CloseHandle(tokenDuplicate);
                }
            }
        }

        private void UndoImpersonation()
        {
            if (_impersonationContext != null)
            {
                _impersonationContext.Undo();
            }
        }
    }

    public class FunctionalComparer<T> : IComparer<T>
    {
        private Func<T, T, int> comparer;
        public FunctionalComparer(Func<T, T, int> comparer)
        {
            this.comparer = comparer;
        }
        public static IComparer<T> Create(Func<T, T, int> comparer)
        {
            return new FunctionalComparer<T>(comparer);
        }
        public int Compare(T x, T y)
        {
            return comparer(x, y);
        }
    }

    //Tuple polyfill for .Net 3.5 http://stackoverflow.com/a/956043
    public struct Tuple<T1, T2> : IEquatable<Tuple<T1, T2>>
    {
        readonly T1 first;
        readonly T2 second;
        public Tuple(T1 first, T2 second)
        {
            this.first = first;
            this.second = second;
        }
        public T1 First { get { return first; } }
        public T2 Second { get { return second; } }
        public override int GetHashCode()
        {
            return first.GetHashCode() ^ second.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            return Equals((Tuple<T1, T2>)obj);
        }
        public bool Equals(Tuple<T1, T2> other)
        {
            return other.first.Equals(first) && other.second.Equals(second);
        }
    }

}
