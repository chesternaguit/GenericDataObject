# GenericDataObject
generic data object for n-tier and repository pattern in .NET Framework<br/>
A helper class that utilizes System.Reflection class to support any reference types.<br/>
This aims to help developers create n-tier systems faster.<br/>
Project includes starting classes for SharePoint and SQL.<br/>
<br/>
//Use it as a helper:<br/>
<br/>
public class Person //your model<br/>
{<br/>
  //the generic data object will assume that the property names here also exist in your SharePoint List<br/>
  public int ID { get; set; }<br/>
  public string Title { get; set; }<br/>
  public string FirstName { get; set; }<br/>
  public string LastName { get; set; }<br/>
  //make sure that the properties declared here matches the Internal Names in your SharePoint List<br/>
  public string Nick_x0020_Name { get; set; }<br/>
}<br/>
<br/>
public class PersonRepository<br/>
{<br/>
  public PersonRepository()<br/>
  {//set your connection variables and pass the Type of the Model as parameter for the Generic Data Object class<br/>
    gSPDataObject<Person>.ConnectionString = "YourConnectionStringHere";<br/>
    gSPDataObject<Person>.spList = "YourSharePointListNameHere";<br/>
    gSPDataObject<Person>.refreshInterval = 5; //optional cache interval in minutes<br/>
  }<br/>
  public bool Create(Person person)<br/>
  {<br/>
    return gSPDataObject<Person>.Create(person);<br/>
  }<br/>
  public List<Person> GetPersons()<br/>
  {<br/>
    return gSPDataObject<person>.GetAll();<br/>
  }<br/>
  public bool Update(Person person)<br/>
  {<br/>
    return gSPDataObject<Person>.Update(person);<br/>
  }<br/>
  public bool Delete(int id)<br/>
  {<br/>
    return gSPDataObject<Person>.Delete(new Person() { ID = id });<br/>
  }<br/>
}<br/>
<br/>
<br/>
<br/>
//Or Inherit it as a base class for your data layer object:<br/>
<br/>
public class DO_Person : gSPDataObject<Person><br/>
{ }<br/>
<br/>
public class BO_Person<br/>
{<br/>
  public BO_Person()<br/>
  {//set variables<br/>
    DO_Person.ConnectionString = "YourConnectionStringHere";<br/>
    DO_Person.spList = "YourSharePointListNameHere";<br/>
    DO_Person.refreshInterval = 5;<br/>
  }<br/>
  public bool Create(Person person)<br/>
  {<br/>
    return DO_Person.Create(person);<br/>
  }<br/>
  public List<Person> GetPersons()<br/>
  {<br/>
    return DO_Person.GetAll();<br/>
  }<br/>
  public bool Update(Person person)<br/>
  {<br/>
    return DO_Person.Update(person);<br/>
  }<br/>
  public bool Delete(int id)<br/>
  {<br/>
    return DO_Person.Delete(new Person() { ID = id });<br/>
  }<br/>
}
