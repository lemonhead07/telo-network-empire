using Telo.Sim.Content;

namespace Telo.Sim.Tests;

public class PropTests
{
    private static string PropsJson()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Telo.slnx"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "game", "content", "props.json"));
    }

    [Fact] public void ShippingPropsAreValid()
    {
        var c = PropCatalog.FromJson(PropsJson());
        Assert.Empty(c.Validate());
        Assert.NotNull(c.Get("vehicle.van"));
    }

    [Fact] public void BadPropFailsLoudly() =>
        Assert.Throws<InvalidDataException>(() => PropCatalog.FromJson("""{ "props": [{ "id": "Van", "size": [1, 0] }] }"""));
}
