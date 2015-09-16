# GenericDataObject
generic data object for n-tier and repository pattern in .NET Framework
A helper class that utilizes System.Reflection class to support any reference types.
This aims to help developers create n-tier systems faster.
Project includes starting classes for SharePoint and SQL.

//Use it as a helper:

public class Person //your model
{
  //the generic data object will assume that the property names here also exist in your SharePoint List
  public int ID { get; set; }
  public string Title { get; set; }
  public string FirstName { get; set; }
  public string LastName { get; set; }
  //make sure that the properties declared here matches the Internal Names in your SharePoint List
  public string Nick_x0020_Name { get; set; }
}

public class PersonRepository
{
  public PersonRepository()
  {//set your connection variables and pass the Type of the Model as parameter for the Generic Data Object class
    gSPDataObject<Person>.ConnectionString = "YourConnectionStringHere";
    gSPDataObject<Person>.spList = "YourSharePointListNameHere";
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



//Or Inherit it as a base class for your data layer object:

public class DO_Person : gSPDataObject<Person>
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
