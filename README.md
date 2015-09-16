# GenericDataObject
generic data object for n-tier and repository pattern in .NET Framework<br/>
A helper class that utilizes System.Reflection class to support any reference types.<br/>
This aims to help developers create n-tier systems faster.<br/>
Project includes starting classes for SharePoint and SQL.<br/>

<h4>USE IT AS A HELPER</h4>
<pre>
public class Person //your model
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
}

public class PersonRepository
{
  public PersonRepository()
  {//set your connection variables and pass the Type of the Model as parameter for the Generic Data Object class
    gSPDataObject&lt;Person&gt;.ConnectionString = "YourConnectionStringHere"
    gSPDataObject&lt;Person&gt;.spList = "YourSharePointListNameHere";
    gSPDataObject&lt;Person&gt;.refreshInterval = 5; //optional cache interval in minutes
  }
  public bool Create(Person person)
  {
    return gSPDataObject&lt;Person&gt;.Create(person);
  }
  public List&lt;Person&gt; GetPersons()
  {
    return gSPDataObject&lt;person&gt;.GetAll();
  }
  public bool Update(Person person)
  {
    return gSPDataObject&lt;Person&gt;.Update(person);
  }
  public bool Delete(int id)
  {
    return gSPDataObject&lt;Person&gt;.Delete(new Person() { ID = id });
  }
}
</pre>


<h4>OR INHERIT IT AS A BASE CLASS FOR YOUR DATA LAYER OBJECT</h4>
<pre>
public class DO_Person : gSPDataObject&lt;Person&gt;
{ }

public class BO_Person
{
  public BO_Person()
  {//set variables
    DO_Person.ConnectionString = "YourConnectionStringHere";
    DO_Person.spList = "YourSharePointListNameHere";
    DO_Person.refreshInterval = 5;
  }
  public bool Create(Person person)
  {
    return DO_Person.Create(person);
  }
  public List&lt;Person&gt; GetPersons()
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
</pre>
<br/>
<h3>TODO</h3>
<ul>
<li>Write Custom Attributes to support custom property name of the Model</li>
<li>Add Function Delegate as parameter on Read Methods to support custom mapping of properties</li>
</ul>
