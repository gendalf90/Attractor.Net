namespace Attractor.Implementation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class AddressAttribute(string pattern) : Attribute
{
    public string Pattern { get; } = pattern;
}
