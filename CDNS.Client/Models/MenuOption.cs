namespace CDNS.Client.Models;

public class MenuOption<T> where T : class?
{
    public string Description { get; set; }
    public T Value { get; set; }
    public MenuOption(string description, T value)
    {
        Description = description;
        Value = value;
    }

    public override string ToString()
    {
        return Description;
    }
}
