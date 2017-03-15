# GenericDataObject
A Generic Helper Class for Multiple-Tier Architecture in .Net Framework<br/>

<h4>OVERVIEW</h4>

The Generic Classes of this project are intended to take care of the communication between your model in your .NET application and the source of your data on the server side which could either be a Database Table or a SharePoint List.So in other words, the generic classes will be serving as the Data Layer.<br/>
The generic classes will be the one communicating with the database/sharepoint to fetch the data and will then pass that data to your models.


<h4>WHY?</h4>

There might be instances that you will be working on an existing project and you plan on implementing Multi-Tier Architecture , where you separate the Presentation (User Interface), Business Logic (Code-Behind), and the Data Access Layer, and most often, some projects involved multiple models and you will have to write separate classes for accessing data for each model and you find it to be a repetitive task.


<h4>SETTING UP YOUR MODEL</h4>

```c#
//you can now specify the SharePoint List name or the SQL Table name for the model
//using the custom attribute "SPListName" and "SQLTableName"
//as an alternative for setting the name via the constructor of the Business Object
//note: List name or Table name specified via Model Attribute will override values
//specified via constructor
[SPlistName(listName="Personalidad")]
[SQLTableName(tableName="tblPersons")]
public class Person
{
    //the generic data object will assume that
    //the property names here also exist in your SharePoint List
    public int ID { get; set; }
    public string Title { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    
    //also make sure that the properties declared here 
    //matches the Internal Names in your SharePoint List
    public string Nick_x0020_Name { get; set; }
    
    //otherwise you may use the helper class "FieldNameAttribute"
    //to specify SharePoint List's Internal Name
    [FieldName("Primary_x0020_Address")]
    public string PrimaryAddress { get; set; }
    
    //add the IgnoreProperty attribute to specify properties
    //that are not to be retrieved from the SharePoint List or SQL Table
    //can be applied to computed Properties
    [IgnoreProperty()]
    public string FullName
    {
        get { return FirstName + " " + LastName; }
    }
}
```


<h4>USE IT AS A HELPER</h4>

```c#
public class PersonRepository
{
  public PersonRepository()
  {
    //set your connection variables and pass 
    //the Type of the Model as parameter for the Generic Data Object class
    gSPDataObject<Person>.ConnectionString = "YourConnectionStringHere"
    gSPDataObject<Person>.spList = "YourSharePointListNameHere";
    //if user token is not defined, it will default to Site SysAccount
    gSPDataObject<Person>.userToken = SPContext.Current.Web.CurrentUser.UserToken; 
    gSPDataObject<Person>.refreshInterval = 5; //optional cache interval in minutes
  }
  public bool Create(Person person)
  {
    return gSPDataObject<Person>.Create(person);
  }
  public List<Person> GetPersons()
  {
    return gSPDataObject<person>.GetAll();
  }
  public bool Update(Person person)
  {
    return gSPDataObject<Person>.Update(person);
  }
  public bool Delete(int id)
  {
    return gSPDataObject<Person>.Delete(new Person() { ID = id });
  }
}
```


<h4>OR INHERIT IT AS A BASE CLASS FOR YOUR DATA LAYER OBJECT</h4>

```c#
public class DO_Person : gSPDataObject<Person>
{ }

public class BO_Person
{
  public BO_Person()
  {//set variables
    DO_Person.ConnectionString = "YourConnectionStringHere";
    //setting up splist here will not be required 
    //if list name is already specified on the model
    DO_Person.spList = "YourSharePointListNameHere";
    DO_Person.userToken = SPContext.Current.Web.CurrentUser.UserToken;
    DO_Person.refreshInterval = 5;
  }
  public bool Create(Person person)
  {
    return DO_Person.Create(person);
  }
  public List<Person> GetPersons()
  {
    return DO_Person.GetAll();
  }
  public bool Update(Person person)
  {
    return DO_Person.Update(person);
  }
  public bool Delete(int id)
  {
    return DO_Person.Delete(new Person() { ID = id });
  }
}
```


<h4>..OR YOU MAY JUST USE IT WHICHEVER YOU LIKE</h4>


<h3>TODO</h3>
<ul>
<li>Enhance property type conversion handling</li>
</ul>

*NOTE: This project is still a work in progress and has not been fully tested
