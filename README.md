[![ci.appveyor.com](https://ci.appveyor.com/api/projects/status/github/blushingpenguin/BlushingPenguin.JsonPath?branch=master&svg=true)](https://ci.appveyor.com/api/projects/status/github/blushingpenguin/BlushingPenguin.JsonPath?branch=master&svg=true)
[![codecov.io](https://codecov.io/gh/blushingpenguin/BlushingPenguin.JsonPath/coverage.svg?branch=master)](https://codecov.io/gh/blushingpenguin/BlushingPenguin.JsonPath?branch=master)

# BlushingPenguin.JsonPath #

BlushingPenguin.JsonPath is a jsonpath implementation for MongoDB.Bson. It is a port of the jsonpath implementation from Newtonsoft.Json.

## Packages ##

BlushingPenguin.JsonPath can also be installed from [nuget.org](https://www.nuget.org/packages/BlushingPenguin.JsonPath/).

Install with package manager:

    Install-Package BlushingPenguin.JsonPath

or with nuget:

    nuget install BlushingPenguin.JsonPath

Or with dotnet:

    dotnet add package BlushingPenguin.JsonPath

## Example usage ##

```csharp
using BlushingPenguin.JsonPath;

void Example()
{
var o = JsonDocument.Parse(@"{
""Stores"": [
    ""Lambton Quay"",
    ""Willis Street""
],
""Manufacturers"": [
    {
    ""Name"": ""Acme Co"",
    ""Products"": [
        {
        ""Name"": ""Anvil"",
        ""Price"": 50
        }
    ]
    },
    {
    ""Name"": ""Contoso"",
    ""Products"": [
        {
        ""Name"": ""Elbow Grease"",
        ""Price"": 99.95
        },
        {
        ""Name"": ""Headlight Fluid"",
        ""Price"": 4
        }
    ]
    }
]
}");

string? name = o.SelectToken("Manufacturers[0].Name")?.GetString();
// Acme Co

decimal? productPrice = o.SelectToken("Manufacturers[0].Products[0].Price")?.GetDecimal();
// 50

string? productName = o.SelectToken("Manufacturers[1].Products[0].Name")?.GetString();
// Elbow Grease

Assert.AreEqual("Acme Co", name);
Assert.AreEqual(50m, productPrice);
Assert.AreEqual("Elbow Grease", productName);

IList<string> storeNames = o.SelectToken("Stores")!.Value.EnumerateArray().Select(s => s.GetString()).ToList();
// Lambton Quay
// Willis Street

IList<string?> firstProductNames = o.RootElement.GetProperty("Manufacturers")!.EnumerateArray().Select(
    m => m.SelectToken("Products[1].Name")?.GetString()).ToList();
// null
// Headlight Fluid

decimal totalPrice = o.RootElement.GetProperty("Manufacturers")!.EnumerateArray().Aggregate(
    0M, (sum, m) => sum + m.SelectToken("Products[0].Price")!.Value.GetDecimal());
// 149.95

```

## Local Development ##

Hacking on `BlushingPenguin.JsonPath` is easy! To quickly get started clone the repo:

    git clone https://github.com/blushingpenguin/BlushingPenguin.JsonPath.git
    cd BlushingPenguin.JsonPath

To compile the code and run the tests just open the solution in
Visual Studio 2019 Community Edition.  To generate a code coverage report
run cover.ps1 from the solution directory.
